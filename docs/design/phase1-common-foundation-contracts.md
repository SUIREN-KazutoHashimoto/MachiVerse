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

P1-01 と P1-02 を完了し、現在の次作業は P1-03 Config 詳細契約とする。

## 2. 設計原則

- Authoritative World Time は wall clock ではなく Simulation Step で表現する。
- network arrival race、thread completion order、wall clock を world outcome の決定要因にしない。
- wire 上の識別子は固定幅・opaque とし、文字列表現は表示・ログ用途に限定する。
- 再送・failover・reconnect で同一論理 Operation の識別子を変更しない。
- save / replay / recovery を跨いで Entity identity と適用済み Operation identity を維持する。
- protocol の意味契約は `docs/protocols` を正本とし、本書はその共通型・共通意味論を定義する。
- deterministic encoding / hash / random の具体契約は `docs/design/phase1-determinism-ordering-random.md` を正本とする。

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

## 6. Pause / resume に対する時間契約

- Pause 開始時に current SimulationStep を固定する。
- Pause 中に受信した simulation-affecting Operation を停止 Step へ即時適用しない。
- resume 後、Core が protocol 規則に従い future valid Step を割り当てる。
- Pause 時間の wall-clock 長さは replay 条件に含めない。

## 7. persistence / replay への最低保存項目

Phase 1 の persistence 詳細化に先立ち、snapshot/replay が最低限保持する共通項目を固定する。

- WorldId
- WorldSeed
- SimulationStep
- StepRate history と current rate_generation
- current MasterGeneration
- EntityId を含む authoritative entity state
- accepted/applied OperationId の再現に必要な履歴
- simulation-affecting Config history
- enabled domain set / dependency declaration

具体的 snapshot boundary、dedup retention、history compaction は後続作業で決定する。

## 8. Phase 1 作業分解

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

状態: 次に着手する。

- schema version
- simulation-affecting / operational / presentation 分類
- startup-only / runtime-safe / world-regeneration-required
- atomic apply boundary
- history / migration
- dependency declaration の Config 上の位置付け

### P1-04 Protocol 共通 envelope

- message envelope
- protocol version / Capability negotiation
- result / error
- correlation / causation
- generation / Step fields

### P1-05 persistence / replay / recovery

- consistent snapshot boundary
- operation/event history boundary
- recovery checkpoint
- dedup retention
- migration failure semantics

### P1-06 pause / late / retry / dedup 共通意味論

- candidate/effective Step
- deadline / grace / defer / reject
- retry
- stale generation
- duplicate handling

### P1-07 横断整合性レビュー

- `docs/architecture` との矛盾確認
- `docs/protocols` へ確定契約を反映
- Phase 2〜4 の blocker 0 件確認

## 9. 未決定事項

P1-02 完了時点の未決定事項は次の通り。

- Config schema / version / classification / migration
- common protocol envelope
- protocol field ごとの immutable payload digest inclusion/exclusion
- snapshot/replay consistency boundary
- authoritative state diagnostic hash の slice/tree granularity
- dedup retention window
- Pause queue / late Operation の具体規則

これらは Phase 1 内の後続作業で解消し、Phase 1 完了時には横断 blocker を 0 件とする。
