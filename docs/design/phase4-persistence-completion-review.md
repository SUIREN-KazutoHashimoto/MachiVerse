# 詳細設計 Phase 4: Persistence Completion Review

Status: Complete / P4-04 Completion Review  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`

## 1. 目的

P4-04のphysical persistence、history、Snapshot、recovery、migration、exportをPhase 1〜3契約と横断監査し、実装へ引き渡せるか判定する。

本書をP4-04 completion判定の正本とする。

## 2. 成果物

- `phase4-persistence-specification.md`
- `phase4-persistence-record-catalog.md`
- 本書

## 3. Durable boundary audit

Operation ACCEPTED:

```text
history append + operation_state insert + meta update
 -> SQLite COMMIT
 -> ACCEPTED response
```

Operation scheduling:

```text
scheduled history + operation_state + scheduler index
 -> COMMIT
 -> authoritative scheduled state
```

Transition:

```text
transition record + operation terminal state + scheduler removal + meta finalized step/token
 -> COMMIT
 -> State(S+1) finalized / publishable
```

COMMIT前にconfirmed publication/terminal successを返さない。

判定: PASS。

## 4. SQLite durability profile audit

- WAL mode。
- synchronous FULL。
- single logical PersistenceCoordinator writer。
- manual checkpoint。
- WAL/checkpoint timingをworld semanticsへ使用しない。
- logical uint64をsigned SQLite INTEGERへtruncateせずU64BEで保持。

判定: PASS。

## 5. History integrity audit

- HistorySequence monotonic uint64。
- previous digest chain維持。
- typed record payload registry。
- normalized semantic payloadからrecord digest算出。
- protobuf wire byte variationをhash inputにしない。
- unknown authoritative recordをskipしない。

判定: PASS。

## 6. Operation dedup audit

- OperationId primary key。
- same id/different digest reject。
- terminal tombstone world lifecycle retention。
- rich result detailだけ有限retention可能。
- Snapshot/history recoveryでdedupを再構築可能。

判定: PASS。

## 7. Same-Step DB ordering audit

`SameStepOrderKeyDbV1`を55 octetsへ固定した。

- phase uint8
- domain rank uint16 BE
- conflict digest 32 bytes
- signed priority biased uint32 BE
- intent id 16 bytes

bytewise DB sortとPhase 1 logical tuple orderが一致する。

判定: PASS。

## 8. Snapshot coverage audit

Required logical sections:

```text
6 core recovery sections + 97 authoritative domain partitions = 103
```

欠落required sectionをpartial restoreしない。

record/section canonical order、lineage/detail state、Operation scheduler/dedup/Config/domain registryをrestore可能。

判定: PASS。

## 9. Snapshot physical format audit

- logical SnapshotDigestはphysical chunk/compression非依存。
- immutable chunk framingを固定。
- 32 MiB target / 64 MiB max fragmentation。
- record/item boundary split。
- stored/logical digest双方を検証。
- Zstandard default compressionだがlogical digest不変。

判定: PASS。

## 10. Snapshot commit crash audit

```text
write staging chunks
 -> fsync chunks
 -> write/fsync manifest
 -> fsync staging directory
 -> atomic rename
 -> fsync snapshots parent
 -> SQLite snapshot catalog/history transaction COMMIT
```

DB commit前のorphan directoryはrecovery candidateではない。

DB commit後はrequired physical filesを先にdurable化済み。

判定: PASS。

## 11. Recovery algorithm audit

- CURRENT generation validate。
- DB quick_check/schema/history chain validate。
- newest usable Snapshotを選択。
- physical/logical digest verify。
- required 103 section verify。
- history anchor接続。
- H+1からreplay。
- transition continuity/state diagnostic verify。
- pending/scheduler/dedup復元。
- protocol resync後READY。

単に古いSnapshotが読めるだけではdurable historyを捨てて起動しない。

判定: PASS。

## 12. Corruption audit

- torn/uncommitted physical tailとcommitted corruptionを区別。
- committed hash mismatchをsilent skipしない。
- older Snapshot + intact historyでsame latest durable stateへ到達可能な場合だけfallback。
- durable accepted/finalized fact lossが避けられない場合startup reject。

判定: PASS。

## 13. History retention audit

Persistence v1.0はlogical history full retention。

- historical replay floor = State(0)。
- semantic history deletionを自動実行しない。
- physical WAL/VACUUM/index maintenanceのみ許容。

storage optimizationよりreplay/audit correctnessを優先するinitial policyとしてblockerなし。

判定: PASS。

## 14. Migration audit

- source generation non-destructive。
- target N+1 staging。
- deterministic migration + full validation。
- target fsync後CURRENT atomic replace。
- switch前crashはsource N継続。
- switch後sourceへsilent rollbackしない。

World/Entity/Operation identityを理由なく変更しない。

判定: PASS。

## 15. Export/import audit

Portable exportは:

- committed Snapshot
- H+1..T history
- manifest/digest

のconsistent bundle。

Importはnew staging generationへ検証loadし、existing active generationを直接上書きしない。

判定: PASS。

## 16. Config cross-review

P4-03 current values:

- snapshot interval: 18000 steps
- retained snapshot count: 12
- compression: zstd level 3
- recovery state digest verification: true

P4-04 formatと矛盾なし。

`persistence.snapshot-retain-count >= 2`を維持する。

P4-06でthroughput/storage budgetを計測しdefault変更が必要な場合はP4-03/P4-04双方を更新する。

判定: PASS with later performance validation。

## 17. Protocol cross-review

- StateContinuityTokenをP4-02 publicationへ伝播可能。
- FULL/DELTA resync baseをCore recovery resultへ接続可能。
- operation status queryをoperation_stateから回答可能。
- confirmed publicationはtransition durability後のみ。

判定: PASS。

## 18. Phase 3 domain ownership audit

97 domain partitionはP4-01 registryどおりSnapshot sectionへ配置。

Persistenceはdomain semantic ownershipを変更せずbyte storage/recoveryを担当する。

foreign direct mutationやstorage row identityをdomain authorityにしない。

判定: PASS。

## 19. Failure classification

- history digest mismatch: fatal/start reject
- transition COMMIT failure: step abort / failed-safe
- snapshot write failure: operational degradation, current world may continue
- unsupported schema: start reject
- migration failure: target abort, source generation維持
- export failure: export abort, active world不変

Phase 4 common failure policyと整合。

## 20. Acceptance criteria

| Criterion | Result |
|---|---|
| physical DB/file layout fixed | PASS |
| history typed schema fixed | PASS |
| durable acceptance boundary fixed | PASS |
| transition commit boundary fixed | PASS |
| full 103-section Snapshot coverage | PASS |
| snapshot chunk/framing fixed | PASS |
| recovery selection/replay fixed | PASS |
| operation tombstone persistence fixed | PASS |
| migration rollback/switch fixed | PASS |
| portable export/import fixed | PASS |
| corruption/torn-write handling fixed | PASS |
| unresolved persistence blocker = 0 | PASS |

## 21. Completion decision

P4-04をCompleteと判定する。

P4-06でperformance/storage budgetをcross-reviewするが、physical schema/commit/recoveryの未確定技術blockerは0件。
