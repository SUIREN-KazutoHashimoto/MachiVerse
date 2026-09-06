# 詳細設計 Phase 2: Admin View内部設計

Status: In Progress  
Tracking: Issue #14  
Parent: `docs/design/phase2-component-internal-design.md`

## 1. 目的

Admin View内部を、operator session/permission projection、health/metrics/log、Config management、operational command、simulation Admin Operation、high-impact confirmation、audit presentationへ責務分離する。

Admin Viewは管理対象componentのinternal mutable stateやConfig fileへ直接アクセスしない。

## 2. 内部module境界

| module | 主責務 |
|---|---|
| `AdminLifecycle` | startup、Gateway接続、login、sync、shutdown |
| `GatewayProtocolBoundary` | `mv.gateway-admin-view` negotiation、encode/decode |
| `AdminSessionState` | separate admin auth/session/permission projection |
| `TargetCatalog` | protocol上到達可能なcomponent/management targetのprojection |
| `HealthDashboardModel` | health/status/metrics presentation model |
| `LogQueryController` | structured log query/stream state |
| `ConfigManagementController` | Config read/change draft/validation/result tracking |
| `CommandController` | operational command request/result tracking |
| `SimulationAdminOperationController` | world-affecting Admin Operation作成/追跡 |
| `HighImpactConfirmation` | high-impact confirmation workflow state |
| `AuditViewModel` | audit query/correlation/presentation |
| `AddonManagementProjection` | addon compatibility/status UI境界。install機能自体は未確定 |
| `PresentationState` | filter/sort/panel/layout/local draft |
| `AdminConfigCoordinator` | Admin View-owned Config |
| `Observability` | Admin View自身のmetrics/log |

## 3. security domain

Admin Viewのsession/permission stateはGeneral View roleと別namespaceとして扱う。

```text
AdminSessionProjection {
  session_state,
  permission_generation_or_version,
  allowed_management_categories,
  effective_context
}
```

- General View AdministratorをAdmin sessionへ自動変換しない。
- UI button visibilityはauthorization authorityではない。
- actual requestはGatewayで再認可される。
- severe revoke受信後はnew protected requestを停止する。

## 4. management target model

Admin Viewはcomponent internal address/objectをtargetとして保持しない。

```text
ManagementTargetProjection {
  target_id,
  component_kind,
  component_instance_reference,
  reachable_capabilities,
  lifecycle_state,
  protocol_status
}
```

TargetCatalogはGateway/protocolから確認されたtargetだけをcurrentとして表示する。

component management reachabilityの最終routing方式が今後変わっても、Admin Viewのinternal controllerはprotocol target abstractionへ依存する。

## 5. health / status / metrics flow

```text
GatewayProtocolBoundary
 -> target/status validation
 -> HealthDashboardModel
 -> presentation
```

health stateはauthoritative World Stateと同一ではない。

World basisを持つmetric/statusは可能な場合:

- WorldId
- basis_step
- MasterGeneration
- ConfigGeneration

等をcontextとして保持する。

異なるbasisのmetricsを同一瞬間のauthoritative snapshotとして誤表示しない。

## 6. structured log

`LogQueryController` はquery definitionとresult windowを分ける。

```text
LogQueryDraft {
  targets,
  time_or_step_range,
  severity_filter,
  operation_id,
  batch_id,
  correlation_id,
  master_generation,
  text_or_field_filters
}
```

- diagnostic logとaudit recordを同一retention前提にしない。
- client側cache expiryはserver-side audit/history削除を意味しない。
- query failureでmanagement authorizationを緩めない。

## 7. Config management

### 7.1 read model

```text
ConfigTargetProjection {
  target,
  schema_version,
  config_generation,
  config_digest_reference,
  fields,
  classifications,
  validation_state
}
```

Admin Viewが対象componentのConfig file path/contentをdirect source of truthとして扱わない。

### 7.2 edit draft

```text
ConfigChangeDraft {
  target,
  base_config_generation,
  edits,
  local_validation_state,
  confirmation_state
}
```

local validationはUX補助であり、owner component validationを置き換えない。

### 7.3 submit flow

```text
ConfigChangeDraft
 -> normalize presentation input
 -> stable Operation/change request create
 -> Gateway authorization
 -> target component validation
 -> accepted/pending
 -> effective apply/result
 -> refresh ConfigTargetProjection
```

expected base ConfigGenerationを必ず保持し、stale stateへのsilent overwriteを避ける。

simulation-affecting changeはCore確定effective Step/resultが返るまで「適用済み」と表示しない。

### 7.4 no generic Undo

UIにgeneric history erasure/rollback semanticsを持たせない。

「元の値へ戻す」はcurrent generationをbaseにしたnew ConfigChangeDraftとして作成する。

## 8. operational command

`CommandController`はprotocol-defined command metadataからrequest UIを形成できる。

最低限internal command descriptor:

```text
CommandDescriptor {
  command_type,
  target_kind,
  parameter_schema_reference,
  permission_requirement,
  impact_classification,
  idempotency_requirement,
  confirmation_classification
}
```

command catalogがprotocol/config metadataで供給されるか静的に実装されるかは後続で決めるが、undefined arbitrary internal method invocationを許可しない。

system/world stateへ影響するcommandはstable OperationIdを持つ。

## 9. Simulation Admin Operation

world-affecting requestは専用controllerから通常のOperation semanticsへmappingする。

```text
Admin intent
 -> local permission/UI check
 -> HighImpactConfirmation if required
 -> stable Operation request
 -> Gateway
 -> Core scheduling/validation
 -> accepted/scheduled/terminal
 -> Audit/Result presentation
```

Admin由来というだけでsame-Step orderingやworld invariantをbypassしない。

