# 詳細設計 Phase 4: Persistence / Snapshot / History / Migration Specification

Status: In Progress / P4-04  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`  
Predecessors: `phase1-persistence-replay-recovery.md`, `phase4-core-data-structures.md`, `phase4-domain-state-registry.md`

## 1. 目的

Phase 1で確定したdurability、Snapshot consistent cut、History hash chain、replay/recovery、migration意味論を、physical directory、embedded database、record schema、snapshot chunk、commit/fsync、recovery selectionへ具体化する。

本書はauthoritative persistenceの標準implementation profileを固定する。

## 2. Physical technology decision

Standard persistence profile:

- metadata / append-only history / operation index: **SQLite 3**
- SQLite journal mode: `WAL`
- SQLite synchronous: `FULL`
- snapshot payload: immutable binary chunk files
- snapshot payload serialization: Protocol Buffers proto3 container
- snapshot default compression: Zstandard
- logical digest / identity: Phase 1 `MV-DCBOR-v1` + SHA-256

SQLite protobuf bytesやphysical chunk bytesをworld semantic digestの正本にしない。

### 2.1 SQLite required pragmas

新規/open時に少なくとも次を検証する。

```sql
PRAGMA journal_mode = WAL;
PRAGMA synchronous = FULL;
PRAGMA foreign_keys = ON;
PRAGMA wal_autocheckpoint = 0;
PRAGMA busy_timeout = 5000;
```

`journal_mode`や`synchronous`がrequested valueへ設定できないstorage/deploymentではauthoritative worldをREADYにしない。

WAL checkpointはPersistenceCoordinatorが明示的に行い、SQLite default auto-checkpointによるcommit latency spikeを避ける。

## 3. World persistence directory

Config/deploymentで解決したpersistence root配下:

```text
<root>/
  worlds/
    <world-id-32hex>/
      CURRENT
      generations/
        0000000000000001/
          world.sqlite3
          world.sqlite3-wal        # runtime transient
          world.sqlite3-shm        # runtime transient
          snapshots/
            <snapshot-id-32hex>/
              manifest.pb
              chunks/
                00000000.mvchunk
                00000001.mvchunk
                ...
        0000000000000002/
          ...
