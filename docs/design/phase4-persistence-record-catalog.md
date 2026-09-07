# 詳細設計 Phase 4: Persistence Record / Encoding Catalog

Status: Complete / P4-04 record catalog  
Tracking: Issue #16  
Parent: `phase4-persistence-specification.md`

## 1. 目的

P4-04本体で残したhistory payload schema、DB canonical key、Snapshot chunk splitting、retention、portable exportを固定する。

## 2. Common persistence wire rules

History `payload_bytes`とSnapshot physical payloadはprotobuf proto3を使用する。

ただしlogical hash/digestはprotobuf serialized bytesではなく、decode後のschema-normalized semantic valueをMV-DCBOR-v1へ写像して計算する。

Fixed-size identity validation:

```text
WorldId / OperationId / BatchId / SnapshotId = 16 bytes
WorldSeed = 32 bytes
Hash256 / ConfigDigest / continuity token = 32 bytes
```

## 3. SameStepOrderKey DB encoding

DB index用canonical binary encodingを55 octetsへ固定する。

```text
SameStepOrderKeyDbV1 := bytes[55]
```

Layout:

```text
Offset  Size  Field
0       1     phase uint8
1       2     domain_rank uint16 big-endian
3       32    conflict_scope_digest raw bytes
35      4     semantic_priority biased uint32 big-endian
39      16    intent_id raw bytes
```

Signed int32 priority bias:

```text
biased = uint32(semantic_priority) XOR 0x80000000
```

これによりbytewise lexicographic ascendingとPhase 1 tuple ascendingが一致する。

`domain_rank` range: 0..65535。

Standard Phase 3 domain rankは10/20/.../80。

DB `scheduled_operation.order_key`はexact length 55を要求する。

## 4. `operation.accepted.v1`

Schema id:

```text
persistence.operation-accepted / 1.0
```

```proto
message OperationAcceptedRecordWireV1 {
  bytes operation_id = 1;
  bytes operation_payload_digest = 2;
  optional bytes batch_id = 3;
  StandardOperationPersistenceWireV1 immutable_operation = 4;
  uint64 accepted_config_generation = 5;
  optional uint64 accepted_master_generation = 6;
}
```

`immutable_operation`にはOperation kind、normalized target/content、scheduling admissionを含め、candidate/effective Step、routing、retry metadataを含めない。

## 5. `operation.scheduled.v1`

Schema id:

```text
persistence.operation-scheduled / 1.0
```

```proto
message OperationScheduledRecordWireV1 {
  bytes operation_id = 1;
  uint64 effective_step = 2;
  bytes same_step_order_key = 3;          // exactly 55
  string scheduling_result_code = 4;
}
```

## 6. `operation.terminal.v1`

Schema id:

```text
persistence.operation-terminal / 1.0
```

```proto
message OperationTerminalRecordWireV1 {
  bytes operation_id = 1;
  bytes operation_payload_digest = 2;
  ResultStatusPersistenceV1 status = 3;
  string result_code = 4;
  optional uint64 effective_step = 5;
  optional bytes rich_result_payload = 6;
  optional string rich_result_schema_id = 7;
}
```

non-applied terminal resultのみ独立recordを標準とする。

transition参加resultは`transition.committed.v1`へ含める。

## 7. `config.changed.v1`

Schema id:

```text
persistence.config-changed / 1.0
```

```proto
message ConfigChangedRecordWireV1 {
  optional bytes operation_id = 1;
  uint64 base_generation = 2;
  uint64 next_generation = 3;
  bytes before_digest = 4;
  bytes after_digest = 5;
  uint64 effective_step = 6;
  repeated ConfigChangedValueWireV1 changed_values = 7;
}

message ConfigChangedValueWireV1 {
  string path = 1;
  ConfigValuePersistenceWireV1 value = 2;
}
```

changed_valuesはpath ASCII ascending、duplicate禁止。

secret material禁止。

## 8. `snapshot.committed.v1`

Schema id:

```text
persistence.snapshot-committed / 1.0
```

```proto
message SnapshotCommittedRecordWireV1 {
  bytes snapshot_id = 1;
  uint64 snapshot_step = 2;
  uint64 history_anchor_sequence = 3;
  bytes history_anchor_digest = 4;
  bytes snapshot_digest = 5;
  bytes physical_manifest_digest = 6;
}
```

