# 詳細設計 Phase 2: Gateway内部設計

Status: Complete / Phase 2 reviewed  
Tracking: Issue #14  
Parent: `docs/design/phase2-component-internal-design.md`

## 1. 目的

Gateway内部を、外部connection、authn/authz/session、confirmed state cache、logical publication buffer、Operation admission/aggregation、Master role、custody/retry/dedup、resyncを分離した責務として定義する。

Gatewayはauthoritative World Stateを所有しない。

## 2. 内部module境界

| module | 主責務 |
|---|---|
| `GatewayLifecycle` | startup/shutdown、Core connection、top-level readiness |
| `CoreProtocolBoundary` | `mv.core-gateway` negotiation、state/result受信、Core submit/status query |
| `PeerProtocolBoundary` | `mv.gateway-gateway` negotiation、batch/result/login proxy |
| `ViewProtocolBoundary` | `mv.gateway-view` connection/message境界 |
| `AdminProtocolBoundary` | `mv.gateway-admin-view` connection/message境界 |
| `SessionCoordinator` | General/Admin session、login proxy、reconnect association |
| `AuthorizationService` | role/permission/operation authorization decision |
| `ConfirmedStateCache` | Core-confirmed derived stateとcontinuity basis保持 |
| `PublicationBuffer` | logical delay window、coalesce、View publication |
| `OperationAdmission` | immutable operation context形成、basis/policy検証 |
| `LocalAggregator` | local operation grouping/mediation/local batch形成 |
| `MasterCoordinator` | current MasterGeneration追従、Master/non-Master role state |
| `CrossGatewayMerger` | Master時のbatch merge/mediation/final batch形成 |
| `CustodyStore` | SOURCE_HELD〜TERMINALのdelivery responsibility |
| `RetryCoordinator` | retry/status query/failover convergence |
| `ResultRouter` | Operation resultをsource session/Gatewayへrouting |
| `ResyncCoordinator` | Core reconnect/cache rebuild/publication gate |
| `GatewayConfigCoordinator` | Gateway Config ownership/apply |
| `Observability` | metrics/log/diagnostics |

Auth/session技術、storage、transportは固定しない。

## 3. state ownership

### 3.1 ConnectionState

protocol境界ごとにconnection-local stateを持つ。

```text
ConnectionState {
  connection_id,
  remote_component_identity,
  negotiated_version,
  negotiation_generation,
  capabilities,
  auth/session association,
  flow_state
}
```

ConnectionId/ComponentInstanceIdはOperation identityの代替にしない。

### 3.2 ConfirmedStateCache

保持可能な最低情報:

```text
ConfirmedCacheState {
  world_id,
  basis_step,
  state_continuity_token,
  confirmed_projection,
  applied_delta_base,
  cache_generation
}
```

- Core confirmed publicationだけをcacheへ昇格する。
- delta base mismatchでblind applyしない。
- cache lossはworld lossではない。
- cache内容からauthoritative Operation acceptanceを捏造しない。

### 3.3 SchedulingPolicyView

Coreから確認済みのscheduling policyと対応basisを保持する。

新規world-affecting Operation admissionでは、confirmed basisとpolicy generationが揃っていることを要求する。

### 3.4 CustodyState

Operation単位で最低限次を保持する。

```text
GatewayCustodyRecord {
  operation_id,
  operation_payload_digest,
  scheduling_admission,
  source_context,
  custody_state,
  current_batch_id,
  last_known_core_state,
  terminal_result_reference
}
```

`SOURCE_HELD -> MASTER_RECEIVED -> CORE_ACCEPTED -> TERMINAL` の意味を保持する。

Core acceptance不明の状態で唯一の再送可能copyを破棄しない。

## 4. General View request flow

```text
ViewProtocolBoundary
 -> SessionCoordinator
 -> AuthorizationService
 -> OperationAdmission
 -> LocalAggregator
 -> Master routing
 -> CrossGatewayMerger (Master only)
 -> CoreProtocolBoundary
 -> Core
```

### 4.1 boundary validation

- envelope/version/Capability/session stateを検証する。
- unauthorized/invalid requestをlocal stable resultでrejectする。
- wire payloadをinternal authorization modelへmappingしてから処理する。

### 4.2 OperationAdmission

world-affecting requestについて:

- stable OperationId/digestを検証する。
- confirmed `admission_basis_step` を固定する。
- Core配布`ConfigGeneration`のscheduling policyを固定する。
- requested not-before/deadlineをnormalizeする。
- candidate Stepはadvisoryとして計算可能だがeffective Stepにしない。

resync中でconfirmed basisがない場合は新規world-affecting admissionを行わない。

### 4.3 LocalAggregator