```

`world-id-32hex`はWorldId canonical lowercase hex。

## 4. PersistenceGeneration

```text
PersistenceGeneration := uint64
```

- initial = 1。
- physical/logical persistence migrationでnew generationを作るごとに+1。
- SimulationStep / ConfigGeneration / MasterGenerationと別identity。
- world orderingへ使用しない。

Directory nameは16桁lowercase hexadecimal zero-padded。

例:

```text
0000000000000001
0000000000000002
```

## 5. CURRENT pointer

`CURRENT`はactive PersistenceGenerationだけを保持するASCII file。

Canonical content:

```text
0000000000000001\n
```

規則:

- exactly 17 bytes。
- temp file write -> fsync -> atomic replace -> parent directory fsync。
- malformed/nonexistent generationはstartup reject。
- migration中のnew generationをCURRENT切替前にactiveと扱わない。

## 6. SQLite integer representation

MachiVerse logical `uint64`全域をSQLite signed INTEGERへ直接格納しない。

次はcanonical big-endian 8-octet BLOBとする。

```text
SimulationStep
HistorySequence
MasterGeneration
ConfigGeneration
PersistenceGeneration
uint64 revision
```

型名:

```text
U64BE := bytes[8], unsigned big-endian
```

bytewise lexicographic comparisonとunsigned numeric comparisonが一致する。

`uint32`以下のbounded enum/countはSQLite INTEGERを使用可能。

## 7. SQLite schema metadata

Database application id / user versionだけをschema authorityにせず、logical tableで明示する。

```sql
CREATE TABLE persistence_meta (
  singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
  world_id BLOB NOT NULL CHECK (length(world_id) = 16),
  persistence_generation BLOB NOT NULL CHECK (length(persistence_generation) = 8),
  schema_major INTEGER NOT NULL CHECK (schema_major BETWEEN 0 AND 65535),
  schema_minor INTEGER NOT NULL CHECK (schema_minor BETWEEN 0 AND 65535),
  world_seed BLOB NOT NULL CHECK (length(world_seed) = 32),
  last_history_sequence BLOB NOT NULL CHECK (length(last_history_sequence) = 8),
  last_history_digest BLOB NOT NULL CHECK (length(last_history_digest) = 32),
  finalized_step BLOB NOT NULL CHECK (length(finalized_step) = 8),
  state_continuity_token BLOB NOT NULL CHECK (length(state_continuity_token) = 32),
  config_generation BLOB NOT NULL CHECK (length(config_generation) = 8),
  config_digest BLOB NOT NULL CHECK (length(config_digest) = 32),
  master_generation BLOB NOT NULL CHECK (length(master_generation) = 8)
);
```

exactly 1 row。

## 8. History table

```sql
CREATE TABLE history_record (
  sequence BLOB PRIMARY KEY CHECK (length(sequence) = 8),
  previous_record_digest BLOB NOT NULL CHECK (length(previous_record_digest) = 32),
  record_type TEXT NOT NULL,
  payload_schema_id TEXT NOT NULL,
  payload_schema_major INTEGER NOT NULL,
  payload_schema_minor INTEGER NOT NULL,
  payload_bytes BLOB NOT NULL,
  normalized_payload_digest BLOB NOT NULL CHECK (length(normalized_payload_digest) = 32),
  record_digest BLOB NOT NULL UNIQUE CHECK (length(record_digest) = 32)
) WITHOUT ROWID;
```

`record_type` / schema idはStableToken validationをapplication layerで行う。

### 8.1 Hash chain

Phase 1:

```text
HistoryRecordDigest = DomainHash(
  "mv.history-record.v1",
  {
    world_id,
    sequence,
    previous_record_digest,
    record_type,
    normalized_record_payload
  }
)
```

をそのまま使用する。

`payload_bytes` protobuf serialization差はdigestへ直接使用せず、decode + normalizeしたsemantic payloadから検証する。

## 9. History record registry

Standard record type:

```text
world.genesis.v1
operation.accepted.v1
operation.scheduled.v1
operation.terminal.v1
config.changed.v1
transition.committed.v1
snapshot.committed.v1
persistence.migrated.v1
master.generation.changed.v1
world.pause.changed.v1
```

P4-04 initial payload schema versionは全て`1.0`。

## 10. `world.genesis.v1`

```proto
message WorldGenesisRecordWireV1 {
  bytes world_id = 1;                         // 16
  bytes world_seed = 2;                       // 32
  uint64 initial_step = 3;                    // must 0
  bytes initial_state_continuity_token = 4;   // 32
  uint64 simulation_config_generation = 5;
  bytes simulation_config_digest = 6;         // 32
  uint32 persistence_schema_major = 7;
  uint32 persistence_schema_minor = 8;
  repeated string required_domains = 9;
  repeated RequiredAddonWireV1 required_addons = 10;
}
```

required domainsはDomainToken ASCII ascending、duplicate禁止。

## 11. Operation index

world-lifetime dedupとrecovery query用materialized index:

```sql
CREATE TABLE operation_state (
  operation_id BLOB PRIMARY KEY CHECK (length(operation_id) = 16),
  payload_digest BLOB NOT NULL CHECK (length(payload_digest) = 32),
  lifecycle INTEGER NOT NULL,
  accepted_sequence BLOB CHECK (accepted_sequence IS NULL OR length(accepted_sequence) = 8),
  scheduled_sequence BLOB CHECK (scheduled_sequence IS NULL OR length(scheduled_sequence) = 8),
  effective_step BLOB CHECK (effective_step IS NULL OR length(effective_step) = 8),
  terminal_sequence BLOB CHECK (terminal_sequence IS NULL OR length(terminal_sequence) = 8),
  terminal_status INTEGER,
  result_code TEXT,
  rich_result_payload BLOB
) WITHOUT ROWID;
```

- primary key: OperationId。
- same id/different digest: reject。
- terminal rowをWorldId lifecycle中deleteしない。
- rich_result_payloadだけConfig retention後にNULL化可能。
- operation_stateはhistory/snapshot recovery stateと常にtransactionally整合させる。

## 12. Scheduler index

```sql
CREATE TABLE scheduled_operation (
  effective_step BLOB NOT NULL CHECK (length(effective_step) = 8),
  order_key BLOB NOT NULL,
  operation_id BLOB NOT NULL CHECK (length(operation_id) = 16),
  PRIMARY KEY (effective_step, order_key, operation_id),
  UNIQUE (operation_id)
) WITHOUT ROWID;
```

`order_key`は`SameStepOrderKey`のcanonical binary index encoding。encoding specはP4-05 determinism artifactで固定する。

arrival orderを格納しない。

## 13. Config state/history index

```sql
CREATE TABLE simulation_config_state (
  generation BLOB PRIMARY KEY CHECK (length(generation) = 8),
  config_digest BLOB NOT NULL CHECK (length(config_digest) = 32),
  effective_step BLOB CHECK (effective_step IS NULL OR length(effective_step) = 8),
  normalized_config_bytes BLOB NOT NULL,
  history_sequence BLOB NOT NULL CHECK (length(history_sequence) = 8)
) WITHOUT ROWID;
```

normalized_config_bytesはsecretを含まないCore SIMULATION Config canonical payload。

## 14. Master / pause recovery state

```sql
CREATE TABLE core_operational_state (
  singleton INTEGER PRIMARY KEY CHECK (singleton = 1),
  master_generation BLOB NOT NULL CHECK (length(master_generation) = 8),
  world_pause_state INTEGER NOT NULL,
  pause_basis_step BLOB CHECK (pause_basis_step IS NULL OR length(pause_basis_step) = 8)
);
```

Master identityそのものはrecovery後に再認証/再選定するためcurrent gateway connectionを永続authorityとしない。

MasterGeneration単調性は保持する。

## 15. Durable acceptance transaction

CoreがOperation ACCEPTEDを返す前にsingle SQLite write transactionで:

1. `history_record`へ`operation.accepted.v1` append。
2. `operation_state`へACCEPTED row insert。
3. `persistence_meta.last_history_*`更新。
4. COMMIT。

COMMIT success後のみCore authoritative ACCEPTEDを返す。

WAL + synchronous FULLをrequiredとする。

## 16. Scheduling durability transaction

final effective Step確定時single transactionで:

1. `operation.scheduled.v1` append。
2. `operation_state.lifecycle=SCHEDULED`更新。
3. `scheduled_operation` insert。
4. last history metadata更新。
5. COMMIT。

COMMIT後、recoveryで別Stepへsilent rescheduleしない。

## 17. Transition commit transaction

`State(S) -> State(S+1)` finalize時、single SQLite transactionで最低限:

1. `transition.committed.v1` history append。
2. applied/terminal Operationの`operation_state`更新。
3. completed `scheduled_operation` row削除。
4. Step Sでeffectiveになったsimulation Config stateをcurrent metadataへ反映。
5. `persistence_meta.finalized_step = S+1`。
6. resulting StateContinuityToken更新。
7. state/config/history digest metadata更新。
8. COMMIT。

COMMIT success前:

- State(S+1) confirmed publish禁止。
- applied terminal success禁止。

COMMIT失敗:

- State(S)をauthorityとして維持。
- in-memory candidate破棄/再計算可能。

## 18. Transition record payload

```proto
message TransitionCommittedRecordWireV1 {
  uint64 effective_step = 1;
  uint64 resulting_step = 2;
  uint64 active_config_generation = 3;
  bytes active_config_digest = 4;
  repeated bytes applied_operation_ids = 5;  // each 16, canonical order
  repeated OperationOutcomeWireV1 operation_outcomes = 6;
  bytes previous_state_continuity_token = 7;
  bytes resulting_state_continuity_token = 8;
  bytes state_diagnostic_hash = 9;
  repeated PartitionDigestWireV1 partition_digests = 10;
}
```

`resulting_step = effective_step + 1`。

partition digest listはPartitionId ASCII ascending。

## 19. Snapshot logical section registry

Snapshot logical sections:

Core recovery sections:

```text
core.world-state-header
core.scheduler-state
core.operation-state
core.detail-directory
core.domain-registry
core.config-state
```

plus Phase 4 registryの97 authoritative partition。

Standard initial required logical section count:

```text
6 + 97 = 103
```

全103 section required。

## 20. Logical Snapshot manifest

```text
LogicalSnapshotManifestV1 {
  persistence_schema_version,
  world_id,
  snapshot_id,
  snapshot_step,
  history_anchor_sequence,
  history_anchor_digest,
  state_continuity_token,
  world_seed,
  simulation_config_generation,
  simulation_config_digest,
  master_generation,
  required_domain_set,
  required_addon_metadata,
  sections: ordered list<LogicalSnapshotSectionV1>,
  snapshot_digest
}
```

```text
LogicalSnapshotSectionV1 {
  section_id: StableToken,
  section_schema: SchemaRefV1,
  logical_item_count: uint64,
  logical_content_digest: Hash256,
  required: bool
}
```

sectionsはsection_id ASCII ascending。

`SnapshotDigest`はPhase 1 ruleどおりlogical manifestから計算し、physical chunk boundary/compressionを含めない。

## 21. Physical Snapshot manifest

`manifest.pb`はlogical manifestとphysical chunk mappingを保持する。

```proto
message PhysicalSnapshotManifestV1 {
  LogicalSnapshotManifestWireV1 logical = 1;
  repeated PhysicalSnapshotChunkDescriptorV1 chunks = 2;
  bytes physical_manifest_digest = 3;
}