## 9. `master.generation.changed.v1`

Schema id:

```text
persistence.master-generation-changed / 1.0
```

```proto
message MasterGenerationChangedRecordWireV1 {
  uint64 previous_generation = 1;
  uint64 next_generation = 2;
  string reason_code = 3;
}
```

Constraint:

```text
next_generation = previous_generation + 1
```

Gateway identityをworld history semanticへ含めない。必要なoperational auditはP4-07別store。

## 10. `world.pause.changed.v1`

Schema id:

```text
persistence.world-pause-changed / 1.0
```

```proto
message WorldPauseChangedRecordWireV1 {
  uint64 basis_step = 1;
  WorldPauseStateWireV1 previous_state = 2;
  WorldPauseStateWireV1 next_state = 3;
  optional bytes operation_id = 4;
}

enum WorldPauseStateWireV1 {
  WORLD_PAUSE_STATE_UNSPECIFIED = 0;
  RUNNING = 1;
  PAUSED = 2;
}
```

wall-clock pause durationを保存してSimulationStepへ加算しない。

## 11. `persistence.migrated.v1`

`phase4-persistence-specification.md`の`PersistenceMigratedRecordWireV1`をschema id:

```text
persistence.migrated / 1.0
```

として使用する。

Migration recipe digestはmigration implementation artifactのnormalized recipe identityをbindする。

## 12. History append invariant

History append transaction前に:

```text
new_sequence = last_history_sequence + 1
previous_digest = last_history_digest
```

をsingle writer authorityで決定する。

SQLite row insertion順をworld orderingへ利用しない。

unique/sequence/hash chain failureはfatal persistence invariant。

## 13. Snapshot section physical fragmentation

Physical chunk target:

```text
target_uncompressed_bytes = 32 MiB
hard_max_uncompressed_bytes = 64 MiB
```

Config化しないformat-level initial profile。

理由: physical chunk boundaryはlogical snapshot digestへ影響しないため、将来format minorでtargetを変更可能。

### 13.1 Fragment unit

Domain partitionはrecord boundaryでfragmentする。

```proto
message SnapshotSectionFragmentV1 {
  string section_id = 1;
  uint32 fragment_index = 2;
  uint32 fragment_count = 3;
  optional bytes first_record_id = 4;
  optional bytes last_record_id = 5;
  uint64 item_count = 6;
  bytes fragment_payload = 7;
}
```

Core sectionもschema-defined item boundaryを持つ。

### 13.2 Packing algorithm

1. section_id ASCII ascending。
2. section内item canonical order。
3. fragment payloadへitemを順に追加。
4. next itemを追加すると32 MiBを超える場合、current fragmentをclose。
5. single itemが32 MiBを超える場合、単独fragmentとする。
6. single itemが64 MiBを超える場合、schema violationとしてsnapshot作成失敗。P4-05 domain stateはrecordを再partition可能な単位へ設計する。
7. fragmentsを順にphysical chunkへpackし、32 MiB targetで同様にclose。
8. chunk uncompressed sizeは64 MiBを超えない。

fragment_countはsection item走査後に確定し、serialization前に書き込む。

## 14. Snapshot section reassembly

sectionごとに:

- fragment_index starts 0。
- exactly `0..fragment_count-1`。
- first/last record range overlap禁止。
- item count合計がLogicalSnapshotSection.item_countと一致。
- reassembled canonical semantic content digestがlogical section digestと一致。

一件でも失敗したSnapshotはrecovery candidate不可。

## 15. Snapshot retention

Config `persistence.snapshot-retain-count` standard default 12。

Deletion eligibility:

- committed snapshotのみ。
- newest 2 committed snapshotは常にretain。
- snapshotを削除してもfull durable history/recovery guaranteeを失わない。
- deletion transaction前にselected snapshotsがcurrent recovery candidateでないことを再確認。

Snapshot deletionはworld outcomeへ影響しない。

## 16. History retention v1.0

Standard v1.0は**logical history full retention**を採用する。

自動semantic deletionを行わない。

保持対象:

- genesis
- Operation accepted/scheduled/terminal
- all transition commit records
- all simulation Config changes
- all snapshot commit records
- persistence migration
- pause changes
- master generation changes

P4-04 v1.0では`historical replay floor = State(0)`。

このためhistory deletion/anchor compactionはstandard v1.0では実行しない。