- Operation semantic identityを変更しない。
- local grouping windowはOPERATIONAL Config。
- network arrival順だけでcanonical orderを決めない。
- local external-request conflictをdeterministicに処理する。
- batch化はtransport aggregationでありtransaction化ではない。

## 5. Master / non-Master path

### non-Master

```text
local batch
 -> CustodyStore SOURCE_HELD
 -> current Masterへtransfer
 -> receipt ACK
 -> MASTER_RECEIVED
 -> Core acceptance/resultを待つ
```

### Master

```text
local + peer batches
 -> validate MasterGeneration
 -> CrossGatewayMerger
 -> final batch
 -> Core submit
 -> Core lifecycle/result tracking
 -> ResultRouter
```

Masterはworld ruleの正本ではない。

## 6. deterministic merge

`CrossGatewayMerger` は同じ有効Operation集合から同じlogical final batchを形成する。

merge inputに使用してよいもの:

- immutable Operation semantic fields
- stable conflict scope
- protocol-defined semantic priority
- Phase 1で定義されたstable identity/order fields

使用してはならないもの:

- socket arrival order
- peer response speed
- thread completion order
- current Master identity
- retry count

Coreのauthoritative `SameStepOrderKey`をGatewayが上書きしない。

## 7. retry / dedup / custody

### 7.1 Gateway-local duplicate

same OperationId + same digest:

- existing custody/lifecycleへattachする。
- new independent world mutationとして扱わない。

same OperationId + different digest:

- protocol mismatchとしてrejectする。

### 7.2 Core acceptance unknown

ACK loss/Master switch/Core reconnect時:

1. same OperationId/digest/contextを保持。
2. status queryまたはsame identity retry。
3. Core UNKNOWNならnormal deliveryへ戻る。
4. ACCEPTED/SCHEDULEDならcustodyをadvance。
5. TERMINALならstored resultへ収束。

### 7.3 retention

Gateway local retentionはCore world-lifetime tombstoneの代替ではない。

Gateway record expiry後もreplayed/retried OperationはCore dedupを通す。

## 8. Cache / publication pipeline

```text
Core confirmed state
 -> CoreProtocolBoundary
 -> continuity validation
 -> ConfirmedStateCache
 -> PublicationBuffer
 -> role/publication filtering
 -> ViewProtocolBoundary
```

### 8.1 publication buffer

約1秒を標準とするlogical bufferはStep/basis範囲として管理する。

- wall-clock sleepだけを意味しない。
- buffered stateのcontinuityを維持する。
- intermediate stateをcoalesce可能。
- Viewへconfirmedとpredictionを混同させるmetadataを生成しない。

### 8.2 stale cache

次の場合にnormal publicationを止める。

- WorldId mismatch
- continuity token mismatch
- unknown delta base
- Core recovery後のcontinuity再確認未完了
- protocol generation mismatchで安全に継続不能

`ResyncCoordinator`へ移行する。

## 9. resync lifecycle

```text
SYNCED
 -> SUSPECT
 -> RESYNCING
 -> SYNCED
```

### SUSPECT

continuity gap、basis anomaly、Core reconnect等を検出した状態。新規confirmed publicationを保留する。

### RESYNCING

- old cacheをcurrentとみなさない。
- Core current finalized basisを取得する。
- 必要ならcacheを全再構築する。
- connected Viewへsync stateを通知する。
- world-affecting Operationの新規admissionを安全なconfirmed basisが戻るまで拒否/保留する。

resync完了後にnormal publication/admissionへ復帰する。

## 10. Session / authn / authz

### 10.1 domain separation

General ViewとAdmin Viewは別permission domainとしてinternal namespace/stateも分ける。

General View AdministratorをAdmin permissionへpromotionしない。

### 10.2 login

connected Gatewayがlogin requestを受け、current Masterへproxyする。

- local non-Masterが独立finalizeしない。
- Master switch時にold Master authorityをblind reuseしない。
- reconnect associationとcredential validationを分離する。

### 10.3 authorization cache

authorization decisionをcacheする場合でも:

- explicit validity/version/effective pointを持たせる。
- privilege revoke後のnew Operationにold permissionを適用しない。
- auth backend障害をallow-by-defaultへしない。

## 11. Admin request flow

```text
AdminProtocolBoundary
 -> admin SessionCoordinator
 -> AuthorizationService
 -> request category validation
 -> target routing
```

Core simulation Admin Operationは通常のstable Operation/custody pathへ入れる。

Config/operational managementについてtarget component protocol routeが未確定な場合でも、Admin Viewからinternal object/direct Config fileへshortcutしない。