candidate Stepをauthoritative effective Stepとして表示しない。

## 10. HighImpactConfirmation

high-impact判定対象の例:

- world destruction
- mass state change
- time control
- large simulation-affecting Config change

具体categoryは後続command/permission matrixで確定する。

内部state:

```text
NOT_REQUIRED
 | REQUIRED
 | CONFIRMING
 | CONFIRMED
 | EXPIRED_OR_INVALID
```

confirmation state/tokenをOperationIdやauthorization credentialの代替にしない。

confirmation後もsubmit時にGateway authorizationを通す。

multi-person approvalが採用される場合も同じrequest identityへapproval evidenceを関連付け、別requestへの使い回しを禁止する設計とする。

## 11. Audit presentation

AuditViewModelでは少なくとも次を相関できる。

- actor reference
- OperationId / request identity
- CorrelationId
- target
- operation type
- requested content summary
- requested time
- effective SimulationStep/boundary
- ConfigGeneration
- result status/code
- reject reason

Admin View local historyはauthoritative audit storeではない。

local cacheを消してもserver-side audit factは変化しない。

## 12. Addon management boundary

Phase 2ではAddon managementを次に限定する。

- installed/known addon compatibility metadata表示
- version/Capability/dependency mismatch表示
- target component startup safety state表示
- official/third-party trust classification表示可能なUI境界

standard Admin Viewがinstall/update/disable/removeを直接実行するかはまだ確定しない。

未確定のinstall機能をgeneric arbitrary file upload/internal code loading APIとして先行実装しない。

## 13. queue設計

### 13.1 protocol receive queue

- session/control、health、log、Config result、Operation resultをcategory分離可能。
- high-volume metrics/logがpermission revokeやterminal resultをstarveしない。

### 13.2 metrics update queue

presentation-only最新値はcoalesce可能。

historical audit/resultと同じlossy policyにしない。

### 13.3 log result buffer

- bounded window/pagination/stream flow controlを設ける。
- overload時はqueryをnarrow/continue token等で制御可能。
- client memory pressureを理由にserver audit deletionを要求しない。

### 13.4 mutation request queue

Config change/command/Admin Operationを保持する。

- stable request/OperationIdを維持する。
- reconnect retryでnew identityへ変更しない。
- session revoke後はnew mutation送信を停止する。

### 13.5 audit query queue

read-only query pathをmutation request pathと分離し、large audit queryがcritical mutation result processingをblockしない。

## 14. lifecycle

```text
STOPPED
 -> STARTING
 -> CONNECTING
 -> NEGOTIATING
 -> AUTHENTICATING
 -> SYNCING_TARGETS
 -> READY
 -> DEGRADED
 -> STOPPING
 -> STOPPED
```

追加state:

- `SESSION_REVOKED`
- `INCOMPATIBLE`
- `READ_ONLY_DEGRADED`（mutation path unavailableだが安全な参照だけ可能な場合）

read-only degradedへ入る条件はpermission/securityを緩めない範囲に限定する。

## 15. failure transition

| failure | 処理 |
|---|---|
| Gateway disconnect | mutation request delivery unknownを保持、reconnect後status収束 |
| session revoke | new protected mutation停止、再auth |
| permission mismatch | request local disable + Gateway rejectを正本として表示 |
| Config stale generation | current projection refresh、silent overwrite禁止 |
| target unavailable | temporary unavailable、request identity保持 |
| metrics stream overload | coalesce/sample、mutation/result path優先 |
| log query overload | query narrowing/pagination/flow control |
| high-impact confirmation expiry | new confirmation要求、OperationId代替にしない |
| protocol mismatch | INCOMPATIBLE、normal management禁止 |

## 16. backpressure priority

1. auth/session/revoke
2. mutation request identity/result
3. high-impact confirmation integrity
4. Config generation consistency
5. audit result correctness
6. health current state
7. metrics freshness
8. diagnostic log volume

metrics/log volumeでConfig/Admin Operation resultをdropしない。

## 17. Admin View Config ownership

- dashboard refresh/presentation interval
- local metrics history/cache capacity
- log query page/window limits
- client-side request timeout presentation
- confirmation UX timeout/display policy
- local filter/layout/accessibility settings
- observability/log limits

対象componentのretry、Config、permission、audit retention policyをAdmin View Configで上書きしない。

## 18. observability

Admin View自身について:

- Gateway connection/session state
- protocol/version/Capability
- target catalog count/reachability
- metrics/log receive backlog
- Config draft/submit/result counts
- stale generation rejects
- command/Admin Operation pending/retry/result counts
- high-impact confirmation count/expiry
- audit query latency/error
- local cache/resource pressure

operator action auditのauthorityはserver-side protocol対象であり、client metricだけに依存しない。

## 19. protocol対応

`mv.gateway-admin-view` categoryとinternal owner:

| category | internal owner |
|---|---|
| handshake/session | GatewayProtocolBoundary + AdminSessionState |
| component health/status | TargetCatalog + HealthDashboardModel |
| metrics/log | HealthDashboardModel + LogQueryController |
| Config read/change | ConfigManagementController |
| operational command | CommandController |
| simulation Admin Operation | SimulationAdminOperationController |
| high-impact confirmation context | HighImpactConfirmation |
| audit | AuditViewModel |
| addon compatibility metadata | AddonManagementProjection |

## 20. 未確定だがblockerではない実装詳細

- Admin credential/IdP/session technology
- exact permission/operation matrix
- exact health/metrics/log schemas
- log collector/search backend
- exact command catalog
- high-impact category/multi-person approval UX
- audit backend/retention
- component management reachability routing
- addon installation standardization
- UI framework
