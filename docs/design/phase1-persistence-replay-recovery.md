# 詳細設計 Phase 1: persistence・replay・recovery 契約

Status: Draft / P1-05 complete  
Tracking: Issue #13  
Parent: `docs/design/phase1-common-foundation-contracts.md`

## 1. 目的

本書は Phase 1 の P1-05 として、MachiVerse の authoritative world を保存・再開・replay・crash recovery するための共通契約を具体化する。

対象は次の通り。

- `State(S)` を基準とする consistent Snapshot boundary
- durable operation / config / transition history
- authoritative finalized Step と durability の関係
- accepted Operation を crash で失わないための ACK boundary
- terminal result の durable boundary
- recovery checkpoint と journal continuation
- corruption / torn-write / missing-history の扱い
- state publication continuity token
- save / replay / recovery 時の Config・WorldSeed・ID・Step の同一性
- snapshot / history compaction の安全条件
- save format migration の failure semantics

本書は `docs/architecture/persistence-replay-recovery.md` と `docs/architecture/persistence-save-recovery-semantics.md` の詳細化である。

## 2. 基本原則

1. authoritative Snapshot は必ず完全な `State(S)` 境界を表す。
2. `State(S) -> State(S+1)` の計算途中・merge途中・apply途中を保存点にしない。
3. authoritative に finalized された Step は crash recovery 後に黙って巻き戻さない。
4. world-affecting Operation を durable acceptance として ACK した後、その Operation identity / immutable payload / recovery に必要な scheduling context を失わない。
5. applied Operation の terminal success は対応 transition の durable commit より先に返さない。
6. persistence I/O timing、compression、thread completion order を world outcome の入力にしない。
7. replay は wall clock / original thread schedule / original network arrival race を再現しない。
8. snapshot / journal / Config / addon dependency の整合性を確認できない場合、部分的に読み込んで起動しない。
9. corruption と schema incompatibility を区別する。互換不能な正常データを「壊れている」とみなして古い状態へ silent fallback しない。
10. Gateway cache / View state は authoritative recovery source としない。

## 3. State boundary

P1-02 の定義を継承し、`effective_step = S` の input は `State(S)` から `State(S+1)` を生成する transition に参加する。

```text
State(0)
  -- transition effective_step=0 --> State(1)
  -- transition effective_step=1 --> State(2)
  ...
```

### 3.1 Snapshot Step

`SnapshotStep = S` は Snapshot に格納される authoritative state が **完全な `State(S)`** であることを意味する。

従って Snapshot(S) は:

- `effective_step < S` の committed effect を含む。
- `effective_step >= S` の world mutation を含まない。
- transition `S` の途中状態を含まない。

### 3.2 finalized Step

`State(S)` が externally authoritative / publishable な finalized state となるのは、`State(S)` へ到達したことを示す persistence commit が durable になった後とする。

- initial `State(0)` は world genesis commit により finalized とする。
- transition `S` の durable commit により `State(S+1)` が finalized になる。
- in-memory calculation が完了していても durable commit 前は externally finalized と扱わない。
- crash recovery 後は last durable finalized state から継続する。

この規則により、外部へ authoritative として公開済みの World Time を crash 後に理由なく巻き戻すことを防ぐ。

## 4. persistence logical model

Phase 1 は physical database / file layout を固定せず、次の論理構造を要求する。

```text
WorldPersistenceSet
  ├─ committed Snapshot(s)
  ├─ append-only durable History
  ├─ persistence metadata / schema information
  └─ required compatibility metadata
```

physical implementation は単一file、複数file、embedded DB、object storage 等を採用できるが、本書の atomicity / integrity / ordering semantics を満たさなければならない。

## 5. Persistence schema version

```text
PersistenceSchemaVersion {
  major: uint16,
  minor: uint16
}
```

規則:

- backward-incompatible semantic change は major を増加する。
- same major 内の backward-compatible change は minor を増加する。
- reader が直接理解できない version は explicit deterministic migration を必要とする。
- unsupported newer semantic を silent ignore しない。
- migration failure は対象 save の load failure とする。

