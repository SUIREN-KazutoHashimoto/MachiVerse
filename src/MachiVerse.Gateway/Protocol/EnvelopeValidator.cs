using MachiVerse.Protocol.V1;

namespace MachiVerse.Gateway.Protocol;

public sealed class EnvelopeValidator
{
    private static readonly HashSet<string> KnownProtocolIds =
    [
        ProtocolConstants.CoreGateway,
        ProtocolConstants.GatewayGateway,
        ProtocolConstants.GatewayView,
        ProtocolConstants.GatewayAdminView
    ];

    public EnvelopeValidationResult Validate(WireEnvelopeV1 envelope)
    {
        ArgumentNullException.ThrowIfNull(envelope);

        if (envelope.CalculateSize() > ProtocolConstants.MaxEnvelopeBytes)
        {
            return EnvelopeValidationResult.Reject("protocol.limit-exceeded");
        }

        if (envelope.EnvelopeVersion != ProtocolConstants.EnvelopeVersion)
        {
            return EnvelopeValidationResult.Reject("protocol.envelope-version-unsupported");
        }

        if (!KnownProtocolIds.Contains(envelope.ProtocolId))
        {
            return EnvelopeValidationResult.Reject("protocol.id-unsupported");
        }

        if (!StableTokenValidator.IsValid(envelope.MessageType) ||
            !StableTokenValidator.IsValid(envelope.PayloadSchemaId))
        {
            return EnvelopeValidationResult.Reject("protocol.invalid-token");
        }

        if (envelope.MessageId.Length != ProtocolConstants.Id128Bytes ||
            envelope.CorrelationId.Length != ProtocolConstants.Id128Bytes ||
            envelope.SenderInstanceId.Length != ProtocolConstants.Id128Bytes)
        {
            return EnvelopeValidationResult.Reject("protocol.invalid-id-width");
        }

        return EnvelopeValidationResult.Accept();
    }
}

public readonly record struct EnvelopeValidationResult(bool Accepted, string? ErrorCode)
{
    public static EnvelopeValidationResult Accept() => new(true, null);
    public static EnvelopeValidationResult Reject(string code) => new(false, code);
}
