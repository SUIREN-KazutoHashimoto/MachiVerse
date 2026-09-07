using System.Buffers.Binary;
using System.Text;

namespace MachiVerse.Simulation.Core.Persistence;

public sealed record ExportHistorySegmentHeader(
    ushort FormatMajor,
    ushort FormatMinor,
    ulong FirstSequence,
    ulong LastSequence,
    uint RecordCount,
    byte[] LogicalDigest);

/// <summary>
/// Physical bundle contract fixed by the Phase 4 persistence record catalog.
/// Protobuf message semantics and logical digest calculation remain schema-owned; this type fixes
/// standard paths and MVLOG001 framing without treating protobuf wire bytes as semantic hashes.
/// </summary>
public static class PortableWorldBundleV1
{
    public const string ManifestRelativePath = "export-manifest.pb";
    public const string SnapshotRelativeDirectory = "snapshot";
    public const string HistoryRelativeDirectory = "history";
    public const string SnapshotManifestRelativePath = "snapshot/manifest.pb";
    public const int HistorySegmentHeaderLength = 64;

    private static ReadOnlySpan<byte> HistoryMagic => "MVLOG001"u8;

    public static string HistorySegmentRelativePath(uint segmentIndex)
        => $"history/{segmentIndex:D8}.mvlog";

    public static async Task WriteManifestAsync(
        PortableWorldExportPaths export,
        ReadOnlyMemory<byte> serializedManifest,
        CancellationToken cancellationToken = default)
    {
        if (serializedManifest.IsEmpty)
            throw new ArgumentException("Export manifest protobuf bytes cannot be empty.", nameof(serializedManifest));
        await PortableWorldExport.WriteArtifactDurablyAsync(
            export,
            ManifestRelativePath,
            serializedManifest,
            cancellationToken);
    }

    public static Task CopyCommittedSnapshotAsync(
        PortableWorldExportPaths export,
        string committedSnapshotDirectory,
        CancellationToken cancellationToken = default)
        => PortableWorldExport.CopyDirectoryDurablyAsync(
            export,
            committedSnapshotDirectory,
            SnapshotRelativeDirectory,
            cancellationToken);

    public static async Task WriteHistorySegmentAsync(
        PortableWorldExportPaths export,
        uint segmentIndex,
        ulong firstSequence,
        IReadOnlyList<ReadOnlyMemory<byte>> serializedRecords,
        ReadOnlyMemory<byte> segmentLogicalDigest,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(export);
        ArgumentNullException.ThrowIfNull(serializedRecords);
        if (serializedRecords.Count == 0)
            throw new ArgumentException("History segment must contain at least one record.", nameof(serializedRecords));
        if (serializedRecords.Count > uint.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(serializedRecords));
        if (segmentLogicalDigest.Length != 32)
            throw new ArgumentException("History segment logical digest must be 32 bytes.", nameof(segmentLogicalDigest));

        var lastSequence = checked(firstSequence + (ulong)serializedRecords.Count - 1UL);
        var path = PortableWorldExport.ResolveStagingPath(export, HistorySegmentRelativePath(segmentIndex));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        if (File.Exists(path)) throw new InvalidDataException("persistence.export-history-segment-exists");

        await using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.WriteThrough);

