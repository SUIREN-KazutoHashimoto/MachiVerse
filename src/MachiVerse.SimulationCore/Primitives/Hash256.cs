using System.Buffers.Binary;
using System.Security.Cryptography;

namespace MachiVerse.SimulationCore.Primitives;

public readonly record struct Hash256(ulong A, ulong B, ulong C, ulong D)
{
    public const int ByteLength = 32;
    public const int HexLength = 64;

    public static Hash256 FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != ByteLength)
        {
            throw new ArgumentException($"Hash256 must be exactly {ByteLength} octets.", nameof(bytes));
        }

        return new Hash256(
            BinaryPrimitives.ReadUInt64BigEndian(bytes[..8]),
            BinaryPrimitives.ReadUInt64BigEndian(bytes[8..16]),
            BinaryPrimitives.ReadUInt64BigEndian(bytes[16..24]),
            BinaryPrimitives.ReadUInt64BigEndian(bytes[24..32]));
    }

    public static Hash256 Sha256(ReadOnlySpan<byte> data)
    {
        Span<byte> digest = stackalloc byte[ByteLength];
        SHA256.HashData(data, digest);
        return FromBytes(digest);
    }

    public void WriteBytes(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
        {
            throw new ArgumentException($"Destination must provide at least {ByteLength} octets.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination[..8], A);
        BinaryPrimitives.WriteUInt64BigEndian(destination[8..16], B);
        BinaryPrimitives.WriteUInt64BigEndian(destination[16..24], C);
        BinaryPrimitives.WriteUInt64BigEndian(destination[24..32], D);
    }

    public override string ToString() => $"{A:x16}{B:x16}{C:x16}{D:x16}";
}
