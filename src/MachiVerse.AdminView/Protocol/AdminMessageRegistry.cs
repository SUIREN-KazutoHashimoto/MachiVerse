namespace MachiVerse.AdminView.Protocol;

public enum AdminMessageDirection
{
    ClientToGateway,
    GatewayToClient,
}

public sealed record AdminMessageDescriptor(
    string MessageType,
    string SchemaId,
    AdminMessageDirection Direction);

public static class AdminMessageRegistry
{
    private static readonly IReadOnlyDictionary<string, AdminMessageDescriptor> Entries =
        new Dictionary<string, AdminMessageDescriptor>(StringComparer.Ordinal)
        {
            ["protocol.hello"] = new("protocol.hello", "protocol.hello.v1", AdminMessageDirection.ClientToGateway),
            ["protocol.accept"] = new("protocol.accept", "protocol.accept.v1", AdminMessageDirection.GatewayToClient),
            ["protocol.reject"] = new("protocol.reject", "protocol.reject.v1", AdminMessageDirection.GatewayToClient),
            ["auth.login"] = new("auth.login", "protocol.auth-login-begin.v1", AdminMessageDirection.ClientToGateway),
            ["auth.login.begin-result"] = new("auth.login.begin-result", "protocol.auth-login-begin-result.v1", AdminMessageDirection.GatewayToClient),
            ["auth.login.result"] = new("auth.login.result", "protocol.auth-login-result.v1", AdminMessageDirection.GatewayToClient),
            ["auth.session.attach"] = new("auth.session.attach", "protocol.auth-session-attach.v1", AdminMessageDirection.ClientToGateway),
            ["auth.session.changed"] = new("auth.session.changed", "protocol.auth-session-state.v1", AdminMessageDirection.GatewayToClient),
            ["component.health.query"] = new("component.health.query", "protocol.health-query.v1", AdminMessageDirection.ClientToGateway),
            ["component.health.result"] = new("component.health.result", "protocol.component-health.v1", AdminMessageDirection.GatewayToClient),
            ["component.log.query"] = new("component.log.query", "protocol.log-query.v1", AdminMessageDirection.ClientToGateway),
            ["component.log.page"] = new("component.log.page", "protocol.log-page.v1", AdminMessageDirection.GatewayToClient),
            ["config.read"] = new("config.read", "protocol.config-read-request.v1", AdminMessageDirection.ClientToGateway),
            ["config.read.result"] = new("config.read.result", "protocol.config-read-result.v1", AdminMessageDirection.GatewayToClient),
            ["config.change"] = new("config.change", "protocol.config-change-request.v1", AdminMessageDirection.ClientToGateway),
            ["config.change.result"] = new("config.change.result", "protocol.config-change-result.v1", AdminMessageDirection.GatewayToClient),
            ["operation.submit"] = new("operation.submit", "protocol.standard-operation.v1", AdminMessageDirection.ClientToGateway),
            ["operation.result"] = new("operation.result", "protocol.operation-status-result.v1", AdminMessageDirection.GatewayToClient),
            ["operational.command"] = new("operational.command", "protocol.operational-command.v1", AdminMessageDirection.ClientToGateway),
            ["audit.query"] = new("audit.query", "protocol.audit-query.v1", AdminMessageDirection.ClientToGateway),
            ["audit.page"] = new("audit.page", "protocol.audit-page.v1", AdminMessageDirection.GatewayToClient),
        };

    public static bool TryGet(string messageType, out AdminMessageDescriptor descriptor)
        => Entries.TryGetValue(messageType, out descriptor!);

    public static void EnsureDirection(string messageType, AdminMessageDirection expected)
    {
        if (!TryGet(messageType, out var descriptor))
        {
            throw new ProtocolValidationException($"Unknown Standard Protocol message type '{messageType}'.");
        }

        if (descriptor.Direction != expected)
        {
            throw new ProtocolValidationException(
                $"Message '{messageType}' is not valid for direction {expected} on Administration View protocol.");
        }
    }
}
