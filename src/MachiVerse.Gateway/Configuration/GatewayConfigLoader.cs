using Tomlyn;
using Tomlyn.Model;

namespace MachiVerse.Gateway.Configuration;

public static class GatewayConfigLoader
{
    public static GatewayConfig LoadFile(string path) => LoadText(File.ReadAllText(path));

    public static GatewayConfig LoadText(string text)
    {
        var model = Toml.ToModel(text);
        var meta = Table(model, "meta");
        RequireString(meta, "format", "machiverse-config");
        RequireString(meta, "schema_version", "1.0");
        RequireString(meta, "component", "gateway");

        var network = Table(model, "network");
        var peer = Table(model, "peer");
        var auth = Table(model, "auth");

        var connect = PositiveInt(network, "connect-timeout-ms");
        var reconnectInitial = PositiveInt(network, "reconnect-initial-ms");
        var reconnectMax = PositiveInt(network, "reconnect-max-ms");
        var heartbeatInterval = PositiveInt(peer, "heartbeat-interval-ms");
        var heartbeatTimeout = PositiveInt(peer, "heartbeat-timeout-ms");
        var idle = PositiveInt(auth, "session-idle-lifetime-seconds");
        var absolute = PositiveInt(auth, "session-absolute-lifetime-seconds");

        if (reconnectMax < reconnectInitial) throw new InvalidDataException("network.reconnect-max-ms must be >= reconnect-initial-ms.");
        if ((long)heartbeatTimeout < (long)heartbeatInterval * 3) throw new InvalidDataException("peer.heartbeat-timeout-ms must be >= 3 * heartbeat-interval-ms.");
        if (absolute < idle) throw new InvalidDataException("auth.session-absolute-lifetime-seconds must be >= session-idle-lifetime-seconds.");

        return new GatewayConfig(connect, reconnectInitial, reconnectMax, heartbeatInterval, heartbeatTimeout, idle, absolute, model);
    }

    private static TomlTable Table(TomlTable parent, string key)
        => parent.TryGetValue(key, out var value) && value is TomlTable table
            ? table
            : throw new InvalidDataException($"Missing TOML table [{key}].");

    private static void RequireString(TomlTable table, string key, string expected)
    {
        if (!table.TryGetValue(key, out var value) || !string.Equals(value as string, expected, StringComparison.Ordinal))
            throw new InvalidDataException($"Invalid config meta field: {key}.");
    }

    private static int PositiveInt(TomlTable table, string key)
    {
        if (!table.TryGetValue(key, out var value) || value is not long number || number is <= 0 or > int.MaxValue)
            throw new InvalidDataException($"Config field {key} must be a positive int32.");
        return (int)number;
    }
}
