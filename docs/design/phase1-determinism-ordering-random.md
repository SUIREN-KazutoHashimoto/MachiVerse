# 詳細設計 Phase 1: 決定論的順序・競合・乱数契約

Status: Draft / P1-02 complete  
Tracking: Issue #13  
Parent: `docs/design/phase1-common-foundation-contracts.md`

## 1. 目的

本書は Phase 1 の P1-02 として、次の横断契約を具体化する。

- same-Step の安定した全順序
- subsystem 間 dependency の決定論的解決
- world mutation conflict の識別と解決
- deterministic random context と乱数生成
- `EntityId` / internal event identity の導出
- 同一 `OperationId` 再送時の immutable payload digest
- 決定論違反診断で用いる hash suite

本書の規則は CPU、OS、thread 数、task scheduler、Gateway 数、Master identity、network arrival timing に依存してはならない。

## 2. 用語

### 2.1 `WorldSeed`

WorldSeed は 256 bit opaque value とする。

```text
WorldSeed := 256-bit opaque value
```

- binary representation は 32 octets。
- human-readable canonical form は 64 桁 lowercase hexadecimal。
- `00...00` を含む全 256 bit 値を有効な seed とする。
- WorldSeed は秘密情報とはみなさない。
- 同一 world の replay / recovery では同一 WorldSeed を維持する。

### 2.2 stable token

決定論的 context へ含める subsystem、purpose、kind、field 等の token は次を満たす。

```text
StableToken := ASCII string
pattern     := [a-z0-9][a-z0-9._/-]{0,63}
```

- lowercase ASCII のみを使用する。
- Unicode 表記揺れを決定論的 key へ持ち込まない。
- 一度永続化または公開した token の意味を変更しない。
- rename が必要な場合は新 token と migration を定義する。
- user-facing label を StableToken として使用しない。

## 3. Deterministic encoding profile

ID、hash、random、ordering 補助 key の意味エンコードには RFC 8949 の deterministic CBOR を基礎とする `MachiVerse Deterministic CBOR v1`（以下 `MV-DCBOR-v1`）を使用する。

### 3.1 共通規則

`MV-DCBOR-v1` は次を必須とする。

- definite-length item のみを使用する。
- integer / length / tag は shortest preferred serialization を使用する。
- map key は deterministic encoding の bytewise lexicographic order で並べる。
- schema 上の field key は原則として unsigned integer を使用する。
- schema が integer と floating point を同義として扱うことを禁止する。
- ID は text 化せず byte string で encode する。
- `SimulationStep` 等の unsigned integer は CBOR unsigned integer として encode する。
- StableToken は ASCII text string として encode する。
- unordered collection を array として encode する場合、要素を schema 指定の stable key で先に sort する。

### 3.2 floating point

ordering key、random context、ID derivation context には floating point を含めてはならない。

Operation payload 等、domain schema が floating point を許可する場合は次を要求する。

- finite value を標準とする。
- NaN / Infinity を許可する field は schema 側で明示する。
- `-0.0` と `+0.0` を同義とする field は digest 前に `+0.0` へ normalize する。
- NaN payload bit pattern に意味を持たせない。
- deterministic CBOR の preferred floating-point serialization を用いる。

protocol wire serialization 自体を CBOR に固定するものではない。`MV-DCBOR-v1` は意味 digest / derivation 用の canonical representation であり、wire format は P1-04 で別途定義できる。

## 4. Hash suite

Phase 1 共通 hash suite v1 を次で固定する。

```text
Hash256(data) = SHA-256(data)
Trunc128(h)   = h[0..15]
```

### 4.1 domain separation

異なる用途で同一 preimage 空間を共有しない。

```text
DomainHash(label, value) =
  SHA-256(ASCII(label) || 0x00 || MV-DCBOR-v1(value))
```

標準 label:

| 用途 | label |
|---|---|
| Entity ID | `mv.entity.v1` |
| internal Event ID | `mv.event.v1` |
| mutation Intent ID | `mv.intent.v1` |
| conflict scope | `mv.scope.v1` |
| deterministic random | `mv.random.v1` |
| immutable Operation payload | `mv.operation-payload.v1` |
| deterministic state diagnostic | `mv.state-diagnostic.v1` |

