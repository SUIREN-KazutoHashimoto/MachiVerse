# 詳細設計 Phase 1: 共通基盤・契約

Status: Draft / Phase 1 in progress  
Tracking: Issue #13  
Source of truth: `docs/requirements` / `docs/architecture` / `docs/protocols`

## 1. 目的

本書は、MachiVerse 全体の詳細設計に先立ち、全コンポーネントと全シミュレーション領域が共有する共通契約を具体化する。

Phase 1 では次を確定対象とする。

1. Simulation Step / World Time
2. Entity ID / Operation ID / Batch ID / Master generation
3. 決定論的順序・競合・乱数 context
4. Config schema・分類・適用境界・履歴
5. Protocol 共通 message envelope / version / Capability / error-result
6. Snapshot / replay / recovery の一貫性境界
7. Pause / resume / late Operation / retry / dedup の共通意味論

P1-01〜P1-05 を完了し、現在の次作業は P1-06 pause / late / retry / dedup 共通意味論とする。

## 2. 設計原則

- Authoritative World Time は wall clock ではなく Simulation Step で表現する。
- network arrival race、thread completion order、wall clock を world outcome の決定要因にしない。
- wire 上の識別子は固定幅・opaque とし、文字列表現は表示・ログ用途に限定する。
- 再送・failover・reconnect で同一論理 Operation の識別子を変更しない。
- save / replay / recovery を跨いで Entity identity と適用済み Operation identity を維持する。
- protocol の意味契約は `docs/protocols` を正本とし、本書はその共通型・共通意味論を定義する。
- deterministic encoding / hash / random の具体契約は `docs/design/phase1-determinism-ordering-random.md` を正本とする。
- Config schema / classification / apply / history の具体契約は `docs/design/phase1-config-contract.md` を正本とする。
- Protocol common envelope / compatibility / result semantics の具体契約は `docs/design/phase1-protocol-envelope.md` を正本とする。
- persistence / replay / recovery の具体契約は `docs/design/phase1-persistence-replay-recovery.md` を正本とする。

## 3. Simulation Step / World Time

### 3.1 `SimulationStep`

`SimulationStep` は符号なし 64 bit 整数とする。

```text
SimulationStep := uint64
```

契約:

- epoch は world の authoritative simulation 開始直前を `0` とする。
- world 初期状態は `step = 0` に対応する。
- 1 回の authoritative state transition が完了するごとに 1 増加する。
- Pause 中は増加しない。
- overrun 時も step skip を行わない。
- 値の wrap-around を禁止する。
- `UINT64_MAX` 到達前に world を安全停止し、新規 step の開始を拒否する。
- wire / persistence では unsigned 64 bit integer として保持し、浮動小数へ変換して保存しない。

### 3.2 `StepRate`

Simulation Step と simulation elapsed time の対応は有理数で保持する。

```text
StepRate {
  numerator: uint32,   // steps
  denominator: uint32 // seconds
}
```

意味は `numerator / denominator` steps per second とする。

契約:

- `numerator > 0` かつ `denominator > 0`。
- 値は最大公約数で約分した canonical form で保持する。
- 標準値 30Hz は `{ numerator: 30, denominator: 1 }`。
- step から elapsed seconds への変換は `step * denominator / numerator` という有理数計算を意味上の正本とする。
- 日時・表示時刻へ変換する境界までは不必要な floating point 丸めを導入しない。
- simulation-affecting StepRate の runtime change は Config change event として明示的 safe Step に適用し、履歴へ記録する。

### 3.3 `WorldTime`

共通契約上の World Time は次の組で表す。

```text
WorldTime {
  step: SimulationStep,
  rate_generation: uint32
}
```

`rate_generation` は StepRate 履歴の世代番号であり、同一 world 内で 0 から単調増加する。

- `rate_generation` そのものは時間順序の tie-break に使わない。
- StepRate 変更後も `SimulationStep` は連続して単調増加する。
- replay 時は対象 step に有効だった `rate_generation` から StepRate 履歴を復元する。

### 3.4 wall clock との境界

- wall clock は運用監視、ログ時刻、UI表示補助には使用できる。
- authoritative apply Step、same-Step ordering、乱数 context、Entity ID生成の因果順序を wall clock から決めない。
- wall clock と World Time の相互変換値は非権威な観測値として扱う。

## 4. 共通識別子

### 4.1 wire representation

次の識別子は wire / persistence 上で 128 bit opaque value とする。

```text
EntityId    := 128-bit opaque value
OperationId := 128-bit opaque value
BatchId     := 128-bit opaque value
WorldId     := 128-bit opaque value
```

共通規則:

