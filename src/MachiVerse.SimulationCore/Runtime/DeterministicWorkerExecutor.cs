namespace MachiVerse.SimulationCore.Runtime;

public readonly record struct CanonicalWorkItem<T>(int CanonicalIndex, T Payload);

public readonly record struct CanonicalWorkResult<T>(int CanonicalIndex, T Value);

public interface IWorkerExecutor
{
    Task<IReadOnlyList<CanonicalWorkResult<TResult>>> ExecuteAsync<TPayload, TResult>(
        IReadOnlyList<CanonicalWorkItem<TPayload>> workItems,
        Func<TPayload, CancellationToken, ValueTask<TResult>> handler,
        CancellationToken cancellationToken = default);
}

public sealed class DeterministicWorkerExecutor : IWorkerExecutor
{
    private readonly int _maxConcurrency;

    public DeterministicWorkerExecutor(int maxConcurrency)
    {
        if (maxConcurrency is < 1 or > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(maxConcurrency), "Worker concurrency must be 1..16.");
        }

        _maxConcurrency = maxConcurrency;
    }

    public async Task<IReadOnlyList<CanonicalWorkResult<TResult>>> ExecuteAsync<TPayload, TResult>(
        IReadOnlyList<CanonicalWorkItem<TPayload>> workItems,
        Func<TPayload, CancellationToken, ValueTask<TResult>> handler,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItems);
        ArgumentNullException.ThrowIfNull(handler);

        if (workItems.Count == 0)
        {
            return Array.Empty<CanonicalWorkResult<TResult>>();
        }

        ValidateCanonicalIndices(workItems);

        var results = new CanonicalWorkResult<TResult>[workItems.Count];
        using var gate = new SemaphoreSlim(_maxConcurrency, _maxConcurrency);
        var tasks = new Task[workItems.Count];

        for (var index = 0; index < workItems.Count; index++)
        {
            var item = workItems[index];
            tasks[index] = ExecuteOneAsync(item, results, gate, handler, cancellationToken);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        Array.Sort(results, static (left, right) => left.CanonicalIndex.CompareTo(right.CanonicalIndex));
        return results;
    }

    private static async Task ExecuteOneAsync<TPayload, TResult>(
        CanonicalWorkItem<TPayload> item,
        CanonicalWorkResult<TResult>[] results,
        SemaphoreSlim gate,
        Func<TPayload, CancellationToken, ValueTask<TResult>> handler,
        CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var value = await handler(item.Payload, cancellationToken).ConfigureAwait(false);
            results[item.CanonicalIndex] = new CanonicalWorkResult<TResult>(item.CanonicalIndex, value);
        }
        finally
        {
            gate.Release();
        }
    }

    private static void ValidateCanonicalIndices<T>(IReadOnlyList<CanonicalWorkItem<T>> items)
    {
        var seen = new bool[items.Count];
        foreach (var item in items)
        {
            if (item.CanonicalIndex < 0 || item.CanonicalIndex >= items.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(items), "CanonicalIndex must be dense 0..Count-1.");
            }

            if (seen[item.CanonicalIndex])
            {
                throw new ArgumentException("CanonicalIndex values must be unique.", nameof(items));
            }

            seen[item.CanonicalIndex] = true;
        }
    }
}