label は protocol payload や domain data から変更できない固定値とする。

## 5. Step transition と same-Step の意味

`effective_step = S` の simulation-affecting input は、authoritative `State(S)` から `State(S+1)` を生成する transition に参加することを意味する。

- `State(S)` はその transition の authoritative input boundary。
- transition の途中状態を外部の authoritative World State として公開しない。
- transition 完了後に `SimulationStep` が `S+1` となる。
- Operation の結果は原則として `State(S+1)` 以降から観測可能となる。
- Config の effective Step については P1-03 で、transition のどの計算条件へ作用するかを追加定義する。

## 6. subsystem dependency graph

### 6.1 `DomainToken`

world outcome に影響する subsystem / operation family は stable な `DomainToken` を持つ。

例:

```text
core.control
core.external-operation
sim.resident
sim.economy
sim.weather
```

名称例は命名規則の例であり、この一覧自体を registry としない。

### 6.2 dependency declaration

各 domain は simulation-affecting dependency を次の論理形で宣言できる。

```text
DomainDependency {
  domain: DomainToken,
  after:  [DomainToken...],
  before: [DomainToken...]
}
```

規則:

- dependency set は simulation-affecting Config / enabled module set の一部として replay 条件に含める。
- Core は起動時および安全な構成変更時に directed acyclic graph であることを検証する。
- cycle が存在する構成は起動または atomic Config apply を拒否する。
- dependency がない domain 同士に暗黙の処理優先度を与えない。

### 6.3 deterministic topological order

実行・merge 上の domain rank が必要な場合、次の Kahn-style rule で決定する。

1. dependency を満たし現在 selectable な domain 集合を得る。
2. selectable 集合から `DomainToken` の ASCII bytewise ascending で最小の domain を選ぶ。
3. 選択 domain を order へ追加し、その edge を除去する。
4. 全 domain が確定するまで繰り返す。

これにより、hash-map iteration order や module discovery order を domain order に使用しない。

## 7. mutation intent

parallel calculation は authoritative state を直接競合 write せず、論理的な mutation intent を生成して deterministic merge boundary へ渡すことを標準モデルとする。

```text
MutationIntent {
  phase:              uint8,
  domain:             DomainToken,
  conflict_scope:     ConflictScope,
  semantic_priority:  int32,
  source_kind:        SourceKind,
  source_id:          128-bit,
  local_ordinal:      uint64,
  mutation_kind:      StableToken,
  payload:            domain-defined
}
```

### 7.1 `SourceKind`

```text
0 = external_operation
1 = scheduled_internal_event
2 = derived_internal_event
3 = system_generated
```

SourceKind は authorization や domain priority を表さず、identity derivation と diagnostic の分類に使用する。

### 7.2 `IntentId`

同一 source が複数 intent を生成できるため、各 intent に internal stable identity を導出する。

```text
IntentId = Trunc128(DomainHash(
  "mv.intent.v1",
  {
    0: world_id,
    1: effective_step,
    2: source_kind,
    3: source_id,
    4: domain,
    5: mutation_kind,
    6: local_ordinal
  }
))
```

- `local_ordinal` は source 内の論理的に安定した順序から決める。
- vector push の実行順、thread number、task completion order で採番しない。
- 同じ source context で同じ ordinal を二重使用した場合は invariant violation。

## 8. conflict scope

### 8.1 `ConflictScope`

world mutation が同一 authoritative resource を競合更新するかを識別するため、domain は最小の安定した conflict scope を生成する。

論理構造:

```text
ConflictScope {
  domain:       DomainToken,
  target_kind:  StableToken,
  target_id:    bytes,
  resource:     StableToken,
  subkey:       bytes | null
}
```

例として Entity component field、inventory slot、ledger account、spatial cell 等を表現できる。

- memory address、container index、database row physical location を使用しない。
- target_id / subkey は domain 上の persistent logical identity とする。
- conflict 判定に wall-clock timestamp を使用しない。

### 8.2 `ConflictScopeDigest`

