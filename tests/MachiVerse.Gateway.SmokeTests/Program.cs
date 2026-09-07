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
static StatePublicationChunkV1 Chunk(StatePublicationV1 publication, ProjectionChunkPayloadV1 payload)
{
    var bytes = payload.ToByteArray();
    return new StatePublicationChunkV1
    {
        PublicationId = publication.PublicationId,
        ChunkIndex = payload.ChunkIndex,
        ChunkCount = publication.ChunkCount,
        UncompressedPayloadDigest = ByteString.CopyFrom(SHA256.HashData(bytes)),
        Compression = (CompressionKindV1)1,
        Payload = ByteString.CopyFrom(bytes)
    };
}

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
var fullPublication = new StatePublicationV1
{
    PublicationId = publicationId,
    Kind = (PublicationKindV1)1,
    StateContinuityToken = Hash(20),
    ChunkCount = 1,
    ProjectionSchemaDigest = Hash(21)
};

var cache = new ConfirmedProjectionCache();
var resync = new ResyncCoordinator(cache);
var fullSnapshot = resync.ApplyOrEnterSuspect(fullPublication, 100, [Chunk(fullPublication, projectionPayload)]);
if (fullSnapshot.BasisStep != 100 || fullSnapshot.Records.Count != 1) throw new InvalidOperationException("FULL publication was not atomically installed.");
if (!resync.AllowsWorldAffectingAdmission) throw new InvalidOperationException("Synced confirmed basis should allow admission gate.");

var deltaPublication = new StatePublicationV1
{
    PublicationId = Id(13),
    Kind = (PublicationKindV1)2,
    StateContinuityToken = Hash(22),
    BaseStateContinuityToken = Hash(20),
    ChunkCount = 1,
    ProjectionSchemaDigest = Hash(21)
};
var deltaPayload = new ProjectionChunkPayloadV1
{
    SubscriptionId = Id(12),
    PublicationId = deltaPublication.PublicationId,
    ChunkIndex = 0
};
deltaPayload.Records.Add(new ProjectionRecordV1
{
    RecordSchemaId = "view.test-record.v1",
    RecordSchemaVersion = new SchemaVersionWireV1 { Major = 1, Minor = 0 },
    RecordId = recordId,
    RecordRevision = 2,
    MutationKind = (ProjectionMutationKindV1)1,
    Payload = ByteString.CopyFromUtf8("record-v2")
});
var deltaSnapshot = resync.ApplyOrEnterSuspect(deltaPublication, 101, [Chunk(deltaPublication, deltaPayload)]);
var key = new ProjectionRecordKey("view.test-record.v1", Convert.ToHexStringLower(recordId.Span));
if (deltaSnapshot.BasisStep != 101 || deltaSnapshot.Records[key].Revision != 2)
    throw new InvalidOperationException("DELTA publication must apply on the matching confirmed continuity token.");

var badDelta = new StatePublicationV1
{
    PublicationId = Id(14),
    Kind = (PublicationKindV1)2,
    StateContinuityToken = Hash(23),
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
var mismatchRejected = false;
try
{
    resync.ApplyOrEnterSuspect(badDelta, 102, [Chunk(badDelta, badDeltaPayload)]);
}
catch (ContinuityMismatchException)
{
    mismatchRejected = true;
}
if (!mismatchRejected || resync.State != GatewaySyncState.Suspect || resync.AllowsWorldAffectingAdmission)
    throw new InvalidOperationException("Continuity mismatch must gate normal admission and enter SUSPECT.");
if (cache.Current?.BasisStep != 101)
    throw new InvalidOperationException("Rejected DELTA must not replace the last confirmed snapshot.");

var request = resync.BeginResync(Id(42), forceFull: false);
if (!request.HasClientBasisStep || request.ClientBasisStep != 101 || !request.HasClientContinuityToken)
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

var scheduling = new SchedulingPolicyProjection();
var policy = new OperationSchedulingPolicyWireV1
{
    OwnerConfigGeneration = 7,
    MinLeadSteps = 2,
    DefaultDeadlineWindowSteps = 90,
    GraceSteps = 15,
    LatePolicy = (LatePolicyWireV1)2
};
var projected = scheduling.Apply(policy);
if (projected.OwnerConfigGeneration != 7 || projected.MinLeadSteps != 2 || projected.DefaultDeadlineWindowSteps != 90)
    throw new InvalidOperationException("Core scheduling policy projection failed.");
var stalePolicyRejected = false;
try
{
    scheduling.Apply(new OperationSchedulingPolicyWireV1
    {
        OwnerConfigGeneration = 6,
        MinLeadSteps = 2,
        GraceSteps = 15,
        LatePolicy = (LatePolicyWireV1)2
    });
}
catch (InvalidDataException)
{
    stalePolicyRejected = true;
}
if (!stalePolicyRejected) throw new InvalidOperationException("Stale scheduling policy generation must be rejected.");

Console.WriteLine("GW-01/GW-02 smoke tests passed.");
