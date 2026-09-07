using Google.Protobuf;
using MachiVerse.Administration.View.Modules.Monitoring;
using MachiVerse.Protocol.V1;

var store = new MonitoringProjectionStore();

var target = new ComponentTargetV1
{
    ComponentKind = (ComponentKindV1)2, // COMPONENT_KIND_GATEWAY
    LogicalInstanceId = Id(1),
};

var health = new ComponentHealthV1
{
    Target = target,
    Health = (HealthStateV1)1, // HEALTH_STATE_HEALTHY
};
health.Metrics.Add(new MetricSampleV1
{
    Name = "gateway.connection.count",
    Value = new MetricValueV1 { UintValue = 3 },
    ObservedAtUnixMillis = 1000,
});
health.Conditions.Add(new HealthConditionV1
{
    Code = "gateway.ready",
    Severity = (HealthConditionSeverityV1)1, // HEALTH_CONDITION_SEVERITY_INFO
    Diagnostic = "fixture-ready",
});

Assert(store.TryApply(Envelope("component.health.result", "protocol.component-health.v1", health)));
Assert(store.Snapshot.Targets.Count == 1);
Assert(store.Snapshot.Health.Count == 1);
Assert(store.Snapshot.Health[0].Metrics.Count == 1);

var logPage = new LogPageV1();
logPage.Records.Add(new StructuredLogRecordV1
{
    RecordId = Id(2),
    TimestampUnixMillis = 1100,
    Severity = (LogSeverityV1)3, // LOG_SEVERITY_INFORMATION
    EventKind = "gateway.fixture",
    Source = target,
    CorrelationId = Id(3),
    OperationId = Id(4),
    Diagnostic = "server-provided diagnostic",
});
Assert(store.TryApply(Envelope("component.log.page", "protocol.log-page.v1", logPage)));
Assert(store.Snapshot.Logs.Count == 1);
Assert(store.Snapshot.Audit.Count == 0);
Assert(store.Snapshot.Logs[0].Correlation.CorrelationId is not null);

var auditPage = new AuditPageV1();
auditPage.Records.Add(new AuditRecordWireV1
{
    AuditRecordId = Id(5),
    TimestampUnixMillis = 1200,
    AuditEventKind = "admin.fixture",
    ActorAccountRef = Id(6),
    OperationId = Id(4),
    TargetKind = "gateway",
    ResultCode = "ok",
});
Assert(store.TryApply(Envelope("audit.page", "protocol.audit-page.v1", auditPage)));
Assert(store.Snapshot.Audit.Count == 1);
Assert(store.Snapshot.Logs.Count == 1); // diagnostic log and audit authority remain separate.

var healthQuery = MonitoringQueryBuilder.BuildHealth([target], ["gateway.connection.count"]);
Assert(healthQuery.Targets.Count == 1);
Assert(healthQuery.MetricNames.Count == 1);

var logQuery = MonitoringQueryBuilder.BuildLog(new LogQueryOptions(
    Targets: [target],
    FromUnixMillis: 1000,
    ToUnixMillis: 2000,
    EventKinds: ["gateway.fixture"],
    CorrelationId: Id(3),
    OperationId: Id(4),
    BasisStep: 10,
    PageSize: 200,
    Cursor: null));
Assert(logQuery.PageSize == 200);
Assert(logQuery.HasCorrelationId);

var auditQuery = MonitoringQueryBuilder.BuildAudit(new AuditQueryOptions(
    FromUnixMillis: 1000,
    ToUnixMillis: 2000,
    AuditEventKinds: ["admin.fixture"],
    OperationId: Id(4),
    SimulationStep: 10,
    PageSize: 200,
    Cursor: null));
Assert(auditQuery.PageSize == 200);
Assert(auditQuery.HasOperationId);

var csv = AuditExportFormatter.ToCsv(store.Snapshot.Audit);
Assert(csv.Contains("admin.fixture", StringComparison.Ordinal));
Assert(csv.Contains("gateway", StringComparison.Ordinal));

store.SetChannelAccess(MonitoringChannel.Audit, MonitoringAccessState.Unauthorized, "auth.unauthorized");
Assert(store.Snapshot.AuditChannel.State == MonitoringAccessState.Unauthorized);
Assert(store.Snapshot.AuditChannel.ReasonCode == "auth.unauthorized");

var mismatched = Envelope("component.log.page", "protocol.audit-page.v1", logPage);
AssertThrows<InvalidDataException>(() => store.TryApply(mismatched));
AssertThrows<ArgumentOutOfRangeException>(() => MonitoringQueryBuilder.BuildAudit(new AuditQueryOptions(
    null, null, [], null, null, 0, null)));

Console.WriteLine("ADMIN-02 smoke checks passed.");

static WireEnvelopeV1 Envelope(string messageType, string schemaId, IMessage payload)
    => new()
    {
        MessageType = messageType,
        PayloadSchemaId = schemaId,
        Payload = payload.ToByteString(),
    };

static ByteString Id(byte first)
{
    var bytes = new byte[16];
    bytes[0] = first;
    return ByteString.CopyFrom(bytes);
}

static void Assert(bool condition)
{
    if (!condition)
    {
        throw new InvalidOperationException("Smoke assertion failed.");
    }
}

static void AssertThrows<TException>(Action action) where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
}
