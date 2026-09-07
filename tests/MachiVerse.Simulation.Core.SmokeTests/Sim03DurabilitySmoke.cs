using System.Security.Cryptography;
using MachiVerse.Simulation.Core.Determinism;
using MachiVerse.Simulation.Core.Persistence;

public static class Sim03DurabilitySmoke
{
    public static async Task RunAsync(
        SqlitePersistenceStore store,
        OpaqueId128 operationId,
        byte[] configDigest)
    {
        var anchor = await store.ReadHistoryAnchorAsync();
        if (anchor.Sequence != 1) throw new InvalidOperationException("SIM-03 scheduling smoke requires accepted history sequence 1.");

        var orderKey = new SameStepOrderKey(
            phase: 1,
            domainRank: 1,
            conflictScopeDigest: SHA256.HashData("sim03-scope"u8),
            semanticPriority: 0,
            intentId: operationId).ToDatabaseBytes();

        var scheduledDigest = SHA256.HashData("history-record-scheduled"u8);
        var scheduledRecord = new HistoryRecordMaterial(
            Sequence: 2,
            PreviousRecordDigest: anchor.Digest,
            RecordType: "operation.scheduled.v1",
            PayloadSchemaId: "core.operation-scheduled.v1",
            PayloadSchemaMajor: 1,
            PayloadSchemaMinor: 0,
            PayloadBytes: [5, 6, 7],
            NormalizedPayloadDigest: SHA256.HashData("normalized-scheduled"u8),
            RecordDigest: scheduledDigest);

        var scheduled = await store.PersistScheduledOperationAsync(operationId, 0, orderKey, scheduledRecord);
        if (scheduled.Status != DurableSchedulingStatus.Scheduled || scheduled.ScheduledSequence != 2 || scheduled.EffectiveStep != 0)
            throw new InvalidOperationException("Scheduling durability transaction failed.");

        var duplicateSchedule = await store.PersistScheduledOperationAsync(operationId, 0, orderKey, scheduledRecord);
        if (duplicateSchedule.Status != DurableSchedulingStatus.Duplicate || duplicateSchedule.ScheduledSequence != 2)
            throw new InvalidOperationException("Duplicate scheduling must converge without appending history.");
        if ((await store.ReadHistoryAnchorAsync()).Sequence != 2)
            throw new InvalidOperationException("Duplicate scheduling must not advance history.");

        var resultingContinuity = SHA256.HashData("state-1-continuity"u8);
        var transitionDigest = SHA256.HashData("history-record-transition"u8);
        var transitionRecord = new HistoryRecordMaterial(
            Sequence: 3,
            PreviousRecordDigest: scheduledDigest,
            RecordType: "transition.committed.v1",
            PayloadSchemaId: "core.transition-committed.v1",
            PayloadSchemaMajor: 1,
            PayloadSchemaMinor: 0,
            PayloadBytes: [8, 9, 10],
            NormalizedPayloadDigest: SHA256.HashData("normalized-transition"u8),
            RecordDigest: transitionDigest);

        var transition = await store.PersistTransitionCommitAsync(
            effectiveStep: 0,
            resultingStep: 1,
            resultingStateContinuityToken: resultingContinuity,
            activeConfigGeneration: 1,
            activeConfigDigest: configDigest,
            history: transitionRecord,
            terminalOperations:
            [
                new TerminalOperationCommit(
                    operationId,
                    TerminalStatus: 1,
                    ResultCode: "operation.ok",
                    RichResultPayload: [11, 12])
            ]);

        if (transition.ResultingStep != 1 || transition.HistorySequence != 3)
            throw new InvalidOperationException("Transition durability transaction result mismatch.");

        var recoveryHead = await store.ReadRecoveryHeadAsync();
        if (recoveryHead.FinalizedStep != 1 || !recoveryHead.ContinuityToken.SequenceEqual(resultingContinuity))
            throw new InvalidOperationException("Recovery head must advance atomically with transition commit.");
        if (recoveryHead.ConfigGeneration != 1 || !recoveryHead.ConfigDigest.SequenceEqual(configDigest))
            throw new InvalidOperationException("Transition commit must persist active Config identity.");
        if ((await store.ReadHistoryAnchorAsync()).Sequence != 3)
            throw new InvalidOperationException("Transition commit must advance history atomically.");

        var staleTransitionRejected = false;
        try
        {
            await store.PersistTransitionCommitAsync(
                effectiveStep: 0,
                resultingStep: 1,
                resultingStateContinuityToken: SHA256.HashData("invalid-continuity"u8),
                activeConfigGeneration: 1,
                activeConfigDigest: configDigest,
                history: transitionRecord with
                {
                    Sequence = 4,
                    PreviousRecordDigest = transitionDigest,
                    RecordDigest = SHA256.HashData("invalid-transition"u8)
                },
                terminalOperations: []);
        }
        catch (InvalidDataException ex) when (ex.Message == "persistence.transition-base-step-mismatch")
        {
            staleTransitionRejected = true;
        }

        if (!staleTransitionRejected)
            throw new InvalidOperationException("A transition from a stale finalized Step must be rejected.");
        if ((await store.ReadHistoryAnchorAsync()).Sequence != 3)
            throw new InvalidOperationException("Rejected transition must not advance history.");
    }
}
