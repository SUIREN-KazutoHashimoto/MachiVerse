namespace MachiVerse.SimulationCore.Primitives;

public readonly record struct FixedQ32_32(long Raw)
{
    public const int FractionBits = 32;
    public const long OneRaw = 1L << FractionBits;

    public static FixedQ32_32 FromInteger(long value)
    {
        var raw = checked((Int128)value * OneRaw);
        return new FixedQ32_32(checked((long)raw));
    }

    public static FixedQ32_32 operator +(FixedQ32_32 left, FixedQ32_32 right) =>
        new(checked(left.Raw + right.Raw));

    public static FixedQ32_32 operator -(FixedQ32_32 left, FixedQ32_32 right) =>
        new(checked(left.Raw - right.Raw));

    public static FixedQ32_32 operator *(FixedQ32_32 left, FixedQ32_32 right)
    {
        var product = checked((Int128)left.Raw * right.Raw);
        var raw = RoundTiesToEven.Divide(product, (Int128)OneRaw);
        return new FixedQ32_32(checked((long)raw));
    }

    public static FixedQ32_32 operator /(FixedQ32_32 left, FixedQ32_32 right)
    {
        if (right.Raw == 0)
        {
            throw new DivideByZeroException();
        }

        var numerator = checked((Int128)left.Raw << FractionBits);
        var raw = RoundTiesToEven.Divide(numerator, right.Raw);
        return new FixedQ32_32(checked((long)raw));
    }
}

public readonly record struct RatioQ0_32(uint Raw);

public readonly record struct ProbabilityPpm
{
    public const uint Maximum = 1_000_000;

    public uint Value { get; }

    public ProbabilityPpm(uint value)
    {
        if (value > Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"ProbabilityPpm must be 0..{Maximum}.");
        }

        Value = value;
    }
}

public readonly record struct ProgressPpm
{
    public const uint Maximum = 1_000_000;

    public uint Value { get; }

    public ProgressPpm(uint value)
    {
        if (value > Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"ProgressPpm must be 0..{Maximum}.");
        }

        Value = value;
    }
}

public readonly record struct ConcentrationPpb
{
    public const uint Maximum = 1_000_000_000;

    public uint Value { get; }

    public ConcentrationPpb(uint value)
    {
        if (value > Maximum)
        {
            throw new ArgumentOutOfRangeException(nameof(value), $"ConcentrationPpb must be 0..{Maximum}.");
        }

        Value = value;
    }
}

public static class RoundTiesToEven
{
    public static Int128 Divide(Int128 numerator, Int128 denominator)
    {
        if (denominator == 0)
        {
            throw new DivideByZeroException();
        }

        var quotient = numerator / denominator;
        var remainder = numerator % denominator;
        if (remainder == 0)
        {
            return quotient;
        }

        var remainderMagnitude = Magnitude(remainder);
        var denominatorMagnitude = Magnitude(denominator);
        var doubledRemainder = checked(remainderMagnitude * 2);

        if (doubledRemainder < denominatorMagnitude)
        {
            return quotient;
        }

        var direction = (numerator < 0) ^ (denominator < 0) ? (Int128)(-1) : (Int128)1;
        if (doubledRemainder > denominatorMagnitude)
        {
            return checked(quotient + direction);
        }

        return (quotient & 1) == 0
            ? quotient
            : checked(quotient + direction);
    }

    private static UInt128 Magnitude(Int128 value)
    {
        if (value >= 0)
        {
            return (UInt128)value;
        }

        return (UInt128)(-(value + 1)) + 1;
    }
}
