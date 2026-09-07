using System.Buffers.Binary;

namespace MachiVerse.SimulationCore.Primitives;

public static class AddressableRandom
{
    public const int WorldSeedBytes = 32;

    public static ulong RandomWord64(
        ReadOnlySpan<byte> worldSeed,
        IMvDcborValue canonicalContext,
        ulong drawIndex,
        ulong retryIndex = 0)
    {
        ArgumentNullException.ThrowIfNull(canonicalContext);
        if (worldSeed.Length != WorldSeedBytes)
        {
            throw new ArgumentException($"WorldSeed must be exactly {WorldSeedBytes} octets.", nameof(worldSeed));
        }

        var seed = worldSeed.ToArray();
        var input = new MvMap(new KeyValuePair<IMvDcborValue, IMvDcborValue>[]
        {
            Pair(0, new MvByteString(seed)),
            Pair(1, canonicalContext),
            Pair(2, new MvUnsigned(drawIndex)),
            Pair(3, new MvUnsigned(retryIndex))
        });

        var digest = DomainHash.Compute("mv.random.v1", input);
        Span<byte> digestBytes = stackalloc byte[Hash256.ByteLength];
        digest.WriteBytes(digestBytes);
        return BinaryPrimitives.ReadUInt64BigEndian(digestBytes[..8]);
    }

    public static ulong BoundedUnsigned(
        ReadOnlySpan<byte> worldSeed,
        IMvDcborValue canonicalContext,
        ulong drawIndex,
        ulong bound)
    {
        if (bound == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bound), "bound must be 1..2^64-1.");
        }

        var fullRange = (UInt128)1 << 64;
        var limit = (fullRange / bound) * bound;
        ulong retryIndex = 0;

        while (true)
        {
            var word = RandomWord64(worldSeed, canonicalContext, drawIndex, retryIndex);
            if ((UInt128)word < limit)
            {
                return word % bound;
            }

            retryIndex = checked(retryIndex + 1);
        }
    }

    private static KeyValuePair<IMvDcborValue, IMvDcborValue> Pair(ulong key, IMvDcborValue value) =>
        new(new MvUnsigned(key), value);
}
