using MachiVerse.Simulation.Core.Determinism;

namespace MachiVerse.Simulation.Core.Persistence;

public sealed record PortableWorldImportPlan(
    string ExportDirectory,
    PersistenceMigrationPaths Migration);

/// <summary>
/// Validated staging boundary for importing a future portable world bundle.
///
/// The concrete bundle format is intentionally delegated to verifyExport/loadIntoStaging because
/// Phase 4 does not yet define the backup/export bundle encoding. This type only guarantees that
/// import occurs into a new persistence generation and CURRENT switches after validation.
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
        ValidateBundleBoundary(export);
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

        ValidateBundleBoundary(plan.ExportDirectory);
        await verifyExport(plan.ExportDirectory, expectedWorldId, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // The loader receives only the new staging generation. The active source generation is
        // never exposed as a write target, and CURRENT remains unchanged until target validation.
        await loadIntoStaging(plan.ExportDirectory, plan.Migration.Staging, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        await PersistenceGenerationMigration.FinalizeValidatedAsync(
            plan.Migration,
            verifyImportedGeneration,
            cancellationToken);
    }

    public static void ValidateBundleBoundary(string exportDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);
        var root = Path.GetFullPath(exportDirectory);
        if (!Directory.Exists(root)) throw new InvalidDataException("persistence.export-missing");
        if (!Directory.EnumerateFileSystemEntries(root).Any())
            throw new InvalidDataException("persistence.export-empty");
    }
}
