using Google.Protobuf;
using MachiVerse.Protocol.V1;

namespace MachiVerse.Administration.View.Modules.Management;

public sealed class ManagementProjectionStore : IManagementModuleBoundary
{
    private readonly OperationalCommandCatalog _commandCatalog;
    private readonly Dictionary<string, ConfigTargetProjection> _configTargets = new(StringComparer.Ordinal);
    private readonly Dictionary<string, TrackedManagementMutation> _mutations = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ConfigChangeRequestV1> _configChangeRequests = new(StringComparer.Ordinal);
    private readonly Dictionary<string, OperationalCommandV1> _commandRequests = new(StringComparer.Ordinal);
    private ManagementChannelState _configChannel = new(ManagementAccessState.Unavailable, "management.not-loaded");
    private ManagementChannelState _commandChannel = new(ManagementAccessState.Unavailable, "management.not-loaded");

    public ManagementProjectionStore(OperationalCommandCatalog commandCatalog)
    {
        _commandCatalog = commandCatalog;
    }

    public event Action? Changed;

    public ManagementSnapshot Snapshot => new(
        ConfigTargets: _configTargets.Values.OrderBy(static x => x.TargetKey, StringComparer.Ordinal).ToArray(),
        Mutations: _mutations.Values.OrderBy(static x => x.OperationId, StringComparer.Ordinal).ToArray(),
        ConfigChannel: _configChannel,
        CommandChannel: _commandChannel);

