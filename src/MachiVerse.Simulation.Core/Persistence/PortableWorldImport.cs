using MachiVerse.Simulation.Core.Determinism;

namespace MachiVerse.Simulation.Core.Persistence;

public sealed record PortableWorldImportPlan(
    string ExportDirectory,
    PersistenceMigrationPaths Migration);

/// <summary>
/// Validated staging boundary for the Phase 4 portable world export format.
/// Bundle codecs/replay verification remain schema-owned; activation always targets a new
/// persistence generation and never overwrites the current source generation directly.
/// </summary>
public static class PortableWorldImport
{
    public static PortableWorldImportPlan PrepareForExistingWorld(
        string exportDirectory,
        string persistenceRoot,
        OpaqueId128 worldId,
        ulong activeGeneration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);
        var export = Path.GetFullPath(exportDirectory);
        ValidateExportDirectoryShape(export);
        var migration = PersistenceGenerationMigration.Prepare(persistenceRoot, worldId, activeGeneration);
        return new PortableWorldImportPlan(export, migration);
    }

    public static async Task LoadAndActivateAsync(
        PortableWorldImportPlan plan,
        Func<string, OpaqueId128, CancellationToken, Task> verifyExport,
        Func<string, WorldPersistencePaths, CancellationToken, Task> loadIntoStaging,
        Func<WorldPersistencePaths, CancellationToken, Task> verifyImportedGeneration,
        OpaqueId128 expectedWorldId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(verifyExport);
        ArgumentNullException.ThrowIfNull(loadIntoStaging);
        ArgumentNullException.ThrowIfNull(verifyImportedGeneration);
        if (expectedWorldId.IsZero) throw new ArgumentException("WorldId ZERO is invalid for import.", nameof(expectedWorldId));

        ValidateExportDirectoryShape(plan.ExportDirectory);
        await verifyExport(plan.ExportDirectory, expectedWorldId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // The importer only receives the new staging generation. The active source generation
        // is not exposed as a write target, and CURRENT is unchanged until full validation.
        await loadIntoStaging(plan.ExportDirectory, plan.Migration.Staging, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        await PersistenceGenerationMigration.FinalizeValidatedAsync(
            plan.Migration,
            verifyImportedGeneration,
            cancellationToken);
    }

    public static void ValidateExportDirectoryShape(string exportDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);
        var root = Path.GetFullPath(exportDirectory);
        if (!Directory.Exists(root)) throw new InvalidDataException("persistence.export-missing");
        if (!File.Exists(Path.Combine(root, "export-manifest.pb")))
            throw new InvalidDataException("persistence.export-manifest-missing");
        if (!Directory.Exists(Path.Combine(root, "snapshot")))
            throw new InvalidDataException("persistence.export-snapshot-missing");
        if (!Directory.Exists(Path.Combine(root, "history")))
            throw new InvalidDataException("persistence.export-history-missing");

        foreach (var segment in Directory.EnumerateFiles(Path.Combine(root, "history"), "*.mvlog", SearchOption.TopDirectoryOnly))
        {
            var name = Path.GetFileNameWithoutExtension(segment);
            if (name.Length != 8 || name.Any(static c => c is < '0' or > '9'))
                throw new InvalidDataException("persistence.export-history-segment-name");
        }
    }
}
