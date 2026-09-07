namespace MachiVerse.SimulationCore.Primitives;

public static class CheckedArithmetic
{
    public static ulong Add(ulong left, ulong right) => checked(left + right);

    public static ulong Increment(ulong value) => checked(value + 1UL);

    public static long Add(long left, long right) => checked(left + right);

    public static long Multiply(long left, long right) => checked(left * right);

    public static Int128 MultiplyWide(long left, long right) => checked((Int128)left * right);
}
