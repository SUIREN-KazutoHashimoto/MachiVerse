namespace MachiVerse.AdminView.Protocol;

public sealed record AdminMessageDescriptor(string MessageType, string SchemaId);

public static class AdminMessageRegistry
{
    private static readonly IReadOnlyDictionary<string, AdminMessageDescriptor> Entries =
        new Dictionary<string, AdminMessageDescriptor>(StringComparer.Ordinal)
        {
            ["protocol.hello"] = new("protocol.hello", "protocol.hello.v1"),
            ["protocol.accept"] = new("protocol.accept", "protocol.accept.v1"),
            ["protocol.reject"] = new("protocol.reject", "protocol.reject.v1"),
            ["auth.login"] = new("auth.login", "protocol.auth-login-begin.v1"),
            ["auth.login.begin-result"] = new("auth.login.begin-result", "protocol.auth-login-begin-result.v1"),
            ["auth.login.result"] = new("auth.login.result", "protocol.auth-login-result.v1"),
            ["auth.session.attach"] = new("auth.session.attach", "protocol.auth-session-attach.v1"),
            ["auth.session.changed"] = new("auth.session.changed", "protocol.auth-session-state.v1"),
            ["component.health.query"] = new("component.health.query", "protocol.health-query.v1"),
            ["component.health.result"] = new("component.health.result", "protocol.component-health.v1"),
            ["component.log.query"] = new("component.log.query", "protocol.log-query.v1"),
            ["component.log.page"] = new("component.log.page", "protocol.log-page.v1"),
            ["config.read"] = new("config.read", "protocol.config-read-request.v1"),
            ["config.read.result"] = new("config.read.result", "protocol.config-read-result.v1"),
            ["config.change"] = new("config.change", "protocol.config-change-request.v1"),
            ["config.change.result"] = new("config.change.result", "protocol.config-change-result.v1"),
            ["operation.submit"] = new("operation.submit", "protocol.standard-operation.v1"),
            ["operation.result"] = new("operation.result", "protocol.operation-status-result.v1"),
            ["operational.command"] = new("operational.command", "protocol.operational-command.v1"),
            ["audit.query"] = new("audit.query", "protocol.audit-query.v1"),
            ["audit.page"] = new("audit.page", "protocol.audit-page.v1"),
        };

    public static bool TryGet(string messageType, out AdminMessageDescriptor descriptor)
        => Entries.TryGetValue(messageType, out descriptor!);
}
