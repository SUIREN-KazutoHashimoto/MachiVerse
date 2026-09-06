# 詳細設計 Phase 2: General View内部設計

Status: In Progress  
Tracking: Issue #14  
Parent: `docs/design/phase2-component-internal-design.md`

## 1. 目的

General View内部を、Gateway confirmed state同期、presentation state、interpolation/prediction、Diver participation、Operation input/result、Three.js rendering、reconnect/resyncへ責務分離する。

General Viewはauthoritative World State、server-side authorization、Diver-resident binding authorityを所有しない。

## 2. 内部module境界

| module | 主責務 |
|---|---|
| `ViewLifecycle` | startup、Gateway接続、login、sync、shutdown |
| `GatewayProtocolBoundary` | `mv.gateway-view` negotiation、decode/encode、result mapping |
| `SessionState` | auth/session/role projection、reconnect state |
| `ConfirmedWorldStore` | Gateway confirmed publicationのcurrent basis保持 |
| `PredictionStore` | local prediction/interpolation state |
| `ReconciliationCoordinator` | predicted表示をconfirmed stateへ収束 |
| `PublicationConsumer` | full/delta/continuity検証とconfirmed state更新 |
| `OperationController` | user inputからstable Operation requestを形成、retry/result tracking |
| `DiverParticipation` | binding projection、join preference、absence policy UI/state |
| `SceneProjection` | confirmed/presentation stateからrender scene model生成 |
| `ThreeRenderer` | Three.js scene/camera/render loop |
| `InteractionController` | input、selection、camera/control、UI action |
| `PresentationState` | panel/filter/selection/accessibility等のlocal state |
| `LocalCache` | non-authoritative asset/presentation/cache data |
| `ViewConfigCoordinator` | View-owned Config |
| `Observability` | client metrics/log/diagnostic |

module名は論理責務を示し、具体framework/component構造を固定しない。

## 3. state classification

### 3.1 ConfirmedWorldStore

Gatewayがconfirmed publicationとして送ったstateだけを保持する。

```text
ConfirmedWorldView {
  world_id,
  basis_step,
  state_continuity_token,
  confirmed_projection
}
```

- `basis_step`はconfirmed World Stateのbasis。
- local frame時刻やprediction時刻をbasis_stepとして上書きしない。
- incompatibleなtoken/Stepを一つのconfirmed stateとして混在させない。

### 3.2 PredictionStore

```text
PredictedPresentationState {
  source_confirmed_token,
  local_prediction_epoch,
  predicted_entities,
  pending_local_feedback
}
```

- non-authoritative。
- confirmed continuity tokenそのものをprediction stateへ付与してauthorityを偽装しない。
-破棄/再生成可能。

### 3.3 PresentationState

Viewだけが所有するstate:

- camera pose/control mode
- selected entity
- open panels
- filter/sort
- hover/focus
- localization/accessibility preference
- asset loading state
- frame interpolation state

World Stateへ送信しない限りsimulation inputではない。

### 3.4 Participation projection

Diver関連でViewが保持するのはserver-confirmed projectionとUI draftを分離する。

```text
DiverParticipationProjection {
  diver_identity_reference,
  binding_status,
  resident_entity_id,
  confirmed_policy_version
}

ParticipationDraft {
  join_preferences,
  absence_policy_edit,
  pending_request_id
}
```

View-local draftをbinding authorityとして扱わない。

## 4. confirmed publication flow

```text
GatewayProtocolBoundary
 -> PublicationConsumer
 -> continuity validation
 -> ConfirmedWorldStore
 -> ReconciliationCoordinator
 -> SceneProjection
 -> ThreeRenderer
```

### 4.1 continuity validation

受信publicationについて少なくとも:

- WorldId
- basis_step
- StateContinuityToken
- delta base token
- resync state

を検証する。

次はnormal applyしない。

- unknown base
- token mismatch
- WorldId change without explicit world transition
- incompatible regression
- protocol generation mismatch

必要な場合Gatewayへfull resyncを要求する。

### 4.2 atomic confirmed swap

confirmed state更新はView内で論理的にatomicにする。