Phase 2の最終横断レビューにより、management target routingのcomponent-level ownerはGatewayとする。exact downstream payload/routeはprotocol Capabilityに従う後続詳細であり、未定義targetへdirect internal accessでfallbackしない。

## 12. queue設計

### 12.1 connection ingress queue

- per-connection/global admission limitを持てる。
- overload時はstable temporary error/RetryAdvice。
- unauthenticated floodがdownstream queueを占有しないようboundary admissionを分離する。

### 12.2 local operation queue

- authorization済み・normalized requestのみ。
- capacity pressureで既にaccepted/custody-held Operationをsilent dropしない。

### 12.3 custody retry queue

- lossless logical queue。
- durable/restore可能性はGateway availability目標に応じて実装するが、Master switch/restartでrequired custodyを失わない構成を要求する。

### 12.4 peer batch queue

Master時に受けるbatch queue。

- stale generationを早期rejectする。
- batch wrapper invalidはentry processing前にreject可能。
- arrival orderをmerge semantic orderにしない。

### 12.5 publication queue

- confirmed stateのみ。
- coalesce/drop policyを許容する。
- terminal Operation resultと同じlossy policyにしない。

### 12.6 result delivery queue

- OperationIdをresult identityとする。
- client disconnect中のresult retentionをpolicy化する。
- client-facing retention expiryしてもCore terminal semanticsを変えない。

## 13. lifecycle

```text
STOPPED
 -> STARTING
 -> CONNECTING_CORE
 -> RESYNCING
 -> READY_NON_MASTER | READY_MASTER
 -> DEGRADED
 -> STOPPING
 -> STOPPED
```

role change:

```text
READY_NON_MASTER
 <-> MASTER_TRANSITION
 <-> READY_MASTER
```

MasterGeneration確定前にMaster-only outputを送らない。

## 14. failure transition

| failure | state/処理 |
|---|---|
| Core disconnect | DEGRADED/RESYNCING、confirmed publication/admission gate |
| Master peer disconnect | generation authority待ち、same identity custody保持 |
| stale MasterGeneration | batch/output reject、contained Operationはterminal rejectにしない |
| cache continuity mismatch | RESYNCING、normal publication停止 |
| auth service unavailable | protected Operation deny/temporary unavailable、bypass禁止 |
| publication overload | coalesce、client側slow-consumer policy |
| custody store failure | new mutation admission制限、既存fact保全を優先 |
| peer merge processing failure | final batch未送信、partial semantic output禁止 |

## 15. backpressure priority

1. auth/security boundary
2. Operation identity/custody preservation
3. Core protocol/status convergence
4. confirmed cache continuity
5. terminal result delivery
6. publication freshness
7. optional diagnostics

slow ViewがCore Operation pathを無制限にblockしないようqueueを分離する。

## 16. Gateway Config ownership

- connection/session limits
- queue capacities/admission thresholds
- aggregation window/count/size
- retry/backoff/timeouts
- publication buffer duration
- cache retention/size
- resync thresholds
- Master health operational thresholds
- result retention
- observability/log limits

world scheduling semantics自体はCore-owned policyを使用し、Gateway Configで勝手に変更しない。

## 17. observability

最低限:

- Core/peer/View/Admin connection count/state
- current MasterGeneration/role
- confirmed cache basis_step/continuity state
- publication buffer depth/span/coalesce/drop
- resync count/duration/reason
- Operation admission/reject/duplicate counts
- local batch count/size
- peer batch queue depth
- custody state counts
- retry/status query counts
- stale generation rejects
- authn/authz success/reject/revoke counts
- result routing backlog
- ConfigGeneration

## 18. protocol対応

| protocol | internal primary modules |
|---|---|
| `mv.core-gateway` | CoreProtocolBoundary, ConfirmedStateCache, OperationAdmission, CustodyStore, ResyncCoordinator, MasterCoordinator |
| `mv.gateway-gateway` | PeerProtocolBoundary, CrossGatewayMerger, CustodyStore, RetryCoordinator, SessionCoordinator |
| `mv.gateway-view` | ViewProtocolBoundary, SessionCoordinator, AuthorizationService, PublicationBuffer, OperationAdmission, ResultRouter |
| `mv.gateway-admin-view` | AdminProtocolBoundary, SessionCoordinator, AuthorizationService, ResultRouter |

## 19. 未確定だがblockerではない実装詳細

- physical transport/serialization/compression
- credential/IdP/session storage product
- physical durable queue/store technology
- exact merge payload-specific fields
- exact queue capacities/timeouts/backoff
- cache representation
- publication full/delta encoding
- metrics/log backend

最終component間ownershipとcompletion判定は `phase2-cross-component-review.md` を正本とする。
