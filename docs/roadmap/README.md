# MachiVerse 実装ロードマップ

Status: Implementation Ready  
Source: `docs/design/phase4-implementation-work-breakdown.md`  
Completion basis: `docs/design/phase4-completion-review.md`

## 1. 目的

本書は、詳細設計 Phase 1〜4 完了後の MachiVerse 実装工程を追跡するための実行ロードマップです。

旧 roadmap Issue #35〜#38 は、詳細設計完了前の「Phase 0: 設計確定」を前提としていました。しかし現在は Phase 4 Completion Review により、実装開始に必要な設計契約、platform profile、acceptance、38 standard implementation work package と dependency DAG が確定しています。

したがって今後のロードマップは「未確定設計を順に決める計画」ではなく、「確定済み設計を依存順に実装・検証・統合する計画」とします。

## 2. 正本と優先順位

ロードマップは設計契約の正本ではありません。実装時の仕様解釈は次を優先します。

1. `docs/requirements/`
2. 各 Phase completion / final review
3. `docs/architecture/`
4. `docs/protocols/` および `docs/protocols/schema/`
5. `docs/design/` の詳細設計
6. 本ロードマップおよび GitHub Issue

本ロードマップと詳細設計が競合する場合、詳細設計を優先します。

## 3. 詳細設計完了状態

詳細設計 Phase 1〜4 は Complete です。

- Phase 1: 共通基盤・契約 — Complete
- Phase 2: コンポーネント内部設計 — Complete
- Phase 3: 世界シミュレーション Domain 設計 — Complete
- Phase 4: 実装直前設計 — Complete
- unresolved detailed-design blocker — 0

Phase 4 では、production implementation Issue へ直接起票可能な 38 work package と dependency DAG が確定しています。

## 4. Standard implementation work package

| 分類 | Work ID | 件数 |
|---|---|---:|
| Simulation Core | `SIM-01..SIM-15` | 15 |
| Gateway | `GW-01..GW-07` | 7 |
| General View | `VIEW-01..VIEW-05` | 5 |
| Administration View | `ADMIN-01..ADMIN-04` | 4 |
| QA / verification | `QA-01..QA-04` | 4 |
| Integration / release | `INT-01..INT-03` | 3 |
| 合計 |  | 38 |

各 package の scope、dependency、acceptance TestCaseId は `docs/design/phase4-implementation-work-breakdown.md` を正本とします。

## 5. 実装 Stage

### Stage A — Foundation

並行開始可能:

- `QA-01` Contract fixtures / schema golden vectors
- `SIM-01` Simulation Core scaffold / deterministic primitives
- `GW-01` Gateway scaffold / protocol-config foundation
- `VIEW-01` General View scaffold / Gateway protocol client
- `ADMIN-01` Admin View scaffold / Gateway protocol client

Stage A が現在の実装開始点です。

### Stage B — Component foundation

- Simulation: `SIM-02`, `SIM-03`, `SIM-04`
- Gateway: `GW-02`
- View: `VIEW-02`
- Admin: `ADMIN-02`, `ADMIN-03` を fixture-driven で進行可能

### Stage C — Common runtime / client behavior

- Simulation: `SIM-05`, `SIM-06`
- Gateway: `GW-03`, `GW-04`
- View: `VIEW-03`, `VIEW-04`
- Admin: `ADMIN-04` を fixture-driven で進行可能

### Stage D — 最大並列実装

- Simulation domains: `SIM-07..SIM-12`
- Simulation observability: `SIM-15`
- Gateway authorization/audit: `GW-05`, `GW-07`

Domain 実装は stable DomainRuntime API を前提に並列化します。

### Stage E — Cross-domain / publication / verification

- Simulation: `SIM-13`, `SIM-14`
- Gateway: `GW-06`
- View: `VIEW-05`
- QA: `QA-02`, `QA-03`

### Stage F — Integration / performance / release

- `INT-01` Single Gateway end-to-end
- `INT-02` Multi-Gateway failover / resync
- `QA-04` Performance / soak harness
- `INT-03` Release acceptance

`INT-03` は全 standard work package 完了後の release gate とします。

## 6. Dependency backbone

