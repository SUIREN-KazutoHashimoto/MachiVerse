namespace MachiVerse.View.State;

public readonly record struct ProjectionRecordKey(string SchemaId, string RecordIdHex);

public sealed record ConfirmedProjectionRecord(
    string SchemaId,
    byte[] RecordId,
    ulong Revision,
    byte[] Payload);

public sealed record ConfirmedWorldSnapshot(
    ulong BasisStep,
    byte[] ContinuityToken,
    byte[] ProjectionSchemaDigest,
    IReadOnlyDictionary<ProjectionRecordKey, ConfirmedProjectionRecord> Records);

public sealed class ConfirmedWorldStore
{
    private ConfirmedWorldSnapshot? _current;

    public ConfirmedWorldSnapshot? Current => Volatile.Read(ref _current);

    internal void Install(ConfirmedWorldSnapshot snapshot) => Volatile.Write(ref _current, snapshot);

    public void ClearForWorldChange() => Volatile.Write(ref _current, null);
}

public sealed class ContinuityMismatchException(string message) : Exception(message);