```text
ConflictScopeDigest = DomainHash("mv.scope.v1", ConflictScope)
```

SHA-256 digest 32 octets全体を使用する。

異なる canonical ConflictScope が同一 digest になったことを実装が検出した場合は silent collision resolution を行わず fatal invariant violation とする。

## 9. stable ordering tuple

### 9.1 `OrderPhase`

Phase 1 共通 phase を次で固定する。

```text
0 = control
1 = external_input
2 = scheduled_internal
3 = derived_internal
4 = system_internal
5 = finalization
```

- 値が小さいほど先。
- domain が勝手に新しい phase 数値を wire/persistence へ追加しない。
- 拡張が必要な場合は Phase 1 共通契約の version update として扱う。
- phase は「Adminだから最優先」等の UI role priority を意味しない。

### 9.2 full ordering key

同一 `effective_step` 内で全順序が必要な item は次で ascending sort する。

```text
SameStepOrderKey = (
  phase,                  // uint8
  domain_rank,            // deterministic dependency order
  conflict_scope_digest,  // 32 octets bytewise ascending
  semantic_priority,      // int32 ascending
  intent_id               // 16 octets bytewise ascending
)
```

`effective_step` を含む global key は次とする。

```text
GlobalOrderKey = (effective_step, SameStepOrderKey)
```

### 9.3 ordering rules

- `semantic_priority` の default は `0`。
- semantic_priority の意味は domain schema が固定し、runtime arrival order から生成しない。
- negative priority を許可し、より小さい値を先とする。
- `IntentId` は最後の total-order tie-breaker であり、business priority を表さない。
- `OperationId` / `EntityId` の大小を直接 business priority として扱わない。
- independent scope 間では execution を parallel 化できるが、diagnostic / replay 上の canonical order は上記 key で一意に定義する。

## 10. conflict resolution modes

各 mutation kind は次の mode のいずれかを schema で固定する。

### 10.1 `exclusive_first_valid`

- 同一 ConflictScope の candidates を SameStepOrderKey で sort する。
- 順に world invariant / precondition を評価する。
- 最初に成立した candidate を採用する。
- 後続 conflict candidate は `conflict_lost` または domain 定義 result とする。

### 10.2 `sequential`

- candidates を SameStepOrderKey 順に適用する。
- 各 candidate は同 scope 内で直前 candidate 適用後の working value を参照できる。
- 最終結果は single-thread canonical order と同一でなければならない。

### 10.3 `set_merge`

- set semantics が domain 上で定義される場合に使用できる。
- duplicate identity の定義を schema で明示する。
- serialize / hash 前には stable sort を行う。

### 10.4 `deterministic_reduce`

- sum、min/max、aggregate 等に使用できる。
- operator が数学的に可換でも floating-point bit result が順序依存となり得る場合、SameStepOrderKey 順の canonical reduction を標準とする。
- parallel tree reduction を使用する場合、canonical single-thread reduction と同一結果になることを implementation test で保証しなければならない。

### 10.5 `custom_deterministic`

上記で表せない domain conflict は、次を満たす pure resolver を domain 詳細設計で定義できる。

```text
result = resolve(frozen_input_state, ordered_candidates)
```

resolver は wall clock、thread ID、iteration order、network metadata、process-local random state を参照してはならない。

## 11. internal Event ID

external Operation 以外の因果 event を安定識別するため internal `EventId` を 128 bit opaque value とする。

```text
EventId := 128-bit opaque value
```

導出 context:

```text
EventContext {
  world_id:             WorldId,
  effective_step:       SimulationStep,
  domain:               DomainToken,
  event_kind:           StableToken,
  parent_operation_id:  OperationId | ZERO,
  parent_event_id:      EventId | ZERO,
  subject_entity_id:    EntityId | ZERO,
  local_ordinal:        uint64
}
```

導出:

```text
EventId = Trunc128(DomainHash("mv.event.v1", EventContext))
```

規則:

- parent が Operation の場合は parent_operation_id を設定する。
- parent が internal event の場合は parent_event_id を設定する。
- root system event は両方 ZERO を許可する。
- root event の local_ordinal は domain 内の決定論的 scheduling key から生成する。
- 同一 context から同一 EventId が再生成されなければならない。

