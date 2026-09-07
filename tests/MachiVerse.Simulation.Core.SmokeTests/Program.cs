using MachiVerse.Simulation.Core.Determinism;
using MachiVerse.Simulation.Core.Runtime;

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

var worldId = OpaqueId128.Parse("00000000000000000000000000000001");
var creatorId = OpaqueId128.Parse("00000000000000000000000000000002");
var domain = new StableToken("sim.resident");
var kind = new StableToken("resident.birth");
var entityA = DerivedIdentity.DeriveEntityId(worldId, 42, domain, creatorId, kind, 0);
var entityB = DerivedIdentity.DeriveEntityId(worldId, 42, domain, creatorId, kind, 0);
Require(entityA == entityB && !entityA.IsZero, "EntityId derivation must be stable and non-zero.");

var seed = new WorldSeed256(new byte[32]);
var context = new RandomContextV1(worldId, 42, domain, new StableToken("birth-trait"), entityA, OpaqueId128.Zero, OpaqueId128.Zero, 0);
var randomA = DeterministicRandom.RandomWord64(seed, context, 0);
var randomB = DeterministicRandom.RandomWord64(seed, context, 0);
Require(randomA == randomB, "RandomWord64 must be addressable and stable.");
Require(DeterministicRandom.BoundedUInt64(seed, context, 1, 7) < 7, "Bounded random result out of range.");

var results = await DeterministicBatchExecutor.RunAsync(new[] { 4, 3, 2, 1 }, 4, static (value, _) => ValueTask.FromResult(value * value));
Require(results.SequenceEqual(new[] { 16, 9, 4, 1 }), "Worker completion must not reorder semantic output slots.");

var scopeDigest = Enumerable.Range(0, 32).Select(static value => (byte)value).ToArray();
var intentA = OpaqueId128.Parse("00000000000000000000000000000010");
var intentB = OpaqueId128.Parse("00000000000000000000000000000011");
var orderA = new SameStepOrderKey(1, 2, scopeDigest, -1, intentA);
var orderB = new SameStepOrderKey(1, 2, scopeDigest, 0, intentB);
Require(orderA.CompareTo(orderB) < 0, "SameStepOrderKey must use signed semantic priority ascending.");
Require(orderA.ToDatabaseBytes().Length == SameStepOrderKey.DatabaseKeyLength, "SameStepOrderKey DB encoding must be 55 bytes.");
Require(orderA.ToDatabaseBytes().AsSpan().SequenceCompareTo(orderB.ToDatabaseBytes()) < 0, "DB byte order must match logical SameStepOrderKey order.");

var half = FixedQ32_32.FromRatio(1, 2);
Require(half.Raw == 1L << 31, "FixedQ32_32 half encoding mismatch.");
Require((half * FixedQ32_32.FromInteger(2)) == FixedQ32_32.One, "FixedQ32_32 multiplication mismatch.");
Require(FixedQ32_32.FromRatio(5, 2).RoundToInteger() == 2, "Round-to-even must round 2.5 to 2.");
Require(FixedQ32_32.FromRatio(7, 2).RoundToInteger() == 4, "Round-to-even must round 3.5 to 4.");

Console.WriteLine("SIM-01 smoke tests passed.");