message PhysicalSnapshotChunkDescriptorV1 {
  uint32 chunk_index = 1;
  string first_section_id = 2;
  string last_section_id = 3;
  uint64 uncompressed_length = 4;
  uint64 stored_length = 5;
  SnapshotCompressionV1 compression = 6;
  bytes logical_payload_digest = 7;
  bytes stored_payload_digest = 8;
  string relative_path = 9;
}
```

chunksはchunk_index ascending。

relative_pathは`chunks/00000000.mvchunk`形式だけを許可し、`..`/absolute path禁止。

## 22. Snapshot chunk logical payload

```proto
message SnapshotChunkPayloadV1 {
  repeated SnapshotSectionPayloadV1 sections = 1;
}

message SnapshotSectionPayloadV1 {
  string section_id = 1;
  SchemaVersionWireV1 schema_version = 2;
  bytes section_payload = 3;
}
```

Domain partition section payload:

```proto
message DomainPartitionSnapshotV1 {
  PartitionStateHeaderWireV1 header = 1;
  repeated DomainRecordSnapshotV1 records = 2;
}
```

recordsはPartitionRecordId bytewise ascending。

## 23. Physical chunk file framing

`*.mvchunk` format v1:

```text
Offset  Size  Field
0       8     magic ASCII "MVCHNK01"
8       2     format_major uint16 BE = 1
10      2     format_minor uint16 BE = 0
12      1     compression enum
13      1     flags = 0
14      2     reserved = 0
16      8     uncompressed_length uint64 BE
24      8     stored_length uint64 BE
32      32    logical_payload_digest SHA-256
64      32    stored_payload_digest SHA-256
96      N     stored payload
```

Header fixed length: 96 bytes。

- logical digestはuncompressed decoded semantic payloadのnormalized digest。
- stored digestはbytes at offset 96..EOFのSHA-256。
- file length = 96 + stored_length。
- unknown flag/reserved nonzeroはformat major 1でreject。

## 24. Snapshot compression

```text
SnapshotCompressionV1 :=
  0 NONE
  1 ZSTD