## 12. EntityId derivation

親文書で固定した `EntityCreationContext` を `MV-DCBOR-v1` で encode し、次で EntityId を導出する。

```text
EntityIdCandidate(nonce) = Trunc128(DomainHash(
  "mv.entity.v1",
  {
    0: world_id,
    1: creation_step,
    2: creator_domain,
    3: creator_entity_id,
    4: creation_kind,
    5: local_ordinal,
    6: nonce
  }
))
```

### 12.1 nonce rule

- 通常 `nonce = 0`。
- candidate が reserved ZERO ID の場合のみ `nonce = 1, 2, ...` と deterministic に増加し、最初の non-zero candidate を採用する。
- 既存の別 creation context と 128 bit collision した場合、runtime creation order に依存する再採番を行わない。
- 異なる creation context の true collision を検出した場合は fatal deterministic invariant violation とする。

### 12.2 local ordinal

local_ordinal は同一 logical creator scope 内で stable でなければならない。

許可例:

- parent event が生成する child list を stable domain key で sort した ordinal
- deterministic spatial cell index
- schema 上の fixed slot number

禁止例:

- atomic increment の取得順
- thread-local buffer の flush 順
- unordered map iteration 順
- database auto-increment value

## 13. deterministic random

### 13.1 RandomContextV1

simulation-affecting random draw は次の context でアドレスする。

```text
RandomContextV1 {
  world_id:             WorldId,
  step:                 SimulationStep,
  domain:               DomainToken,
  purpose:              StableToken,
  subject_entity_id:    EntityId | ZERO,
  event_id:             EventId | ZERO,
  operation_id:         OperationId | ZERO,
  local_ordinal:        uint64
}
```

- purpose は `choose-destination`、`birth-trait` 等の stable semantic token とし、関数名やsource line番号を使わない。
- local_ordinal は同一 logical context で複数 draw family が必要な場合の stable discriminator。
- event_id / operation_id は該当する因果 identity のみ設定し、不要側は ZERO。

### 13.2 RandomWord64

状態を持たない addressable random word を次で定義する。

```text
RandomWord64(context, draw_index, retry_index) =
  BE_U64(first_8_bytes(DomainHash(
    "mv.random.v1",
    {
      0: world_seed,
      1: context,
      2: draw_index,
      3: retry_index
    }
  )))
```

- `draw_index: uint64` は logical draw number。
- `retry_index: uint64` は rejection sampling 等の同一 draw 内の再試行に使用し、通常 `0`。
- call count に応じて暗黙 increment される shared cursor を持たない。
- 無関係な random draw の追加で既存 draw の値がずれない。

### 13.3 bounded unsigned integer

`0 <= result < bound` の unbiased integer が必要な場合、`bound` は `1..2^64-1` とし rejection sampling を使用する。

概念手順:

```text
limit = floor(2^64 / bound) * bound
retry = 0
loop:
  x = RandomWord64(context, draw_index, retry)
  if x < limit:
    return x % bound
  retry += 1
```

`2^64` の中間値は 65 bit 以上の整数または同値な overflow-safe 実装で扱う。

### 13.4 uniform double

表示ではなく simulation logic で `[0, 1)` の binary64 値が必要な場合の標準変換を次とする。

```text
u = RandomWord64(context, draw_index, 0)
result = (u >> 11) * 2^-53
```

- 53 bit precision を使用する。
- domain が decimal / fixed-point の方が適切な場合は整数 random を基礎に domain 側で deterministic mapping する。
- platform 差の強い transcendental function を乱数 API 自体の一部にしない。

### 13.5 禁止事項

simulation-affecting random に次を使用しない。

- OS entropy を draw ごとに直接使用すること
- wall clock
- process ID / thread ID
- task completion order
- shared mutable PRNG cursor
- hash-map iteration position
- retry/network timing

Master Gateway selection 等、world outcome と分離された operational random は本 RandomContextV1 の対象外とする。

## 14. immutable Operation payload digest

同一 OperationId が retry / reconnect / failover を跨いで別内容へ変化していないことを確認するため、payload digest を SHA-256 で固定する。

