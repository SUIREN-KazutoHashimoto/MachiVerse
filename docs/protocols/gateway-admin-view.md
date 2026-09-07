# Gateway・Admin View間Protocol設計書

Status: Phase 4 implementation baseline aligned  
ProtocolId: `mv.gateway-admin-view`

## 1. 正本

本protocolのexternal boundary ownerはGatewayです。

実装時の優先順位:

1. `docs/protocols/schema/*.proto` — wire declaration
2. `docs/protocols/schema/message-registry-v1.md` — MessageType/payload mapping
3. `docs/design/phase4-protocol-payload-catalog.md` — payload semantics/Capability
4. `docs/design/phase4-auth-session-protocol.md` — auth/session/permission
5. 本書 — architecture-level boundary summary

本書からStandard Protocol v1へ未登録message/payloadを追加しません。

## 2. Transport / component boundary

Standard browser transport:

```text
TLS binary WebSocket
path: /ws/v1/admin
ProtocolId: mv.gateway-admin-view
serialization: Protocol Buffers
```

Admin Viewはconnected Gatewayとだけmanagement connectionを持ちます。

- component internal object/APIへdirect accessしない。
- other component Config fileをdirect read/writeしない。
- shared DTO/DLLをcommunication contractとして使用しない。
- Gatewayがmanagement target routingを所有する。
- Simulation Coreへ影響するOperationもGateway経由とする。

## 3. Common envelope / negotiation

`WireEnvelopeV1`と共通handshakeを使用します。

Admin required baseline Capability:

```text
protocol.protobuf.v1
protocol.auth-bff.v1
protocol.session-generation.v1
protocol.admin-health.v1
```

Message-specific Capability:

```text
component.log.query -> protocol.admin-log.v1
config.read/change   -> protocol.admin-config.v1
audit.query          -> protocol.admin-audit.v1
```

required Capability不足は`protocol.capability-missing`として扱い、silent downgradeしません。

MessageId/CorrelationIdはtrace identityでありcredentialやOperation dedup identityではありません。

## 4. Auth / session

Browser auth/sessionはPhase 4 profileに従います。

- OIDC + OAuth 2.0 Authorization Code + PKCE S256
- Gateway BFF
- upstream access/refresh tokenをbrowser JavaScriptへ露出しない
- opaque session cookie
- `/ws/v1/admin` Upgrade時にTLS/Origin/session/Admin auth domainを検証
- `auth.session.attach`でexpected session generationを確認

Login finalizationはMaster Gateway authority contractに従い、connected non-Master Gatewayが独立finalizeしません。

Canonical Admin permission registry:

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

General View Administratorから自動付与しません。UI availabilityだけでauthorizationを完結させず、Gatewayがprotected requestを認可します。

## 5. Canonical message set

Standard Protocol v1の`mv.gateway-admin-view` normal messageはMessage Registryを正本とし、次を使用します。

### Auth/session

```text
auth.login
auth.login.begin-result
auth.login.result
auth.session.attach
auth.session.changed
```

### Health/log

```text
component.health.query
component.health.result
component.log.query
component.log.page
```

### Config

```text
config.read
config.read.result
config.change
config.change.result
```

### Operation / command

```text
operation.submit
operation.result
operational.command
```

### Audit

```text
audit.query
audit.page
```

Unknown/mismatched standard messageをgeneric pass-throughしません。

## 6. Component health / metrics

Payload:

- request: `HealthQueryV1`
- result: `ComponentHealthV1`

Admin ViewはGatewayから公開されたcomponent health/status/metricsを参照します。

World basisを持つstatusはWorldContextを使用できます。Health/metrics presentationがWorld State authorityそのものではない点を維持します。

metric naming/cardinalityはPhase 4 observability contractへ従います。

## 7. Structured log

Payload:

- request: `LogQueryV1`
- result: `LogPageV1`

Phase 4 constraints:

- `page_size`: 1..1000、default 200
- cursor: 最大256 bytesのopaque operational token
- queryはworld mutationではない
- credential/token secretをStructuredLogRecord attributeへ出さない

LogQueryV1のcanonical fieldを越えるfilterをwire fieldが存在するものとして実装しません。追加filterが必要ならschema/design amendmentを先に行います。

## 8. Config read

Payload:

- request: `ConfigReadRequestV1`
- result: `ConfigReadResultV1`

- owner componentが公開可能なConfig projectionを返す。
- target component ConfigGenerationをpayloadで明示する。
- sensitive valueはPhase 4 Config公開policyに従いdefault非公開。
- Admin View/Gatewayによるother component Config file direct accessをstandard contractにしない。

## 9. Config change

Payload:

- request: `ConfigChangeRequestV1`
- result: `ConfigChangeResultV1`

Canonical semantics:

- stable OperationId
- immutable payload digest
- expected base ConfigGeneration
- change key canonical order / duplicate reject
- invalid setのatomic reject
- target owner validation
- simulation-affecting場合のauthoritative effective Step
- resulting ConfigGeneration / ConfigDigest

