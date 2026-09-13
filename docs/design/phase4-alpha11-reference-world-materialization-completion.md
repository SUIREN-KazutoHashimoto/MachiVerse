# Alpha 1.1 — QA-04 reference-world materialization completion

Status: Accepted production authority checkpoint  
Tracking: #240  
Parent: `phase4-alpha11-normative-closure.md`

## 1. Scope

This checkpoint records completion of the `perf.reference.v1` initial reference-world materialization authority. It does not declare the QA-04 release gate complete and does not establish the authoritative full Step loop.

The completion criterion is the conjunction of:

1. every canonical reference-world class has a production materializer backed by accepted authority;
2. all reference-world dependency blockers are removed only after their exact production-path evidence succeeds;
3. Society/Governance and Infrastructure populations match their canonical decomposition counts;
4. runtime inspection may report `referenceWorldMaterialized=true` only while the full Step-loop and release-evidence flags remain fail-closed until separately proven.

## 2. Canonical reference classes

All nine `perf.reference.v1` classes are production-materializer available:

| class | canonical count | state |
|---|---:|---|
| `resident.persistent-identity` | 1,000,000 | available |
| `participation.control_mode` | 1,000,000 | available |
| `physical.d0-presence` | 1,000,000 | available |
| `environment.d0-cell-cohort` | 1,000,000 | available |
| `environment.d1-aggregate` | 250,000 | available |
| `society-governance.active-record` | 2,000,000 | available |
| `infrastructure.active-record` | 500,000 | available |
| `spatial.hot-terrain-brick` | 500,000 | available |
| `transaction.active-cross-domain` | canonical scenario target | available |

`Qa04ReferenceWorldDependencyContractV1.Blockers` and its failure-code set are therefore empty at this checkpoint.

## 3. Society/Governance completion

The Society/Governance decomposition is exactly **2,000,000 / 2,000,000** accepted records.

The final Governance package contributes **259,000** records and preserves the previously accepted nested `governance.law_rule` snapshot authority. Its production proof establishes exact materialization, Snapshot recovery, semantic rehash, reference closure, identity uniqueness, and fail-closed drift detection for the remaining Governance slices.

The former blocker:

```text
qa04.material.society-governance-partition-mapping-undefined
```

is resolved and must not be retained by the reference-world dependency contract.

## 4. Infrastructure completion

The Infrastructure/Information decomposition is exactly **500,000 / 500,000** records across all 16 canonical slices.

The aggregate production proof composes the already accepted authorities without redefining them:

```text
topology             120,100
services + queue     290,000
dependency            20,000
facility_service      15,000
information.delivery  20,000
media_distribution     5,000
record_store           10,000
address/failure/lineage 19,900
--------------------------------
total                 500,000
```

The proof additionally validates all 16 slice counts, full descriptor coverage, cross-slice RecordId uniqueness, production Snapshot encode/recovery, and recovered semantic identity.

Accepted production evidence:

```text
workflow: INT-03 full Infrastructure topology validation
run:      34750371338
job:      103705927245
output:   infrastructure-aggregate-full-production-pass records=500000 recovered=500000 topology=120100 services_queue=290000 dependency=20000 facility=15000 delivery=20000 media=5000 record_store=10000 tail=19900 slices=16
```

This satisfies the removal condition in `phase4-alpha11-infrastructure-network-v2.md` for the former blocker:

```text
qa04.material.infrastructure-node-edge-authority-undefined
```

The blocker must not be retained after this checkpoint.

## 5. Runtime readiness boundary

After the two final parent blockers are removed, runtime inspection has the following exact boundary:

```text
referenceWorldMaterialized       = true
authoritativeStepLoopAvailable   = false
releaseEvidenceCapable           = false
```

`referenceWorldMaterialized=true` means the canonical reference-world authority/materializer contract is complete. It does **not** mean that the canonical full Step loop, exact-103 production evidence, determinism repetition matrix, persistence/publication stress, or 24-hour soak have passed.

Until the authoritative full Step loop is separately assembled and proven, runtime/release evidence must continue to expose:

```text
qa04.target.authoritative-step-loop-not-assembled
```

and must remain fail-closed for release capability.

## 6. Non-regression requirements

The following are release-contract regressions:

- reintroducing either resolved reference-world blocker;
- reporting fewer or more than 2,000,000 Society/Governance active records;
- reporting fewer or more than 500,000 Infrastructure/Information active records;
- reporting any blocked canonical reference class after accepted completion;
- setting `authoritativeStepLoopAvailable=true` solely because the reference world is complete;
- setting `releaseEvidenceCapable=true` before the remaining release gates are proven;
- treating reduced/Resident-only Step-loop probes as canonical full-reference-world release evidence.

The next release-gate work item is the authoritative full Step-loop composition and production proof over the completed reference world.