- binary wire order は network byte order とする。
- human-readable canonical form は 32 桁 lowercase hexadecimal とし、区切り文字を持たない。
- 大文字・ハイフン付き等を入力で許容する場合も、正規化後の値比較は 128 bit binary value で行う。
- 0 値は invalid / unassigned とし、永続オブジェクトへ割り当てない。
- 識別子の辞書順を domain 上の優先度として解釈しない。

### 4.2 `EntityId`

EntityId は world 内で永続かつ一意であり、save / restart / replay を跨いで不変とする。

生成は deterministic creation context から行う。Phase 1 の共通入力を次で固定する。

```text
EntityCreationContext {
  world_id: WorldId,
  creation_step: SimulationStep,
  creator_domain: utf8-string,
  creator_entity_id: EntityId | ZERO,
  creation_kind: utf8-string,
  local_ordinal: uint64
}
```

規則:

- `creator_domain` と `creation_kind` は protocol / subsystem ごとに固定された stable token を使用する。
- `local_ordinal` は同一 creator context 内の deterministic ordinal であり、thread completion order から採番しない。
- 同一 logical creation event は replay でも同一 context を生成しなければならない。
- concrete derivation は `MV-DCBOR-v1` で context を deterministic encodeし、domain-separated SHA-256 の先頭 128 bitを利用する。
- reserved ZERO が生成された場合のみ deterministic nonce を増加させて再導出する。
- 異なる creation context 間の true 128 bit collision を検出した場合、runtime creation order 依存の再採番をせず fatal invariant violation とする。

詳細は `docs/design/phase1-determinism-ordering-random.md` を参照する。

### 4.3 `OperationId`

OperationId は一つの論理 Operation の End-to-End identity とする。

- origin で 1 回だけ発行する。
- Gateway hop、Master 切替、retry、reconnect、ACK loss で変更しない。
- payload を変更して同じ OperationId を再利用することを禁止する。
- receiver は同じ OperationId で異なる immutable payload digest を検出した場合、protocol violation として拒否する。
- immutable payload digest algorithm は domain-separated SHA-256 とする。
- dedup key の主キーは OperationId とする。
- OperationId の生成方式は origin component の責務だが、128 bit の一意性契約を満たすことを必須とする。

### 4.4 `BatchId`

BatchId は一つの論理 batch の identity とする。

- retry では同一 BatchId を維持する。
- batch 内容を変更した場合は新規 BatchId を発行する。
- BatchId 単独では world outcome の ordering key としない。
- batch 内各 Operation は個別の OperationId を必須とする。

### 4.5 `MasterGeneration`

Master generation は Core が権威を持つ符号なし 64 bit 整数とする。

```text
MasterGeneration := uint64
```

- world 起動時の初期 generation は `1`。
- Master の authoritative reassignment ごとに 1 増加する。
- `0` は no-master / not-assigned の sentinel としてのみ利用可能。
- stale generation の Master output は Core が拒否する。
- generation は Master identity と独立している。
- wrap-around を禁止し、上限到達前に安全停止する。

## 5. same-Step ordering 契約

`SimulationStep` が第一の authoritative time coordinate であり、`effective_step = S` の simulation-affecting input は `State(S)` から `State(S+1)` を生成する transition に参加する。

same-Step の canonical total order は次の tuple で固定する。

```text
SameStepOrderKey = (
  phase,
  domain_rank,
  conflict_scope_digest,
  semantic_priority,
  intent_id
)
```

- `phase` は control / external_input / scheduled_internal / derived_internal / system_internal / finalization の固定列挙。
- `domain_rank` は dependency DAG から deterministic topological sort で決定する。
- 同順位 domain は stable DomainToken の ASCII bytewise ascending で決定する。
- `conflict_scope_digest` は domain-separated SHA-256。
- `semantic_priority` は domain schema が固定し、default 0。
- `intent_id` は deterministic source context から導出した 128 bit identity で、最後の total-order tie-breaker とする。

次を ordering key の暗黙入力にしない。

- physical arrival timestamp
- thread ID / completion order
- process-local iteration order
- Gateway数
- Master identity
- retry count

OperationId / BatchId / EntityId の大小自体を business priority として解釈しない。

詳細は `docs/design/phase1-determinism-ordering-random.md` を参照する。

## 6. Config 共通契約

Config の詳細は `docs/design/phase1-config-contract.md` を正本とする。

Phase 1 共通契約として次を固定する。