physical serialization format version と logical persistence schema version を分離してもよいが、両方の互換性を検証可能にする。

## 6. History identity

### 6.1 `HistorySequence`

```text
HistorySequence := uint64
```

- world persistence history 内で 1 から単調増加する。
- 0 は no-record / genesis sentinel。
- record append の durable logical sequence を識別する。
- wrap-around を禁止する。
- HistorySequence を simulation ordering、business priority、random context、EntityId derivation に使用しない。
- retry timing / thread completion order 等により HistorySequence の値が異なっても、それだけで world outcome を変えてはならない。

### 6.2 history hash chain

P1-02 の domain-separated SHA-256 を用い、P1-05 で次の label を登録する。

```text
mv.history-record.v1
mv.snapshot.v1
mv.snapshot-id.v1
mv.state-continuity.v1
```

論理 record digest:

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

- sequence `1` の `previous_record_digest` は ZERO256。
- sequence `N > 1` は sequence `N-1` の digest を参照する。
- missing / reorder / silent replacement を検出できること。
- hash chain は world simulation ordering key ではなく persistence integrity / continuity 用である。

physical storage は complete record と torn / partial record を区別できる framing / checksum を持たなければならない。

## 7. durable history record types

Phase 1 共通 record type を次で定義する。

```text
world.genesis.v1
operation.accepted.v1
operation.scheduled.v1
operation.terminal.v1
config.changed.v1
transition.committed.v1
snapshot.committed.v1
persistence.migrated.v1
```

protocol/domain 固有 record を追加できるが、recovery に必要な authoritative fact を non-durable log のみに置かない。

## 8. `WorldGenesisRecordV1`

world 作成時の durable genesis を次の意味で保持する。

```text
WorldGenesisRecordV1 {
  world_id: WorldId,
  world_seed: WorldSeed,
  initial_step: 0,
  initial_state_continuity_token: Hash256,
  simulation_config_generation: ConfigGeneration,
  simulation_config_digest: ConfigDigest,
  persistence_schema_version: PersistenceSchemaVersion,
  required_domain_set,
  required_addon_compatibility_metadata
}
```

- WorldId / WorldSeed を restart / replay で再発行しない。
- world generation input の `WORLD_REGENERATION_REQUIRED` Config を world metadata として再現可能にする。
- genesis の成立前に world を externally active としない。

## 9. Operation durable acceptance

### 9.1 `OperationAcceptedRecordV1`

Core が world-affecting Operation を **durably accepted** と扱う場合、最低限次を保存する。

```text
OperationAcceptedRecordV1 {
  operation_id: OperationId,
  operation_payload_digest: Hash256,
  batch_id: BatchId | NONE,
  normalized_immutable_operation,
  deterministic_scheduling_constraints,
  accepted_master_generation: MasterGeneration | NONE,
  accepted_config_generation: ConfigGeneration,
  terminal_state: NONE
}
```

`deterministic_scheduling_constraints` は P1-06 で candidate Step / deadline / grace 等の concrete field を確定する。

### 9.2 accepted ACK boundary

Core が world-affecting Operation について `ACCEPTED` を返す場合:

1. immutable payload digest を検証する。
2. dedup / protocol validation を行う。
3. `OperationAcceptedRecordV1` を durable にする。
4. durable completion 後にのみ `ACCEPTED` ACK を外部へ返す。

従って crash が ACK 直後に発生しても、recovery は OperationId と logical request を再構成できなければならない。

Gateway / Master の hop-local ACK はその protocol が保証する custody scope を明示する。world-authoritative `ACCEPTED` と hop receipt を同一視しない。

### 9.3 durable acceptance 前の crash

record が durable になる前に crash した request は Core accepted とみなさない。

sender は stable OperationId / immutable digest を維持して retry できる。

## 10. Operation scheduling record

Core が final authoritative `effective_step` を確定した後、その決定を recovery で再現する必要がある場合、次を durable history に保持する。

```text
OperationScheduledRecordV1 {
  operation_id,
  effective_step: SimulationStep,
  same_step_order_key,
  scheduling_result_code
}
```

