using System.Buffers.Binary;

namespace MachiVerse.SimulationCore.Primitives;

public readonly record struct Id128(ulong High, ulong Low)
{
    public const int ByteLength = 16;
    public const int HexLength = 32;

    public bool IsZero => High == 0 && Low == 0;

    public static Id128 FromBytes(ReadOnlySpan<byte> bytes, bool allowZero = false)
    {
        if (bytes.Length != ByteLength)
        {
            throw new ArgumentException($"Id128 must be exactly {ByteLength} octets.", nameof(bytes));
        }

        var value = new Id128(
            BinaryPrimitives.ReadUInt64BigEndian(bytes[..8]),
            BinaryPrimitives.ReadUInt64BigEndian(bytes[8..]));

        if (!allowZero && value.IsZero)
        {
            throw new ArgumentException("ZERO Id128 is not valid for this identity.", nameof(bytes));
        }

        return value;
    }

    public static bool TryParseHex(string? text, out Id128 value, bool allowZero = false)
    {
        value = default;
        if (text is null || text.Length != HexLength)
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromHexString(text);
        }
        catch (FormatException)
        {
            return false;
        }

        if (bytes.Length != ByteLength)
        {
            return false;
        }

        value = new Id128(
            BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(0, 8)),
            BinaryPrimitives.ReadUInt64BigEndian(bytes.AsSpan(8, 8)));
        return allowZero || !value.IsZero;
    }

    public void WriteBytes(Span<byte> destination)
    {
        if (destination.Length < ByteLength)
        {
            throw new ArgumentException($"Destination must provide at least {ByteLength} octets.", nameof(destination));
        }

        BinaryPrimitives.WriteUInt64BigEndian(destination[..8], High);
        BinaryPrimitives.WriteUInt64BigEndian(destination[8..16], Low);
    }

    public override string ToString() => $"{High:x16}{Low:x16}";
}