render loopが半分old/半分newのstate containerをconfirmed snapshotとして読む構造を避ける。

large stateのphysical copy方式は固定せず、logical consistencyを要求する。

## 5. interpolation / prediction / reconcile

### 5.1 interpolation

confirmed samples間の表示補間は`PredictionStore`または専用presentation bufferで行う。

- world mutationを発生させない。
- render frame rateとSimulationStepを同一視しない。
- non-predictable fieldはhold/snap等のpresentation policyを選択できる。

### 5.2 local immediate feedback

Diver inputにはauthoritative result前にlocal feedbackを表示できる。

例:

- input accepted locally
- requested motion/action animation
- pending indicator

ただしGateway/Core rejectで必ずcorrection可能にする。

### 5.3 reconciliation

confirmed publication/result受信時:

1. relevant pending predictionをOperationId/semantic targetで関連付ける。
2. confirmed resultとの差を評価する。
3. presentation-only correctionを行う。
4. confirmed storeを最終表示truthとする。
5. correctionが完了したprediction entryを破棄する。

補正のvisual algorithm/thresholdはView Configに置けるがworld semanticsへ影響させない。

## 6. OperationController

Viewからworld-affecting requestを送る責務を一箇所に集約する。

```text
User intent
 -> local role/UI eligibility check
 -> stable Operation request create
 -> Gateway send
 -> pending tracking
 -> accepted/scheduled/terminal result
 -> UI/reconciliation
```

UI eligibility checkはUX最適化でありauthorization authorityではない。

### 6.1 local request state

```text
LOCAL_DRAFT
 -> SENT
 -> ACKED_OR_ACCEPTED
 -> PENDING_AUTHORITATIVE
 -> TERMINAL
```

transport failure:

```text
SENT/PENDING_AUTHORITATIVE
 -> DELIVERY_UNKNOWN
 -> same OperationId retry/status convergence
```

same logical retryでOperationId/immutable payloadを変更しない。

### 6.2 result retention

View local result cacheは有限でよい。

local expiry後にsame Operationをnew identityで再実行しない。必要ならGateway/Core status semanticsへ問い合わせる。

## 7. Diver participation

### 7.1 join preference

Viewはbroad preferenceの編集・送信UIを持てる。

- arbitrary resident takeover UIにしない。
- candidateが必ず存在するように見せない。
- server-confirmed binding resultだけをactive bindingへ反映する。

### 7.2 binding

原則1resident/1Diverのserver-side ruleを表示する。

Viewがduplicate tab/windowを検出しても、最終exclusive control enforcementはGateway/authoritative participation layer側で行う。

### 7.3 reconnect

same Diver identity/sessionの復帰を試みる。

- old binding projectionをblind trustしない。
- server-confirmed binding statusとcurrent resident stateを再取得する。
- resident death等でbinding継続不可の場合は通常participation flowへ戻る。

### 7.4 absence policy

不在時優先方針のediting stateとeffective confirmed policyを分離する。

simulation-affecting policy changeはstable OperationとしてGatewayへ送る。

- local save成功をworld effective successと表示しない。
- effective Step/resultをconfirmed後に反映する。
- disconnect reasonの違いだけで別policyを自動適用しない。

## 8. Three.js rendering責務

`ThreeRenderer`はThree.jsでfull-3D presentationを行う。

責務:

- scene graph presentation
- camera
- visible object lifecycle
- geometry/material/texture presentation
- full-3D underground/overhang等の描画
- frame scheduling
- render-side LOD/culling
- presentation animation

責務外:

- authoritative collision/physics
- world visibility ruleのauthoritative判定
- Entity identity生成
- world state mutation
- SimulationStep progression

render-side LODで表示しないEntityがworldから存在しなくなったと解釈しない。

## 9. SceneProjection

wire payloadを直接Three.js objectへ結合しない。

```text
ConfirmedWorldView
 + PredictedPresentationState
 + PresentationState
 -> SceneProjectionModel
 -> ThreeRenderer
```

これによりprotocol schema変更、render backend detail、prediction logicを分離する。

SceneProjectionModelはView component-local typeである。

## 10. queue / buffer

### 10.1 protocol receive queue