```text
OperationPayloadDigest = DomainHash(
  "mv.operation-payload.v1",
  CanonicalImmutableOperationPayload
)
```

### 14.1 digest に含める意味情報

少なくとも次の immutable semantics を含める。

- operation type
- logical target
- operation arguments / requested mutation
- origin が確定した request-scoped logical attributes
- candidate/effective scheduling semantics のうち origin 後に変更してはならない field

### 14.2 digest から除外する transport metadata

次は digest へ含めない。

- retry count
- physical receive timestamp
- socket / connection ID
- current hop Gateway identity
- packet sequence
- ACK state
- routing-only correlation metadata

P1-04 で common envelope を確定するとき、各 field を immutable digest inclusion / exclusion のどちらかへ明示分類する。

同じ OperationId で digest が異なる request を受信した場合は duplicate として片方を選ばず、protocol violation として拒否する。

## 15. deterministic state diagnostic hash

決定論違反検出の共通 hash algorithm は SHA-256 とする。

```text
StateDiagnosticHash = DomainHash(
  "mv.state-diagnostic.v1",
  CanonicalAuthoritativeStateSlice
)
```

- hash 対象 slice / tree 構造は P1-05 persistence 設計で確定する。
- diagnostic hash 計算順そのものが world outcome に影響してはならない。
- hash mismatch は不一致検出であり、自動修復判断とは分離する。

## 16. replay / persistence contract

replay が P1-02 の結果を再現するため、少なくとも次を固定・復元できなければならない。

- WorldSeed
- WorldId
- effective SimulationStep
- simulation-affecting enabled domain set / dependency declaration
- OperationId と immutable payload semantics
- accepted Operation の effective Step
- internal event を再生成する causal inputs
- simulation-affecting Config history

RandomWord の個々の出力値を通常 replay log へ保存する必要はない。同じ RandomContextV1 を再構築できることを正本とする。

## 17. implementation conformance tests

全 Core implementation は最低限次の自動試験を持つ。

1. 同じ EntityCreationContext から同じ EntityId を得る。
2. 同じ EventContext から同じ EventId を得る。
3. 同じ RandomContextV1 / draw_index から thread 数に関係なく同じ RandomWord64 を得る。
4. unordered input collection の物理 iteration order を変えても同じ SameStepOrderKey sequence を得る。
5. 1 / 2 / 4 / 8 / 16 thread で同じ logical input から同じ authoritative result を得る。
6. Gateway arrival order を並べ替えても同じ effective Operation set なら同じ Core canonical order を得る。
7. 同じ OperationId + same immutable payload は同じ digest、payload 変更時は異なる digest となる。
8. conflict resolver は candidate input permutation に関係なく canonical sort 後に同じ結果を得る。

標準 test vector は実装フェーズで本 schema から生成し、component 間で共有する。

## 18. P1-02 完了時点での確定事項

P1-02 で次を確定した。

- `WorldSeed` は 256 bit。
- deterministic semantic encoding は `MV-DCBOR-v1`。
- common hash は SHA-256。
- EntityId は domain-separated SHA-256 の先頭 128 bit から導出する。
- internal EventId / IntentId を deterministic context から導出する。
- same-Step canonical order は phase / deterministic domain rank / conflict scope / semantic priority / IntentId で全順序化する。
- dependency graph の同順位選択は DomainToken lexical order で固定する。
- conflict resolution mode と deterministic merge 要件を固定する。
- simulation-affecting random は stateless addressable hash random とする。
- bounded integer は rejection sampling、uniform binary64 は 53 bit mapping とする。
- Operation immutable payload digest は SHA-256。
- state diagnostic hash algorithm も SHA-256 とする。

## 19. 次の作業

P1-03 Config 詳細契約へ進む。

P1-03 では少なくとも次を確定する。

- Config document schema / schema version
- simulation-affecting / operational / presentation 分類
- startup-only / runtime-safe / world-regeneration-required 分類
- atomic apply boundary と effective Step
- dependency declaration の Config 上の位置付け
- default 補完と old Config migration
- Config history / digest / replay contract
