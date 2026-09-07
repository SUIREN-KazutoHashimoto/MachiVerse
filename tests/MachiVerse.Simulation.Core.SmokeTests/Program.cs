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

Console.WriteLine("SIM-01 smoke tests passed.");
