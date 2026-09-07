using Tomlyn.Model;

namespace MachiVerse.Gateway.Configuration;

public sealed record GatewayConfig(
    int ConnectTimeoutMs,
    int ReconnectInitialMs,
    int ReconnectMaxMs,
    int HeartbeatIntervalMs,
    int HeartbeatTimeoutMs,
    int SessionIdleLifetimeSeconds,
    int SessionAbsoluteLifetimeSeconds,
    TomlTable Raw);
