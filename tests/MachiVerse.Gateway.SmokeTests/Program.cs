using System.Security.Cryptography;
using Google.Protobuf;
using MachiVerse.Gateway.Configuration;
using MachiVerse.Gateway.Protocol;
using MachiVerse.Gateway.State;
using MachiVerse.Protocol.V1;

var config = GatewayConfigLoader.LoadFile("config/gateway.toml");
if (config.ReconnectMaxMs < config.ReconnectInitialMs) throw new InvalidOperationException("Config validation failed.");

static ByteString Id(byte value) => ByteString.CopyFrom(Enumerable.Repeat(value, 16).ToArray());
static ByteString Hash(byte value) => ByteString.CopyFrom(Enumerable.Repeat(value, 32).ToArray());
const int CompressionNoneWireValue = 1;
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

var publicationId = Id(10);
var recordId = Id(11);
var projectionPayload = new ProjectionChunkPayloadV1
{
    SubscriptionId = Id(12),
    PublicationId = publicationId,
    ChunkIndex = 0
};
projectionPayload.Records.Add(new ProjectionRecordV1
{
    RecordSchemaId = "view.test-record.v1",
    RecordSchemaVersion = new SchemaVersionWireV1 { Major = 1, Minor = 0 },
    RecordId = recordId,
    RecordRevision = 1,
    MutationKind = (ProjectionMutationKindV1)1,
    Payload = ByteString.CopyFromUtf8("record-v1")
});
var chunkPayloadBytes = projectionPayload.ToByteArray();
var fullPublication = new StatePublicationV1
{
    PublicationId = publicationId,
    Kind = (PublicationKindV1)1,
    StateContinuityToken = Hash(20),
    ChunkCount = 1,
    ProjectionSchemaDigest = Hash(21)
};
var fullChunk = new StatePublicationChunkV1
{
    PublicationId = publicationId,
    ChunkIndex = 0,
    ChunkCount = 1,
    UncompressedPayloadDigest = ByteString.CopyFrom(SHA256.HashData(chunkPayloadBytes)),
    Compression = (CompressionKindV1)1,
    Payload = ByteString.CopyFrom(chunkPayloadBytes)
};

var cache = new ConfirmedProjectionCache();
var resync = new ResyncCoordinator(cache);
var fullSnapshot = resync.ApplyOrEnterSuspect(fullPublication, 100, [fullChunk]);
if (fullSnapshot.BasisStep != 100 || fullSnapshot.Records.Count != 1) throw new InvalidOperationException("FULL publication was not atomically installed.");
if (!resync.AllowsWorldAffectingAdmission) throw new InvalidOperationException("Synced confirmed basis should allow admission gate.");

var badDelta = new StatePublicationV1
{
    PublicationId = Id(13),
    Kind = (PublicationKindV1)2,
    StateContinuityToken = Hash(22),
    BaseStateContinuityToken = Hash(99),
    ChunkCount = 1,
    ProjectionSchemaDigest = Hash(21)
};
var badDeltaPayload = new ProjectionChunkPayloadV1
{
    SubscriptionId = Id(12),
    PublicationId = badDelta.PublicationId,
    ChunkIndex = 0
};
var badDeltaBytes = badDeltaPayload.ToByteArray();
var badDeltaChunk = new StatePublicationChunkV1
{
    PublicationId = badDelta.PublicationId,
    ChunkIndex = 0,
    ChunkCount = 1,
    UncompressedPayloadDigest = ByteString.CopyFrom(SHA256.HashData(badDeltaBytes)),
    Compression = (CompressionKindV1)1,
    Payload = ByteString.CopyFrom(badDeltaBytes)
};
var mismatchRejected = false;
try
{
    resync.ApplyOrEnterSuspect(badDelta, 101, [badDeltaChunk]);
}
catch (ContinuityMismatchException)
{
    mismatchRejected = true;
}
if (!mismatchRejected || resync.State != GatewaySyncState.Suspect || resync.AllowsWorldAffectingAdmission)
    throw new InvalidOperationException("Continuity mismatch must gate normal admission and enter SUSPECT.");

var request = resync.BeginResync(Id(42), forceFull: false);
if (!request.HasClientBasisStep || request.ClientBasisStep != 100 || !request.HasClientContinuityToken)
    throw new InvalidOperationException("Resync request must carry the last confirmed basis when continuation is possible.");

var negotiation = new ProtocolNegotiationState();
var accept = new ProtocolAcceptV1
{
    NegotiatedVersion = new ProtocolVersionV1 { Major = 1, Minor = 0 },
    NegotiationGeneration = 1
};
accept.EffectiveOptionalCapabilities.Add("protocol.protobuf.v1");
negotiation.Accept(accept);
if (!negotiation.IsNegotiated || negotiation.NegotiationGeneration != 1) throw new InvalidOperationException("Protocol negotiation state failed.");

Console.WriteLine("GW-01/GW-02 smoke tests passed.");
