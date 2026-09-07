using System.Net.WebSockets;
using System.Security.Cryptography;
using Google.Protobuf;
using MachiVerse.AdminView.Presentation;
using MachiVerse.AdminView.Session;
using MachiVerse.Protocol.V1;
using Microsoft.AspNetCore.Components;

namespace MachiVerse.AdminView.Protocol;

public enum AdminGatewayConnectionState
{
    Disconnected,
    Connecting,
    Negotiating,
    Ready,
    Incompatible,
    Faulted,
}

public sealed class AdminGatewayClient(
    NavigationManager navigationManager,
    ProtocolEnvelopeValidator validator,
    AdminSessionState session,
    AdminRequestStore requestStore) : IAsyncDisposable
{
    private readonly ByteString _senderInstanceId = NewId128();
    private ClientWebSocket? _socket;
    private CancellationTokenSource? _receiveLoopCancellation;
    private Task? _receiveLoopTask;

    public event Action? Changed;
    public event Action<WireEnvelopeV1>? EnvelopeReceived;

    public AdminGatewayConnectionState State { get; private set; } = AdminGatewayConnectionState.Disconnected;
    public ProtocolVersionV1? NegotiatedVersion { get; private set; }
    public uint NegotiationGeneration { get; private set; }
    public IReadOnlySet<string> EffectiveOptionalCapabilities { get; private set; } = new HashSet<string>(StringComparer.Ordinal);
    public string? LastError { get; private set; }

    public Uri GatewayUri => BuildGatewayUri(new Uri(navigationManager.BaseUri, UriKind.Absolute));

    public async Task ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (State is not AdminGatewayConnectionState.Disconnected and not AdminGatewayConnectionState.Faulted and not AdminGatewayConnectionState.Incompatible)
        {
            return;
        }

        await DisposeSocketAsync();
        SetState(AdminGatewayConnectionState.Connecting);

        try
        {
            _socket = new ClientWebSocket();
            await _socket.ConnectAsync(GatewayUri, cancellationToken);
            SetState(AdminGatewayConnectionState.Negotiating);

            await SendBootstrapHelloAsync(cancellationToken);
            var response = await ReceiveEnvelopeAsync(_socket, cancellationToken);
            validator.ValidateBootstrap(response.Envelope, response.SerializedLength);

            switch (response.Envelope.MessageType)
            {
                case "protocol.accept":
                    ApplyAccept(ProtocolAcceptV1.Parser.ParseFrom(response.Envelope.Payload));
                    break;
                case "protocol.reject":
                    var reject = ProtocolRejectV1.Parser.ParseFrom(response.Envelope.Payload);
                    LastError = $"{reject.Code}: {reject.Diagnostic}";
                    SetState(AdminGatewayConnectionState.Incompatible);
                    await DisposeSocketAsync();
                    return;
                default:
                    throw new ProtocolValidationException($"Expected protocol.accept/reject, received '{response.Envelope.MessageType}'.");
            }

            _receiveLoopCancellation = new CancellationTokenSource();
            _receiveLoopTask = ReceiveLoopAsync(_socket, _receiveLoopCancellation.Token);
            SetState(AdminGatewayConnectionState.Ready);
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SetState(ex is ProtocolValidationException ? AdminGatewayConnectionState.Incompatible : AdminGatewayConnectionState.Faulted);
            await DisposeSocketAsync();
            throw;
        }
    }

    public async Task DisconnectAsync(CancellationToken cancellationToken = default)
    {
        if (_socket is { State: WebSocketState.Open })
        {
            try
            {
                await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "admin-view disconnect", cancellationToken);
            }
            catch (WebSocketException)
            {
                // The remote peer may already be gone; disposal below is authoritative locally.
            }
        }

        await DisposeSocketAsync();
        NegotiatedVersion = null;
        NegotiationGeneration = 0;
        EffectiveOptionalCapabilities = new HashSet<string>(StringComparer.Ordinal);
        SetState(AdminGatewayConnectionState.Disconnected);
    }

    public async Task<ByteString> SendAsync(
        string messageType,
        string schemaId,
        IMessage payload,
        ByteString? correlationId = null,
        OperationContextWireV1? operationContext = null,
        WorldContextWireV1? worldContext = null,
        CancellationToken cancellationToken = default)
    {
        if (_socket is not { State: WebSocketState.Open } || State != AdminGatewayConnectionState.Ready || NegotiatedVersion is null)
        {
            throw new InvalidOperationException("Gateway protocol is not negotiated and ready.");
        }

        if (!AdminMessageRegistry.TryGet(messageType, out var descriptor) || !string.Equals(descriptor.SchemaId, schemaId, StringComparison.Ordinal))
        {
            throw new ProtocolValidationException($"Message/schema pair '{messageType}'/'{schemaId}' is not in the canonical Admin registry.");
        }

        var messageId = NewId128();
        var effectiveCorrelationId = correlationId ?? messageId;
        var envelope = new WireEnvelopeV1
        {
            EnvelopeVersion = AdminProtocolConstants.EnvelopeVersion,
            ProtocolId = AdminProtocolConstants.ProtocolId,
            ProtocolVersion = NegotiatedVersion.Clone(),
            NegotiationGeneration = NegotiationGeneration,
            MessageType = messageType,
            MessageId = messageId,
            CorrelationId = effectiveCorrelationId,
            SenderInstanceId = _senderInstanceId,
            PayloadSchemaId = schemaId,
            PayloadSchemaVersion = new SchemaVersionWireV1 { Major = 1, Minor = 0 },
            PayloadCompression = CompressionKindV1.None,
            Payload = ByteString.CopyFrom(payload.ToByteArray()),
        };

        if (operationContext is not null)
        {
            envelope.OperationContext = operationContext;
        }

        if (worldContext is not null)
        {
            envelope.WorldContext = worldContext;
        }

        var bytes = envelope.ToByteArray();
        validator.ValidateNegotiated(envelope, bytes.Length, NegotiatedVersion, NegotiationGeneration);
        requestStore.TrackSubmitted(messageId, effectiveCorrelationId, messageType, operationContext?.OperationId);
        await _socket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken);
        return messageId;
    }

    public Task<ByteString> SendSessionAttachAsync(ByteString sessionId, ulong expectedSessionGeneration, CancellationToken cancellationToken = default)
    {
        ProtocolEnvelopeValidator.ValidateId128(sessionId, "session_id");
        var payload = new AuthSessionAttachV1
        {
            SessionId = sessionId,
            ExpectedSessionGeneration = expectedSessionGeneration,
        };

        return SendAsync("auth.session.attach", "protocol.auth-session-attach.v1", payload, cancellationToken: cancellationToken);
    }

    private async Task SendBootstrapHelloAsync(CancellationToken cancellationToken)
    {
        if (_socket is null)
        {
            throw new InvalidOperationException("WebSocket is not initialized.");
        }

        var hello = new ProtocolHelloV1 { ProtocolId = AdminProtocolConstants.ProtocolId };
        hello.SupportedVersions.Add(new SupportedVersionRangeV1
        {
            Major = AdminProtocolConstants.ProtocolMajor,
            MinMinor = AdminProtocolConstants.ProtocolMinMinor,
            MaxMinor = AdminProtocolConstants.ProtocolMaxMinor,
        });
        hello.ProvidedCapabilities.Add(AdminProtocolConstants.RequiredCapabilities);
        hello.RequiredCapabilities.Add(AdminProtocolConstants.RequiredCapabilities);

        var messageId = NewId128();
        var envelope = new WireEnvelopeV1
        {
            EnvelopeVersion = AdminProtocolConstants.EnvelopeVersion,
            ProtocolId = AdminProtocolConstants.ProtocolId,
            ProtocolVersion = new ProtocolVersionV1 { Major = 0, Minor = 0 },
            NegotiationGeneration = 0,
            MessageType = "protocol.hello",
            MessageId = messageId,
            CorrelationId = messageId,
            SenderInstanceId = _senderInstanceId,
            PayloadSchemaId = "protocol.hello.v1",
            PayloadSchemaVersion = new SchemaVersionWireV1 { Major = 1, Minor = 0 },
            PayloadCompression = CompressionKindV1.None,
            Payload = ByteString.CopyFrom(hello.ToByteArray()),
        };

        var bytes = envelope.ToByteArray();
        validator.ValidateBootstrap(envelope, bytes.Length);
        await _socket.SendAsync(bytes, WebSocketMessageType.Binary, true, cancellationToken);
    }

    private void ApplyAccept(ProtocolAcceptV1 accept)
    {
        var version = accept.NegotiatedVersion ?? throw new ProtocolValidationException("protocol.accept is missing negotiated_version.");
        if (version.Major != AdminProtocolConstants.ProtocolMajor || version.Minor < AdminProtocolConstants.ProtocolMinMinor || version.Minor > AdminProtocolConstants.ProtocolMaxMinor)
        {
            throw new ProtocolValidationException($"Gateway negotiated unsupported protocol version {version.Major}.{version.Minor}.");
        }

        if (accept.NegotiationGeneration == 0)
        {
            throw new ProtocolValidationException("protocol.accept must establish a non-zero negotiation generation.");
        }

        NegotiatedVersion = version.Clone();
        NegotiationGeneration = accept.NegotiationGeneration;
        EffectiveOptionalCapabilities = accept.EffectiveOptionalCapabilities.ToHashSet(StringComparer.Ordinal);
    }

    private async Task ReceiveLoopAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                var received = await ReceiveEnvelopeAsync(socket, cancellationToken);
                if (NegotiatedVersion is null)
                {
                    throw new ProtocolValidationException("Received normal message without a negotiated protocol version.");
                }

                validator.ValidateNegotiated(received.Envelope, received.SerializedLength, NegotiatedVersion, NegotiationGeneration);
                Dispatch(received.Envelope);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            SetState(ex is ProtocolValidationException ? AdminGatewayConnectionState.Incompatible : AdminGatewayConnectionState.Faulted);
        }
    }

    private void Dispatch(WireEnvelopeV1 envelope)
    {
        if (envelope.MessageType == "auth.session.changed")
        {
            session.Apply(AuthSessionStateV1.Parser.ParseFrom(envelope.Payload));
        }
        else if (envelope.MessageType == "operation.result")
        {
            var result = OperationStatusResultV1.Parser.ParseFrom(envelope.Payload);
            requestStore.ApplyOperationResult(result, envelope.CorrelationId);
        }

        EnvelopeReceived?.Invoke(envelope);
        Changed?.Invoke();
    }

    private static async Task<ReceivedEnvelope> ReceiveEnvelopeAsync(ClientWebSocket socket, CancellationToken cancellationToken)
    {
        using var stream = new MemoryStream();
        var buffer = new byte[16 * 1024];

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer, cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                throw new WebSocketException("Gateway closed the Administration View WebSocket.");
            }

            if (result.MessageType != WebSocketMessageType.Binary)
            {
                throw new ProtocolValidationException("Standard Administration View protocol accepts binary WebSocket messages only.");
            }

            stream.Write(buffer, 0, result.Count);
            if (stream.Length > AdminProtocolConstants.MaxEnvelopeBytes)
            {
                throw new ProtocolValidationException("Serialized envelope exceeds 8 MiB.");
            }

            if (result.EndOfMessage)
            {
                break;
            }
        }

        var bytes = stream.ToArray();
        WireEnvelopeV1 envelope;
        try
        {
            envelope = WireEnvelopeV1.Parser.ParseFrom(bytes);
        }
        catch (InvalidProtocolBufferException ex)
        {
            throw new ProtocolValidationException($"Invalid WireEnvelopeV1 protobuf: {ex.Message}");
        }

        return new ReceivedEnvelope(envelope, bytes.Length);
    }

    private static Uri BuildGatewayUri(Uri baseUri)
    {
        var isLoopback = baseUri.IsLoopback;
        var scheme = baseUri.Scheme switch
        {
            "https" => "wss",
            "http" when isLoopback => "ws",
            _ => throw new InvalidOperationException("Administration View requires HTTPS/WSS outside loopback development."),
        };

        return new UriBuilder(baseUri)
        {
            Scheme = scheme,
            Port = baseUri.IsDefaultPort ? -1 : baseUri.Port,
            Path = AdminProtocolConstants.WebSocketPath,
            Query = string.Empty,
            Fragment = string.Empty,
        }.Uri;
    }

    private static ByteString NewId128()
    {
        var bytes = RandomNumberGenerator.GetBytes(16);
        if (bytes.All(static b => b == 0))
        {
            bytes[0] = 1;
        }

        return ByteString.CopyFrom(bytes);
    }

    private void SetState(AdminGatewayConnectionState state)
    {
        State = state;
        Changed?.Invoke();
    }

    private async Task DisposeSocketAsync()
    {
        if (_receiveLoopCancellation is not null)
        {
            await _receiveLoopCancellation.CancelAsync();
            _receiveLoopCancellation.Dispose();
            _receiveLoopCancellation = null;
        }

        if (_receiveLoopTask is not null)
        {
            try
            {
                await _receiveLoopTask;
            }
            catch (OperationCanceledException)
            {
            }
            _receiveLoopTask = null;
        }

        _socket?.Dispose();
        _socket = null;
    }

    public async ValueTask DisposeAsync() => await DisconnectAsync();

    private sealed record ReceivedEnvelope(WireEnvelopeV1 Envelope, int SerializedLength);
}
