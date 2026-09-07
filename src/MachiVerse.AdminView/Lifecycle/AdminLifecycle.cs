using MachiVerse.AdminView.Configuration;
using MachiVerse.AdminView.Protocol;
using MachiVerse.AdminView.Session;

namespace MachiVerse.AdminView.Lifecycle;

public enum AdminApplicationState
{
    Starting,
    Ready,
    Connecting,
    Connected,
    Incompatible,
    Faulted,
}

public sealed class AdminLifecycle(
    AdminViewConfigLoader configLoader,
    AdminGatewayClient gateway,
    AdminSessionState session)
{
    private bool _initialized;

    public event Action? Changed;

    public AdminApplicationState State { get; private set; } = AdminApplicationState.Starting;
    public AdminGatewayClient Gateway => gateway;
    public AdminSessionState Session => session;
    public AdminViewConfig? Config { get; private set; }
    public IReadOnlyList<string> DefaultedConfigKeys { get; private set; } = [];
    public bool CanConnect => _initialized && gateway.State is AdminGatewayConnectionState.Disconnected or AdminGatewayConnectionState.Faulted or AdminGatewayConnectionState.Incompatible;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        gateway.Changed += OnGatewayChanged;
        session.Changed += OnSessionChanged;

        try
        {
            var loaded = await configLoader.LoadAsync(cancellationToken);
            Config = loaded.Config;
            DefaultedConfigKeys = loaded.DefaultedKeys;
            _initialized = true;
            State = AdminApplicationState.Ready;
            Changed?.Invoke();
        }
        catch
        {
            State = AdminApplicationState.Faulted;
            Changed?.Invoke();
            throw;
        }
    }

    public async Task ConnectGatewayAsync(CancellationToken cancellationToken = default)
    {
        if (!_initialized)
        {
            await InitializeAsync(cancellationToken);
        }

        State = AdminApplicationState.Connecting;
        Changed?.Invoke();

        try
        {
            await gateway.ConnectAsync(cancellationToken);
            State = gateway.State == AdminGatewayConnectionState.Ready
                ? AdminApplicationState.Connected
                : gateway.State == AdminGatewayConnectionState.Incompatible
                    ? AdminApplicationState.Incompatible
                    : AdminApplicationState.Faulted;
        }
        catch
        {
            State = gateway.State == AdminGatewayConnectionState.Incompatible
                ? AdminApplicationState.Incompatible
                : AdminApplicationState.Faulted;
            throw;
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    public async Task DisconnectGatewayAsync(CancellationToken cancellationToken = default)
    {
        await gateway.DisconnectAsync(cancellationToken);
        session.Clear();
        State = AdminApplicationState.Ready;
        Changed?.Invoke();
    }

    private void OnGatewayChanged()
    {
        if (gateway.State == AdminGatewayConnectionState.Incompatible)
        {
            State = AdminApplicationState.Incompatible;
        }
        else if (gateway.State == AdminGatewayConnectionState.Faulted)
        {
            State = AdminApplicationState.Faulted;
        }

        Changed?.Invoke();
    }

    private void OnSessionChanged() => Changed?.Invoke();
}