```

Default: ZSTD level 3 from Config。

Compressionはlogical SnapshotDigestに影響しない。

Dictionary compressionはv1.0標準では使用しない。

## 25. Snapshot staging / commit

Snapshot `snapshot_id=X`:

1. Step boundaryでimmutable RecoveryState root + history anchor Hをfreeze。
2. `snapshots/.staging-X/`作成。
3. chunk filesをwrite。
4. each chunk file flush/fsync。
5. manifest.pb write + fsync。
6. staging directory fsync。
7. directoryを`snapshots/X/`へatomic rename。
8. snapshots parent directory fsync。
9. SQLite transactionでsnapshot catalog insert + `snapshot.committed.v1` append。
10. SQLite COMMIT。
11. COMMIT後のみrecovery candidateとして公開。

Crash cases:

- step 1..8でcrash: DB catalogなし、staging/orphan snapshotを無視/GC。
- step 8後/9前crash: complete orphan directoryだがcatalogなし、recovery candidateではない。
- DB commit後: filesは先にdurable化済みでなければならない。

## 26. Snapshot catalog

```sql
CREATE TABLE snapshot_catalog (
  snapshot_id BLOB PRIMARY KEY CHECK (length(snapshot_id) = 16),
  snapshot_step BLOB NOT NULL CHECK (length(snapshot_step) = 8),
  history_anchor_sequence BLOB NOT NULL CHECK (length(history_anchor_sequence) = 8),
  history_anchor_digest BLOB NOT NULL CHECK (length(history_anchor_digest) = 32),
  state_continuity_token BLOB NOT NULL CHECK (length(state_continuity_token) = 32),
  snapshot_digest BLOB NOT NULL CHECK (length(snapshot_digest) = 32),
  physical_manifest_digest BLOB NOT NULL CHECK (length(physical_manifest_digest) = 32),
  relative_directory TEXT NOT NULL UNIQUE
) WITHOUT ROWID;
```

newest selectionはSnapshotIdの大小でなく`snapshot_step`、history anchor validity、schema compatibilityで行う。

## 27. Snapshot ID

Phase 1 derivationを変更しない。

ZERO生成時deterministic nonce ruleはP4-04で次へ固定する。

```text
nonce starts 1
repeat SnapshotId derivation with { ..., nonce }
until non-zero
```

nonceはsmallest successful unsigned integerを使用する。

## 28. Recovery selection algorithm

1. CURRENT parse/active generation validate。
2. SQLite open + integrity/schema metadata validation。
3. `snapshot_catalog`をsnapshot_step descendingで列挙。
4. candidateごとにdirectory/manifest existence確認。
5. logical/physical manifest digest確認。
6. all required 103 sections確認。
7. all chunk stored digest確認。
8. decompress/decode後logical section digest確認。
9. required domain/addon/schema compatibility確認。
10. history anchor digestが`history_record` chainへ接続することを確認。
11. newest fully usable candidateを選ぶ。
12. snapshot RecoveryState load。
13. H+1からhistory replay。
14. transitionごとにStateContinuityToken/state diagnosticを検証。
15. last durable finalized stateへ到達。
16. pending Operation/scheduler/dedup stateを照合。
17. protocol resync完了後READY。

latest snapshot failure時、earlier snapshot + intact full historyでlatest durable stateへ到達できる場合だけfallbackする。

## 29. SQLite integrity check startup policy

Normal startup:

```sql
PRAGMA quick_check;
```

結果が`ok`以外ならREADY拒否。

Operator-requested deep validation / migration:

```sql
PRAGMA integrity_check;
```

を使用できる。

SQLite check結果だけでMachiVerse history hash/section digest検証を省略しない。

## 30. WAL checkpoint policy

- auto-checkpoint disabled。
- background PersistenceCoordinatorがPASSIVE checkpointをperiodically実行可能。
- graceful shutdown / snapshot maintenanceでTRUNCATE checkpointを試行可能。
- checkpoint失敗/reader blockageはworld semantic failureではないが、WAL growthをobservabilityへ通知する。
- transition durabilityはcheckpoint完了ではなくWAL transaction COMMIT + synchronous FULL boundaryを使用する。

## 31. History retention / compaction

Initial standard rule:

- operation terminal tombstone: WorldId lifecycle全期間。
- genesis record:全期間。
- Config simulation history:全期間。
- persistence migration record:全期間。
- history hash-chain continuity anchor: compaction後も保持。

Transition/history detailは、少なくともoldest retained committed snapshot以前をcompact候補にできるが、次を全て満たすまで削除しない。

- configured historical replay floor。
- pending Operationなし。
- operation tombstone semantics維持。
- Config history維持。
- required audit/history維持。
- continuity anchor維持。

physical compaction algorithmはP4-04 completion後半で追加する。

## 32. Rich Operation result retention

Config `result.rich-retention-seconds`はGateway local resultに適用する。

Core persistenceではrich terminal payloadをstorage budgetに応じてcompactできるが、最低限:

```text
operation_id
payload_digest
terminal_status
result_code
effective_step
terminal_history_sequence
```

をworld lifecycle中保持する。

## 33. Persistence migration

標準migrationはsource generationを破壊しないcopy-on-write generation migration。

```text
Generation N
 -> create Generation N+1 staging
 -> deterministic schema/data migration
 -> validate SQLite/history/snapshots/config/domain/addon
 -> write `persistence.migrated.v1`
 -> fsync all target files/directories
 -> finalize target generation directory
 -> atomic CURRENT replace to N+1
