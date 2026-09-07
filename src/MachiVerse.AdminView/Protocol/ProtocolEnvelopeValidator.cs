using System.Text.RegularExpressions;
using Google.Protobuf;
using MachiVerse.Protocol.V1;

namespace MachiVerse.AdminView.Protocol;

public sealed class ProtocolEnvelopeValidator
{
    private static readonly Regex StableToken = new("^[a-z0-9][a-z0-9._/-]{0,63}$", RegexOptions.CultureInvariant);

    public void ValidateBootstrap(WireEnvelopeV1 envelope, int serializedLength)
    {
        ValidateCommon(envelope, serializedLength);

        if (envelope.NegotiationGeneration != 0)
        {
            throw new ProtocolValidationException("Bootstrap envelope must use negotiation_generation=0.");
        }

        if (envelope.ProtocolVersion.Major != 0 || envelope.ProtocolVersion.Minor != 0)
        {
            throw new ProtocolValidationException("Bootstrap envelope must use protocol_version=0.0.");
        }

        if (envelope.MessageType is not ("protocol.accept" or "protocol.reject" or "protocol.hello"))
        {
            throw new ProtocolValidationException($"Message '{envelope.MessageType}' is not valid during bootstrap.");
        }
    }

    public void ValidateNegotiated(WireEnvelopeV1 envelope, int serializedLength, ProtocolVersionV1 version, uint negotiationGeneration)
    {
        ValidateCommon(envelope, serializedLength);

        if (envelope.ProtocolVersion.Major != version.Major || envelope.ProtocolVersion.Minor != version.Minor)
        {
            throw new ProtocolValidationException("Envelope protocol version does not match the negotiated version.");
        }

        if (envelope.NegotiationGeneration != negotiationGeneration || negotiationGeneration == 0)
        {
            throw new ProtocolValidationException("Envelope negotiation generation is stale or invalid.");
        }
    }

    private static void ValidateCommon(WireEnvelopeV1 envelope, int serializedLength)
    {
        if (serializedLength <= 0 || serializedLength > AdminProtocolConstants.MaxEnvelopeBytes)
        {
            throw new ProtocolValidationException("Serialized envelope exceeds the Standard Protocol v1 size limit.");
        }

        if (envelope.EnvelopeVersion != AdminProtocolConstants.EnvelopeVersion)
        {
            throw new ProtocolValidationException($"Unsupported envelope_version {envelope.EnvelopeVersion}.");
        }

        if (!string.Equals(envelope.ProtocolId, AdminProtocolConstants.ProtocolId, StringComparison.Ordinal))
        {
            throw new ProtocolValidationException($"Unexpected ProtocolId '{envelope.ProtocolId}'.");
        }

        ValidateVersion(envelope.ProtocolVersion, "protocol_version");
        ValidateStableToken(envelope.MessageType, "message_type");
        ValidateStableToken(envelope.PayloadSchemaId, "payload_schema_id");
        ValidateId128(envelope.MessageId, "message_id");
        ValidateId128(envelope.CorrelationId, "correlation_id");
        ValidateId128(envelope.SenderInstanceId, "sender_instance_id");

        if (envelope.HasCausationId)
        {
            ValidateId128(envelope.CausationId, "causation_id");
        }

        if (!AdminMessageRegistry.TryGet(envelope.MessageType, out var descriptor))
        {
            throw new ProtocolValidationException($"Unknown Standard Protocol message type '{envelope.MessageType}'.");
        }

        if (!string.Equals(descriptor.SchemaId, envelope.PayloadSchemaId, StringComparison.Ordinal))
        {
            throw new ProtocolValidationException($"Payload schema '{envelope.PayloadSchemaId}' does not match message '{envelope.MessageType}'.");
        }

        ValidateVersion(envelope.PayloadSchemaVersion, "payload_schema_version");
        if (envelope.PayloadSchemaVersion.Major != 1)
        {
            throw new ProtocolValidationException("Standard Protocol v1 payload schema major must be 1.");
        }

        if (envelope.PayloadCompression != CompressionKindV1.None)
        {
            throw new ProtocolValidationException("ADMIN-01 foundation accepts uncompressed payloads only; wire.gzip.v1 was not negotiated.");
        }
    }

    public static void ValidateId128(ByteString value, string fieldName)
    {
        if (value.Length != 16 || value.ToByteArray().All(static b => b == 0))
        {
            throw new ProtocolValidationException($"{fieldName} must be a non-zero Id128 (16 bytes).");
        }
    }

    public static void ValidateHash256(ByteString value, string fieldName)
    {
        if (value.Length != 32)
        {
            throw new ProtocolValidationException($"{fieldName} must be Hash256 (32 bytes).");
        }
    }

    private static void ValidateStableToken(string token, string fieldName)
    {
        if (string.IsNullOrEmpty(token) || !StableToken.IsMatch(token))
        {
            throw new ProtocolValidationException($"{fieldName} is not a valid StableToken.");
        }
    }

    private static void ValidateVersion(ProtocolVersionV1 version, string fieldName)
    {
        if (version.Major > ushort.MaxValue || version.Minor > ushort.MaxValue)
        {
            throw new ProtocolValidationException($"{fieldName} exceeds uint16 semantic bounds.");
        }
    }

    private static void ValidateVersion(SchemaVersionWireV1 version, string fieldName)
    {
        if (version.Major > ushort.MaxValue || version.Minor > ushort.MaxValue)
        {
            throw new ProtocolValidationException($"{fieldName} exceeds uint16 semantic bounds.");
        }
    }
}

public sealed class ProtocolValidationException(string message) : Exception(message);
