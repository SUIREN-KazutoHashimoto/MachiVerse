using MachiVerse.Simulation.Core.Determinism;

namespace MachiVerse.Simulation.Core.WorldState;

public sealed class WorldStateHeaderV1
{
    public WorldStateHeaderV1(
        OpaqueId128 worldId,
        ulong step,
        byte[] worldSeedDigest,
        ulong configGeneration,
        ulong masterGeneration,
        uint rateGeneration,
        byte[]? previousStateDigest = null)
    {
        if (worldId.IsZero) throw new ArgumentException("WorldId ZERO is invalid.", nameof(worldId));
        RequireHash(worldSeedDigest, nameof(worldSeedDigest));
        if (configGeneration == 0) throw new ArgumentOutOfRangeException(nameof(configGeneration), "ConfigGeneration starts at 1 for initialized WorldState.");
        if (masterGeneration == 0) throw new ArgumentOutOfRangeException(nameof(masterGeneration), "MasterGeneration starts at 1.");
        if (previousStateDigest is not null) RequireHash(previousStateDigest, nameof(previousStateDigest));

        Schema = new SchemaRefV1("core.world-state");
        WorldId = worldId;
        Step = step;
        WorldSeedDigest = worldSeedDigest.ToArray();
        ConfigGeneration = configGeneration;
        MasterGeneration = masterGeneration;
        RateGeneration = rateGeneration;
        PreviousStateDigest = previousStateDigest?.ToArray();
    }

    public SchemaRefV1 Schema { get; }
    public OpaqueId128 WorldId { get; }
    public ulong Step { get; }
    public byte[] WorldSeedDigest { get; }
    public ulong ConfigGeneration { get; }
    public ulong MasterGeneration { get; }
    public uint RateGeneration { get; }
    public byte[]? PreviousStateDigest { get; }

    private static void RequireHash(byte[] value, string name)
    {
        ArgumentNullException.ThrowIfNull(value, name);
        if (value.Length != 32) throw new ArgumentException($"{name} must be exactly 32 bytes.", name);
    }
}

public sealed class PartitionStateHeaderV1
{
    public PartitionStateHeaderV1(
        DomainPartitionIdentityV1 identity,
        ulong revision,
        ulong basisStep,
        DetailLevelV1 detailLevel,
        ulong itemCount,
        byte[] canonicalDigest)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (revision == 0) throw new ArgumentOutOfRangeException(nameof(revision), "Partition revision starts at 1.");
        if (!Enum.IsDefined(detailLevel)) throw new ArgumentOutOfRangeException(nameof(detailLevel));
        ArgumentNullException.ThrowIfNull(canonicalDigest);
        if (canonicalDigest.Length != 32) throw new ArgumentException("canonical_digest must be exactly 32 bytes.", nameof(canonicalDigest));

        PartitionId = identity.PartitionId;
        OwnerDomain = identity.OwnerDomain;
        Schema = identity.PartitionSchema;
        Revision = revision;
        BasisStep = basisStep;
        DetailLevel = detailLevel;
        ItemCount = itemCount;
        CanonicalDigest = canonicalDigest.ToArray();
    }

    public StableToken PartitionId { get; }
    public StableToken OwnerDomain { get; }
    public SchemaRefV1 Schema { get; }
    public ulong Revision { get; }
    public ulong BasisStep { get; }
    public DetailLevelV1 DetailLevel { get; }
    public ulong ItemCount { get; }
    public byte[] CanonicalDigest { get; }
}

public sealed record PartitionStateRefV1(PartitionStateHeaderV1 Header);

public sealed class OrderedPartitionDirectoryV1
{
    private readonly SortedDictionary<string, PartitionStateRefV1> _entries = new(StringComparer.Ordinal);

    public OrderedPartitionDirectoryV1(IEnumerable<PartitionStateRefV1> partitions)
    {
        ArgumentNullException.ThrowIfNull(partitions);
        foreach (var partition in partitions)
        {
            ArgumentNullException.ThrowIfNull(partition);
            if (!_entries.TryAdd(partition.Header.PartitionId.Value, partition))
                throw new InvalidDataException("world-state.duplicate-partition-id");
        }
    }

    public int Count => _entries.Count;
    public IEnumerable<PartitionStateRefV1> CanonicalEntries => _entries.Values;

    public PartitionStateRefV1 Get(string partitionId)
        => _entries.TryGetValue(partitionId, out var value)
            ? value
            : throw new KeyNotFoundException($"Partition not present in WorldState: {partitionId}");

    public void ValidateStandardCompleteness()
    {
        if (_entries.Count != StandardDomainPartitionRegistry.StandardPartitionCount)
            throw new InvalidDataException("world-state.standard-partition-count-mismatch");

        foreach (var standard in StandardDomainPartitionRegistry.Entries)
        {
            if (!_entries.TryGetValue(standard.PartitionId.Value, out var state))
                throw new InvalidDataException($"world-state.required-partition-missing:{standard.PartitionId.Value}");
            if (state.Header.OwnerDomain != standard.OwnerDomain || state.Header.Schema != standard.PartitionSchema)
                throw new InvalidDataException($"world-state.partition-identity-mismatch:{standard.PartitionId.Value}");
        }
    }
}
