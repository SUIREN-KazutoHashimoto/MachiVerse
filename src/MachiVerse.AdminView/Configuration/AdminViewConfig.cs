namespace MachiVerse.AdminView.Configuration;

public sealed record AdminViewConfig(
    uint DashboardRefreshMs,
    uint MetricsLocalHistorySamples,
    ushort MetricsMaxSeries,
    ushort LogDefaultPageSize,
    uint LogLocalWindowRecords,
    ushort AuditDefaultPageSize,
    uint RequestPresentationTimeoutMs,
    uint ConfirmationUxTimeoutSeconds)
{
    public static AdminViewConfig Defaults { get; } = new(
        DashboardRefreshMs: 1000,
        MetricsLocalHistorySamples: 3600,
        MetricsMaxSeries: 200,
        LogDefaultPageSize: 200,
        LogLocalWindowRecords: 5000,
        AuditDefaultPageSize: 200,
        RequestPresentationTimeoutMs: 30000,
        ConfirmationUxTimeoutSeconds: 120);
}

public sealed record AdminViewConfigLoadResult(
    AdminViewConfig Config,
    IReadOnlyList<string> DefaultedKeys);

public sealed class AdminViewConfigException(string message, Exception? innerException = null)
    : Exception(message, innerException);
