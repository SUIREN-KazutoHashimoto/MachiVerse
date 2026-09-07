# Simulation Core Phase 0 設計確定監査

Status: In Progress  
Tracking: Issue #35  
Scope: Simulation Core Phase 0

## 1. 目的

Issue #35 の Phase 0 を、Simulation Core 実装前に必要な設計契約が `docs/architecture/` と `docs/protocols/` の正本上で一意に読める状態へ収束させる。

Phase 1〜4 詳細設計で既に確定した内容を再設計することは本作業の目的ではない。詳細設計だけに残っている確定事項を architecture / protocol の正本へ昇格し、旧文書に残る `未確定` / `詳細設計で決定` 表記と矛盾を解消する。

## 2. 正本と移行規則

Phase 0 完了後、Simulation Core の標準設計は次を正本とする。

1. `docs/requirements/` の確定要件
2. `docs/architecture/` の Simulation Core / world / determinism / spatial / Config / addon 契約
3. `docs/protocols/` の Core-owned protocol 契約および version-controlled schema

`docs/design/phase1-*`〜`phase4-*` は決定根拠・詳細設計履歴として保持するが、Phase 0 対象の標準意味論を `docs/design/` だけに残さない。

詳細設計文書内の途中 Status 表記と Phase 4 completion review が競合する場合は、`phase4-completion-review.md` の完了判定を採用する。

## 3. Phase 0 checklist 監査

| Issue #35 項目 | 現在状態 | 正本化先 | Phase 0 対応 |
|---|---|---|---|
| 未確定の Simulation Core 要件整理 | 要監査 | 本書 / `simulation-core.md` | stale TBD を semantic blocker / implementation-local に分類する |
| Core が所有する Protocol 契約具体化 | 詳細設計では解消済み | `docs/protocols/core-gateway.md` / schema | transport、serialization、state publication、heartbeat/status を正本へ昇格する |
| full-3D 前提の空間表現詳細設計 | 詳細設計では解消済み | `spatial-model.md` | coordinate/numeric/terrain/index の標準方式を正本へ昇格する |
| 状態更新・乱数・競合解決の決定論設計 | 大半確定、旧記述あり | `deterministic-update-execution.md` / `deterministic-random-id-numerics.md` | ordering/hash/random/ID/reduction を正本へ統合する |
| 最大16スレッドの決定論的並列実行設計 | 論理契約確定、旧 TBD あり | `core-concurrency.md` | frozen read / stable output / canonical merge / barrier 契約を正本化する |
| 外部 Config 項目定義 | 共通契約確定、Core field は詳細設計に存在 | `configuration.md` / Core architecture | Core schema 1.0 の key/default/range/impact/mutability を正本化する |
| addon 拡張を考慮した責務境界 | 基本方針確定 | `addon-boundary-safety.md` / `simulation-core.md` | Core addon が authority/determinism/protocol boundary を破らない条件を明示する |

## 4. 既に確定済みとして扱う基礎契約

Phase 0 では次を再検討対象にしない。

- `SimulationStep := uint64`、初期状態 `State(0)`、wrap 禁止。
- `effective_step = S` は `State(S) -> State(S+1)` transition への参加を意味する。
- Pause 中は Step を進めず、処理遅延だけを理由に Step skip しない。
- same-Step canonical order は `SameStepOrderKey` を使用し、network arrival / thread completion / Master identity を順序根拠にしない。
- World-affecting random は addressable deterministic random とし、shared mutable PRNG cursor を使用しない。
- persistent identity は deterministic logical creation context から導出する。
- authoritative state mutation は frozen read / intent / deterministic merge / invariant validation / durable commit / finalize の境界を通す。
- physical worker count は 1〜16、worker count / scheduling timing を world outcome へ入力しない。
- standard authoritative world は full 3D であり、単一 heightmap を authoritative terrain としない。
- Simulation Core Config は Core 自身が所有し、他 component は Config file を直接読まない。
- standard protocol へ addon functional generic extension slot を持ち込まない。

## 5. stale TBD の分類規則

既存 architecture/protocol の `未確定` / `今後決定` / `詳細設計で決定` は次のいずれかへ分類する。

### A. Phase 0 semantic blocker

world outcome、persistent identity、replay、protocol compatibility、authoritative state、Config meaning、component responsibility のいずれかを実装者判断へ残すもの。

Phase 0 完了前に architecture/protocol へ明示的に確定する。

### B. implementation-local choice

class/file 名、lock implementation、worker scheduler の物理方式、internal collection、host deployment、compatible patch version 等、既存 semantic contract を変えず差し替えられるもの。

Phase 0 blocker としない。ただし結果が deterministic contract を満たすことを acceptance test で検証する。

### C. standard scope 外

multi-Core addon の具体仕様、addon distribution framework 等、Issue #35 の standard single-Core Phase 0 に含めないもの。

標準契約を破らない extension boundary だけ確定し、具体仕様は別 Issue とする。

## 6. Phase 0 で正本へ昇格する確定事項

### 6.1 Deterministic primitives