将来storage pressure対応でsemantic compactionを追加する場合、Persistence schema minor/major updateとreplay guarantee変更の明示を要求する。

## 17. Physical SQLite maintenance

Logical historyを削除しなくても次を許可する。

- WAL checkpoint
- `VACUUM`/incremental vacuum where safe
- index rebuild
- page cache tuning

これらでHistorySequence/record digest/payload semanticsを変更しない。

## 18. Portable world export

Standard export format:

```text
MachiVerseWorldExportV1/
  export-manifest.pb
  snapshot/
    manifest.pb
    chunks/...
  history/
    00000000.mvlog
    00000001.mvlog
    ...
```

Exportはcommitted Snapshot `S` + history anchor `H` + target durable history sequence `T >= H`のconsistent bundle。

## 19. Export manifest

```proto
message WorldExportManifestV1 {
  uint32 format_major = 1;              // =1
  uint32 format_minor = 2;              // =0
  bytes world_id = 3;
  bytes snapshot_id = 4;
  uint64 snapshot_step = 5;
  uint64 history_anchor_sequence = 6;
  bytes history_anchor_digest = 7;
  uint64 target_history_sequence = 8;
  bytes target_history_digest = 9;
  bytes snapshot_digest = 10;
  repeated ExportHistorySegmentDescriptorV1 history_segments = 11;
  bytes export_digest = 12;
}
```

`export_digest`はlogical manifest + segment logical digestsをMV-DCBOR-v1でbindする。

## 20. History export segment format

`*.mvlog` framing:

```text
magic: 8 bytes ASCII "MVLOG001"
format major: uint16 BE = 1
format minor: uint16 BE = 0
first sequence: uint64 BE
last sequence: uint64 BE
record count: uint32 BE
segment logical digest: 32 bytes
then repeated:
  record_length uint32 BE
  HistoryRecordExportWireV1 protobuf bytes
```

segment target uncompressed size: 64 MiB。

recordを跨いでsplitしない。

## 21. Export creation

1. committed Snapshotを選択/必要なら作成。
2. target durable sequence Tをfreeze。
3. snapshot directoryをlogical digest検証付きcopy。
4. H+1..T history recordsをsequence順にsegment化。
5. each segment digest verify。
6. export-manifest生成。
7. full bundle verify。
8. destination staging directoryをfinal nameへatomic renameできる場合はrename。できないmediaではcompletion markerを最後にwriteする。

Export I/O中もworldを進行可能。target T以降はbundleへ含めない。

## 22. Export import

Importは直接existing active generationを上書きしない。

- new World persistence generation stagingへload。
- world id collision時は同一world continuityとして明示importする場合のみ許可。別worldとしてWorldIdを書き換えるgeneric importは禁止。
- snapshot/history hash chainをverify。
- target history Tまでreplay verify。
- success後のみCURRENT/registrationへactivate。

## 23. Recovery/record compatibility

Unknown history record type:

- current persistence schemaがrequired semanticとして知らないrecordはstartup reject。
- optional diagnostic recordとしてschema registryが`non-authoritative`分類したrecordだけskip可能。

Standard registryのrecordは全てauthoritative/recovery-relevantとして扱う。

## 24. Error codes

```text
persistence.sqlite-open-failed
persistence.sqlite-pragma-mismatch
persistence.integrity-failed
persistence.history-gap
persistence.history-digest-mismatch
persistence.record-schema-unsupported
persistence.snapshot-missing
persistence.snapshot-digest-mismatch
persistence.snapshot-section-missing
persistence.snapshot-fragment-invalid
persistence.snapshot-item-too-large
persistence.current-invalid
persistence.migration-failed
persistence.export-invalid
persistence.commit-failed
```

## 25. Acceptance criteria

- DB bytewise orderがSameStepOrderKey logical orderと一致する。
- all standard history record typeをtyped payloadへdecodeできる。
- protobuf physical bytesが異なってもnormalized payload同一ならlogical record digestが同一。
- 32 MiB target/64 MiB max chunk split/reassemblyでsection digestが維持される。
- latest snapshot corruption時、older+full historyでsame terminal stateへ到達できる。
- logical history full retentionによりState(0)からhistorical replay可能。
- terminal tombstoneをworld lifecycle中保持する。
- portable exportを別staging persistenceへimport/replay検証できる。
- migration/source generationをCURRENT切替前に破壊しない。
