namespace MachiVerse.SimulationCore.Primitives;

public enum OrderPhase : byte
{
    Control = 0,
    ExternalInput = 1,
    ScheduledInternal = 2,
    DerivedInternal = 3,
    SystemInternal = 4,
    Finalization = 5
}

public readonly record struct SameStepOrderKey(
    OrderPhase Phase,
    ushort DomainRank,
    Hash256 ConflictScopeDigest,
    int SemanticPriority,
    Id128 IntentId) : IComparable<SameStepOrderKey>
{
    public int CompareTo(SameStepOrderKey other)
    {
        var phase = Phase.CompareTo(other.Phase);
        if (phase != 0)
        {
            return phase;
        }

        var domain = DomainRank.CompareTo(other.DomainRank);
        if (domain != 0)
        {
            return domain;
        }

        var scope = CompareHash(ConflictScopeDigest, other.ConflictScopeDigest);
        if (scope != 0)
        {
            return scope;
        }

        var priority = SemanticPriority.CompareTo(other.SemanticPriority);
        if (priority != 0)
        {
            return priority;
        }

        return CompareId(IntentId, other.IntentId);
    }

    private static int CompareHash(Hash256 left, Hash256 right)
    {
        Span<byte> leftBytes = stackalloc byte[Hash256.ByteLength];
        Span<byte> rightBytes = stackalloc byte[Hash256.ByteLength];
        left.WriteBytes(leftBytes);
        right.WriteBytes(rightBytes);
        return CompareBytes(leftBytes, rightBytes);
    }

    private static int CompareId(Id128 left, Id128 right)
    {
        Span<byte> leftBytes = stackalloc byte[Id128.ByteLength];
        Span<byte> rightBytes = stackalloc byte[Id128.ByteLength];
        left.WriteBytes(leftBytes);
        right.WriteBytes(rightBytes);
        return CompareBytes(leftBytes, rightBytes);
    }

    private static int CompareBytes(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        for (var index = 0; index < left.Length; index++)
        {
            var comparison = left[index].CompareTo(right[index]);
            if (comparison != 0)
            {
                return comparison;
            }
        }

        return 0;
    }
}