- physical arrival timestamp を保存して replay ordering に使用しない。
- final effective Step が durable に確定した後、recovery で別 Step へ黙って再割当てしない。
- P1-06 で late / defer / reject の concrete scheduling state machine を追加する。

## 11. Config history persistence

P1-03 の `SimulationConfigHistoryEntry` を authoritative history として保存する。

```text
ConfigChangedRecordV1 {
  operation_id: OperationId | NONE,
  base_generation,
  next_generation,
  before_digest,
  after_digest,
  effective_step,
  normalized_changed_values
}
```

- `SIMULATION + RUNTIME_SAFE` change は transition 開始前に durable な change fact を持つ。
- replay では元の effective Step / generation を使用する。
- current Config file の値を historical replay へ silent override しない。

## 12. transition commit

### 12.1 `TransitionCommitRecordV1`

`State(S) -> State(S+1)` の authoritative finalization を次で表す。

```text
TransitionCommitRecordV1 {
  effective_step: S,
  resulting_step: S + 1,
  active_config_generation,
  active_config_digest,
  applied_operation_ids_in_order,
  operation_outcomes,
  previous_state_continuity_token,
  resulting_state_continuity_token,
  state_diagnostic_hash: Hash256 | NONE
}
```

規則:

- `resulting_step == effective_step + 1`。
- `applied_operation_ids_in_order` は P1-02 の same-Step canonical order と一致する。
- simulation-affecting Config change が Step S に有効な場合、new generation を active_config として記録する。
- `operation_outcomes` はこの transition で terminal success / world-state reject 等になった Operation を再構成できる machine-readable semantic result を保持する。
- retry count、network arrival timestamp、thread id を含めない。

### 12.2 commit before publication

TransitionCommitRecord が durable になる前に `State(S+1)` を externally authoritative state として publish しない。

commit durable 後:

- `State(S+1)` を finalized とみなす。
- Core → Gateway confirmed state publication の basis_step として使用できる。
- applied Operation の terminal `SUCCESS` / terminal world result を返せる。

### 12.3 in-memory failure before commit

State(S+1) の計算が完了していても commit 前に process failure した場合、recovery は last durable State(S) から transition S を再計算できる。

外部へ finalized publication / terminal success を返していないため、observable rollback としない。

## 13. terminal Operation result durability

### 13.1 applied Operation

world transition に参加した Operation の terminal result は `TransitionCommitRecordV1.operation_outcomes` に durable に含める。

- terminal success を record durability 前に返さない。
- crash 後 duplicate retry を受けた場合、retention 範囲内では同じ terminal semantic result を再構成する。

### 13.2 non-applied terminal result

world-state mutationなしで terminal reject / no-change となる Operation は次で保持できる。

```text
OperationTerminalRecordV1 {
  operation_id,
  operation_payload_digest,
  status,
  result_code,
  effective_step: SimulationStep | NONE,
  result_details_required_for_dedup
}
```

Core がこの terminal result を End-to-End final として返す場合、record を先に durable にする。

### 13.3 pre-authoritative reject

authentication failure、malformed envelope 等、Core authoritative Operation acceptance 境界へ到達していない request は world persistence history へ必ずしも保存しない。

監査要件がある場合は別 audit persistence の対象とできる。

## 14. state continuity token

state publication / resync で process-local sequence に依存せず causal continuity を識別するため、次を定義する。

```text
StateContinuityToken := Hash256
```

### 14.1 genesis token

```text
Token(0) = DomainHash(
  "mv.state-continuity.v1",
  {
    world_id,
    step: 0,
    genesis_record_digest
  }
)
```

### 14.2 transition token

```text
Token(S+1) = DomainHash(
  "mv.state-continuity.v1",
  {
    world_id,
    step: S+1,
    previous_token: Token(S),
    transition_commit_record_digest
  }
)
```

- same committed causal history から同じ token を得る。
- process restart / Gateway change で token を再採番しない。
- token は world-state cryptographic equality proof ではない。
- implementation divergence 検出には P1-02 の state diagnostic hash を使用する。
- token の大小を ordering / priority に使用しない。

### 14.3 protocol use

Core → Gateway confirmed state / delta は protocol schema が必要とする場合、次を持てる。

