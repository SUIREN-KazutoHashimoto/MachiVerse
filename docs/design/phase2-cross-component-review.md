# 詳細設計 Phase 2: 横断整合性レビュー

Status: Complete / Phase 2 complete  
Tracking: Issue #14  
Parent: `docs/design/phase2-component-internal-design.md`

## 1. 目的

本書はPhase 2の最終レビューとして、Simulation Core、Gateway、General View、Admin Viewの内部設計を横断し、state ownership、data flow、queue/backpressure、lifecycle/failure transition、Config ownership、observability、protocol mapping、Phase 3開始条件を確認する。

Phase 1で確定したidentity、ordering、durability、retry/dedup、Config、protocol version/Capability意味論は再定義しない。

## 2. レビュー対象

- `docs/design/phase2-component-internal-design.md`
- `docs/design/phase2-simulation-core-internal-design.md`
- `docs/design/phase2-gateway-internal-design.md`
- `docs/design/phase2-general-view-internal-design.md`
- `docs/design/phase2-admin-view-internal-design.md`
- `docs/design/phase1-cross-cutting-review.md`
- `docs/protocols/core-gateway.md`
- `docs/protocols/gateway-gateway.md`
- `docs/protocols/gateway-view.md`
- `docs/protocols/gateway-admin-view.md`

## 3. state ownership最終整理

| state / authority | owner | 他componentの扱い |
|---|---|---|
| Authoritative World State / finalized SimulationStep | Simulation Core | Gateway/Viewは派生・confirmed projectionのみ |
| world-affecting Operation acceptance/schedule/terminal fact | Simulation Core | Gatewayはcustody、View/Adminはrequest trackingのみ |
| MasterGeneration / Master assignment authority | Simulation Core | Gatewayはcurrent generation/roleを追従 |
| Gateway confirmed cache / publication buffer | Gateway | authorityではなく再構築可能なderived state |
| authn/authz/session authority | Gateway側server domain | View/Adminはsession/permission projectionのみ |
| Gateway custody/retry state | Gateway | Core dedup authorityの代替ではない |
| General View confirmed display basis | General View `ConfirmedWorldStore` | Gateway confirmed publicationからのみ昇格 |
| General View prediction/presentation | General View | non-authoritative、破棄・reconcile可能 |
| Admin View management draft/presentation | Admin View | target component state/config authorityではない |
| component Config | 各component自身 | 他componentはprotocol公開された意味だけ利用 |
| operator request audit | Gateway + target component | Admin View local historyはprojection/cacheのみ |

### 3.1 audit ownership

Admin actionのauditは一つのclient-side logへ集約しない。

- Gatewayはactor/session/authorization/request routing/result correlationのserver-side audit factを所有する。
- Target componentは自身が実行したConfig change、operational command、world mutation、reject等のexecution factを所有する。
- Simulation Coreのworld-affecting factはPhase 1 persistence/history contractへ結び付く。
- Admin Viewはこれらをquery/correlationして表示するがauthorityではない。

## 4. Diver binding authorityの解消

旧architectureではDiverとresidentのbinding stateの保存場所・authorityが未確定だった。Phase 2ではcomponent-level ownershipを次で確定する。

### 4.1 Gateway responsibility

Gatewayは次のoperational control authorityを持つ。

- authenticated sessionとDiver identityのassociation
- role/permission verification
- concurrent active-control admission
- 原則1resident/1Diverの外部操作要求に対する排他admission
- reconnect時のsame Diver identity確認
- Master/failoverを跨ぐsession/control state convergence

Gateway-local stateだけをworld-affecting binding truthとして扱わない。

### 4.2 Core / Phase 3 domain responsibility

world結果へ影響するbinding状態はCore authority下に置く。

Phase 3のresident/participation domainは`DomainRuntime` contractへ配置し、少なくとも次をauthoritative domain stateとして表現可能にする。

- Diver identity referenceとresident EntityIdのeffective binding
- binding開始/終了のeffective SimulationStep
- resident死亡等によるbinding validity transition
- absence behavior policyのeffective value/version
- simulationへ影響するparticipation policy history

binding/absence policy changeがworld outcomeへ影響する場合はstable OperationとしてGatewayからCoreへ入り、Phase 1のscheduling/dedup/persistence意味論へ従う。

