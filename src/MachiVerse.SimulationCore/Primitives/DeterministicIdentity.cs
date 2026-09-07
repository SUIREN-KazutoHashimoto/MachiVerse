namespace MachiVerse.SimulationCore.Primitives;

public static class DeterministicIdentity
{
    public static Id128 DeriveEntityId(
        Id128 worldId,
        ulong creationStep,
        StableToken creatorDomain,
        Id128 creatorEntityId,
        StableToken creationKind,
        ulong localOrdinal,
        ulong nonce)
    {
        var context = new MvMap(new KeyValuePair<IMvDcborValue, IMvDcborValue>[]
        {
            Pair(0, IdValue(worldId)),
            Pair(1, new MvUnsigned(creationStep)),
            Pair(2, new MvText(creatorDomain.Value)),
            Pair(3, IdValue(creatorEntityId)),
            Pair(4, new MvText(creationKind.Value)),
            Pair(5, new MvUnsigned(localOrdinal)),
            Pair(6, new MvUnsigned(nonce))
        });

        return Truncate128(DomainHash.Compute("mv.entity.v1", context));
    }

    public static Id128 DeriveIntentId(
        Id128 worldId,
        ulong effectiveStep,
        byte sourceKind,
        Id128 sourceId,
        StableToken domain,
        StableToken mutationKind,
        ulong localOrdinal)
    {
        if (sourceKind > 3)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceKind), "SourceKind must be 0..3 in the v1 registry.");
        }

        var context = new MvMap(new KeyValuePair<IMvDcborValue, IMvDcborValue>[]
        {
            Pair(0, IdValue(worldId)),
            Pair(1, new MvUnsigned(effectiveStep)),
            Pair(2, new MvUnsigned(sourceKind)),
            Pair(3, IdValue(sourceId)),
            Pair(4, new MvText(domain.Value)),
            Pair(5, new MvText(mutationKind.Value)),
            Pair(6, new MvUnsigned(localOrdinal))
        });

        return Truncate128(DomainHash.Compute("mv.intent.v1", context));
    }

    private static KeyValuePair<IMvDcborValue, IMvDcborValue> Pair(ulong key, IMvDcborValue value) =>
        new(new MvUnsigned(key), value);

    private static MvByteString IdValue(Id128 value)
    {
        var bytes = new byte[Id128.ByteLength];
        value.WriteBytes(bytes);
        return new MvByteString(bytes);
    }

    private static Id128 Truncate128(Hash256 digest)
    {
        Span<byte> bytes = stackalloc byte[Hash256.ByteLength];
        digest.WriteBytes(bytes);
        return Id128.FromBytes(bytes[..Id128.ByteLength]);
    }
}
