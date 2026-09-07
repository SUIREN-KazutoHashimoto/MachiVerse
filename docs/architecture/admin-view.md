# Admin View設計

Status: Phase 4 implementation baseline aligned

## 1. 目的

Admin ViewはMachiVerse各componentを運用・診断・設定するsystem operator向けUIです。

General ViewのAdministratorを含む利用者roleとは別auth/authz domainであり、General View権限をAdmin View permissionへ自動昇格しません。

実装baselineはPhase 4詳細設計を正本とします。

- `docs/design/phase2-admin-view-internal-design.md`
- `docs/design/phase4-auth-session-protocol.md`
- `docs/design/phase4-protocol-payload-catalog.md`
- `docs/design/phase4-implementation-work-breakdown.md`
- `docs/design/phase4-test-acceptance.md`
- `docs/protocols/gateway-admin-view.md`
- `docs/protocols/schema/`
- `docs/roadmap/administration-view.md`

## 2. Runtime / external boundary

Standard Admin View runtimeはstandalone Blazor WebAssembly / `net10.0`です。

Gateway boundary:

```text
Admin View
  -> TLS binary WebSocket /ws/v1/admin
  -> Protocol Buffers / mv.gateway-admin-view
  -> Gateway
  -> authoritative owner/component
```

Admin Viewは次へ直接依存しません。

- Simulation Core connection
- target component internal object/API
- target component Config file
- target component database/filesystem
- production DLL/shared internal DTO

External management入口とtarget routingのownerはGatewayです。target unavailable時にdirect internal accessへfallbackしません。

## 3. Auth / session / permission domain

Browser authentication profileはPhase 4 auth/session contractに従います。

- OpenID Connect Core 1.0
- OAuth 2.0 Authorization Code Grant
- PKCE `S256`
- Gateway BFF
- browser JavaScriptへaccess token/refresh tokenを露出しない
- opaque Gateway session cookie
- WebSocket attach時のOrigin/session/auth-domain/session-generation検証

Admin View standard permission registry:

```text
admin.health.read
admin.metrics.read
admin.log.read
admin.config.read
admin.config.write.operational
admin.config.write.presentation
admin.config.write.simulation
admin.command.execute.low-impact
admin.command.execute.high-impact
admin.operation.submit
admin.audit.read
admin.session.read
admin.security.revoke-session
```

- General View Administratorから上記permissionを自動付与しない。
- permission change/revokeごとにsession generationを更新する。
- old session generationでnew protected requestをadmitしない。
- UI button visibilityはauthorization authorityではなく、Gatewayがrequestを再認可する。
- auth/authorization outage時にpermission checkをbypassしない。

## 4. Internal responsibility model

Phase 2で確定した主module:

- `AdminLifecycle`
- `GatewayProtocolBoundary`
- `AdminSessionState`
- `TargetCatalog`
- `HealthDashboardModel`
- `LogQueryController`
- `ConfigManagementController`
- `CommandController`
- `SimulationAdminOperationController`
- `HighImpactConfirmation`
- `AuditViewModel`
- `AddonManagementProjection`
- `PresentationState`
- `AdminConfigCoordinator`
- `Observability`

各controllerはprotocol projectionへ依存し、component internal implementationへ依存しません。

## 5. Health / metrics

Admin Viewは`component.health.query` / `component.health.result`とPhase 4 observability contractに基づき、Gatewayから公開されたhealth/status/metricsを表示します。

主なpresentation対象:

- component/target identity and reachability
- health/readiness
- Core current Simulation Step / lag
- Master Gateway identity / MasterGeneration
- Gateway resync state
- protocol / Capability mismatch
- ConfigGeneration / validation state
- retry/dedup diagnostic
- save/recovery state
- CPU / memory / connection等のoperational metrics

異なるbasisのmetricを同一authoritative snapshotとして誤表示しません。health state自体をWorld Stateと同一視しません。

## 6. Structured log

`component.log.query` / `component.log.page`を使用します。

Phase 4 payload contractではtarget、time range、event kind、CorrelationId、OperationId、basis Step、page/cursorを扱います。表示側はStructuredLogRecordのseverity、BatchId、SimulationStep等を相関できます。

- diagnostic logとsecurity/management auditを同一authority/retentionとして扱わない。
- credential/token/secretをUI側で復元しない。
- high-volume log/metricsでsession revokeやmutation resultをstarveしない。
- bounded result window/paginationを使用する。

## 7. Config read / change

### 7.1 Read

Admin Viewはtarget Config fileを直接開かず、`config.read` / `config.read.result`によるprojectionを表示します。

保持するread modelには少なくとも次を含めます。

- target
- ConfigGeneration
- ConfigDigest reference
- field values permitted for disclosure
- impact/mutability classification
- validation state

sensitive valueはPhase 4 Config公開policyに従いdefault非公開です。

### 7.2 Change

`config.change` / `config.change.result`を使用します。

