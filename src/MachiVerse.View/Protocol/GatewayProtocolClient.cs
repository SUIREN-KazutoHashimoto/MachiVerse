using System.Net.WebSockets;
using MachiVerse.Protocol.V1;

namespace MachiVerse.View.Protocol;

public sealed class GatewayProtocolClient : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();

    public ViewLifecycleState State { get; private set; } = ViewLifecycleState.Starting;
    public event Action<ViewLifecycleState>? StateChanged;

    public async Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        SetState(State is ViewLifecycleState.Closed or ViewLifecycleState.Faulted ? ViewLifecycleState.Reconnecting : ViewLifecycleState.Connecting);
        try
        {
            await _socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            SetState(ViewLifecycleState.Negotiating);
        }
        catch
        {
            SetState(ViewLifecycleState.Faulted);
            throw;
        }
    }

    public async Task SendAsync(WireEnvelopeV1 envelope, CancellationToken cancellationToken = default)
    {
        var bytes = GatewayEnvelopeCodec.Encode(envelope);
        await _socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Binary, true, cancellationToken).ConfigureAwait(false);
    }

    public async Task<WireEnvelopeV1> ReceiveAsync(CancellationToken cancellationToken = default)
    {
        var buffer = new byte[64 * 1024];
        using var message = new MemoryStream();
        while (true)
        {
            var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken).ConfigureAwait(false);
            if (result.MessageType == WebSocketMessageType.Close)
            {
                SetState(ViewLifecycleState.Closed);
                throw new WebSocketException("Gateway closed the WebSocket connection.");
            }
            if (result.MessageType != WebSocketMessageType.Binary)
                throw new InvalidDataException("Protocol requires binary WebSocket messages.");

            message.Write(buffer, 0, result.Count);
            if (message.Length > GatewayEnvelopeCodec.MaxSerializedEnvelopeBytes)
                throw new InvalidDataException("protocol.limit-exceeded: envelope exceeds 8 MiB.");
            if (result.EndOfMessage) break;
        }

        return GatewayEnvelopeCodec.Decode(message.ToArray());
    }

    public void MarkAuthenticating() => SetState(ViewLifecycleState.Authenticating);
    public void MarkSyncing() => SetState(ViewLifecycleState.Syncing);
    public void MarkReady() => SetState(ViewLifecycleState.Ready);
    public void MarkResyncing() => SetState(ViewLifecycleState.Resyncing);
    public void MarkDegraded() => SetState(ViewLifecycleState.Degraded);

    private void SetState(ViewLifecycleState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "view-dispose", CancellationToken.None).ConfigureAwait(false);
        }
        _socket.Dispose();
    }
}
