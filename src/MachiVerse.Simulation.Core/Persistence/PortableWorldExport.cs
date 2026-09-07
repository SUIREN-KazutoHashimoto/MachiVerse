using System.Buffers.Binary;
using System.Security.Cryptography;

namespace MachiVerse.Simulation.Core.Persistence;

public sealed record PortableWorldExportPaths(
    string StagingDirectory,
    string FinalDirectory,
    string SnapshotDirectory,
    string HistoryDirectory,
    string ManifestPath);

public sealed record ExportHistoryRecord(
    ulong Sequence,
    byte[] RecordDigest,
    byte[] EncodedRecord);

/// <summary>
/// Phase 4 portable world export physical boundary.
/// The protobuf codecs for WorldExportManifestV1 and HistoryRecordExportWireV1 remain schema-owned;
/// this type fixes the directory layout, MVLOG001 framing, durable staging and final publication.
/// </summary>
public static class PortableWorldExport
{
    private static ReadOnlySpan<byte> SegmentMagic => "MVLOG001"u8;
    private const ushort FormatMajor = 1;
    private const ushort FormatMinor = 0;
    private const int SegmentHeaderLength = 64;

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
        var snapshot = Path.Combine(staging, "snapshot");
        var history = Path.Combine(staging, "history");
        Directory.CreateDirectory(snapshot);
        Directory.CreateDirectory(history);
        return new PortableWorldExportPaths(
            staging,
            final,
            snapshot,
            history,
            Path.Combine(staging, "export-manifest.pb"));
    }

    public static async Task CopyCommittedSnapshotAsync(
        string committedSnapshotDirectory,
        PortableWorldExportPaths export,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(committedSnapshotDirectory);
        ArgumentNullException.ThrowIfNull(export);
        if (!Directory.Exists(committedSnapshotDirectory))
            throw new InvalidDataException("persistence.export-snapshot-missing");
        if (Directory.EnumerateFileSystemEntries(export.SnapshotDirectory).Any())
            throw new InvalidDataException("persistence.export-snapshot-target-not-empty");

        await CopyDirectoryDurablyAsync(committedSnapshotDirectory, export.SnapshotDirectory, cancellationToken);
    }

    public static async Task<string> WriteHistorySegmentAsync(
        PortableWorldExportPaths export,
        uint segmentIndex,
        IReadOnlyList<ExportHistoryRecord> records,
        byte[] segmentLogicalDigest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(records);
        RequireHash256(segmentLogicalDigest, nameof(segmentLogicalDigest));
        if (records.Count == 0) throw new ArgumentException("History segment must contain at least one record.", nameof(records));

        var first = records[0].Sequence;
        if (first == 0) throw new InvalidDataException("persistence.export-history-sequence-zero");
        var expected = first;
        for (var index = 0; index < records.Count; index++)
        {
            var record = records[index];
            if (record.Sequence != expected)
                throw new InvalidDataException("persistence.export-history-gap");
            RequireHash256(record.RecordDigest, nameof(record.RecordDigest));
            if (record.EncodedRecord is null || record.EncodedRecord.Length == 0)
                throw new InvalidDataException("persistence.export-history-record-empty");
            if (record.EncodedRecord.LongLength > uint.MaxValue)
                throw new InvalidDataException("persistence.export-history-record-too-large");
            if (index + 1 < records.Count)
            {
                if (expected == ulong.MaxValue) throw new OverflowException("HistorySequence cannot wrap.");
                expected++;
            }
        }

        var last = records[^1].Sequence;
        var path = Path.Combine(export.HistoryDirectory, $"{segmentIndex:00000000}.mvlog");
        if (File.Exists(path)) throw new InvalidDataException("persistence.export-history-segment-exists");

        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);

        var header = new byte[SegmentHeaderLength];
        SegmentMagic.CopyTo(header);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(8, 2), FormatMajor);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(10, 2), FormatMinor);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(12, 8), first);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(20, 8), last);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(28, 4), checked((uint)records.Count));
        segmentLogicalDigest.CopyTo(header, 32);
        await stream.WriteAsync(header, cancellationToken);

        var lengthBuffer = new byte[4];
        foreach (var record in records)
        {
            BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, checked((uint)record.EncodedRecord.Length));
            await stream.WriteAsync(lengthBuffer, cancellationToken);
            await stream.WriteAsync(record.EncodedRecord, cancellationToken);
        }

        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
        return path;
    }

    public static async Task WriteManifestDurablyAsync(
        PortableWorldExportPaths export,
        ReadOnlyMemory<byte> manifestProtobuf,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(export);
        if (manifestProtobuf.IsEmpty) throw new ArgumentException("Export manifest cannot be empty.", nameof(manifestProtobuf));
        if (File.Exists(export.ManifestPath)) throw new InvalidDataException("persistence.export-manifest-exists");

        await using var stream = new FileStream(
            export.ManifestPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 64 * 1024,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);
        await stream.WriteAsync(manifestProtobuf, cancellationToken);
        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    public static async Task FinalizeValidatedAsync(
        PortableWorldExportPaths export,
        Func<PortableWorldExportPaths, CancellationToken, Task> verifyBundle,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(verifyBundle);
        if (!File.Exists(export.ManifestPath)) throw new InvalidDataException("persistence.export-manifest-missing");
        if (!Directory.EnumerateFileSystemEntries(export.SnapshotDirectory).Any())
            throw new InvalidDataException("persistence.export-snapshot-empty");

        await verifyBundle(export, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        if (!OperatingSystem.IsWindows())
        {
            FlushTreeDirectories(export.SnapshotDirectory);
            FlushTreeDirectories(export.HistoryDirectory);
            DurableFileSystem.FlushDirectory(export.StagingDirectory);
        }
        DurableFileSystem.AtomicMoveDirectory(export.StagingDirectory, export.FinalDirectory);
    }

    public static async Task ValidateHistorySegmentFramingAsync(
        string path,
        byte[] expectedSegmentLogicalDigest,
        CancellationToken cancellationToken = default)
    {
        RequireHash256(expectedSegmentLogicalDigest, nameof(expectedSegmentLogicalDigest));
        await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous);
        if (stream.Length < SegmentHeaderLength) throw new InvalidDataException("persistence.export-history-segment-truncated");
        var header = new byte[SegmentHeaderLength];
        await stream.ReadExactlyAsync(header, cancellationToken);
        if (!header.AsSpan(0, 8).SequenceEqual(SegmentMagic)) throw new InvalidDataException("persistence.export-history-magic");
        if (BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(8, 2)) != FormatMajor ||
            BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(10, 2)) != FormatMinor)
            throw new InvalidDataException("persistence.export-history-version");
        var first = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(12, 8));
        var last = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(20, 8));
        var count = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(28, 4));
        if (count == 0 || first == 0 || last < first || last - first + 1 != count)
            throw new InvalidDataException("persistence.export-history-range");
        if (!CryptographicOperations.FixedTimeEquals(header.AsSpan(32, 32), expectedSegmentLogicalDigest))
            throw new InvalidDataException("persistence.export-history-logical-digest");

        for (uint index = 0; index < count; index++)
        {
            var lengthBytes = new byte[4];
            await stream.ReadExactlyAsync(lengthBytes, cancellationToken);
            var length = BinaryPrimitives.ReadUInt32BigEndian(lengthBytes);
            if (length == 0 || length > int.MaxValue)
                throw new InvalidDataException("persistence.export-history-record-length");
            var record = new byte[checked((int)length)];
            await stream.ReadExactlyAsync(record, cancellationToken);
        }
        if (stream.Position != stream.Length)
            throw new InvalidDataException("persistence.export-history-trailing-bytes");
    }

    private static async Task CopyDirectoryDurablyAsync(string source, string destination, CancellationToken cancellationToken)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            ValidateSourceRelativePath(relative);
            Directory.CreateDirectory(Path.Combine(destination, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
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

    private static void FlushTreeDirectories(string root)
    {
        foreach (var directory in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories)
                     .OrderByDescending(static value => value.Length))
            DurableFileSystem.FlushDirectory(directory);
        DurableFileSystem.FlushDirectory(root);
    }

    private static void ValidateSourceRelativePath(string relativePath)
    {
        if (Path.IsPathRooted(relativePath) || relativePath.StartsWith("..", StringComparison.Ordinal))
            throw new InvalidDataException("persistence.export-path-invalid");
    }

    private static void RequireHash256(byte[] value, string field)
    {
        ArgumentNullException.ThrowIfNull(value, field);
        if (value.Length != 32) throw new ArgumentException($"{field} must be exactly 32 bytes.", field);
    }
}