```text
basis_step
state_continuity_token
base_state_continuity_token | NONE
```

- delta の base token が receiver の保持 token と一致しなければ blind apply せず resync する。
- Gateway → View でも confirmed publication continuity の識別に同じ Core-derived token を伝播できる。
- Gateway が独自に authoritative-looking token を生成しない。

## 15. RecoveryState

Snapshot は public World State だけではなく、同一因果系列を継続するための authoritative recovery state を保持する。

```text
RecoveryStateV1 {
  world_state,
  simulation_step,
  world_seed,
  active_step_rate_and_history_cursor,
  active_simulation_config,
  config_generation,
  config_digest,
  enabled_domain_set_and_dependency_state,
  deterministic_scheduler_state,
  pending_accepted_operations,
  retained_operation_dedup_terminal_state,
  entity_identity_state_required_for_continuation,
  current_master_generation,
  state_continuity_token
}
```

### 15.1 deterministic scheduler state

将来発火する内部event / delayed action が現在Stateだけから再導出できない場合、その scheduler state を RecoveryState に含める。

- process-local timer を authoritative scheduler state の代替にしない。
- wall-clock timer queue を simulation future event の正本にしない。

### 15.2 random state

P1-02 の world random は stateless RandomContext ベースのため、共有PRNG cursor / mutable random state を Snapshotへ保存する必要はない。

ただし domain が別の mutable random state を simulation-affecting state として導入することは標準契約に反する。

## 16. Snapshot manifest

### 16.1 `SnapshotManifestV1`

```text
SnapshotManifestV1 {
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
  required_addon_compatibility_metadata,
  sections: [SnapshotSectionDescriptor...],
  snapshot_digest
}
```

`SnapshotSectionDescriptor` は logical section identity、logical content digest、required/optional 等を識別可能にする。

### 16.2 `SnapshotId`

```text
SnapshotId := Trunc128(DomainHash(
  "mv.snapshot-id.v1",
  {
    world_id,
    snapshot_step,
    history_anchor_sequence,
    history_anchor_digest,
    state_continuity_token
  }
))
```

ZERO が生成された場合は deterministic nonce を加えて再導出する。

SnapshotId の大小を snapshot priority として使用しない。

### 16.3 `SnapshotDigest`

```text
SnapshotDigest = DomainHash(
  "mv.snapshot.v1",
  normalized_snapshot_manifest_without_snapshot_digest
)
```

各 section content は自身の digest で検証し、manifest digest が section digest 群を bind する。

compression / encryption / physical chunking を変更しても logical content digest の意味を変えない。

## 17. Snapshot consistency cut

Snapshot は `(SnapshotStep = S, HistoryAnchor = H)` の組として consistent cut を持つ。

cut 時点で:

- authoritative world は完全な `State(S)` boundary にある。
- history sequence `<= H` の durable fact が RecoveryState に反映済み、または snapshot 内の pending/dedup stateとして継続可能である。
- history `H+1` 以降は Snapshot load 後に replay する continuation とする。
- snapshot 内 state と history anchor の間に causal gap を作らない。

### 17.1 running snapshot

原則として simulation を進行しながら保存できる。

1. Step boundary で short consistency barrier を取る。
2. `State(S)` と RecoveryState metadata の immutable view / copy-on-write root を freeze する。
3. history anchor H を固定する。
4. barrier を解放し simulation を継続する。
5. frozen view を background I/O で保存する。
6. 全 section / manifest 検証後に snapshot commit を公開する。

background write の完了順を world outcome に使用しない。

### 17.2 stop-the-world fallback

安全な immutable view を構成できない、または memory / I/O pressure が Config threshold を超える場合、完全な Step boundary で simulation を一時停止して Snapshot を作成してよい。

- transition途中では停止保存しない。
- Pauseの有無によって World State結果を変えない。
- 保存完了後に同じ次Stepから継続する。

## 18. Snapshot commit protocol

physical implementation に関係なく次の意味を満たす。

1. snapshot section を staging 状態へ書く。
2. section digest を検証する。
3. manifest を生成する。
4. manifest / snapshot digest を検証する。
5. committed snapshot として atomic に discoverable にする。
6. commit 完了後にのみ recovery candidate として列挙可能にする。