stale generationをsilent applyしません。

Permissionはchange classificationに応じて`admin.config.write.operational` / `admin.config.write.presentation` / `admin.config.write.simulation`をGatewayで要求します。

## 10. Operational command

Payload: `OperationalCommandV1`

- command kindはdefined/registered commandを指す。
- state-changing commandはOperationId / immutable payload digest required。
- `payload_schema_id/version`とcommand catalogを一致させる。
- MessageId/CorrelationIdをretry/dedup identityにしない。
- arbitrary shell/script/internal method invocationのgeneric escape hatchにしない。

Permissionはimpact classificationに応じて:

```text
admin.command.execute.low-impact
admin.command.execute.high-impact
```

Exact standard command catalogはPhase 4 wire schemaでは固定済みではなく、`ADMIN-03`とGateway implementation cross-reviewで確定します。

## 11. Simulation Admin Operation

Payload: `StandardOperationV1` via `operation.submit`、resultは`OperationStatusResultV1`です。

Required Admin permissionは`admin.operation.submit`です。

Responsibility:

- Gateway: Admin authn/authz、format、target、allowed condition、protocol admission
- Core: UI roleに依存しないWorld State invariant/state transition validation

Admin由来であることだけを理由にsimulation-affecting Operationをunconditional highest priorityにしません。

candidate Stepをauthoritative `effective_step`として扱わず、Core確定後のresultを使用します。

## 12. High-impact confirmation boundary

High-impact actionは追加confirmation/audit対象です。

Phase 4 Standard Protocol v1は`admin.action.*`等の専用message familyを登録していません。したがってimplementationは未登録wire messageを独自追加しません。

固定安全条件:

- high-impact commandは`admin.command.execute.high-impact`で認可する。
- simulation operationは`admin.operation.submit`で認可する。
- confirmation state/tokenをOperationIdまたはauthorization credentialの代替にしない。
- confirmation expiry後は再確認する。
- submit時にGateway authorizationを再度通す。
- ACK/ACCEPTEDとterminal effect successを区別する。

Confirmation UX/evidence transportの具体化は`ADMIN-04`で行い、Standard v1 wire変更が必要なら先にdesign amendment/schema/registry/acceptanceを更新します。

## 13. Audit

Payload:

- request: `AuditQueryV1`
- result: `AuditPageV1`

Permission: `admin.audit.read`。

Admin View local cacheをaudit authorityとせず、Gateway audit/target execution factを相関表示します。

AuditRecordへcredential/token secretを含めません。

## 14. Result / retry

共通`ResultV1` / `OperationStatusResultV1`に従います。

UI/handlerは少なくとも次をmachine-readable code/stateで区別します。

- accepted / pending
- terminal success / no-change
- authorization reject
- invalid target/request
- stale ConfigGeneration
- duplicate / identity mismatch
- temporarily unavailable / resync
- version/Capability mismatch
- target invariant reject
- internal failure

Diagnostic textの文字列比較でcontrol flowを決めません。

## 15. Pause / reconnect

- Pause中のsimulation-affecting requestをstopped Stepへ曖昧適用しない。
- retry/reconnectでstable Operation identityを変更しない。
- reconnect時はProtocol/Capability/session generationを再確認する。
- severe session revoke後はnew protected requestを停止する。

## 16. Addon boundary

Standard Protocol v1のcurrent `mv.gateway-admin-view` Message RegistryにAddon install/update/disable/remove messageはありません。

Addonについてはarchitecture上、compatibility/safety metadataの将来交換を許容しますが、Addon-specific functional payloadやgeneric extension areaをStandard Protocolへ載せません。

Current production work `ADMIN-01..ADMIN-04` でAddon management wire/APIを先行実装しません。

## 17. Forbidden

- General View AdministratorのAdmin permissionへのautomatic promotion
- Admin View→Core direct connection
- component internal API/filesystem/Config fileへのdirect fallback
- UI-only authorization
- raw OAuth tokenのbrowser JavaScript露出
- MessageId/CorrelationIdをOperation dedup identityにすること
- stale ConfigGeneration silent overwrite
- candidate Stepをauthoritative effective Stepとして扱うこと
- required Capability不足のsilent degradation
- unregistered standard message/payloadの独自追加
- generic arbitrary command/addon extension channel
- generic Undoによるhistory消去
- ACK/acceptedをterminal successと同一視すること

## 18. Implementation mapping

- `ADMIN-01`: Gateway protocol client/auth/session foundation
- `ADMIN-02`: health/metrics/log/audit presentation
- `ADMIN-03`: Config/operational command management
- `ADMIN-04`: high-impact/simulation Admin Operation

実装順・依存関係は`docs/roadmap/administration-view.md`と`phase4-implementation-work-breakdown.md`を正本とします。