- operator-editable Config は component-owned UTF-8 TOML 1.0 document とする。
- schema version は `major.minor`。
- field は `SIMULATION / OPERATIONAL / PRESENTATION` の impact と、`RUNTIME_SAFE / RESTART_REQUIRED / WORLD_REGENERATION_REQUIRED` の mutability を持つ。
- compatible old Config は deterministic migration と schema default completion を行い、補完後 Config を atomic write-back する。
- unknown field、future unsupported schema、不整合、write-back failure は fail-fast とする。
- `ConfigGeneration := uint64` で atomic revision を識別する。
- EffectiveConfig は `MV-DCBOR-v1` と domain-separated SHA-256 (`mv.config.v1`) で `ConfigDigest` を持つ。
- runtime file edit / filesystem event をそのまま effective Config にしない。runtime activation は explicit Config change Operation とする。
- runtime change は stable `OperationId`、expected base generation、atomic change set を持つ。
- `SIMULATION + RUNTIME_SAFE` change は explicit `effective_step = S` を持ち、`State(S) -> State(S+1)` transition開始前に全体を切り替える。
- saved world の simulation Config/history を restore continuation の正本とし、current local file の差を過去へ silent override しない。
- Config file 自体を component boundary 越しに共有しない。

## 7. Protocol 共通契約

Protocol envelope / compatibility の詳細は `docs/design/phase1-protocol-envelope.md` を正本とする。

Phase 1 共通契約として次を固定する。

- 標準4境界は `mv.core-gateway` / `mv.gateway-gateway` / `mv.gateway-view` / `mv.gateway-admin-view` の ProtocolId を持つ。
- normal message は `ProtocolEnvelopeV1` の共通意味を持つ。
- protocol version は `uint16 major + uint16 minor`。
- handshake は双方のsupported rangeから共通Majorの最大値、続いて共通Minor範囲の最大値を選ぶ。
- CapabilityId は StableToken とし、required / provided set を相互検証する。
- connection中のCapability changeはreconnectを基本とし、safe live renegotiationは明示Capabilityとbarrierがある場合だけ許可する。
- `NegotiationGeneration := uint32` でnegotiated semanticsの世代を識別する。
- MessageId / CorrelationId / CausationId / ComponentInstanceId は128-bit operational identityであり、world outcomeへ使用しない。
- `WorldContextV1` で `world_id / basis_step / effective_step / master_generation / config_generation` を明示する。
- `effective_step` はCore確定済みauthoritative Stepだけに使用し、candidate Stepとは分離する。
- `OperationContextV1` でOperationId / immutable payload digest / BatchIdをtransport identityから分離する。
- immutable Operation digestへMessageId、CorrelationId、BatchId、MasterGeneration、retry/routing metadata、candidate/final Stepを含めない。
- common result status、stable error/result code、RetryAdviceを定義する。
- ACKはprotocol hopの受理状態であり、authoritative world mutationのterminal successと同一視しない。
- standard protocolのaddon情報はcompatibility metadataに限定し、addon functional payload用generic slotを設けない。

## 8. persistence / replay / recovery 共通契約

persistence の詳細は `docs/design/phase1-persistence-replay-recovery.md` を正本とする。

Phase 1 共通契約として次を固定する。

- Snapshot は完全な `State(S)` boundary を表し、transition途中状態を保存しない。
- `State(S+1)` は transition S の durable `TransitionCommitRecordV1` 後に externally finalized / publishable とする。
- `HistorySequence := uint64` の append-only durable history と SHA-256 hash chain で persistence continuity を追跡する。
- world-affecting Operation の Core `ACCEPTED` は `OperationAcceptedRecordV1` の durability 後にのみ返す。
- applied Operation の terminal result は対応 transition commit の durability 前に返さない。
- Snapshot は `(snapshot_step=S, history_anchor=H)` の consistent cut を持ち、RecoveryState と `H+1` 以降のhistoryから継続する。
- `RecoveryStateV1` は public World Stateだけでなく pending accepted Operation、dedup state、deterministic scheduler state、Config、StepRate、domain metadata 等の継続状態を含む。
- `StateContinuityToken` で process restart を跨いだ state publication / delta continuity を識別する。
- committed Snapshot は staging / partial Snapshot と区別し、manifest / section digest 検証後にのみ recovery candidate とする。
- torn uncommitted tail は安全に切り捨て可能だが、committed region corruptionや acknowledged durable Operationの欠落をsilent ignoreしない。
- recovery は latest usable Snapshot + contiguous valid history を replayし、last durable finalized stateまで復元する。
- persistence migrationはdeterministic / non-destructiveとし、target全体検証後にのみpublishする。
- history compactionは pending Operation、dedup retention、Config/replay guaranteeを失わない場合のみ許可する。

## 9. Pause / resume に対する時間契約