```text
QA-01
 ├─ SIM-01 -> SIM-02/03/04 -> SIM-05/06 -> SIM-07..12 -> SIM-13 -> SIM-14
 ├─ GW-01  -> GW-02 -> GW-03 -> GW-04 -> GW-05 -> GW-06
 │                                           └──────-> GW-07
 ├─ VIEW-01 -> VIEW-02 -> VIEW-03
 │                     └-> VIEW-04 -> VIEW-05
 └─ ADMIN-01 -> ADMIN-02
               └-------> ADMIN-03 -> ADMIN-04

SIM-06 -> QA-02
SIM-03 + GW-04 -> QA-03
SIM-13 + SIM-14 + GW-06 + QA-02 -> QA-04

component minimum viable flow -> INT-01 -> INT-02 -> INT-03
```

正確な依存条件は `phase4-implementation-work-breakdown.md` の各 package 定義を参照します。

## 7. コンポーネント別ロードマップ Issue

既存 roadmap Issue を実装追跡 Issue として再利用します。

- #35: Simulation Core — `SIM-01..SIM-15`
- #36: Gateway — `GW-01..GW-07`
- #37: General View — `VIEW-01..VIEW-05`
- #38: Administration View — `ADMIN-01..ADMIN-04`

QA / Integration は別 roadmap Issue で `QA-01..QA-04` / `INT-01..INT-03` を追跡します。

## 8. Platform baseline

Phase 4 で確定済みの standard implementation profile:

```text
Simulation Core / Gateway: .NET 10 LTS / C# 14
Gateway: ASP.NET Core 10
General View: standalone Blazor WebAssembly net10.0
Administration View: standalone Blazor WebAssembly net10.0
General View renderer: Three.js THREE.WebGPURenderer
Preferred renderer backend: WebGPU
Compatibility backend: WebGL 2 through WebGPURenderer fallback
Custom material/shader: TSL / node-material first
Protocol serialization: Protocol Buffers proto3
Internal transport: gRPC bidirectional streaming
View/Admin transport: binary WebSocket over TLS
Persistence: SQLite WAL/FULL
```

Compatible servicing/package patch は各 build/package lock で固定し、exact patch の更新は compatibility / acceptance を通して管理します。

## 9. 実装 Issue の運用

1 standard work package を原則 1 implementation Issue とします。

各 Issue は最低限次を保持します。

- `ImplementationWorkId`
- target component / base branch
- scope
- dependencies
- authoritative design references
- acceptance TestCaseId
- Definition of Done

実装 branch は対象 component 常設 branch から作成します。

- Simulation Core: `simulation`
- Gateway: `gateway`
- General View: `view`
- Administration View: `administration-view`
- cross-component integration: 各 component が `develop` へ統合された後に実施

component 間で compiled DTO / shared runtime DLL を契約 authority として共有しません。

## 10. Definition of Done

各 implementation package は、少なくとも次を満たして完了とします。

- package scope を満たす
-依存する design / schema contract に適合する
- package に紐づく acceptance test が通る
- component independence を破壊しない
- deterministic / security / persistence / protocol semantics を silent に変更しない
-必要な test fixture / diagnostic / observability を含む
-対象 component branch への PR が review / required checks を通過する

## 11. 設計変更が必要になった場合

実装中に Phase 4 contract を変更する必要が判明した場合は、implementation Issue 内で仕様を silent に変更しません。

1. design amendment Issue を作成する
2. `documentation` 責任分野で正本を更新する
3. affected schema/token/version を更新する
4. migration / compatibility を評価する
5. affected P4 acceptance test を更新する
6. dependent implementation Issue に反映する
7. 正本更新後に実装を再開する

## 12. 旧ロードマップからの移行

旧 #35〜#38 の `Phase 0: 設計確定` checklist は、Phase 4 Completion Review の完了により実装 gate としては superseded します。

特に以下をロードマップ上の未決定事項として再度扱いません。

- Protocol transport / serialization / schema
- Core deterministic execution / persistence / Config
- Gateway auth/session/cache/custody
- View platform / Web technology / renderer backend
- Admin View protocol / permission / audit boundary
- component responsibility boundary

これらを変更する場合は「ロードマップ消化」ではなく design amendment として扱います。

## 13. 現在の開始条件

現時点で Stage A の開始を妨げる detailed-design blocker はありません。

最初に並行して起票・着手する package は次です。

```text
QA-01
SIM-01
GW-01
VIEW-01
ADMIN-01
```

この 5 package を新しい implementation roadmap の正式な開始点とします。