        var header = new byte[HistorySegmentHeaderLength];
        HistoryMagic.CopyTo(header.AsSpan(0, 8));
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(8, 2), 1);
        BinaryPrimitives.WriteUInt16BigEndian(header.AsSpan(10, 2), 0);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(12, 8), firstSequence);
        BinaryPrimitives.WriteUInt64BigEndian(header.AsSpan(20, 8), lastSequence);
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(28, 4), checked((uint)serializedRecords.Count));
        segmentLogicalDigest.Span.CopyTo(header.AsSpan(32, 32));
        await stream.WriteAsync(header, cancellationToken);

        var lengthBuffer = new byte[4];
        foreach (var record in serializedRecords)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (record.IsEmpty) throw new InvalidDataException("persistence.export-history-record-empty");
            if ((ulong)record.Length > uint.MaxValue)
                throw new InvalidDataException("persistence.export-history-record-too-large");
            BinaryPrimitives.WriteUInt32BigEndian(lengthBuffer, checked((uint)record.Length));
            await stream.WriteAsync(lengthBuffer, cancellationToken);
            await stream.WriteAsync(record, cancellationToken);
        }

        await stream.FlushAsync(cancellationToken);
        stream.Flush(flushToDisk: true);
    }

    public static async Task<ExportHistorySegmentHeader> ValidateHistorySegmentAsync(
        string segmentPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segmentPath);
        await using var stream = new FileStream(
            segmentPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var header = new byte[HistorySegmentHeaderLength];
        await ReadExactlyAsync(stream, header, cancellationToken);
        if (!header.AsSpan(0, 8).SequenceEqual(HistoryMagic))
            throw new InvalidDataException("persistence.export-history-magic-invalid");

        var major = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(8, 2));
        var minor = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(10, 2));
        if (major != 1 || minor != 0)
            throw new InvalidDataException("persistence.export-history-version-unsupported");

        var first = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(12, 8));
        var last = BinaryPrimitives.ReadUInt64BigEndian(header.AsSpan(20, 8));
        var count = BinaryPrimitives.ReadUInt32BigEndian(header.AsSpan(28, 4));
        if (count == 0 || last < first || checked(last - first + 1UL) != count)
            throw new InvalidDataException("persistence.export-history-range-invalid");
        var digest = header.AsSpan(32, 32).ToArray();

        var lengthBuffer = new byte[4];
        for (uint index = 0; index < count; index++)
        {
            await ReadExactlyAsync(stream, lengthBuffer, cancellationToken);
            var recordLength = BinaryPrimitives.ReadUInt32BigEndian(lengthBuffer);
            if (recordLength == 0)
                throw new InvalidDataException("persistence.export-history-record-empty");
            await SkipExactlyAsync(stream, recordLength, cancellationToken);
        }
        if (stream.ReadByte() != -1)
            throw new InvalidDataException("persistence.export-history-trailing-bytes");

        return new ExportHistorySegmentHeader(major, minor, first, last, count, digest);
    }

    public static void ValidateBundleStructure(string exportDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportDirectory);
        var root = Path.GetFullPath(exportDirectory);
        if (!Directory.Exists(root)) throw new InvalidDataException("persistence.export-missing");
        if (!File.Exists(Path.Combine(root, ManifestRelativePath)))
            throw new InvalidDataException("persistence.export-manifest-missing");
        if (!File.Exists(Path.Combine(root, SnapshotManifestRelativePath.Replace('/', Path.DirectorySeparatorChar))))
            throw new InvalidDataException("persistence.export-snapshot-manifest-missing");

        var historyDirectory = Path.Combine(root, HistoryRelativeDirectory);
        if (!Directory.Exists(historyDirectory))
            throw new InvalidDataException("persistence.export-history-directory-missing");

        var segments = Directory.EnumerateFiles(historyDirectory, "*.mvlog", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .OrderBy(static name => name, StringComparer.Ordinal)
            .ToArray();
        for (var i = 0; i < segments.Length; i++)
        {
            var expected = $"{i:D8}.mvlog";
            if (!string.Equals(segments[i], expected, StringComparison.Ordinal))
                throw new InvalidDataException("persistence.export-history-segment-name-invalid");
        }
    }

    private static async Task ReadExactlyAsync(Stream stream, Memory<byte> buffer, CancellationToken cancellationToken)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer[offset..], cancellationToken);
            if (read == 0) throw new EndOfStreamException("persistence.export-history-truncated");
            offset += read;
        }
    }

    private static async Task SkipExactlyAsync(Stream stream, uint count, CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        ulong remaining = count;
        while (remaining > 0)
        {
            var requested = (int)Math.Min((ulong)buffer.Length, remaining);
            var read = await stream.ReadAsync(buffer.AsMemory(0, requested), cancellationToken);
            if (read == 0) throw new EndOfStreamException("persistence.export-history-truncated");
            remaining -= (uint)read;
        }
    }
}