- Pause 開始時に current SimulationStep を固定する。
- Pause 中に受信した simulation-affecting Operation を停止 Step へ即時適用しない。
- resume 後、Core が protocol 規則に従い future valid Step を割り当てる。
- Pause 時間の wall-clock 長さは replay 条件に含めない。

## 10. persistence / replay への最低保存項目

Snapshot / history が最低限保持または再構成可能にする項目を次とする。

- WorldId
- WorldSeed
- SimulationStep / finalized Step frontier
- StepRate history と current rate_generation
- current MasterGeneration
- EntityId を含む authoritative entity state
- deterministic scheduler / future event state
- accepted / scheduled / applied Operation identity と immutable payload
- retained terminal result / dedup state
- simulation-affecting Config generation / digest / history
- enabled domain set / dependency declaration
- HistorySequence / history anchor digest
- StateContinuityToken
- required addon / migration compatibility metadata

exact dedup retention window と history floor は P1-06 で確定する。

## 11. Phase 1 作業分解

### P1-01 共通時間・識別子

状態: 完了。

- SimulationStep / StepRate / WorldTime
- WorldId / EntityId / OperationId / BatchId
- MasterGeneration

### P1-02 決定論的順序・競合・乱数 context

状態: 完了。

正本: `docs/design/phase1-determinism-ordering-random.md`

- `MV-DCBOR-v1` deterministic semantic encoding
- SHA-256 common hash suite / domain separation
- same-Step ordering tuple
- deterministic dependency topological order
- conflict scope / deterministic merge modes
- internal EventId / IntentId
- EntityId derivation algorithm
- stateless RandomContextV1 / RandomWord64
- rejection sampling / uniform binary64 mapping
- immutable Operation payload digest algorithm
- deterministic state diagnostic hash algorithm

### P1-03 Config 詳細契約

状態: 完了。

正本: `docs/design/phase1-config-contract.md`

- TOML 1.0 Config document / component ownership
- `ConfigSchemaVersion` major.minor / deterministic migration
- default completion / atomic write-back
- SIMULATION / OPERATIONAL / PRESENTATION classification
- RUNTIME_SAFE / RESTART_REQUIRED / WORLD_REGENERATION_REQUIRED classification
- `ConfigGeneration` / `ConfigDigest`
- atomic runtime ConfigChangeSet / optimistic base generation check
- simulation effective Step boundary
- Config history / save / replay / restore contract
- cross-component effective information distribution boundary

### P1-04 Protocol 共通 envelope

状態: 完了。

正本: `docs/design/phase1-protocol-envelope.md`

- `ProtocolEnvelopeV1`
- ProtocolId / MessageType / ProtocolVersion
- MessageId / CorrelationId / CausationId / ComponentInstanceId
- WorldContextV1 / OperationContextV1
- NegotiationGeneration
- deterministic version selection handshake
- required / provided Capability negotiation
- addon compatibility metadata boundary
- immutable Operation payload digest inclusion/exclusion
- common result / error / retry taxonomy
- ACK / terminal result separation

### P1-05 persistence / replay / recovery

状態: 完了。

正本: `docs/design/phase1-persistence-replay-recovery.md`

- `State(S)` consistent Snapshot boundary
- `HistorySequence` / durable history hash chain
- durable Operation acceptance / terminal result boundary
- `TransitionCommitRecordV1` / finalized Step frontier
- `StateContinuityToken`
- `RecoveryStateV1` / `SnapshotManifestV1`
- `(SnapshotStep, HistoryAnchor)` consistent cut
- running Snapshot / stop-the-world fallback
- recovery checkpoint / deterministic replay algorithm
- torn tail / committed corruption distinction
- history compaction safety conditions
- deterministic non-destructive persistence migration

### P1-06 pause / late / retry / dedup 共通意味論

状態: 次に着手する。

- candidate/effective Step
- deadline / grace / defer / reject
- Pause queue assignment
- retry
- stale generation
- duplicate handling / dedup retention
- Batch partial completion / retry state machine
- Master failover custody

### P1-07 横断整合性レビュー

- `docs/architecture` との矛盾確認
- `docs/protocols` へ確定契約を反映
- Phase 2〜4 の blocker 0 件確認

## 12. 未決定事項

P1-05 完了時点の未決定事項は次の通り。

- exact dedup retention window
- candidate Step / deadline / grace concrete field
- Pause queue / late Operation の具体規則
- Batch ACK / partial completion / retry state machine
- Master failover custody の exact state machine
- authoritative state diagnostic hash の large-world slice/tree granularity
- physical storage product / concrete binary serialization / compression / encryption

これらは Phase 1 内の後続作業または component implementation 詳細で解消し、Phase 1 完了時には横断 blocker を 0 件とする。
