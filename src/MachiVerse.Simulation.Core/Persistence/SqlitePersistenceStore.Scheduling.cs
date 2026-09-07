using System.Security.Cryptography;
using MachiVerse.Simulation.Core.Determinism;
using Microsoft.Data.Sqlite;

namespace MachiVerse.Simulation.Core.Persistence;

public enum DurableSchedulingStatus
{
    Scheduled = 1,
    Duplicate = 2
}

public sealed record DurableSchedulingResult(DurableSchedulingStatus Status, ulong ScheduledSequence, ulong EffectiveStep);

public sealed partial class SqlitePersistenceStore
{
    private const int ScheduledLifecycle = 2;

    public async Task<DurableSchedulingResult> PersistScheduledOperationAsync(
        OpaqueId128 operationId,
        ulong effectiveStep,
        byte[] orderKey,
        HistoryRecordMaterial history,
        CancellationToken cancellationToken = default)
    {
        if (operationId.IsZero) throw new ArgumentException("OperationId ZERO is invalid.", nameof(operationId));
        ArgumentNullException.ThrowIfNull(orderKey);
        if (orderKey.Length != SameStepOrderKey.DatabaseKeyLength)
            throw new ArgumentException($"orderKey must be exactly {SameStepOrderKey.DatabaseKeyLength} bytes.", nameof(orderKey));
        ValidateHistoryMaterial(history, "operation.scheduled.v1");

        using var transaction = _connection.BeginTransaction();
        try
        {
            var existing = await ReadSchedulingStateAsync(operationId, transaction, cancellationToken)
                ?? throw new InvalidDataException("persistence.operation-not-accepted");

            if (existing.Lifecycle >= ScheduledLifecycle)
            {
                if (existing.Lifecycle == ScheduledLifecycle &&
                    existing.ScheduledSequence is { } scheduledSequence &&
                    existing.EffectiveStep == effectiveStep &&
                    existing.OrderKey is not null &&
                    CryptographicOperations.FixedTimeEquals(existing.OrderKey, orderKey))
                {
                    transaction.Commit();
                    return new DurableSchedulingResult(DurableSchedulingStatus.Duplicate, scheduledSequence, effectiveStep);
                }

                throw new InvalidDataException("persistence.operation-scheduling-mismatch");
            }

            var anchor = await ReadHistoryAnchorAsync(transaction, cancellationToken);
            if (anchor.Sequence == ulong.MaxValue) throw new OverflowException("HistorySequence cannot wrap.");
            if (history.Sequence != anchor.Sequence + 1)
                throw new InvalidDataException("persistence.history-sequence-gap");
            if (!CryptographicOperations.FixedTimeEquals(history.PreviousRecordDigest, anchor.Digest))
                throw new InvalidDataException("persistence.history-previous-digest-mismatch");

            await InsertHistoryRecordAsync(history, transaction, cancellationToken);

            await using (var operation = _connection.CreateCommand())
            {
                operation.Transaction = transaction;
                operation.CommandText = """
UPDATE operation_state
SET lifecycle=$lifecycle, scheduled_sequence=$scheduled_sequence, effective_step=$effective_step
WHERE operation_id=$operation_id AND lifecycle=$accepted_lifecycle;
""";
                operation.Parameters.AddWithValue("$lifecycle", ScheduledLifecycle);
                operation.Parameters.AddWithValue("$scheduled_sequence", U64Be.Encode(history.Sequence));
                operation.Parameters.AddWithValue("$effective_step", U64Be.Encode(effectiveStep));
                operation.Parameters.AddWithValue("$operation_id", operationId.ToBytes());
                operation.Parameters.AddWithValue("$accepted_lifecycle", AcceptedLifecycle);
                if (await operation.ExecuteNonQueryAsync(cancellationToken) != 1)
                    throw new InvalidDataException("persistence.operation-schedule-state-race");
            }

            await using (var scheduled = _connection.CreateCommand())
            {
                scheduled.Transaction = transaction;
                scheduled.CommandText = """
INSERT INTO scheduled_operation (effective_step, order_key, operation_id)
VALUES ($effective_step, $order_key, $operation_id);
""";
                scheduled.Parameters.AddWithValue("$effective_step", U64Be.Encode(effectiveStep));
                scheduled.Parameters.AddWithValue("$order_key", orderKey);
                scheduled.Parameters.AddWithValue("$operation_id", operationId.ToBytes());
                await scheduled.ExecuteNonQueryAsync(cancellationToken);
            }

            await UpdateHistoryAnchorAsync(history, transaction, cancellationToken);
            transaction.Commit();
            return new DurableSchedulingResult(DurableSchedulingStatus.Scheduled, history.Sequence, effectiveStep);
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private async Task<SchedulingState?> ReadSchedulingStateAsync(
        OpaqueId128 operationId,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
SELECT o.lifecycle, o.scheduled_sequence, o.effective_step, s.order_key
FROM operation_state o
LEFT JOIN scheduled_operation s ON s.operation_id = o.operation_id
WHERE o.operation_id=$operation_id;
""";
        command.Parameters.AddWithValue("$operation_id", operationId.ToBytes());
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken)) return null;

        return new SchedulingState(
            reader.GetInt32(0),
            reader.IsDBNull(1) ? null : U64Be.Decode((byte[])reader[1]),
            reader.IsDBNull(2) ? null : U64Be.Decode((byte[])reader[2]),
            reader.IsDBNull(3) ? null : (byte[])reader[3]);
    }

    private async Task UpdateHistoryAnchorAsync(
        HistoryRecordMaterial history,
        SqliteTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using var command = _connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
UPDATE persistence_meta
SET last_history_sequence=$sequence, last_history_digest=$digest
WHERE singleton=1;
""";
        command.Parameters.AddWithValue("$sequence", U64Be.Encode(history.Sequence));
        command.Parameters.AddWithValue("$digest", history.RecordDigest);
        if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
            throw new InvalidDataException("persistence.meta-update-failed");
    }

    private sealed record SchedulingState(
        int Lifecycle,
        ulong? ScheduledSequence,
        ulong? EffectiveStep,
        byte[]? OrderKey);
}
