# Standard Protocol v1 Message Registry

Status: Canonical Standard MessageType → Protobuf payload mapping

## 1. 共通bootstrap

Handshake bootstrapでは`negotiation_generation=0`を許可し、normal message前にversion/capability negotiationを完了する。

| MessageType | Protobuf payload | SchemaId |
|---|---|---|
| `protocol.hello` | `ProtocolHelloV1` | `protocol.hello.v1` |
| `protocol.accept` | `ProtocolAcceptV1` | `protocol.accept.v1` |
| `protocol.reject` | `ProtocolRejectV1` | `protocol.reject.v1` |

## 2. `mv.core-gateway`

| Direction | MessageType | Protobuf payload | SchemaId | Context |
|---|---|---|---|---|
| G→C | `gateway.register` | `GatewayRegisterV1` | `protocol.gateway-register.v1` | none |
| G→C | `gateway.heartbeat` | `GatewayHeartbeatV1` | `protocol.gateway-heartbeat.v1` | optional world |
| C→G | `gateway.role-state` | `GatewayRoleStateV1` | `protocol.gateway-role-state.v1` | master generation |
| C→G | `master.generation.changed` | `MasterGenerationStateV1` | `protocol.master-generation-state.v1` | master generation |
| C→G | `world.scheduling-policy` | `OperationSchedulingPolicyWireV1` | `protocol.scheduling-policy.v1` | world + config generation |
| G→C | `operation.batch.submit` | `OperationBatchV1` | `protocol.operation-batch.v1` | world + batch operation context |
| C→G | `operation.batch.result` | `OperationBatchResultV1` | `protocol.operation-batch-result.v1` | world + batch context |
| G→C | `operation.status.query` | `OperationStatusQueryV1` | `protocol.operation-status-query.v1` | world |
| C→G | `operation.status.result` | `OperationStatusResultV1` | `protocol.operation-status-result.v1` | world |
| C→G | `world.state.begin` | `StatePublicationV1` | `protocol.state-publication.v1` | basis_step required |
| C→G | `world.state.chunk` | `StatePublicationChunkV1` | `protocol.state-publication-chunk.v1` | basis_step required |
| G→C | `world.state.resync-request` | `StateResyncRequestV1` | `protocol.state-resync-request.v1` | world |
| C→G | `component.health` | `ComponentHealthV1` | `protocol.component-health.v1` | optional world |

Production connection requires mTLS before protocol handshake. `gateway.register.gateway_logical_id` must match the authenticated Gateway certificate identity.

## 3. `mv.gateway-gateway`

| Direction | MessageType | Protobuf payload | SchemaId | Context |
|---|---|---|---|---|
| G↔G | `peer.heartbeat` | `PeerHeartbeatV1` | `protocol.peer-heartbeat.v1` | optional world + observed master generation |
| G→M | `gateway.batch.transfer` | `OperationBatchV1` | `protocol.operation-batch.v1` | world + master generation + batch context |
| M→G | `gateway.batch.ack` | `GatewayBatchAckV1` | `protocol.gateway-batch-ack.v1` | world + master generation + batch context |
| M→G | `operation.result.route` | `OperationStatusResultV1` | `protocol.operation-status-result.v1` | world + operation context |
| G↔M | `operation.status.forward` | `OperationStatusQueryV1` | `protocol.operation-status-query.v1` | world |
| G→M | `auth.login.proxy` | `AuthLoginProxyV1` | `protocol.auth-login-proxy.v1` | master generation |
| M→G | `auth.login.proxy-result` | `AuthLoginProxyResultV1` | `protocol.auth-login-proxy-result.v1` | master generation |
| G→M | `auth.login.assertion` | `AuthLoginAssertionV1` | `protocol.auth-login-assertion.v1` | master generation |
| M→G | `auth.login.result` | `AuthLoginResultV1` | `protocol.auth-login-result.v1` | optional world |
| G↔M | `auth.session.query` | `AuthSessionQueryV1` | `protocol.auth-session-query.v1` | none |
| G↔M | `auth.session.result` | `AuthSessionResultV1` | `protocol.auth-session-result.v1` | none |
| G↔M | `auth.session.revoke` | `AuthSessionRevokeV1` | `protocol.auth-session-revoke.v1` | none |
| M→G | `master.state` | `MasterGenerationStateV1` | `protocol.master-generation-state.v1` | master generation |