これによりGateway failoverやView reconnectがworld binding historyを書き換えない。

### 4.3 General View responsibility

General Viewはserver-confirmed participation projectionとUI draftだけを保持する。

- local draftをbinding authorityにしない。
- old reconnect cacheをcurrent bindingとしてblind reuseしない。
- server-confirmed binding stateへ収束する。

## 5. Admin component management routingの解消

Admin Viewからのexternal management boundaryはGatewayに固定する。

```text
Admin View
 -> mv.gateway-admin-view
 -> Gateway management target routing
 -> target-specific protocol boundary
```

### 5.1 Gateway責務

Gatewayはmanagement target routingのownerとなる。

- Admin Viewからcomponent internal object/addressを受け取らない。
- stable `ManagementTarget` abstractionへ正規化する。
- permission、target availability、Capabilityを検証する。
- target-specific routeが存在しない操作をdirect accessで代替しない。

### 5.2 downstream mapping

- Simulation Core target: `mv.core-gateway`
- current Gateway local target: Gateway内部management handler
- peer Gateway target: `mv.gateway-gateway`上のcapability-defined management/diagnostic route
- General View target: connected `mv.gateway-view`境界でprotocol上公開されたclient diagnostic/management capabilityだけを利用
- Admin View自身: local self-observabilityはAdmin View内部、server-side connection/session factはGateway側

新しいmanagement機能が必要な場合、既存protocolのCapability/明示category拡張または追加protocolを別途設計する。未定義のinternal API呼出しへfallbackしない。

exact payload schema、collector、transport implementationは後続詳細として残せるが、component-level routing ownerは確定した。

## 6. protocol mapping最終確認

### Core ↔ Gateway

`mv.core-gateway`

- Core `ProtocolBoundary` / `OperationIngress` / `PublicationProjection` / `MasterCoordinator`
- Gateway `CoreProtocolBoundary` / `ConfirmedStateCache` / `OperationAdmission` / `CustodyStore` / `ResyncCoordinator`

確認結果:

- candidate Stepとauthoritative effective Stepを分離。
- durable acceptance/terminal resultとhop ACKを分離。
- continuity mismatchでresync。
- MasterGeneration authorityはCore。

### Gateway ↔ Gateway

`mv.gateway-gateway`

- Gateway `PeerProtocolBoundary` / `CrossGatewayMerger` / `CustodyStore` / `RetryCoordinator`

確認結果:

- SOURCE_HELD〜TERMINAL custodyを維持。
- Master receiptとCore acceptanceを分離。
- stale generation rejectをOperation terminal rejectへ変換しない。
- network arrival orderをmerge authorityにしない。

### Gateway ↔ General View

`mv.gateway-view`

- Gateway `ViewProtocolBoundary` / `SessionCoordinator` / `AuthorizationService` / `PublicationBuffer` / `OperationAdmission` / `ResultRouter`
- View `GatewayProtocolBoundary` / `PublicationConsumer` / `ConfirmedWorldStore` / `OperationController` / `DiverParticipation`

確認結果:

- confirmed stateとpredictionを分離。
- session/auth authorityはGateway側。
- View local predictionはworld mutation authorityを持たない。
- reconnect/resyncでcontinuity確認前のold stateをcurrent表示しない。

### Gateway ↔ Admin View

`mv.gateway-admin-view`

- Gateway `AdminProtocolBoundary` / `SessionCoordinator` / `AuthorizationService` / management target routing / `ResultRouter`
- Admin View `GatewayProtocolBoundary` / `AdminSessionState` / management controllers / `AuditViewModel`

確認結果:

- General View roleとAdmin permissionを分離。
- target component Config file/internal objectへdirect accessしない。
- mutation requestはstable identityを維持。
- client confirmationはauthorization/OperationIdの代替ではない。

## 7. queue / backpressure横断確認

### 7.1 world-affecting path

```text
View/Admin request
 -> Gateway boundary admission
 -> authorization
 -> Gateway custody/aggregation
 -> Core Operation acceptance/scheduling
 -> Step pipeline
 -> durable terminal result
```

- durable acceptance後のOperationをpressureだけでdropしない。
- retry/failoverでsame Operation identityを維持する。
- slow presentation clientをCore mutation pipelineへ直接連鎖させない。

### 7.2 publication path

