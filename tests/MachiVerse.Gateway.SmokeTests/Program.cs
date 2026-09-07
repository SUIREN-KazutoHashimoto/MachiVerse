using Google.Protobuf;
using MachiVerse.Gateway.Configuration;
using MachiVerse.Gateway.Protocol;
using MachiVerse.Protocol.V1;

var config = GatewayConfigLoader.LoadFile("config/gateway.toml");
if (config.ReconnectMaxMs < config.ReconnectInitialMs) throw new InvalidOperationException("Config validation failed.");

static ByteString Id(byte value) => ByteString.CopyFrom(Enumerable.Repeat(value, 16).ToArray());
const int CompressionNoneWireValue = 1; // COMPRESSION_KIND_NONE in canonical common.proto.
var envelope = new WireEnvelopeV1
{
    EnvelopeVersion = 1,
    ProtocolId = "mv.gateway-view",
    ProtocolVersion = new ProtocolVersionV1 { Major = 1, Minor = 0 },
    NegotiationGeneration = 1,
    MessageType = "state.publication",
    MessageId = Id(1),
    CorrelationId = Id(2),
    SenderInstanceId = Id(3),
    PayloadSchemaId = "mv.state-publication.v1",
    PayloadSchemaVersion = new SchemaVersionWireV1 { Major = 1, Minor = 0 },
    PayloadCompression = (CompressionKindV1)CompressionNoneWireValue,
    Payload = ByteString.Empty
};
var decoded = WireEnvelopeValidator.DecodeAndValidate(envelope.ToByteArray(), "mv.gateway-view");
if (decoded.MessageId != envelope.MessageId) throw new InvalidOperationException("Envelope round-trip failed.");
if ((int)decoded.PayloadCompression != CompressionNoneWireValue) throw new InvalidOperationException("Compression enum wire value mismatch.");

Console.WriteLine("GW-01 smoke tests passed.");
