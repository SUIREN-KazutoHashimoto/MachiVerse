using System.Net.WebSockets;
using Google.Protobuf;
using MachiVerse.Protocol.V1;

namespace MachiVerse.GeneralView.Protocol;

public sealed class GatewayProtocolClient : IAsyncDisposable
{
    private const int MaxEnvelopeBytes = 8 * 1024 * 1024;
    private ClientWebSocket? _socket;

    public GatewayConnectionState State { get; private set; } = GatewayConnectionState.Starting;

    public async Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (!string.Equals(endpoint.Scheme, "wss", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Production Gateway View transport requires wss://.", nameof(endpoint));
        }

        await DisposeSocketAsync();
        State = GatewayConnectionState.Connecting;
        _socket = new ClientWebSocket();
        await _socket.ConnectAsync(endpoint, cancellationToken);
        State = GatewayConnectionState.Negotiating;
    }

    public async Task SendAsync(WireEnvelopeV1 envelope, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        var socket = RequireOpenSocket();
        var payload = envelope.ToByteArray();
        if (payload.Length > MaxEnvelopeBytes)
        {
            throw new InvalidOperationException("WireEnvelopeV1 exceeds the 8 MiB protocol hard limit.");
        }

        await socket.SendAsync(payload.AsMemory(), WebSocketMessageType.Binary, true, cancellationToken);
    }

    public async Task<WireEnvelopeV1> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var socket = RequireOpenSocket();
        var buffer = new byte[64 * 1024];
        using var stream = new MemoryStream();

        while (true)
        {
            var result = await socket.ReceiveAsync(buffer.AsMemory(), cancellationToken);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                State = GatewayConnectionState.Disconnected;
                throw new WebSocketException("Gateway closed the WebSocket.");
            }

            if (result.MessageType != WebSocketMessageType.Binary)
            {
                throw new WebSocketException("Normal protocol messages must use binary WebSocket frames.");
            }

            stream.Write(buffer, 0, result.Count);
            if (stream.Length > MaxEnvelopeBytes)
            {
                throw new InvalidDataException("WireEnvelopeV1 exceeds the 8 MiB protocol hard limit.");
            }

            if (result.EndOfMessage)
            {
                return WireEnvelopeV1.Parser.ParseFrom(stream.ToArray());
            }
        }
    }

    public void MarkAuthenticating() => State = GatewayConnectionState.Authenticating;
    public void MarkSyncing() => State = GatewayConnectionState.Syncing;
    public void MarkReady() => State = GatewayConnectionState.Ready;

    private ClientWebSocket RequireOpenSocket() =>
        _socket is { State: WebSocketState.Open }
            ? _socket
            : throw new InvalidOperationException("Gateway WebSocket is not open.");

    private async ValueTask DisposeSocketAsync()
    {
        if (_socket is null)
        {
            return;
        }

        _socket.Dispose();
        _socket = null;
        State = GatewayConnectionState.Disconnected;
        await ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync() => DisposeSocketAsync();
}

public enum GatewayConnectionState
{
    Starting,
    Connecting,
    Negotiating,
    Authenticating,
    Syncing,
    Ready,
    Disconnected
}
