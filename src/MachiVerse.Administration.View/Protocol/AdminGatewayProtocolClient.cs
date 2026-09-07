using System.Net.WebSockets;
using MachiVerse.Protocol.V1;

namespace MachiVerse.Administration.View.Protocol;

public sealed class AdminGatewayProtocolClient : IAsyncDisposable
{
    private readonly ClientWebSocket _socket = new();

    public AdminViewLifecycleState State { get; private set; } = AdminViewLifecycleState.Starting;
    public event Action<AdminViewLifecycleState>? StateChanged;

    public async Task ConnectAsync(Uri endpoint, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        SetState(State is AdminViewLifecycleState.Closed or AdminViewLifecycleState.Faulted ? AdminViewLifecycleState.Reconnecting : AdminViewLifecycleState.Connecting);
        try
        {
            await _socket.ConnectAsync(endpoint, cancellationToken).ConfigureAwait(false);
            SetState(AdminViewLifecycleState.Negotiating);
        }
        catch
        {
            SetState(AdminViewLifecycleState.Faulted);
            throw;
        }
    }

    public async Task SendAsync(WireEnvelopeV1 envelope, CancellationToken cancellationToken = default)
    {
        var bytes = AdminGatewayEnvelopeCodec.Encode(envelope);
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
                SetState(AdminViewLifecycleState.Closed);
                throw new WebSocketException("Gateway closed the Admin View WebSocket connection.");
            }
            if (result.MessageType != WebSocketMessageType.Binary)
                throw new InvalidDataException("Protocol requires binary WebSocket messages.");

            message.Write(buffer, 0, result.Count);
            if (message.Length > AdminGatewayEnvelopeCodec.MaxSerializedEnvelopeBytes)
                throw new InvalidDataException("protocol.limit-exceeded: envelope exceeds 8 MiB.");
            if (result.EndOfMessage) break;
        }
        return AdminGatewayEnvelopeCodec.Decode(message.ToArray());
    }

    public void MarkAuthenticating() => SetState(AdminViewLifecycleState.Authenticating);
    public void MarkSyncing() => SetState(AdminViewLifecycleState.Syncing);
    public void MarkReady() => SetState(AdminViewLifecycleState.Ready);
    public void MarkDegraded() => SetState(AdminViewLifecycleState.Degraded);

    private void SetState(AdminViewLifecycleState state)
    {
        State = state;
        StateChanged?.Invoke(state);
    }

    public async ValueTask DisposeAsync()
    {
        if (_socket.State is WebSocketState.Open or WebSocketState.CloseReceived)
        {
            await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "admin-view-dispose", CancellationToken.None).ConfigureAwait(false);
        }
        _socket.Dispose();
    }
}
