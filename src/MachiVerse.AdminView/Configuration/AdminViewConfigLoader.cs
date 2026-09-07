using Tomlyn;
using Tomlyn.Model;

namespace MachiVerse.AdminView.Configuration;

public sealed class AdminViewConfigLoader(HttpClient httpClient)
{
    private static readonly HashSet<string> RootKeys =
        ["meta", "dashboard", "metrics", "log", "audit", "request", "confirmation"];

    public async Task<AdminViewConfigLoadResult> LoadAsync(CancellationToken cancellationToken = default)
    {
        var text = await httpClient.GetStringAsync("admin-view.toml", cancellationToken);
        return Parse(text);
    }

    public static AdminViewConfigLoadResult Parse(string text)
    {
        TomlTable root;
        try
        {
            root = Toml.ToModel(text);
        }
        catch (Exception ex)
        {
            throw new AdminViewConfigException("Admin View Config is not valid TOML 1.0.", ex);
        }

        EnsureAllowedKeys(root, RootKeys, "root");

        var meta = RequireTable(root, "meta");
        EnsureAllowedKeys(meta, ["format", "schema_version", "component"], "meta");
        RequireString(meta, "format", "machiverse-config", "meta.format");
        RequireString(meta, "schema_version", "1.0", "meta.schema_version");
        RequireString(meta, "component", "admin-view", "meta.component");

        var defaulted = new List<string>();
        var defaults = AdminViewConfig.Defaults;

        var dashboard = OptionalTable(root, "dashboard");
        EnsureAllowedKeys(dashboard, ["refresh-ms"], "dashboard");

        var metrics = OptionalTable(root, "metrics");
        EnsureAllowedKeys(metrics, ["local-history-samples", "max-series"], "metrics");

        var log = OptionalTable(root, "log");
        EnsureAllowedKeys(log, ["default-page-size", "local-window-records"], "log");

        var audit = OptionalTable(root, "audit");
        EnsureAllowedKeys(audit, ["default-page-size"], "audit");

        var request = OptionalTable(root, "request");
        EnsureAllowedKeys(request, ["presentation-timeout-ms"], "request");

        var confirmation = OptionalTable(root, "confirmation");
        EnsureAllowedKeys(confirmation, ["ux-timeout-seconds"], "confirmation");

        var config = new AdminViewConfig(
            DashboardRefreshMs: ReadUInt32(dashboard, "refresh-ms", defaults.DashboardRefreshMs, 250, 60000, "dashboard.refresh-ms", defaulted),
            MetricsLocalHistorySamples: ReadUInt32(metrics, "local-history-samples", defaults.MetricsLocalHistorySamples, 60, 100000, "metrics.local-history-samples", defaulted),
            MetricsMaxSeries: ReadUInt16(metrics, "max-series", defaults.MetricsMaxSeries, 10, 5000, "metrics.max-series", defaulted),
            LogDefaultPageSize: ReadUInt16(log, "default-page-size", defaults.LogDefaultPageSize, 1, 1000, "log.default-page-size", defaulted),
            LogLocalWindowRecords: ReadUInt32(log, "local-window-records", defaults.LogLocalWindowRecords, 100, 100000, "log.local-window-records", defaulted),
            AuditDefaultPageSize: ReadUInt16(audit, "default-page-size", defaults.AuditDefaultPageSize, 1, 1000, "audit.default-page-size", defaulted),
            RequestPresentationTimeoutMs: ReadUInt32(request, "presentation-timeout-ms", defaults.RequestPresentationTimeoutMs, 1000, 300000, "request.presentation-timeout-ms", defaulted),
            ConfirmationUxTimeoutSeconds: ReadUInt32(confirmation, "ux-timeout-seconds", defaults.ConfirmationUxTimeoutSeconds, 10, 1800, "confirmation.ux-timeout-seconds", defaulted));

        return new AdminViewConfigLoadResult(config, defaulted);
    }

    private static TomlTable RequireTable(TomlTable root, string key)
    {
        if (!root.TryGetValue(key, out var value) || value is not TomlTable table)
        {
            throw new AdminViewConfigException($"Required TOML table [{key}] is missing or invalid.");
        }

        return table;
    }

    private static TomlTable OptionalTable(TomlTable root, string key)
    {
        if (!root.TryGetValue(key, out var value))
        {
            return new TomlTable();
        }

        return value as TomlTable
            ?? throw new AdminViewConfigException($"TOML value '{key}' must be a table.");
    }

    private static void EnsureAllowedKeys(TomlTable table, IReadOnlySet<string> allowed, string tablePath)
    {
        foreach (var key in table.Keys)
        {
            if (!allowed.Contains(key))
            {
                throw new AdminViewConfigException($"Unknown Admin View Config key '{tablePath}.{key}'.");
            }
        }
    }

    private static void RequireString(TomlTable table, string key, string expected, string path)
    {
        if (!table.TryGetValue(key, out var value) || value is not string text || !string.Equals(text, expected, StringComparison.Ordinal))
        {
            throw new AdminViewConfigException($"Config '{path}' must be exactly '{expected}'.");
        }
    }

    private static ushort ReadUInt16(TomlTable table, string key, ushort defaultValue, long min, long max, string path, List<string> defaulted)
        => checked((ushort)ReadInteger(table, key, defaultValue, min, max, path, defaulted));

    private static uint ReadUInt32(TomlTable table, string key, uint defaultValue, long min, long max, string path, List<string> defaulted)
        => checked((uint)ReadInteger(table, key, defaultValue, min, max, path, defaulted));

    private static long ReadInteger(TomlTable table, string key, long defaultValue, long min, long max, string path, List<string> defaulted)
    {
        if (!table.TryGetValue(key, out var value))
        {
            defaulted.Add(path);
            return defaultValue;
        }

        var number = value switch
        {
            long v => v,
            int v => v,
            short v => v,
            byte v => v,
            uint v => v,
            ushort v => v,
            ulong v when v <= long.MaxValue => checked((long)v),
            _ => throw new AdminViewConfigException($"Config '{path}' must be an integer."),
        };

        if (number < min || number > max)
        {
            throw new AdminViewConfigException($"Config '{path}' must be in range {min}..{max}; received {number}.");
        }

        return number;
    }
}
