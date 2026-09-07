namespace MachiVerse.AdminView.Protocol;

public static class AdminProtocolConstants
{
    public const string ProtocolId = "mv.gateway-admin-view";
    public const uint EnvelopeVersion = 1;
    public const uint ProtocolMajor = 1;
    public const uint ProtocolMinMinor = 0;
    public const uint ProtocolMaxMinor = 0;
    public const int MaxEnvelopeBytes = 8 * 1024 * 1024;
    public const string WebSocketPath = "/ws/v1/admin";

    public static IReadOnlyList<string> RequiredCapabilities { get; } =
    [
        "protocol.protobuf.v1",
        "protocol.auth-bff.v1",
        "protocol.session-generation.v1",
        "protocol.admin-health.v1",
    ];
}
