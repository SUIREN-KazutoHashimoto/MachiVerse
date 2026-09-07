using MachiVerse.SimulationCore.Primitives;
using MachiVerse.SimulationCore.Runtime;

namespace MachiVerse.SimulationCore;

public static class ContractSelfTest
{
    public static int Run()
    {
        Assert(StableToken.TryParse("core.world-state", out _), "StableToken valid vector");
        Assert(!StableToken.TryParse("Core.World-State", out _), "StableToken invalid vector");

        var idBytes = Convert.FromHexString("000102030405060708090a0b0c0d0e0f");
        var worldId = Id128.FromBytes(idBytes);
        Assert(worldId.ToString() == "000102030405060708090a0b0c0d0e0f", "Id128 roundtrip");

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

        var creatorId = Id128.FromBytes(Convert.FromHexString("101112131415161718191a1b1c1d1e1f"));
        var entityId = DeterministicIdentity.DeriveEntityId(
            worldId,
            42,
            StableToken.Parse("resident"),
            creatorId,
            StableToken.Parse("birth"),
            7,
            0);
        Assert(entityId.ToString() == "1715d3bae14ddbc9372568e46698dc0e", "EntityId derivation vector");

        var sourceId = Id128.FromBytes(Convert.FromHexString("202122232425262728292a2b2c2d2e2f"));
        var intentId = DeterministicIdentity.DeriveIntentId(
            worldId,
            43,
            0,
            sourceId,
            StableToken.Parse("physical_built"),
            StableToken.Parse("entity.move"),
            3);
        Assert(intentId.ToString() == "1060de0caa85e65a7e9088cc0631d728", "IntentId derivation vector");

        var randomSeed = Convert.FromHexString("000102030405060708090a0b0c0d0e0f101112131415161718191a1b1c1d1e1f");
        var randomContext = new MvMap(new KeyValuePair<IMvDcborValue, IMvDcborValue>[]
        {
            new(new MvUnsigned(0), new MvText("test"))
        });
        var randomWord = AddressableRandom.RandomWord64(randomSeed, randomContext, 5, 0);
        Assert(randomWord == 5_408_621_233_273_370_037UL, "RandomWord64 golden vector");
        Assert(AddressableRandom.BoundedUnsigned(randomSeed, randomContext, 5, 10) < 10, "bounded random range");

        var scopeDigest = Hash256.Sha256("scope"u8);
        var earlier = new SameStepOrderKey(OrderPhase.ExternalInput, 0, scopeDigest, 0, intentId);
        var later = new SameStepOrderKey(OrderPhase.ScheduledInternal, 0, scopeDigest, -100, intentId);
        Assert(earlier.CompareTo(later) < 0, "SameStepOrderKey phase precedence");

        var worker = new DeterministicWorkerExecutor(3);
        var work = new[]
        {
            new CanonicalWorkItem<int>(2, 30),
            new CanonicalWorkItem<int>(0, 10),
            new CanonicalWorkItem<int>(1, 20)
        };
        var workerResults = worker.ExecuteAsync(
            work,
            async (payload, cancellationToken) =>
            {
                await Task.Delay(40 - payload, cancellationToken);
                return payload;
            }).GetAwaiter().GetResult();
        Assert(workerResults.Select(result => result.CanonicalIndex).SequenceEqual(new[] { 0, 1, 2 }), "worker canonical result order");
        Assert(workerResults.Select(result => result.Value).SequenceEqual(new[] { 10, 20, 30 }), "worker completion timing independence");

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
