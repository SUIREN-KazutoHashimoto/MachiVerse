using Google.Protobuf;
using MachiVerse.AdminView.Protocol;
using MachiVerse.Protocol.V1;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MachiVerse.AdminView.Tests;

[TestClass]
public sealed class ProtocolEnvelopeValidatorTests
{
    private readonly ProtocolEnvelopeValidator _validator = new();

    [TestMethod]
    public void ValidateNegotiated_AcceptsCanonicalEnvelope()
    {
        var version = new ProtocolVersionV1 { Major = 1, Minor = 0 };
        var payload = new HealthQueryV1();
        var envelope = CreateEnvelope("component.health.query", "protocol.health-query.v1", payload.ToByteString(), version, 7);
        var bytes = envelope.ToByteArray();

        _validator.ValidateNegotiated(envelope, bytes.Length, version, 7);
        AdminMessageRegistry.EnsureDirection(envelope.MessageType, AdminMessageDirection.ClientToGateway);
    }

    [TestMethod]
    public void ValidateNegotiated_RejectsWrongProtocolId()
    {
        var version = new ProtocolVersionV1 { Major = 1, Minor = 0 };
        var envelope = CreateEnvelope("component.health.query", "protocol.health-query.v1", ByteString.Empty, version, 7);
        envelope.ProtocolId = "mv.gateway-view";

        Assert.ThrowsException<ProtocolValidationException>(() =>
            _validator.ValidateNegotiated(envelope, envelope.CalculateSize(), version, 7));
    }

    [TestMethod]
    public void ValidateNegotiated_RejectsMessageSchemaMismatch()
    {
        var version = new ProtocolVersionV1 { Major = 1, Minor = 0 };
        var envelope = CreateEnvelope("component.health.query", "protocol.audit-query.v1", ByteString.Empty, version, 7);

        Assert.ThrowsException<ProtocolValidationException>(() =>
            _validator.ValidateNegotiated(envelope, envelope.CalculateSize(), version, 7));
    }

    [TestMethod]
    public void MessageRegistry_RejectsGatewayOnlyMessageAsClientOutbound()
    {
        Assert.ThrowsException<ProtocolValidationException>(() =>
            AdminMessageRegistry.EnsureDirection("component.health.result", AdminMessageDirection.ClientToGateway));
    }

    [TestMethod]
    public void MessageRegistry_AcceptsGatewayOnlyMessageAsInbound()
    {
        AdminMessageRegistry.EnsureDirection("component.health.result", AdminMessageDirection.GatewayToClient);
    }

    [TestMethod]
    public void ValidateId128_RejectsZeroIdentity()
    {
        Assert.ThrowsException<ProtocolValidationException>(() =>
            ProtocolEnvelopeValidator.ValidateId128(ByteString.CopyFrom(new byte[16]), "message_id"));
    }

    private static WireEnvelopeV1 CreateEnvelope(string messageType, string schemaId, ByteString payload, ProtocolVersionV1 version, uint generation)
    {
        return new WireEnvelopeV1
        {
            EnvelopeVersion = 1,
            ProtocolId = AdminProtocolConstants.ProtocolId,
            ProtocolVersion = version.Clone(),
            NegotiationGeneration = generation,
            MessageType = messageType,
            MessageId = Id(1),
            CorrelationId = Id(2),
            SenderInstanceId = Id(3),
            PayloadSchemaId = schemaId,
            PayloadSchemaVersion = new SchemaVersionWireV1 { Major = 1, Minor = 0 },
            PayloadCompression = CompressionKindV1.None,
            Payload = payload,
        };
    }

    private static ByteString Id(byte first)
    {
        var bytes = new byte[16];
        bytes[0] = first;
        return ByteString.CopyFrom(bytes);
    }
}