- hash: SHA-256。
- deterministic semantic encoding: `MV-DCBOR-v1`。
- domain-separated hash label: `mv.entity.v1`, `mv.event.v1`, `mv.intent.v1`, `mv.scope.v1`, `mv.random.v1`, `mv.operation-payload.v1`, `mv.state-diagnostic.v1`。
- `EntityId`, `EventId`, `IntentId`, `OperationId`, `BatchId` 等の標準 persistent/logical ID は canonical 16-octet representation を使用する。
- deterministic random は `RandomContextV1` + `draw_index` + `retry_index` を SHA-256 domain hash へ入力した addressable `RandomWord64` を基礎とする。
- conflict resolution は mutation kind ごとに `exclusive_first_valid`, `sequential`, `set_merge`, `deterministic_reduce`, `custom_deterministic` のいずれかを schema で固定する。
- reduction は integer/fixed-point first、checked arithmetic、canonical order、round-to-even を標準とする。

### 6.2 Authoritative numeric / spatial profile

- authoritative world calculation は integer / fixed-point first。
- checked int32/int64 と Int128 intermediate を使用し、overflow の silent wrap を禁止する。
- world root coordinate は world-centered right-handed frame。
- authoritative position は millimetre integer `Vec3Mm { x:int64, y:int64, z:int64 }`。
- orientation は `QuaternionQ30`。
- natural terrain authority は Sparse Brick Octree Signed Distance Field v1 とし、地下空間・洞窟・トンネル・overhang を solid/void geometry として表現可能にする。
- spatial query の derived broad-phase index は hierarchical AABB grid を標準とし、query result は stable ID で sort/unique してから exact test する。

### 6.3 Parallel update profile

- domain calculation は finalized `State(S)` の immutable/frozen read view を基礎にする。
- parallel work output は stable target/logical identity で key 付けする。
- authoritative merge は canonical key で sort した後に行う。
- iterative solver は明示的 double buffer / barrier を使用し、iteration 内で previous buffer を共通 read basis とする。
- thread completion order、worker ID、hash-map iteration orderを merge/reduction/random/identity へ使用しない。
- physical scheduling technology、work stealing、lock-free/locked collection の選択は上記契約を満たす限り implementation-local とする。

### 6.4 Protocol profile

Core↔Gateway standard profile:

- serialization: Protocol Buffers proto3。
- transport: gRPC bidirectional streaming。
- state publication: FULL / DELTA + continuity token + chunk assembly/digest validation。
- standard envelope hard limit: 8 MiB。
- state publication chunk: 1 MiB 以下。
- required compression: none。optional gzip は negotiated Capability とする。
- Gateway logical identity を protocol 上の stable identity として持つ。
- Master heartbeat / status query / resync は protocol message registry と schema で表現する。

### 6.5 Simulation Core Config schema baseline

Core Config schema identityは `config.simulation-core / 1.0` とする。

Phase 0 で少なくとも次のカテゴリを standard field として持つ。

- `simulation.step-rate.*`
- `runtime.worker-count`
- `runtime.domain-timeout-ms`
- `scheduling.*`
- `detail.*`
- `persistence.*`
- `publication.*`
- `master.*`
- `queue.*`
- `observability.*`

exact key/type/default/range/impact/mutability は `configuration.md` の Simulation Core schema 節へ統合する。

## 7. Phase 0 blocker ではないもの

次は standard semantics を変更しない限り Phase 0 blocker としない。

- concrete class / namespace / source file layout
- physical worker scheduler / work stealing implementation
- lock / lock-free / immutable collection の具体選択
- internal queue container / index data structure
- telemetry backend/vendor
- container/orchestrator/deployment topology
- compatible runtime/package patch pin
- multi-Core addon の具体仕様
- addon distribution / package manager の具体実装

## 8. 完了条件

Issue #35 Phase 0 は次をすべて満たした時点で完了とする。

1. Issue #35 の7 checklist項目すべてに architecture/protocol の正本がある。
2. Phase 0 semantic decision が `docs/design/` にしか存在する状態がない。
3. `simulation-core.md`, `core-concurrency.md`, `deterministic-random-id-numerics.md`, `spatial-model.md`, `configuration.md`, `core-gateway.md` の stale TBD が解消または明示的に implementation-local / scope外へ分類されている。
4. Protocol document と version-controlled `.proto` schema の意味が一致する。
5. Config schema の key/default/range/impact/mutability が一意で hidden default を要求しない。
6. 1 / 2 / 4 / 8 / 16 worker で同一 logical input の authoritative digest が一致する acceptance 条件を維持する。
7. thread/network/wall-clock/renderer/addon metadata 等の operational factor が world outcome へ混入しない。
8. unresolved Phase 0 design blocker が0件である。

## 9. 作業順

1. P0-A: stale TBD / source-of-truth audit。
2. P0-B: Core↔Gateway protocol の Phase 4 確定事項を `docs/protocols` へ同期。
3. P0-C: Spatial / numeric / deterministic primitive を architecture へ同期。
4. P0-D: deterministic merge / parallel execution を architecture へ同期。
5. P0-E: Simulation Core Config schema / addon boundary を architecture へ同期。
6. P0-F: cross-document consistency review、Issue #35 checklist 完了判定。