    public ConfigReadRequestV1 BuildConfigRead(ComponentTargetV1 target, IEnumerable<string>? keys = null)
    {
        ValidateTarget(target);
        var request = new ConfigReadRequestV1 { Target = target.Clone() };
        if (keys is null)
        {
            return request;
        }

        request.Keys.Add(keys
            .Select(ValidateConfigKey)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static key => key, StringComparer.Ordinal));
        return request;
    }

    public ConfigChangeDraft CreateDraft(ConfigTargetProjection current)
    {
        ArgumentNullException.ThrowIfNull(current);
        if (current.ConfigGeneration == 0)
        {
            throw new InvalidOperationException("Config change draft requires a confirmed non-zero ConfigGeneration.");
        }

        var target = ProjectionTarget(current);
        return new ConfigChangeDraft(target, current.ConfigGeneration, Array.Empty<ConfigChangeEdit>());
    }

    public ConfigChangeDraft CreateValueReturnDraft(
        ConfigTargetProjection current,
        IEnumerable<ConfigChangeEdit> desiredValues)
    {
        var draft = CreateDraft(current);
        return draft.WithEdits(NormalizeEdits(desiredValues));
    }

    public ConfigChangeRequestV1 PrepareConfigChange(
        ConfigChangeDraft draft,
        ByteString operationId,
        ByteString immutablePayloadDigest,
        ulong? requestedEffectiveStep = null)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ValidateTarget(draft.Target);
        ValidateId128(operationId, nameof(operationId));
        ValidateHash256(immutablePayloadDigest, nameof(immutablePayloadDigest));
        if (draft.BaseConfigGeneration == 0)
        {
            throw new InvalidDataException("expected_base_generation must be non-zero.");
        }

        var normalized = NormalizeEdits(draft.Edits);
        if (normalized.Count == 0)
        {
            throw new InvalidDataException("Config change must contain at least one edit.");
        }

        var request = new ConfigChangeRequestV1
        {
            Target = draft.Target.Clone(),
            OperationId = operationId,
            ImmutablePayloadDigest = immutablePayloadDigest,
            ExpectedBaseGeneration = draft.BaseConfigGeneration,
        };
        request.Changes.Add(normalized.Select(static edit => new ConfigChangeEntryV1
        {
            Key = edit.Key,
            Value = edit.Value.Clone(),
        }));
        if (requestedEffectiveStep is { } step)
        {
            request.RequestedEffectiveStep = step;
        }

        var operationKey = ManagementIdentity.Hex(operationId);
        RegisterPreparedMutation(
            ManagementMutationKind.ConfigChange,
            operationKey,
            ManagementIdentity.Hex(immutablePayloadDigest),
            ManagementIdentity.TargetKey(draft.Target),
            "config.change",
            draft.BaseConfigGeneration);
        _configChangeRequests[operationKey] = request.Clone();
        Changed?.Invoke();
        return request;
    }

    public OperationalCommandV1 PrepareOperationalCommand(
        string commandKind,
        ComponentTargetV1 target,
        ByteString payload,
        ByteString? operationId = null,
        ByteString? immutablePayloadDigest = null)
    {
        ValidateTarget(target);
        ArgumentNullException.ThrowIfNull(payload);
        var descriptor = _commandCatalog.Require(commandKind);
        if (!descriptor.AllowedTargetKinds.Contains(target.ComponentKind))
        {
            throw new InvalidOperationException(
                $"Operational command '{commandKind}' is not registered for target '{target.ComponentKind}'.");
        }

        if (descriptor.StateChanging)
        {
            if (operationId is null || immutablePayloadDigest is null)
            {
                throw new InvalidDataException("State-changing operational command requires OperationId and immutable payload digest.");
            }
            ValidateId128(operationId, nameof(operationId));
            ValidateHash256(immutablePayloadDigest, nameof(immutablePayloadDigest));
        }
        else if (operationId is not null || immutablePayloadDigest is not null)
        {
            if (operationId is null || immutablePayloadDigest is null)
            {
                throw new InvalidDataException("Operational command identity must include both OperationId and immutable payload digest.");
            }
            ValidateId128(operationId, nameof(operationId));
            ValidateHash256(immutablePayloadDigest, nameof(immutablePayloadDigest));
        }

        var command = new OperationalCommandV1
        {
            Target = target.Clone(),
            CommandKind = descriptor.CommandKind,
            PayloadSchemaId = descriptor.PayloadSchemaId,
            PayloadSchemaVersion = new SchemaVersionWireV1
            {
                Major = descriptor.PayloadSchemaMajor,
                Minor = descriptor.PayloadSchemaMinor,
            },
            Payload = payload,
        };

        if (operationId is not null)
        {
            command.OperationId = operationId;
            command.ImmutablePayloadDigest = immutablePayloadDigest!;
            var operationKey = ManagementIdentity.Hex(operationId);
            RegisterPreparedMutation(
                ManagementMutationKind.OperationalCommand,
                operationKey,
                ManagementIdentity.Hex(immutablePayloadDigest!),
                ManagementIdentity.TargetKey(target),
                descriptor.CommandKind,
                expectedBaseGeneration: null);
            _commandRequests[operationKey] = command.Clone();
        }

        _commandChannel = new ManagementChannelState(ManagementAccessState.Available);
        Changed?.Invoke();
        return command;
    }

    public ConfigChangeRequestV1 RetryConfigChange(ByteString operationId)
    {
        ValidateId128(operationId, nameof(operationId));
        var key = ManagementIdentity.Hex(operationId);
        return _configChangeRequests.TryGetValue(key, out var request)
            ? request.Clone()
            : throw new KeyNotFoundException($"Unknown Config change OperationId '{key}'.");
    }

    public OperationalCommandV1 RetryOperationalCommand(ByteString operationId)
    {
        ValidateId128(operationId, nameof(operationId));
        var key = ManagementIdentity.Hex(operationId);
        return _commandRequests.TryGetValue(key, out var request)
            ? request.Clone()
            : throw new KeyNotFoundException($"Unknown command OperationId '{key}'.");
    }

    public void MarkSubmitted(ByteString operationId)
        => UpdateMutationState(operationId, ManagementMutationState.Submitted, null, 0, "Unspecified", null, null);

    public void MarkDeliveryUnknown(ByteString operationId)
        => UpdateMutationState(operationId, ManagementMutationState.DeliveryUnknown, "request.delivery-unknown", 3, "ReconnectThenRetry", null, null);

    public void SetAccess(ManagementAccessState config, ManagementAccessState command, string? reasonCode = null)
    {
        _configChannel = new ManagementChannelState(config, reasonCode);
        _commandChannel = new ManagementChannelState(command, reasonCode);
        Changed?.Invoke();
    }

    public bool TryApply(WireEnvelopeV1 envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        switch (envelope.MessageType)
        {
            case "config.read.result":
                RequireSchema(envelope, "protocol.config-read-result.v1");
                ApplyConfigRead(ConfigReadResultV1.Parser.ParseFrom(envelope.Payload));
                return true;
            case "config.change.result":
                RequireSchema(envelope, "protocol.config-change-result.v1");
                ApplyConfigChangeResult(envelope, ConfigChangeResultV1.Parser.ParseFrom(envelope.Payload));
                return true;
            case "operation.result":
                RequireSchema(envelope, "protocol.operation-status-result.v1");
                ApplyOperationResult(OperationStatusResultV1.Parser.ParseFrom(envelope.Payload));
                return true;
            default:
                return false;
        }
    }

    private void ApplyConfigRead(ConfigReadResultV1 wire)
    {
        if (wire.Target is null)
        {
            throw new InvalidDataException("Config read result target is required.");
        }
        ValidateTarget(wire.Target);

        var entries = new List<ConfigEntryProjection>(wire.Entries.Count);
        string? previous = null;
        foreach (var entry in wire.Entries.OrderBy(static x => x.Key, StringComparer.Ordinal))
        {
            var key = ValidateConfigKey(entry.Key);
            if (string.Equals(previous, key, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"Duplicate Config key '{key}' in read result.");
            }
            previous = key;

            entries.Add(new ConfigEntryProjection(
                key,
                entry.Sensitive || entry.EffectiveValue is null
                    ? null
                    : JsonFormatter.Default.Format(entry.EffectiveValue),
                entry.Impact,
                entry.Mutability,
                entry.Sensitive,
                entry.Sensitive));
        }

        var digest = wire.ConfigDigest.Length == 0
            ? null
            : ValidateHash256(wire.ConfigDigest, nameof(wire.ConfigDigest));
        var targetKey = ManagementIdentity.TargetKey(wire.Target);
        var instanceId = wire.Target.HasLogicalInstanceId ? ManagementIdentity.Hex(wire.Target.LogicalInstanceId) : null;
        _configTargets[targetKey] = new ConfigTargetProjection(
            targetKey,
            wire.Target.ComponentKind.ToString(),
            instanceId,
            wire.ConfigGeneration,
            digest,
            entries,
            ManagementResultProjection.FromWire(wire.Result));
        _configChannel = new ManagementChannelState(ManagementAccessState.Available);
        Changed?.Invoke();
    }

    private void ApplyConfigChangeResult(WireEnvelopeV1 envelope, ConfigChangeResultV1 wire)
    {
        var operationId = RequireOperationContextId(envelope);
        var key = ManagementIdentity.Hex(operationId);
        if (!_mutations.TryGetValue(key, out var mutation))
        {
            throw new InvalidDataException($"Config change result references unknown OperationId '{key}'.");
        }
        if (mutation.Kind != ManagementMutationKind.ConfigChange)
        {
            throw new InvalidDataException($"OperationId '{key}' is not a Config change request.");
        }
        ValidateContextDigest(envelope, mutation);

        var result = ManagementResultProjection.FromWire(wire.Result);
        var state = ResultState(result.StatusValue, result.Code);
        _mutations[key] = mutation with
        {
            State = state,
            ResultCode = result.Code,
            RetryAdviceValue = result.RetryAdviceValue,
            RetryAdvice = result.RetryAdvice,
            ResultingGeneration = wire.ResultingGeneration == 0 ? null : wire.ResultingGeneration,
            EffectiveStep = wire.HasEffectiveStep ? wire.EffectiveStep : null,
        };
        _configChannel = result.Code == "auth.unauthorized"
            ? new ManagementChannelState(ManagementAccessState.Unauthorized, result.Code)
            : new ManagementChannelState(ManagementAccessState.Available);
        Changed?.Invoke();
    }

    private void ApplyOperationResult(OperationStatusResultV1 wire)
    {
        ValidateId128(wire.OperationId, nameof(wire.OperationId));
        var key = ManagementIdentity.Hex(wire.OperationId);
        if (!_mutations.TryGetValue(key, out var mutation))
        {
            return;
        }

        if (wire.HasOperationPayloadDigest)
        {
            ValidateHash256(wire.OperationPayloadDigest, nameof(wire.OperationPayloadDigest));
            var digest = ManagementIdentity.Hex(wire.OperationPayloadDigest);
            if (!string.Equals(digest, mutation.ImmutablePayloadDigest, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Same OperationId returned with a different immutable payload digest.");
            }
        }

        var terminal = wire.HasTerminalResult
            ? ManagementResultProjection.FromWire(wire.TerminalResult)
            : null;
        var state = (int)wire.State switch
        {
            2 => ManagementMutationState.Accepted,
            3 => ManagementMutationState.Pending,
            4 when terminal is not null => ResultState(terminal.StatusValue, terminal.Code),
            4 => ManagementMutationState.Terminal,
            _ => mutation.State,
        };
        _mutations[key] = mutation with
        {
            State = state,
            ResultCode = terminal?.Code ?? mutation.ResultCode,
            RetryAdviceValue = terminal?.RetryAdviceValue ?? mutation.RetryAdviceValue,
            RetryAdvice = terminal?.RetryAdvice ?? mutation.RetryAdvice,
            EffectiveStep = wire.HasEffectiveStep ? wire.EffectiveStep : mutation.EffectiveStep,
        };
        Changed?.Invoke();
    }

    private void RegisterPreparedMutation(
        ManagementMutationKind kind,
        string operationId,
        string digest,
        string targetKey,
        string requestKind,
        ulong? expectedBaseGeneration)
    {
        if (_mutations.TryGetValue(operationId, out var existing))
        {
            if (!string.Equals(existing.ImmutablePayloadDigest, digest, StringComparison.Ordinal))
            {
                throw new InvalidDataException("Same OperationId cannot be reused with a different immutable payload digest.");
            }
            return;
        }

        _mutations.Add(operationId, new TrackedManagementMutation(
            kind,
            operationId,
            digest,
            targetKey,
            requestKind,
            ManagementMutationState.Prepared,
            ResultCode: null,
            RetryAdviceValue: 0,
            RetryAdvice: "Unspecified",
            ExpectedBaseGeneration: expectedBaseGeneration,
            ResultingGeneration: null,
            EffectiveStep: null));
    }

    private void UpdateMutationState(
        ByteString operationId,
        ManagementMutationState state,
        string? resultCode,
        int retryAdviceValue,
        string retryAdvice,
        ulong? resultingGeneration,
        ulong? effectiveStep)
    {
        ValidateId128(operationId, nameof(operationId));
        var key = ManagementIdentity.Hex(operationId);
        if (!_mutations.TryGetValue(key, out var mutation))
        {
            throw new KeyNotFoundException($"Unknown management OperationId '{key}'.");
        }

        _mutations[key] = mutation with
        {
            State = state,
            ResultCode = resultCode ?? mutation.ResultCode,
            RetryAdviceValue = retryAdviceValue,
            RetryAdvice = retryAdvice,
            ResultingGeneration = resultingGeneration ?? mutation.ResultingGeneration,
            EffectiveStep = effectiveStep ?? mutation.EffectiveStep,
        };
        Changed?.Invoke();
    }

    private static ManagementMutationState ResultState(int status, string code)
        => status switch
        {
            1 => ManagementMutationState.Terminal,
            2 => ManagementMutationState.Accepted,
            3 => ManagementMutationState.Pending,
            4 => ManagementMutationState.Terminal,
            5 => ManagementMutationState.Terminal,
            6 when string.Equals(code, "config.stale-generation", StringComparison.Ordinal) => ManagementMutationState.StaleGeneration,
            6 => ManagementMutationState.Rejected,
            7 => ManagementMutationState.Failed,
            _ => ManagementMutationState.Pending,
        };

    private static IReadOnlyList<ConfigChangeEdit> NormalizeEdits(IEnumerable<ConfigChangeEdit> edits)
    {
        ArgumentNullException.ThrowIfNull(edits);
        var byKey = new Dictionary<string, ConfigChangeEdit>(StringComparer.Ordinal);
        foreach (var edit in edits)
        {
            ArgumentNullException.ThrowIfNull(edit);
            var key = ValidateConfigKey(edit.Key);
            if (edit.Value is null || edit.Value.ValueCase == ConfigValueWireV1.ValueOneofCase.None)
            {
                throw new InvalidDataException($"Config change '{key}' must contain a typed value.");
            }
            if (!byKey.TryAdd(key, new ConfigChangeEdit(key, edit.Value.Clone())))
            {
                throw new InvalidDataException($"Duplicate Config change key '{key}'.");
            }
        }
        return byKey.Values.OrderBy(static edit => edit.Key, StringComparer.Ordinal).ToArray();
    }

    private static string ValidateConfigKey(string key)
    {
        if (string.IsNullOrEmpty(key) || key.Length > 256)
        {
            throw new InvalidDataException("Config key must be non-empty and at most 256 ASCII characters.");
        }
        foreach (var ch in key)
        {
            var valid = ch is >= 'a' and <= 'z'
                || ch is >= '0' and <= '9'
                || ch is '.' or '_' or '-';
            if (!valid)
            {
                throw new InvalidDataException($"Config key '{key}' is not canonical ASCII field-path syntax.");
            }
        }
        return key;
    }

    private static ComponentTargetV1 ProjectionTarget(ConfigTargetProjection projection)
    {
        if (!Enum.TryParse<ComponentKindV1>(projection.ComponentKind, out var kind) || (int)kind == 0)
        {
            throw new InvalidDataException("Config projection has an invalid component kind.");
        }
        var target = new ComponentTargetV1 { ComponentKind = kind };
        if (projection.LogicalInstanceId is not null)
        {
            target.LogicalInstanceId = ByteString.CopyFrom(Convert.FromHexString(projection.LogicalInstanceId));
        }
        ValidateTarget(target);
        return target;
    }

    private static void ValidateTarget(ComponentTargetV1 target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if ((int)target.ComponentKind == 0)
        {
            throw new InvalidDataException("Management target component kind is required.");
        }
        if (target.HasLogicalInstanceId)
        {
            ValidateId128(target.LogicalInstanceId, nameof(target.LogicalInstanceId));
        }
    }

    private static ByteString RequireOperationContextId(WireEnvelopeV1 envelope)
    {
        if (!envelope.HasOperationContext || envelope.OperationContext is null || !envelope.OperationContext.HasOperationId)
        {
            throw new InvalidDataException("Management mutation result requires OperationContext.OperationId.");
        }
        ValidateId128(envelope.OperationContext.OperationId, "operation_context.operation_id");
        return envelope.OperationContext.OperationId;
    }

    private static void ValidateContextDigest(WireEnvelopeV1 envelope, TrackedManagementMutation mutation)
    {
        if (envelope.OperationContext?.HasOperationPayloadDigest != true)
        {
            return;
        }
        ValidateHash256(envelope.OperationContext.OperationPayloadDigest, "operation_context.operation_payload_digest");
        var digest = ManagementIdentity.Hex(envelope.OperationContext.OperationPayloadDigest);
        if (!string.Equals(digest, mutation.ImmutablePayloadDigest, StringComparison.Ordinal))
        {
            throw new InvalidDataException("Same OperationId returned with a different immutable payload digest.");
        }
    }

    private static void ValidateId128(ByteString value, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 16 || value.All(static b => b == 0))
        {
            throw new InvalidDataException($"{fieldName} must be a non-zero Id128.");
        }
    }

    private static string ValidateHash256(ByteString value, string fieldName)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Length != 32)
        {
            throw new InvalidDataException($"{fieldName} must be a Hash256.");
        }
        return ManagementIdentity.Hex(value);
    }

    private static void RequireSchema(WireEnvelopeV1 envelope, string expected)
    {
        if (!string.Equals(envelope.PayloadSchemaId, expected, StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                $"Message '{envelope.MessageType}' expected payload schema '{expected}', received '{envelope.PayloadSchemaId}'.");
        }
    }
}
