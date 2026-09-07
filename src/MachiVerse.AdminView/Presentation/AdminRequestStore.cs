using Google.Protobuf;
using MachiVerse.Protocol.V1;

namespace MachiVerse.AdminView.Presentation;

public enum AdminRequestPresentationState
{
    Submitted,
    Accepted,
    Pending,
    Terminal,
    Failed,
}

public sealed record AdminRequestRecord(
    string MessageId,
    string CorrelationId,
    string MessageType,
    string? OperationId,
    AdminRequestPresentationState State,
    string? ResultCode,
    RetryAdviceV1 RetryAdvice,
    DateTimeOffset SubmittedAt,
    DateTimeOffset UpdatedAt);

public sealed class AdminRequestStore
{
    private readonly Dictionary<string, AdminRequestRecord> _byMessageId = new(StringComparer.Ordinal);

    public event Action? Changed;
    public IReadOnlyCollection<AdminRequestRecord> Requests => _byMessageId.Values.OrderByDescending(static x => x.UpdatedAt).ToArray();

    public void TrackSubmitted(ByteString messageId, ByteString correlationId, string messageType, ByteString? operationId)
    {
        var now = DateTimeOffset.UtcNow;
        var id = ToHex(messageId);
        _byMessageId[id] = new AdminRequestRecord(
            id,
            ToHex(correlationId),
            messageType,
            operationId is { Length: > 0 } ? ToHex(operationId) : null,
            AdminRequestPresentationState.Submitted,
            null,
            RetryAdviceV1.Unspecified,
            now,
            now);
        Changed?.Invoke();
    }

    public void ApplyOperationResult(OperationStatusResultV1 result, ByteString correlationId)
    {
        var operationId = ToHex(result.OperationId);
        var existing = _byMessageId.Values.LastOrDefault(x => string.Equals(x.OperationId, operationId, StringComparison.Ordinal));
        if (existing is null)
        {
            return;
        }

        var terminal = result.State == OperationLifecycleWireStateV1.Terminal;
        var presentationState = result.State switch
        {
            OperationLifecycleWireStateV1.Accepted => AdminRequestPresentationState.Accepted,
            OperationLifecycleWireStateV1.Scheduled => AdminRequestPresentationState.Pending,
            OperationLifecycleWireStateV1.Terminal when result.TerminalResult?.Status == ResultStatusV1.Failed => AdminRequestPresentationState.Failed,
            OperationLifecycleWireStateV1.Terminal => AdminRequestPresentationState.Terminal,
            _ => AdminRequestPresentationState.Pending,
        };

        var terminalResult = terminal ? result.TerminalResult : null;
        _byMessageId[existing.MessageId] = existing with
        {
            CorrelationId = ToHex(correlationId),
            State = presentationState,
            ResultCode = terminalResult?.Code,
            RetryAdvice = terminalResult?.RetryAdvice ?? RetryAdviceV1.Unspecified,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        Changed?.Invoke();
    }

    private static string ToHex(ByteString bytes) => Convert.ToHexString(bytes.ToByteArray()).ToLowerInvariant();
}