Production peer connection requires mTLS. Valid certificate identity does not itself grant Master authority; `MasterGeneration` remains authoritative.

## 4. `mv.gateway-view`

| Direction | MessageType | Protobuf payload | SchemaId | Context |
|---|---|---|---|---|
| V→G | `auth.login` | `AuthLoginBeginV1` | `protocol.auth-login-begin.v1` | none |
| G→V | `auth.login.begin-result` | `AuthLoginBeginResultV1` | `protocol.auth-login-begin-result.v1` | none |
| G→V | `auth.login.result` | `AuthLoginResultV1` | `protocol.auth-login-result.v1` | none |
| V→G | `auth.session.attach` | `AuthSessionAttachV1` | `protocol.auth-session-attach.v1` | none |
| G→V | `auth.session.changed` | `AuthSessionStateV1` | `protocol.auth-session-state.v1` | none |
| V→G | `world.subscribe` | `ViewSubscriptionRequestV1` | `protocol.view-subscription-request.v1` | world |
| G→V | `world.state.begin` | `StatePublicationV1` | `protocol.state-publication.v1` | basis_step required |
| G→V | `world.state.chunk` | `StatePublicationChunkV1` | `protocol.state-publication-chunk.v1` | basis_step required |
| V→G | `world.state.resync-request` | `StateResyncRequestV1` | `protocol.state-resync-request.v1` | world |
| V→G | `operation.submit` | `StandardOperationV1` | `protocol.standard-operation.v1` | operation context |
| G→V | `operation.result` | `OperationStatusResultV1` | `protocol.operation-status-result.v1` | operation context |
| V→G | `participation.binding.request` | `StandardOperationV1` | `protocol.standard-operation.v1` | world + operation context |
| G→V | `participation.binding.state` | `ParticipationBindingViewV1` | `protocol.participation-binding-view.v1` | basis_step |
| G→V | `component.resync-state` | `ResyncStateV1` | `protocol.resync-state.v1` | world |

Login bootstrap uses HTTPS BFF endpoints where redirect is required. Normal WebSocket message transport is TLS binary WebSocket at `/ws/v1/view`.

## 5. `mv.gateway-admin-view`

| Direction | MessageType | Protobuf payload | SchemaId | Context |
|---|---|---|---|---|
| A→G | `auth.login` | `AuthLoginBeginV1` | `protocol.auth-login-begin.v1` | none |
| G→A | `auth.login.begin-result` | `AuthLoginBeginResultV1` | `protocol.auth-login-begin-result.v1` | none |
| G→A | `auth.login.result` | `AuthLoginResultV1` | `protocol.auth-login-result.v1` | none |
| A→G | `auth.session.attach` | `AuthSessionAttachV1` | `protocol.auth-session-attach.v1` | none |
| G→A | `auth.session.changed` | `AuthSessionStateV1` | `protocol.auth-session-state.v1` | none |
| A→G | `component.health.query` | `HealthQueryV1` | `protocol.health-query.v1` | optional world |
| G→A | `component.health.result` | `ComponentHealthV1` | `protocol.component-health.v1` | optional world |
| A→G | `component.log.query` | `LogQueryV1` | `protocol.log-query.v1` | optional world |
| G→A | `component.log.page` | `LogPageV1` | `protocol.log-page.v1` | optional world |
| A→G | `config.read` | `ConfigReadRequestV1` | `protocol.config-read-request.v1` | optional world |
| G→A | `config.read.result` | `ConfigReadResultV1` | `protocol.config-read-result.v1` | optional world |
| A→G | `config.change` | `ConfigChangeRequestV1` | `protocol.config-change-request.v1` | operation context |
| G→A | `config.change.result` | `ConfigChangeResultV1` | `protocol.config-change-result.v1` | operation context |
| A→G | `operation.submit` | `StandardOperationV1` | `protocol.standard-operation.v1` | operation context |
| G→A | `operation.result` | `OperationStatusResultV1` | `protocol.operation-status-result.v1` | operation context |
| A→G | `operational.command` | `OperationalCommandV1` | `protocol.operational-command.v1` | operation context |
| A→G | `audit.query` | `AuditQueryV1` | `protocol.audit-query.v1` | optional world |
| G→A | `audit.page` | `AuditPageV1` | `protocol.audit-page.v1` | optional world |
| A→G | `admin.action.prepare` | `AdminActionPrepareV1` | `protocol.admin-action-prepare.v1` | operation context |
| G→A | `admin.action.plan` | `AdminActionPlanV1` | `protocol.admin-action-plan.v1` | operation context |
| A→G | `admin.action.confirm` | `AdminActionConfirmV1` | `protocol.admin-action-confirm.v1` | operation context |
| G→A | `admin.action.confirmed` | `AdminActionConfirmationV1` | `protocol.admin-action-confirmation.v1` | operation context |
| A→G | `admin.action.commit` | `AdminActionCommitV1` | `protocol.admin-action-commit.v1` | operation context |
| G→A | `admin.action.result` | `AdminActionResultV1` | `protocol.admin-action-result.v1` | operation context |
| A→G | `addon.inventory.query` | `AddonInventoryQueryV1` | `protocol.addon-inventory-query.v1` | optional world |
| G→A | `addon.inventory.result` | `AddonInventoryResultV1` | `protocol.addon-inventory-result.v1` | optional world |
| A→G | `addon.catalog.query` | `AddonCatalogQueryV1` | `protocol.addon-catalog-query.v1` | none |
| G→A | `addon.catalog.page` | `AddonCatalogPageV1` | `protocol.addon-catalog-page.v1` | none |