```text
Core finalized/durable state
 -> Core publication projection
 -> Gateway confirmed cache
 -> Gateway publication buffer
 -> General View confirmed store
 -> presentation/prediction
```

- downstreamではintermediate publication/frameをcoalesce可能。
- continuity token/basis dependencyを壊すcoalesceはしない。
- rendering failureがCore/Gateway authoritative lifecycleへ逆流しない。

### 7.3 management/diagnostic path

- auth/session/revoke、mutation result、audit correctnessをmetrics/log freshnessより優先する。
- high-volume metrics/logをlossy/coalesce可能にしてもaudit/terminal resultを同じqueue policyへ置かない。

## 8. lifecycle / failure transition横断確認

### Core failure

- persistence integrity/deterministic invariant failure: `FAILED_SAFE`。
- publication failureだけならworld integrityを維持したまま`DEGRADED`可能。
- Gateway 0台でもworld Stepを停止しない。

### Gateway failure

- Core disconnect/continuity anomaly: `RESYNCING`へ移行しconfirmed publicationとnew world-affecting admissionをgate。
- Master switch: same Operation identity/custodyを維持。
- auth outage: fail-openしない。

### General View failure

- render/asset failure: `DEGRADED_RENDERING`でprotocol/sessionと分離。
- continuity mismatch: `RESYNCING`。
- session revoke: new mutation input停止。

### Admin View failure

- Gateway disconnect: pending mutation identityを保持し、reconnect後status convergence。
- stale ConfigGeneration: refresh後new requestとして再作成しsilent overwriteしない。
- metrics/log overload: mutation/result pathを優先。

## 9. Config ownership横断確認

Config fileは各component自身だけが所有する。

- Core: simulation/scheduling/persistence/detail/master eligibility等
- Gateway: connection/session/cache/publication/aggregation/retry/resync等
- General View: rendering/prediction/reconcile/local presentation等
- Admin View: dashboard/query/cache/confirmation UX等

他componentのConfigをdirect read/writeしない。simulation-affecting semanticsはowner componentがprotocol/historyで公開する。

## 10. observability横断確認

component-local observabilityは共通trace identityへ相関可能にする。

最低共通context:

- ComponentInstanceId
- WorldId / basis_step / effective_step（該当時）
- OperationId / BatchId / CorrelationId
- MasterGeneration
- ConfigGeneration
- protocol version / Capability / NegotiationGeneration
- lifecycle/failure transition reason
- queue saturation/backpressure event

metrics/log timingをworld ordering、random、identity生成へ使用しない。

## 11. Phase 3開始条件

Phase 3のdomain設計は開始可能と判定する。

各Core domainは`DomainRuntime`へ次を登録する。

```text
DomainDefinition
  - stable DomainToken
  - dependencies
  - state ownership
  - read-set / write-intent contract
  - deterministic update phases
  - operation kinds
  - diagnostic partition schema
  - publication projection contribution
```

Phase 3でdomainを追加しても次を再定義しない。

- SimulationStep / ordering / random / ID
- Config semantics
- Operation scheduling/retry/dedup
- persistence/durability
- protocol common envelope/version/Capability
- Core authoritative state boundary
- Gateway custody/cache/publication boundary
- View prediction/presentation boundary

## 12. 非blockerとして残す実装詳細

- physical transport/serialization/compression
- task scheduler/lock/data structure
- persistence/database/file layout
- credential/IdP/session product
- exact queue capacities/timeouts/backoff
- exact state full/delta encoding
- exact metrics/log backend
- exact permission/command catalog
- exact Three.js/WebGL/WebGPU/UI framework
- exact prediction/reconcile algorithm
- exact Admin high-impact UX
- exact addon install/update mechanism
- management category payload schema

これらはPhase 2で確定した責務境界を変更せず後続で決定可能である。

## 13. completion判定

Issue #14の完了条件に対する判定:

- 4componentそれぞれの内部責務境界: 完了
- state ownership: 完了
- data flow: 完了
- queue/backpressure: 完了
- lifecycle/failure transition: 完了
- Config ownership: 完了
- observability: 完了
- protocol境界と内部module対応: 完了
- Diver binding component authority: 解消
- Admin management routing owner: 解消
- Phase 3 domain受け皿: 完了
- 未承認実装技術の固定: なし
- unresolved component-level blocker: **0件**

Phase 2は完了と判定する。
