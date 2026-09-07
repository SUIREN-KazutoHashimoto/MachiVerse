using System.Security.Cryptography;
using Google.Protobuf;
using MachiVerse.Protocol.V1;
using MachiVerse.View.Protocol;
using MachiVerse.View.State;

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

var publicationId = Id(1);
var recordId = Id(2);
var payload = new ProjectionChunkPayloadV1
{
    SubscriptionId = Id(3),
    PublicationId = publicationId,
    ChunkIndex = 0
};
payload.Records.Add(new ProjectionRecordV1
{
    RecordSchemaId = "view.test-record.v1",
    RecordSchemaVersion = new SchemaVersionWireV1 { Major = 1, Minor = 0 },
    RecordId = recordId,
    RecordRevision = 1,
    MutationKind = (ProjectionMutationKindV1)1,
    Payload = ByteString.CopyFromUtf8("record-v1")
});
var publication = new StatePublicationV1
{
    PublicationId = publicationId,
    Kind = (PublicationKindV1)1,
    StateContinuityToken = Hash(4),
    ChunkCount = 1,
    ProjectionSchemaDigest = Hash(5)
};

var store = new ConfirmedWorldStore();
var consumer = new PublicationConsumer(store);
var confirmed = consumer.Consume(publication, 20, [Chunk(publication, payload)]);
if (consumer.LifecycleState != ViewLifecycleState.Ready || confirmed.BasisStep != 20 || confirmed.Records.Count != 1)
    throw new InvalidOperationException("FULL publication must atomically produce a Ready confirmed snapshot.");

var delta = new StatePublicationV1
{
    PublicationId = Id(6),
    Kind = (PublicationKindV1)2,
    StateContinuityToken = Hash(7),
    BaseStateContinuityToken = Hash(4),
    ChunkCount = 1,
    ProjectionSchemaDigest = Hash(5)
};
var deltaPayload = new ProjectionChunkPayloadV1
{
    SubscriptionId = Id(3),
    PublicationId = delta.PublicationId,
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
var deltaConfirmed = consumer.Consume(delta, 21, [Chunk(delta, deltaPayload)]);
var key = new ProjectionRecordKey("view.test-record.v1", Convert.ToHexStringLower(recordId.Span));
if (deltaConfirmed.BasisStep != 21 || deltaConfirmed.Records[key].Revision != 2 || consumer.LifecycleState != ViewLifecycleState.Ready)
    throw new InvalidOperationException("Matching DELTA must advance the confirmed snapshot atomically.");

var mismatch = new StatePublicationV1
{
    PublicationId = Id(8),
    Kind = (PublicationKindV1)2,
    StateContinuityToken = Hash(9),
    BaseStateContinuityToken = Hash(99),
    ChunkCount = 1,
    ProjectionSchemaDigest = Hash(5)
};
var mismatchPayload = new ProjectionChunkPayloadV1
{
    SubscriptionId = Id(3),
    PublicationId = mismatch.PublicationId,
    ChunkIndex = 0
};
var rejected = false;
try
{
    consumer.Consume(mismatch, 22, [Chunk(mismatch, mismatchPayload)]);
}
catch (ContinuityMismatchException)
{
    rejected = true;
}
if (!rejected || consumer.LifecycleState != ViewLifecycleState.Resyncing)
    throw new InvalidOperationException("Continuity mismatch must enter Resyncing and remain outside normal confirmed state.");
if (store.Current?.BasisStep != 21)
    throw new InvalidOperationException("Rejected DELTA must not replace the last confirmed snapshot.");

var request = consumer.CreateResyncRequest(Id(10), forceFull: false);
if (!request.HasClientBasisStep || request.ClientBasisStep != 21 || !request.HasClientContinuityToken)
    throw new InvalidOperationException("Resync request must use last confirmed basis/token.");

Console.WriteLine("VIEW-02 smoke tests passed.");
