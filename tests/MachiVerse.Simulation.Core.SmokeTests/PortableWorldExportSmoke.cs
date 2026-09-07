using System.Runtime.CompilerServices;
using System.Security.Cryptography;
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
            await File.WriteAllBytesAsync(Path.Combine(sourceSnapshot, "manifest.pb"), new byte[] { 1, 2, 3 });
            await File.WriteAllBytesAsync(Path.Combine(sourceSnapshot, "chunks", "00000000.mvchunk"), new byte[] { 4, 5, 6 });

            var exportFinal = Path.Combine(root, "MachiVerseWorldExportV1");
            var export = PortableWorldExport.Prepare(exportFinal);
            await PortableWorldExport.CopyCommittedSnapshotAsync(sourceSnapshot, export);

            var segmentDigest = SHA256.HashData("segment-logical"u8);
            var segmentPath = await PortableWorldExport.WriteHistorySegmentAsync(
                export,
                segmentIndex: 0,
                records:
                [
                    new ExportHistoryRecord(2, SHA256.HashData("record-2"u8), new byte[] { 10, 11 }),
                    new ExportHistoryRecord(3, SHA256.HashData("record-3"u8), new byte[] { 12, 13, 14 })
                ],
                segmentLogicalDigest: segmentDigest);
            await PortableWorldExport.ValidateHistorySegmentFramingAsync(segmentPath, segmentDigest);
            await PortableWorldExport.WriteManifestDurablyAsync(export, new byte[] { 20, 21, 22 });

            await PortableWorldExport.FinalizeValidatedAsync(
                export,
                async (candidate, cancellationToken) =>
                {
                    if (!File.Exists(candidate.ManifestPath))
                        throw new InvalidDataException("fixture.export-manifest-missing");
                    if (!File.Exists(Path.Combine(candidate.SnapshotDirectory, "manifest.pb")))
                        throw new InvalidDataException("fixture.export-snapshot-missing");
                    await PortableWorldExport.ValidateHistorySegmentFramingAsync(
                        Path.Combine(candidate.HistoryDirectory, "00000000.mvlog"),
                        segmentDigest,
                        cancellationToken);
                });

            PortableWorldImport.ValidateExportDirectoryShape(exportFinal);

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
                    PortableWorldImport.ValidateExportDirectoryShape(exportDirectory);
                    if (expectedWorldId.IsZero) throw new InvalidDataException("persistence.export-world-id-invalid");
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