staging / partial snapshot を recovery candidate として扱わない。

`SnapshotCommittedRecordV1` は少なくとも snapshot_id、snapshot_step、history anchor、snapshot_digest を durable historyへ記録可能にする。

## 19. recovery checkpoint

committed Snapshot は次の recovery checkpoint を与える。

```text
RecoveryCheckpointV1 {
  world_id,
  snapshot_id,
  snapshot_step,
  history_anchor_sequence,
  history_anchor_digest,
  state_continuity_token,
  simulation_config_generation,
  simulation_config_digest
}
```

checkpoint は「このSnapshotを読み込み、history `H+1` から継続すれば同じ causal line を再構成できる」という契約である。

## 20. crash recovery algorithm

標準 recovery は次で行う。

1. committed snapshot candidate を列挙する。
2. world_id / persistence version / required addon / Config compatibility を検証する。
3. manifest / section digest を検証する。
4. newest usable consistent Snapshot を選択する。
5. `RecoveryState(S)` を load する。
6. snapshot の history anchor digest と durable history chain を接続確認する。
7. `H+1` から complete / valid record を sequence順に読む。
8. Operation acceptance / scheduling / Config change を再構成する。
9. transition commit ごとに通常と同じ deterministic execution rule で Stateを再計算する。
10. recorded applied Operation order / Config generation / continuity token と一致することを確認する。
11. state diagnostic hash が存在する checkpoint では再計算値と比較する。
12. last valid durable finalized state まで到達する。
13. pending accepted Operation / dedup state を再構成する。
14. protocol connectionを再確立し、normal publication前に必要な resync を行う。

recovery中に wall clock elapsed time を simulation Stepへ自動加算しない。

## 21. torn tail と corruption

### 21.1 incomplete uncommitted tail

process crash により最後の physical record が partial / torn で、かつ durable completionが成立していないことを storage layer が判定できる場合:

- その incomplete tail を無視 / truncate してよい。
- durable complete record より前へ巻き戻さない。
- durable ACK を返した Operation record を torn tail と誤認して消してはならない。

### 21.2 committed region corruption

既に durable とされた record / Snapshot section の hash mismatch、history gap、unexpected sequence 等を検出した場合:

- corrupted item を部分的に読み飛ばして後続historyを通常適用しない。
- redundant copy / replica / earlier snapshot + intact history 等の別経路から同じ durable facts を復元できる場合は利用できる。
- durable accepted Operation または finalized state を失うことになる復旧しかできない場合、silent data-loss startup を行わず world startup を拒否する。

### 21.3 latest Snapshot corruption

latest Snapshotのみが破損していても、その Snapshot作成前後の durable history が intact で、以前の正常Snapshotから同じ latest finalized stateまで再構成できる場合は fallback可能とする。

単に「古いSnapshotが読めた」だけを理由に、後続の durable accepted / finalized history を捨てて起動しない。

## 22. replay modes

### 22.1 recovery replay

crash recovery のため latest durable finalized stateまで進める replay。

- external new input を取り込まない。
- history に保存された Operation / Config change / scheduling decision を使用する。
- replay完了後に normal network input を再開する。

### 22.2 historical replay

監査・デバッグ・歴史閲覧等で target `State(T)` まで再計算する。

- source Snapshot `S <= T` を選ぶ。
- transition `S ... T-1` を replay する。
- original wall-clock speed を再現する必要はない。
- historical replay result を current authoritative worldへ自動commitしない。

### 22.3 deterministic verification replay

同一 checkpoint / history を複数回再生し、state diagnostic hash 等で一致を検証できる。

高度な divergence localization は Phase 1標準要件外とする。

## 23. replay input boundary

replay の world outcome input は最低限次を再現する。

- WorldId / WorldSeed
- source State / RecoveryState
- SimulationStep
- StepRate history
- simulation-affecting Config generation / digest / history
- enabled domain set / dependency declarations
- accepted authoritative Operation logical payload
- final effective Step / same-Step ordering fact
- world-affecting Admin Operation
- state内から再導出できない authoritative scheduled event state

