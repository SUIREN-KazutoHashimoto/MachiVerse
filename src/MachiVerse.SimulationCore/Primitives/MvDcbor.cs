using System.Buffers;
using System.Buffers.Binary;
using System.Text;

namespace MachiVerse.SimulationCore.Primitives;

public interface IMvDcborValue;

public sealed record MvUnsigned(ulong Value) : IMvDcborValue;
public sealed record MvNegative(long Value) : IMvDcborValue;
public sealed record MvByteString(ReadOnlyMemory<byte> Value) : IMvDcborValue;
public sealed record MvText(string Value) : IMvDcborValue;
public sealed record MvArray(IReadOnlyList<IMvDcborValue> Items) : IMvDcborValue;
public sealed record MvMap(IReadOnlyList<KeyValuePair<IMvDcborValue, IMvDcborValue>> Entries) : IMvDcborValue;
public sealed record MvBoolean(bool Value) : IMvDcborValue;
public sealed record MvNull : IMvDcborValue;

public static class MvDcbor
{
    public static byte[] Encode(IMvDcborValue value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var writer = new ArrayBufferWriter<byte>();
        WriteValue(writer, value);
        return writer.WrittenSpan.ToArray();
    }

    private static void WriteValue(IBufferWriter<byte> writer, IMvDcborValue value)
    {
        switch (value)
        {
            case MvUnsigned unsigned:
                WriteHead(writer, 0, unsigned.Value);
                return;
            case MvNegative negative when negative.Value < 0:
                WriteHead(writer, 1, checked((ulong)(-1 - negative.Value)));
                return;
            case MvNegative:
                throw new ArgumentOutOfRangeException(nameof(value), "MvNegative requires a negative integer.");
            case MvByteString bytes:
                WriteHead(writer, 2, checked((ulong)bytes.Value.Length));
                WriteBytes(writer, bytes.Value.Span);
                return;
            case MvText text:
                var utf8 = Encoding.UTF8.GetBytes(text.Value);
                WriteHead(writer, 3, checked((ulong)utf8.Length));
                WriteBytes(writer, utf8);
                return;
            case MvArray array:
                WriteHead(writer, 4, checked((ulong)array.Items.Count));
                foreach (var item in array.Items)
                {
                    WriteValue(writer, item);
                }
                return;
            case MvMap map:
                WriteMap(writer, map);
                return;
            case MvBoolean boolean:
                WriteByte(writer, boolean.Value ? (byte)0xf5 : (byte)0xf4);
                return;
            case MvNull:
                WriteByte(writer, 0xf6);
                return;
            default:
                throw new NotSupportedException($"Unsupported MV-DCBOR value type: {value.GetType().FullName}");
        }
    }

    private static void WriteMap(IBufferWriter<byte> writer, MvMap map)
    {
        var encoded = new List<(byte[] Key, byte[] Value)>(map.Entries.Count);
        foreach (var entry in map.Entries)
        {
            encoded.Add((Encode(entry.Key), Encode(entry.Value)));
        }

        encoded.Sort(static (left, right) => CompareBytes(left.Key, right.Key));
        for (var index = 1; index < encoded.Count; index++)
        {
            if (CompareBytes(encoded[index - 1].Key, encoded[index].Key) == 0)
            {
                throw new InvalidOperationException("Duplicate canonical MV-DCBOR map key.");
            }
        }

        WriteHead(writer, 5, checked((ulong)encoded.Count));
        foreach (var entry in encoded)
        {
            WriteBytes(writer, entry.Key);
            WriteBytes(writer, entry.Value);
        }
    }

    private static int CompareBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        var common = Math.Min(left.Length, right.Length);
        for (var index = 0; index < common; index++)
        {
            var comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return left.Length.CompareTo(right.Length);
    }

    private static void WriteHead(IBufferWriter<byte> writer, int major, ulong value)
    {
        if (value < 24)
        {
            WriteByte(writer, (byte)((major << 5) | (int)value));
            return;
        }

        if (value <= byte.MaxValue)
        {
            WriteByte(writer, (byte)((major << 5) | 24));
            WriteByte(writer, (byte)value);
            return;
        }

        if (value <= ushort.MaxValue)
        {
            WriteByte(writer, (byte)((major << 5) | 25));
            Span<byte> bytes = stackalloc byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(bytes, (ushort)value);
            WriteBytes(writer, bytes);
            return;
        }

        if (value <= uint.MaxValue)
        {
            WriteByte(writer, (byte)((major << 5) | 26));
            Span<byte> bytes = stackalloc byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(bytes, (uint)value);
            WriteBytes(writer, bytes);
            return;
        }

        WriteByte(writer, (byte)((major << 5) | 27));
        Span<byte> wide = stackalloc byte[8];
        BinaryPrimitives.WriteUInt64BigEndian(wide, value);
        WriteBytes(writer, wide);
    }

    private static void WriteByte(IBufferWriter<byte> writer, byte value)
    {
        var span = writer.GetSpan(1);
        span[0] = value;
        writer.Advance(1);
    }

    private static void WriteBytes(IBufferWriter<byte> writer, ReadOnlySpan<byte> value)
    {
        var span = writer.GetSpan(value.Length);
        value.CopyTo(span);
        writer.Advance(value.Length);
    }
}
