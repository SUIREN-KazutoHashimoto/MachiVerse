using System.Runtime.CompilerServices;
using MachiVerse.Simulation.Core.Determinism;
using MachiVerse.Simulation.Core.Persistence;

internal static class PortableWorldExportSmoke
{
    [ModuleInitializer]
    internal static void Initialize() => RunAsync().GetAwaiter().GetResult();

    private static async Task RunAsync()
    {
        var root = Path.Combine(Path.GetTempPath(), "machiverse-sim03-export-" + Guid.NewGuid().ToString("N"));
        try
        {
            var sourceSnapshot = Path.Combine(root, "source-snapshot");
            Directory.CreateDirectory(Path.Combine(sourceSnapshot, "chunks"));
            await File.WriteAllBytesAsync(Path.Combine(sourceSnapshot, "manifest.fixture"), [1, 2, 3]);
            await File.WriteAllBytesAsync(Path.Combine(sourceSnapshot, "chunks", "chunk.fixture"), [4, 5, 6]);

            var exportFinal = Path.Combine(root, "portable-export");
            var export = PortableWorldExport.Prepare(exportFinal);
            await PortableWorldExport.CopyTreeDurablyAsync(export, sourceSnapshot, "state");
            await PortableWorldExport.WriteArtifactDurablyAsync(export, "metadata/custom-format.bin", [20, 21, 22]);
            await PortableWorldExport.WriteArtifactDurablyAsync(export, "history/range.custom", [30, 31, 32]);

            var traversalRejected = false;
            try
            {
                PortableWorldExport.ResolveArtifactPath(export, "../escape.bin");
            }
            catch (InvalidDataException)
            {
                traversalRejected = true;
            }
            if (!traversalRejected)
                throw new InvalidOperationException("Portable export boundary must reject path traversal.");

            await PortableWorldExport.FinalizeValidatedAsync(
                export,
                static (candidate, _) =>
                {
                    if (!File.Exists(Path.Combine(candidate.StagingDirectory, "metadata", "custom-format.bin")))
                        throw new InvalidDataException("fixture.export-metadata-missing");
                    if (!File.Exists(Path.Combine(candidate.StagingDirectory, "state", "chunks", "chunk.fixture")))
                        throw new InvalidDataException("fixture.export-state-missing");
                    return Task.CompletedTask;
                });

            PortableWorldImport.ValidateBundleBoundary(exportFinal);
            if (File.Exists(Path.Combine(exportFinal, "export-manifest.pb")) ||
                Directory.EnumerateFiles(exportFinal, "*.mvlog", SearchOption.AllDirectories).Any())
                throw new InvalidOperationException("SIM-03 must not invent the unresolved backup/export bundle format.");

            var persistenceRoot = Path.Combine(root, "persistence");
            var worldId = OpaqueId128.Parse("00000000000000000000000000000051");
            var active = PersistenceLayout.Resolve(persistenceRoot, worldId, 1);
            PersistenceLayout.EnsureGenerationDirectories(active);
            await File.WriteAllTextAsync(active.DatabasePath, "active-generation");
            await PersistenceLayout.WriteCurrentAsync(active, 1);

            var import = PortableWorldImport.PrepareForExistingWorld(exportFinal, persistenceRoot, worldId, 1);
            if (PersistenceLayout.ReadCurrent(active) != 1)
                throw new InvalidOperationException("Preparing import must not alter the active generation.");

            await PortableWorldImport.LoadAndActivateAsync(
                import,
                static (exportDirectory, expectedWorldId, _) =>
                {
                    PortableWorldImport.ValidateBundleBoundary(exportDirectory);
                    if (expectedWorldId.IsZero) throw new InvalidDataException("persistence.export-world-id-invalid");
                    if (!File.Exists(Path.Combine(exportDirectory, "metadata", "custom-format.bin")))
                        throw new InvalidDataException("fixture.export-format-verification-failed");
                    return Task.CompletedTask;
                },
                static async (_, staging, cancellationToken) =>
                {
                    await File.WriteAllTextAsync(staging.DatabasePath, "imported-generation", cancellationToken);
                    Directory.CreateDirectory(staging.SnapshotsDirectory);
                },
                static (staging, _) =>
                {
                    if (!File.Exists(staging.DatabasePath))
                        throw new InvalidDataException("persistence.import-staging-database-missing");
                    return Task.CompletedTask;
                },
                worldId);

            if (PersistenceLayout.ReadCurrent(active) != 2)
                throw new InvalidOperationException("Validated import must activate the new persistence generation.");
            if (!Directory.Exists(active.GenerationDirectory))
                throw new InvalidOperationException("Import must not destroy the source active generation.");
            if (!Directory.Exists(import.Migration.Target.GenerationDirectory))
                throw new InvalidOperationException("Imported target generation was not finalized.");
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