```

CURRENT切替前crash: Nをactiveとして継続。

CURRENT切替後: N+1をactive。Nはrollback/debug用にretain可能だが、operatorが明示しない限り自動rollbackしない。

## 34. Migration record

```proto
message PersistenceMigratedRecordWireV1 {
  uint64 source_persistence_generation = 1;
  uint64 target_persistence_generation = 2;
  uint32 source_schema_major = 3;
  uint32 source_schema_minor = 4;
  uint32 target_schema_major = 5;
  uint32 target_schema_minor = 6;
  bytes source_terminal_history_digest = 7;
  bytes target_terminal_history_digest = 8;
  bytes migration_recipe_digest = 9;
}
```

migrationでWorldId/EntityId/OperationIdを理由なく再発行しない。

## 35. Backup/copy safety

running persistence directoryをfilesystem-level random copyしてconsistent backupとみなさない。

Supported exportはcommitted Snapshot + required history rangeをconsistent bundleとして生成する。

SQLite online backup API等を使用する場合もsnapshot/history causal boundaryを検証する。

## 36. Failure classification

| failure | classification |
|---|---|
| unsupported persistence major | component_start_reject |
| SQLite quick_check failure | fatal/start reject |
| history hash gap/mismatch | fatal authority |
| committed snapshot chunk missing | candidate invalid; fallback only if same durable state recoverable |
| latest snapshot corrupt but older+history complete | recoverable fallback |
| durable history loss | fatal/start reject |
| snapshot staging failure | operational failure; world may continue |
| SQLite transition COMMIT failure | step_abort / degraded or failed-safe |
| CURRENT malformed | component_start_reject |
| migration validation failure | migration abort; source generation remains |

## 37. P4-04 acceptance status

確定済み:

- physical world directory/generation layout
- SQLite WAL + FULL durability profile
- uint64 SQLite representation
- history/operation/scheduler/config/core state tables
- transition/accept/schedule commit transaction boundary
- logical 103-section Snapshot registry
- physical manifest/chunk framing
- snapshot staging/fsync/commit sequence
- recovery selection algorithm
- migration generation/CURRENT switching
- initial compaction safety rule

未確定:

- exact history payload schemas for accepted/scheduled/terminal/config/master/pause records
- deterministic `SameStepOrderKey` binary DB encoding
- snapshot chunk target sizing/splitting algorithm
- historical replay retention default
- compaction transaction/anchor record format
- backup/export bundle format
- performance budget cross-review

blocker: なし。P4-04後半で上記を閉じる。
