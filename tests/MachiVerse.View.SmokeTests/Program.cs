using System.Security.Cryptography;
using Google.Protobuf;
using MachiVerse.Protocol.V1;
using MachiVerse.View.Protocol;
using MachiVerse.View.State;

static ByteString Id(byte value) => ByteString.CopyFrom(Enumerable.Repeat(value, 16).ToArray());
static ByteString Hash(byte value) => ByteString.CopyFrom(Enumerable.Repeat(value, 32).ToArray());

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
    Payload = ByteString.CopyFromUtf8("record")
});
var bytes = payload.ToByteArray();
var publication = new StatePublicationV1
{
    PublicationId = publicationId,
    Kind = (PublicationKindV1)1,
    StateContinuityToken = Hash(4),
    ChunkCount = 1,
    ProjectionSchemaDigest = Hash(5)
};
var chunk = new StatePublicationChunkV1
{
    PublicationId = publicationId,
    ChunkIndex = 0,
    ChunkCount = 1,
    UncompressedPayloadDigest = ByteString.CopyFrom(SHA256.HashData(bytes)),
    Compression = (CompressionKindV1)1,
    Payload = ByteString.CopyFrom(bytes)
};

var store = new ConfirmedWorldStore();
var consumer = new PublicationConsumer(store);
var confirmed = consumer.Consume(publication, 20, [chunk]);
if (consumer.LifecycleState != ViewLifecycleState.Ready || confirmed.BasisStep != 20 || confirmed.Records.Count != 1)
    throw new InvalidOperationException("FULL publication must atomically produce a Ready confirmed snapshot.");

var mismatchedPublicationId = Id(6);
var mismatchPayload = new ProjectionChunkPayloadV1
{
    SubscriptionId = Id(3),
    PublicationId = mismatchedPublicationId,
    ChunkIndex = 0
};
var mismatchBytes = mismatchPayload.ToByteArray();
var mismatch = new StatePublicationV1
{
    PublicationId = mismatchedPublicationId,
    Kind = (PublicationKindV1)2,
    StateContinuityToken = Hash(7),
    BaseStateContinuityToken = Hash(99),
    ChunkCount = 1,
    ProjectionSchemaDigest = Hash(5)
};
var mismatchChunk = new StatePublicationChunkV1
{
    PublicationId = mismatchedPublicationId,
    ChunkIndex = 0,
    ChunkCount = 1,
    UncompressedPayloadDigest = ByteString.CopyFrom(SHA256.HashData(mismatchBytes)),
    Compression = (CompressionKindV1)1,
    Payload = ByteString.CopyFrom(mismatchBytes)
};
var rejected = false;
try
{
    consumer.Consume(mismatch, 21, [mismatchChunk]);
}
catch (ContinuityMismatchException)
{
    rejected = true;
}
if (!rejected || consumer.LifecycleState != ViewLifecycleState.Resyncing)
    throw new InvalidOperationException("Continuity mismatch must enter Resyncing and remain outside normal confirmed state.");
if (store.Current?.BasisStep != 20)
    throw new InvalidOperationException("Rejected DELTA must not replace the last confirmed snapshot.");

var request = consumer.CreateResyncRequest(Id(8), forceFull: false);
if (!request.HasClientBasisStep || request.ClientBasisStep != 20 || !request.HasClientContinuityToken)
    throw new InvalidOperationException("Resync request must use last confirmed basis/token.");

Console.WriteLine("VIEW-02 smoke tests passed.");