`audit.query`が`AuditPageV1`をrequest payloadとして使用する旧表記は誤りであり、本registryがsupersedeする。

High-impact `config.change` / `operational.command` / simulation Admin Operation / addon actionはdirect applyせず、`admin.action.prepare` → `admin.action.plan` → `admin.action.confirm` → `admin.action.confirmed` → `admin.action.commit` → `admin.action.result` を使用する。

Normal WebSocket message transport is TLS binary WebSocket at `/ws/v1/admin`.

## 6. Capability baseline

| ProtocolId | Required baseline capabilities |
|---|---|
| `mv.core-gateway` | `protocol.protobuf.v1`, `protocol.state-full.v1`, `protocol.operation-batch.v1`, `protocol.operation-status.v1` |
| `mv.gateway-gateway` | `protocol.protobuf.v1`, `protocol.operation-batch.v1`, `protocol.operation-status.v1`, `protocol.auth-bff.v1` |
| `mv.gateway-view` | `protocol.protobuf.v1`, `protocol.state-full.v1`, `protocol.auth-bff.v1`, `protocol.session-generation.v1`, `protocol.view-projection.v1` |
| `mv.gateway-admin-view` | `protocol.protobuf.v1`, `protocol.auth-bff.v1`, `protocol.session-generation.v1`, `protocol.admin-health.v1` |

`mv.gateway-admin-view`のrequired baselineはread-only health/bootstrapを成立させる最小集合であり、optional featureをsilent downgradeする意味ではない。各featureは次のCapabilityでgateする。

| Feature / message family | Required Capability |
|---|---|
| `component.health.*` | `protocol.admin-health.v1` |
| `component.log.*` | `protocol.admin-log-query.v1` |
| `config.*` | `protocol.admin-config.v1` |
| `operation.submit`, `operation.result`, `operational.command` | `protocol.admin-operation.v1` |
| `audit.*` | `protocol.admin-audit.v1` |
| `admin.action.*` | `protocol.admin-confirmation.v1` plus the underlying action capability |
| `addon.inventory.*`, `addon.catalog.*`, addon action | `protocol.admin-addon-management.v1` |

Required feature Capability不足は`protocol.capability-missing`で明示rejectし、high-impact actionをordinary direct pathへdowngradeしない。

## 7. Registry rule

Every normal Standard Protocol v1 message must resolve to exactly one row for its ProtocolId/direction/MessageType and exactly one schema type/schema id.

Unknown or mismatched standard message/payload is not generic pass-through.

Expected reject:

```text
protocol.unknown-message-type
protocol.payload-schema-mismatch
protocol.capability-missing
```

Registry change requires schema/version/Capability compatibility review and P4-08 fixture update.
