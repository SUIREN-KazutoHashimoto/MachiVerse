namespace MachiVerse.Gateway.Protocol;

public static class ProtocolConstants
{
    public const uint EnvelopeVersion = 1;
    public const int MaxEnvelopeBytes = 8 * 1024 * 1024;
    public const int Id128Bytes = 16;
    public const int Hash256Bytes = 32;

    public const string CoreGateway = "mv.core-gateway";
    public const string GatewayGateway = "mv.gateway-gateway";
    public const string GatewayView = "mv.gateway-view";
    public const string GatewayAdminView = "mv.gateway-admin-view";
}