次は replay input としない。

- original wall clock timestamp
- original network latency
- original thread scheduling
- original Gateway count
- original Master identity
- original retry count
- log output timing
- Snapshot I/O timing

## 24. recovery と Master generation

Snapshot / history は last known `MasterGeneration` を保持する。

Core recovery直後は pre-crash connection / Master authority を無条件に再信頼しない。

- protocol handshake / current authority を再確立する。
- stale old-generation output を currentとして受理しない。
- recovery後のMaster選出・generation遷移は Core authority の protocol規則に従う。
- exact failover/reassignment state machine は P1-06 で確定する。

## 25. publication after recovery

normal state publication を再開する前に:

1. RecoveryState / history replayを last durable finalized stateまで完了する。
2. current `basis_step` と `StateContinuityToken` を確定する。
3. Gateway connection / protocol negotiationを成立させる。
4. receiverの保持 continuity token と比較する。
5. continuityが証明できない場合 full resync / protocol-defined rebuildを行う。
6. resync完了前に inconsistent delta chainをnormal confirmed stateとして公開しない。

Gateway cacheの内容が recovery result と異なる場合、Core側を正本とする。

## 26. history compaction

Snapshot作成後も historyを即時削除してよいとは限らない。

history segmentを削除 / compactできるのは、その削除後も少なくとも次を満たす場合のみとする。

- configured replay guarantee を満たせる。
- latest required recovery checkpointからcontinuationできる。
- pending accepted Operationを失わない。
- P1-06で定義する dedup retention 中の Operation identity / terminal resultを失わない。
- Config history / world migration / audit requirementを壊さない。
- required state continuityを検証できる。

exact retention duration / history floor / storage quota は Config と P1-06 で具体化する。

## 27. dedup state と Snapshot

P1-06で exact retention window を定義するまで、P1-05 は次を固定する。

- `ACCEPTED / PENDING` で terminal result未確定の OperationId は compactionで削除しない。
- retained dedup window 内の terminal OperationId / immutable digest / terminal semantic result は Snapshot または後続historyから復元可能にする。
- Snapshotを取得したこと自体を理由に OperationId historyを捨てない。
- duplicate retryにより二重world mutationを起こさない。

## 28. save / Config compatibility

P1-03に従い、saved simulation Configを continuation の正本とする。

- saved `ConfigGeneration` / `ConfigDigest` を検証する。
- current local Configとの差を impact / mutability で分類する。
- replay過去区間へcurrent Configをsilent適用しない。
- `WORLD_REGENERATION_REQUIRED` 値がsaved worldと不整合なら既存WorldIdで起動しない。
- compatible deterministic Config migrationを行う場合、migration結果を検証し履歴化する。

## 29. addon / domain compatibility

saved worldがrequired addon / domain / Capabilityを必要とする場合:

- required identity / version / semantic compatibilityを起動前に確認する。
-不足・非互換をsilent disableで回避しない。
- deterministic migrationが完全成功した場合のみ新構成で起動できる。
- migrationがworld state / Config / scheduled stateを変換する場合、それらを一つのconsistent migration resultとして検証する。

## 30. persistence migration

### 30.1 migration properties

save migration は:

- explicit source version / target version を持つ。
- deterministic transformation とする。
- source WorldId / EntityId / OperationIdの意味を理由なく再発行しない。
- unknown semantic dataをsilent discardしない。
- migration後の Snapshot / history / Config / addon dependency consistencyを全体検証する。

### 30.2 non-destructive migration

標準 migration は source save set を破壊的に上書きしてから検証する方式を禁止する。

1. source を read-only input とする。
2. target save set を staging生成する。
3. target全体を検証する。
4. successful targetをatomicにpublishする。
5. publish成功後にのみsource retention policyを適用できる。

### 30.3 failure

migrationが途中失敗した場合:

- staging targetをactive saveとして扱わない。
- source saveを維持する。
- compatibility errorをdiagnostic可能にする。
- meaningを推測してpartial startupしない。

schema incompatibilityによるmigration failureをdata corruption fallbackとして扱わない。