- state publication、Operation result、session/control messageをcategory分離可能にする。
- slow render frameがprotocol readをblockし続けない構造にする。

### 10.2 confirmed publication buffer

- continuity順を守る。
- renderが追いつかない場合intermediate presentation updateをcoalesce可能。
- base dependencyを壊すdeltaを勝手にskipしない。

### 10.3 render command queue

presentation-only。

- stale intermediate visual updateをcoalesce/drop可能。
- authoritative result/event notificationを同じlossy queueへ依存させない。

### 10.4 Operation send/retry queue

- stable OperationId保持。
- connection lossでpending requestをnew Operation化しない。
- session revoke時はnew sendsを止め、既送信Operationのauthoritative stateと区別する。

### 10.5 asset loading queue

world semanticsと独立したpresentation queue。

asset failureはplaceholder/degraded renderingへ移行できるが、confirmed World Stateを改変しない。

## 11. lifecycle

```text
STOPPED
 -> STARTING
 -> CONNECTING
 -> NEGOTIATING
 -> AUTHENTICATING
 -> SYNCING
 -> READY
 -> RESYNCING
 -> READY
 -> STOPPING
 -> STOPPED
```

追加状態:

- `DEGRADED_RENDERING`: asset/render機能の一部低下
- `SESSION_REVOKED`: world mutation input停止、再auth要求
- `INCOMPATIBLE`: protocol/Capability不整合でnormal session開始不可

SYNCING/RESYNCING中はold stateをcurrent confirmed-looking worldとして表示しない。

## 12. failure transition

| failure | 処理 |
|---|---|
| Gateway disconnect | RECONNECTING相当、prediction freeze/clear policy、same identity recovery |
| continuity mismatch | RESYNCING、delta apply停止、full rebuild要求 |
| session revoke | mutation input停止、confirmed public state accessはpermission次第 |
| Operation reject | local prediction reconcile/cancel、stable reason表示 |
| render exception | DEGRADED_RENDERING、protocol/sessionを可能な限り維持 |
| asset failure | placeholder/degraded presentation、world state不変 |
| protocol Major/Capability mismatch | INCOMPATIBLE、normal state apply禁止 |
| local cache corruption | cache破棄、Gatewayから再取得 |

## 13. backpressure

優先順位:

1. session/security/control
2. Operation terminal result
3. confirmed continuity
4. current confirmed state
5. pending Operation delivery
6. presentation freshness
7. assets/optional diagnostics

frame dropやrender coalesceは許容するが、confirmed continuityとOperation identityをlossyにしない。

## 14. View Config ownership

- render quality/LOD/culling presentation policy
- target frame/presentation pacing
- local interpolation/prediction policy
- reconcile visual threshold/duration
- local cache capacity
- asset loading concurrency/capacity
- reconnect operational timing
- UI/accessibility/localization preference defaults
- client observability limits

Gateway publication delay、Core Step rate、authorization policyをView Configで上書きしない。

## 15. observability

最低限:

- connection/session/sync lifecycle
- current confirmed world_id/basis_step
- continuity mismatch/resync count
- confirmed publication receive/apply latency
- render frame rate/frame drop
- scene object count/LOD statistics
- prediction count/correction count/magnitude category
- Operation pending/retry/reject/terminal counts
- asset queue/cache state
- protocol/version/Capability diagnostics

presentation metricをworld outcomeへ使用しない。

## 16. protocol対応

`mv.gateway-view` categoryと内部owner:

| category | internal owner |
|---|---|
| handshake/session | GatewayProtocolBoundary + SessionState |
| state publication | PublicationConsumer + ConfirmedWorldStore |
| resync | PublicationConsumer + ViewLifecycle |
| Operation request/result | OperationController |
| Diver binding/participation | DiverParticipation |
| role/revoke | SessionState + InteractionController |

## 17. 未確定だがblockerではない実装詳細

- exact Three.js version
- WebGL/WebGPU選択
- UI framework
- asset format/pipeline
- exact scene graph layout
- exact prediction algorithm
- reconcile animation
- browser/device support matrix
- exact join preference schema
- exact absence policy categories
- local persistence technology
