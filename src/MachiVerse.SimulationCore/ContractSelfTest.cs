using MachiVerse.SimulationCore.Primitives;

namespace MachiVerse.SimulationCore;

public static class ContractSelfTest
{
    public static int Run()
    {
        Assert(StableToken.TryParse("core.world-state", out _), "StableToken valid vector");
        Assert(!StableToken.TryParse("Core.World-State", out _), "StableToken invalid vector");

        var idBytes = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
        var id = Id128.FromBytes(idBytes);
        Assert(id.ToString() == "000102030405060708090a0b0c0d0e0f", "Id128 roundtrip");

        var abc = Hash256.Sha256("abc"u8);
        Assert(abc.ToString() == "ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", "SHA-256 abc");

        var context = new MvMap(new KeyValuePair<IMvDcborValue, IMvDcborValue>[]
        {
            new(new MvUnsigned(0), new MvByteString(idBytes)),
            new(new MvUnsigned(1), new MvUnsigned(24)),
            new(new MvUnsigned(2), new MvText("core.world-state"))
        });

        var encoded = Convert.ToHexString(MvDcbor.Encode(context)).ToLowerInvariant();
        Assert(encoded == "a30050000102030405060708090a0b0c0d0e0f0118180270636f72652e776f726c642d7374617465", "MV-DCBOR context");

        var digest = DomainHash.Compute("mv.test.v1", context).ToString();
        Assert(digest == "0dadebbf1ed87eeb30698f9f07be9b3052ccb7e2e2f79939c1188673e873bb25", "DomainHash context");

        Assert(RoundTiesToEven.Divide(5, 2) == 2, "round ties to even 2.5 -> 2");
        Assert(RoundTiesToEven.Divide(7, 2) == 4, "round ties to even 3.5 -> 4");
        Assert(RoundTiesToEven.Divide(-5, 2) == -2, "round ties to even -2.5 -> -2");
        Assert(RoundTiesToEven.Divide(-7, 2) == -4, "round ties to even -3.5 -> -4");

        var two = FixedQ32_32.FromInteger(2);
        var three = FixedQ32_32.FromInteger(3);
        Assert((two * three).Raw == FixedQ32_32.FromInteger(6).Raw, "FixedQ32_32 multiply");
        Assert((FixedQ32_32.FromInteger(6) / three).Raw == two.Raw, "FixedQ32_32 divide");
        Assert(new ProbabilityPpm(1_000_000).Value == 1_000_000, "ProbabilityPpm upper bound");

        Console.WriteLine("SIM-01 contract self-test: PASS");
        return 0;
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"Contract self-test failed: {name}");
        }
    }
}