## 31. state diagnostic hash boundary

P1-02の `mv.state-diagnostic.v1` は Snapshot validation / deterministic replay verification に利用する。

Phase 1 P1-05では:

- committed Snapshotに full RecoveryState または authoritative world-state diagnostic hashを1つ保持可能にする。
- TransitionCommitRecordでは `state_diagnostic_hash` を optional とする。
- Configにより一定Step間隔 / verification modeで記録できる。
- hash計算自体が world outcomeを変えてはならない。

大規模world向けの slice / Merkle tree granularity は Phase 1後続またはcomponent詳細設計へ残す。

## 32. physical storage に要求する性質

Phase 1 は特定製品を固定しないが、選定する storage は少なくとも次を成立させる。

- durable write completionを判定できる。
- complete / torn recordを区別できる。
- ordered appendまたは同等のhistory sequence durabilityを実現できる。
- committed Snapshotをpartial stagingと区別できる。
- atomic publish / commit markerまたは同等機構を持つ。
- crash後にdurable prefixを回復できる。
- silent corruptionをdigest検証で検出可能にする。

local filesystemだけを前提とせず、同等 semanticsを提供するDB / object storage実装を許可する。

## 33. durability と performance

30Hz Stepごとに `TransitionCommitRecord` を durable finalized boundary とすることは論理契約である。

implementation は性能のため:

- append log
- group commit
- WAL
- batched fsync
- replicated durable log
- copy-on-write Snapshot

等を利用できる。

ただし externally finalized とした Step / terminal result / durable accepted Operationを、実装上の遅延flushによってcrash後に失うことは許可しない。

必要ならworld progressionがstorage durabilityへ追いつくまで実時間からlagしてよい。durability不足を理由にStep skipや非durable publishを行わない。

## 34. failure handling

persistence subsystemがdurable writeを継続できない場合:

- 新しい authoritative finalized Stepを増やし続けない。
- durable acceptanceを保証できないworld-affecting Operationへ`ACCEPTED`を返さない。
- existing last durable finalized stateを保持する。
- component health / Admin Viewへpersistence failureを診断可能にする。
- Configで定義された安全動作に従いsimulationをpause / safe stopできる。

storage回復後のresumeはlast durable boundaryとin-memory stateの一致を検証してから行う。

## 35. 禁止事項

- transition途中のSnapshot
- Snapshot write completion orderをworld orderingに使うこと
- durable acceptance前のauthoritative `ACCEPTED` ACK
- transition commit前のauthoritative State publication
- transition commit前のapplied terminal success
- acknowledged durable Operationをcrash recoveryでsilent dropすること
- hash mismatchしたSnapshot sectionだけ無視してpartial loadすること
- history gapを飛ばして後続recordをnormal applyすること
- schema incompatibilityをcorruption扱いして古いworldへsilent fallbackすること
- current local simulation Configをhistorical replayへsilent injectすること
- Gateway cacheをCore recovery sourceにすること
- save/recoveryを理由にWorldId / EntityId / OperationIdを再発行すること

## 36. P1-06 への引き渡し

P1-05完了時点で次はP1-06へ引き渡す。

- candidate Step / deadline / grace concrete field
- late Operationのdefer / reject rule
- Pause queueのassignment rule
- accepted Operationのscheduling state machine
- Master failover時のunfinished Batch / Operation custody
- exact dedup retention window / terminal result expiry
- duplicate retryでoriginal terminal detailがretention外の場合のresponse
- Batch partial completion / retry state machine

P1-05により、これらのstateをどのdurability boundaryで保持・復元するかは固定済みとする。

## 37. 未決定事項

P1-05完了後も、Phase 1横断契約として残る未決定事項は次の通り。

- exact dedup retention window
- candidate Step / deadline / grace field
- Pause queue / late Operation concrete semantics
- Batch ACK / partial completion / retry state machine
- Master failover custodyのexact state machine
- large-world state diagnostic hashのslice/tree granularity
- physical storage product / concrete binary serialization / compression / encryption

physical storage製品等はPhase 1横断契約を満たす範囲でcomponent implementation設計へ委ねる。
