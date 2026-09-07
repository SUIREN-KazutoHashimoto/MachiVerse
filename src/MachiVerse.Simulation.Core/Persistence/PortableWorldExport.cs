namespace MachiVerse.Simulation.Core.Persistence;

public sealed record PortableWorldExportPaths(
    string StagingDirectory,
    string FinalDirectory);

/// <summary>
/// Crash-safe filesystem boundary for a future portable world bundle.
///
/// Phase 4 explicitly leaves the backup/export bundle format unresolved. This type therefore
/// does not define a manifest schema, history segment framing, file extension, or directory
/// layout. A higher-level format implementation may write arbitrary validated artifacts inside
/// StagingDirectory and then atomically publish the completed bundle through FinalizeValidatedAsync.
/// </summary>
public static class PortableWorldExport
{
    public static PortableWorldExportPaths Prepare(string finalDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(finalDirectory);
        var final = Path.GetFullPath(finalDirectory);
        var parent = Path.GetDirectoryName(final)
            ?? throw new ArgumentException("Export destination must have a parent directory.", nameof(finalDirectory));
        Directory.CreateDirectory(parent);
        if (Directory.Exists(final) || File.Exists(final))
            throw new InvalidDataException("persistence.export-target-exists");

        var staging = final + ".staging-" + Guid.NewGuid().ToString("N");
        if (Directory.Exists(staging) || File.Exists(staging))
            throw new InvalidDataException("persistence.export-staging-exists");
        Directory.CreateDirectory(staging);
        return new PortableWorldExportPaths(staging, final);
    }

    public static string ResolveArtifactPath(PortableWorldExportPaths export, string relativePath)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath) || relativePath.Contains("..", StringComparison.Ordinal) || relativePath.Contains('\\'))
            throw new InvalidDataException("persistence.export-path-invalid");

        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var candidate = Path.GetFullPath(Path.Combine(export.StagingDirectory, normalized));
        var root = Path.GetFullPath(export.StagingDirectory) + Path.DirectorySeparatorChar;
        if (!candidate.StartsWith(root, StringComparison.Ordinal))
            throw new InvalidDataException("persistence.export-path-invalid");
        return candidate;
    }

    public static async Task WriteArtifactDurablyAsync(
        PortableWorldExportPaths export,
        string relativePath,
        ReadOnlyMemory<byte> bytes,
        CancellationToken cancellationToken = default)
    {
        if (bytes.IsEmpty) throw new ArgumentException("Export artifact cannot be empty.", nameof(bytes));
        var path = ResolveArtifactPath(export, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) throw new InvalidDataException("persistence.export-artifact-exists");

        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(bytes, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    public static async Task CopyTreeDurablyAsync(
        PortableWorldExportPaths export,
        string sourceDirectory,
        string destinationRelativeDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory);
        if (!Directory.Exists(sourceDirectory))
            throw new InvalidDataException("persistence.export-source-missing");

        var destination = ResolveArtifactPath(export, destinationRelativeDirectory + "/.boundary");
        destination = Path.GetDirectoryName(destination)!;
        Directory.CreateDirectory(destination);
        if (Directory.EnumerateFileSystemEntries(destination).Any())
            throw new InvalidDataException("persistence.export-copy-target-not-empty");

        foreach (var directory in Directory.EnumerateDirectories(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, directory);
            ValidateSourceRelativePath(relative);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(sourceDirectory, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(sourceDirectory, file);
            ValidateSourceRelativePath(relative);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read, 128 * 1024, FileOptions.Asynchronous);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 128 * 1024, FileOptions.Asynchronous | FileOptions.WriteThrough);
            await input.CopyToAsync(output, cancellationToken);
            await output.FlushAsync(cancellationToken);
            output.Flush(flushToDisk: true);
        }
    }

    public static async Task FinalizeValidatedAsync(
        PortableWorldExportPaths export,
        Func<PortableWorldExportPaths, CancellationToken, Task> verifyBundle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(verifyBundle);
        if (!Directory.Exists(export.StagingDirectory))
            throw new InvalidDataException("persistence.export-staging-missing");
        if (!Directory.EnumerateFileSystemEntries(export.StagingDirectory).Any())
            throw new InvalidDataException("persistence.export-staging-empty");

        await verifyBundle(export, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            foreach (var directory in Directory.EnumerateDirectories(export.StagingDirectory, "*", SearchOption.AllDirectories)
                         .OrderByDescending(static value => value.Length))
                DurableFileSystem.FlushDirectory(directory);
            DurableFileSystem.FlushDirectory(export.StagingDirectory);
        }
        DurableFileSystem.AtomicMoveDirectory(export.StagingDirectory, export.FinalDirectory);
        if (!OperatingSystem.IsWindows())
            DurableFileSystem.FlushDirectory(Path.GetDirectoryName(export.FinalDirectory)!);
    }

    private static void ValidateSourceRelativePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.StartsWith("..", StringComparison.Ordinal))
            throw new InvalidDataException("persistence.export-path-invalid");
    }
}