- stable OperationId / immutable payload digest
- expected base ConfigGeneration
- normalized change set
- owner-side type/range/cross-constraint validation
- atomic change-set semantics
- simulation-affecting場合のauthoritative effective Step
- resulting ConfigGeneration / digest

stale ConfigGenerationをsilent overwriteしません。invalid setをpartial apply前提で扱いません。

Generic Undoは提供しません。元の値へ戻す場合もcurrent generationをbaseにしたnew change requestとします。

## 8. Operational command

`operational.command`はregistered/defined commandだけをrequestします。

`CommandController`は少なくとも次のmetadataを扱います。

```text
command type
target kind
parameter schema reference
required permission
impact classification
idempotency requirement
confirmation classification
```

state-changing commandはOperationId / immutable payload digestを持ちます。MessageId / CorrelationIdをdedup identityに使用しません。

Exact command catalogはPhase 4でwire schemaとして固定せず、`ADMIN-03`とGateway実装のcross-reviewで確定します。arbitrary internal method、shell、scriptをgeneric commandとして実行できる設計にしません。

## 9. Simulation Admin Operation

Worldへ影響するAdmin OperationはAdmin View→Gateway→Simulation Core pathを使用します。

Gateway:

- Admin authentication/authorization
- operation format
- target
- Admin operationとしてのallowed condition
- protocol validation

Simulation Core:

- UI roleを解釈しない
- World State invariant / reference consistencyを維持する
- deterministic scheduling/state-transition contractを維持する

Admin由来という理由だけでsimulation-affecting Operationを無条件最優先にしません。candidate Stepをauthoritative effective Stepとして表示せず、Core確定resultを待ちます。

## 10. High-impact confirmation

World destruction、mass state change、time control、大規模simulation-affecting Config change等のhigh-impact actionは追加確認とaudit対象です。

Phase 4で確定している安全条件:

- high-impact command authorizationには`admin.command.execute.high-impact`等の対応permissionを使用する。
- simulation Admin Operationは`admin.operation.submit`をGatewayで認可する。
- confirmation state/tokenをOperationIdまたはauthorization credentialの代替にしない。
- confirmation後もsubmit時にGateway authorizationを通す。
- confirmation expiry後は再確認を要求する。
- ACK / acceptedをterminal effect successと表示しない。

Exact confirmation UX/evidence representationはStandard Protocol v1へ新しいmessage familyを追加せず、`ADMIN-04`実装時に既存Protocol/BFF/session契約と整合させて確定します。wire変更が必要になった場合は実装より先にdesign amendmentを行います。

## 11. Audit / no generic Undo

Admin View local historyをauthoritative audit storeとしません。

Audit presentationは少なくとも次を相関します。

- actor reference
- OperationId / request identity
- CorrelationId
- target
- operation type
- requested content summary
- request time
- effective SimulationStep/boundary
- ConfigGeneration
- result status/code
- reject reason

Gateway側authorization/routing factとtarget component execution factを相関表示します。local cache削除でserver-side audit factを消しません。

## 12. Pause / reconnect / failure

- Pause中にsimulation-affecting Operationをstopped Stepへ曖昧applyしない。
- reconnect retryでstable request/OperationIdを別identityへ変更しない。
- Gateway disconnect時のdelivery-unknown stateはstatus convergenceで解決する。
- session revoke後はnew protected mutationを停止する。
- Config stale generationではprojection refreshを要求しsilent overwriteしない。
- protocol mismatch時は`INCOMPATIBLE`としてnormal managementを停止する。

## 13. Addon boundary

Addon compatibility/status presentationは将来拡張のUI boundaryとして維持します。

現在のPhase 4 production work package `ADMIN-01..ADMIN-04` にはAddon install/update/disable/removeのstandard implementationは含まれていません。

Phase 2で確定している範囲:

- installed/known Addon compatibility metadata表示
- version/Capability/dependency mismatch表示
- target startup safety state表示
- official/third-party trust classificationを表示可能なboundary

未確定のAddon install機能をgeneric file upload/internal code loading APIとして先行実装しません。詳細は`addon-boundary-safety.md`に従います。

## 14. Protocol / Capability error display

Admin Viewはstable result/error codeに基づき少なくとも次を診断できるようにします。

- protocol version mismatch
- required Capability missing
- auth/session stale/revoke
- authorization reject
- Config stale/invalid
- target unavailable/resyncing
- Operation pending/terminal failure

Machine behaviorをdiagnostic textの文字列比較へ依存させません。

## 15. Implementation roadmap

Current standard implementation work:

| Work ID | Scope |
|---|---|
| `ADMIN-01` | Admin View scaffold / Gateway protocol client |
| `ADMIN-02` | Health / metrics / log / audit UI |
| `ADMIN-03` | Config / operational command management |
| `ADMIN-04` | High-impact / simulation Admin Operation |

Implementation order/dependencyは`docs/roadmap/administration-view.md`と`phase4-implementation-work-breakdown.md`を正本とします。

Phase番号ベースで同内容を再設計せず、Phase 4からimplementation workへ移行します。
