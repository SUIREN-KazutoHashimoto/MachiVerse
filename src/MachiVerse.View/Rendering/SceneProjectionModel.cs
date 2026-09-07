using MachiVerse.View.State;

namespace MachiVerse.View.Rendering;

public sealed record SceneProjectionRecord(
    string RecordSchemaId,
    string RecordIdHex,
    ulong RecordRevision);

public sealed record SceneProjectionModel(
    ulong BasisStep,
    string ContinuityTokenHex,
    string ProjectionSchemaDigestHex,
    IReadOnlyList<SceneProjectionRecord> Records)
{
    public static SceneProjectionModel FromConfirmed(ConfirmedWorldSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (snapshot.ContinuityToken.Length == 0)
            throw new InvalidDataException("view.scene-projection.missing-continuity-token");
        if (snapshot.ProjectionSchemaDigest.Length != 32)
            throw new InvalidDataException("view.scene-projection.invalid-schema-digest");

        var records = snapshot.Records
            .OrderBy(static item => item.Key.SchemaId, StringComparer.Ordinal)
            .ThenBy(static item => item.Key.RecordIdHex, StringComparer.Ordinal)
            .Select(static item => new SceneProjectionRecord(
                item.Value.SchemaId,
                item.Key.RecordIdHex,
                item.Value.Revision))
            .ToArray();

        return new SceneProjectionModel(
            snapshot.BasisStep,
            Convert.ToHexStringLower(snapshot.ContinuityToken),
            Convert.ToHexStringLower(snapshot.ProjectionSchemaDigest),
            records);
    }
}
