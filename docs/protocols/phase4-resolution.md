# Standard Protocol v1 Phase 4 Resolution

Status: Complete  
Tracking: Issue #17

## 1. 目的

本書は `docs/protocols/*.md` に残るPhase 1/2時点の未決定事項をPhase 4の最終設計へ同期し、Standard Protocol v1のtransport、serialization、authentication、schema、code generation境界を一意にする。

既存のboundary文書はsemantic contractとして有効である。ただし各文書末尾の「詳細設計へ残す事項」「component実装へ残す事項」のうち、本書でResolvedとした項目はhistorical handoff記録として扱う。

## 2. Standard boundary profile

| ProtocolId | Standard transport | Serialization | Production authentication |
|---|---|---|---|
| `mv.core-gateway` | HTTP/2 gRPC bidirectional streaming | Protocol Buffers proto3 | mutual TLS |
| `mv.gateway-gateway` | HTTP/2 gRPC bidirectional streaming | Protocol Buffers proto3 | mutual TLS |
| `mv.gateway-view` | TLS WebSocket binary | Protocol Buffers proto3 | Gateway BFF session / OIDC bootstrap |
| `mv.gateway-admin-view` | TLS WebSocket binary | Protocol Buffers proto3 | Gateway BFF session / OIDC bootstrap |

Internal logical service:

```proto
rpc Connect(stream WireEnvelopeV1) returns (stream WireEnvelopeV1);
```

External standard paths:

```text
/ws/v1/view
/ws/v1/admin
```

WebSocket text frameをnormal protocol transportとして使用しない。

## 3. Compression

Required baseline:

```text
NONE
```

Negotiated optional capability:

```text
wire.gzip.v1
```

Transport implementationがgRPC compressionやWebSocket compressionをoperational optimizationとして利用してもよいが、Protocol Capability/world semanticsをsilentに変更しない。

## 4. Exact schema source

Version-controlled schema source:

```text
docs/protocols/schema/common.proto
docs/protocols/schema/auth.proto
docs/protocols/schema/payloads.proto
```

Message registry:

```text
docs/protocols/schema/message-registry-v1.md
```

Wire declarationの正本は`.proto`である。

Semantic validation、context requiredness、stable token validation、fixed-length ID、authority、retry/dedup、security、compatibilityの正本はPhase 4 design文書である。

## 5. Component-local code generation

各componentは同一version-controlled `.proto` sourceから自身のbuild内でcode generationする。

許可:

- componentごとのgenerated namespace/package。
- language/runtimeに応じたlocal wrapper。
- compile-time generator/tooling差異。ただし同じwire schemaへ一致すること。

禁止:

- generated shared DTO DLL/packageをcomponent independenceの代替にすること。
- generated sourceを手編集して`.proto`と乖離させること。
- `.proto`に存在しないfield numberをlocal forkすること。

Exact protoc/generator patch versionはpackage/tool lockへpinし、schema digestをbuild provenanceへ含める。

## 6. Internal component authentication

`mv.core-gateway` / `mv.gateway-gateway` は `docs/design/phase4-internal-component-auth-profile.md` を正本とする。

Productionではmutual TLS required。

- Core identity: `urn:machiverse:component:simulation-core`
- Gateway identity: `urn:machiverse:component:gateway:<gateway-logical-id>`
- Gateway certificate identityとmessage上の`GatewayLogicalId`を一致検証する。
- certificate identityはMaster authorityではない。
- Master authorityはCore-issued `MasterGeneration` / role stateを正本とする。
- `ComponentInstanceId`はcredentialではない。

## 7. Browser authentication/session

`mv.gateway-view` / `mv.gateway-admin-view` は `docs/design/phase4-auth-session-protocol.md` を正本とする。

Standard profile:

- OpenID Connect。
- OAuth 2.0 Authorization Code。
- PKCE S256。
- Gateway BFF。
- browser JavaScriptへaccess/refresh tokenを公開しない。
- opaque secure HttpOnly session cookie。
- WebSocket Upgrade時にTLS、Origin、session、auth domainを検証する。

General ViewとAdmin Viewのauth/permission domainを分離する。

## 8. State publication

FULL/DELTAのwire contractは次を正本とする。

- `StatePublicationV1`
- `StatePublicationChunkV1`
- `ProjectionChunkPayloadV1`
- `ProjectionRecordV1`
- `StateResyncRequestV1`

Rules:

- FULLはbase continuity tokenなし。
- DELTAはbase continuity token required。
- base mismatchはblind applyせずresync。
- chunk digest検証完了前にconfirmed stateとしてinstallしない。
- View predictionへconfirmed continuity tokenを付けない。

## 9. Gateway registration / heartbeat / role

Core↔Gateway:

- `gateway.register` → `GatewayRegisterV1`
- `gateway.heartbeat` → `GatewayHeartbeatV1`
- `gateway.role-state` → `GatewayRoleStateV1`

Gateway↔Gateway:

- `peer.heartbeat` → `PeerHeartbeatV1`

Heartbeat/electionのarrival timingをworld orderingへ使用しない。

## 10. Operation / Batch

Operation/Batch schemaは次を正本とする。

- `StandardOperationV1`
- `OperationBatchV1`
- `GatewayBatchAckV1`
- `OperationBatchResultV1`
- `OperationStatusQueryV1`
- `OperationStatusResultV1`

Same logical Operation retryはOperationId、immutable payload digest、scheduling admission contextを維持する。

Master receipt ACKをCore durable acceptanceと同一視しない。

## 11. Admin management payload

Admin health/log/config/audit payloadは `payloads.proto` と `phase4-protocol-payload-catalog.md` を正本とする。

Audit registryの正しいpair:

```text
Admin View -> Gateway: audit.query / AuditQueryV1
Gateway -> Admin View: audit.page / AuditPageV1
```

旧 `phase4-protocol-schema.md` にある `audit.query -> protocol.audit-page` はsuperseded typoとする。

## 12. 旧未決定事項 resolution

| Historical handoff item | Resolution |
|---|---|
| concrete network transport | Resolved: gRPC bidi internal / TLS WebSocket binary external |
| serialization | Resolved: Protocol Buffers proto3 |
| compression | Resolved: NONE baseline, gzip optional capability |
| protocol-specific payload schema | Resolved: Phase 4 payload catalog + `.proto` |
| state FULL/DELTA strategy | Resolved: publication/chunk/projection schema |
| browser auth credential/session technology | Resolved: OIDC Authorization Code + PKCE + BFF session |
| internal component authentication | Resolved: production mTLS + service identity binding |
| role/permission matrix | Resolved: Phase 4 auth/session protocol |
| Gateway identity | Resolved semantically: GatewayLogicalId + certificate identity binding |
| heartbeat/role payload | Resolved: Phase 4 payload catalog |
| Admin health/log/config/audit schema | Resolved: Phase 4 payload catalog + `.proto` |
| schema tooling/code generation policy | Resolved: same `.proto` source, component-local generation |
| timeout/backoff values | Config-owned implementation value, semantics already fixed |
| durable queue/dedup physical structure | Implementation-local physical layout; lifecycle/custody semantics fixed |
| endpoint address/port | Deployment-local; protocol transport/path contract remains fixed |
| certificate issuer/private key provider | Deployment-local under internal auth profile |
| addon-specific functional protocol | Separate addon protocol/framework; not Standard Protocol v1 generic payload |

## 13. Boundary document interpretation

### `core-gateway.md`

Semantic scheduling、durability、dedup、custody、MasterGeneration contractは有効。

Physical transport/serialization/Gateway identity/heartbeat/full-delta schemaを「未決定」とする記述は本書により解決済み。

### `gateway-gateway.md`

Semantic custody、retry、failover、deterministic merge、login proxy contractは有効。

Physical transport/serialization/Gateway identity/heartbeat/login session handoff technologyを「未決定」とする記述は本書により解決済み。

### `gateway-view.md`

Semantic confirmed/predicted state separation、Diver binding、Operation identity、role separationは有効。

Transport、serialization、browser auth/session、role permission、publication schema、resync representationを「未決定」とする記述はPhase 4 contractへ置換される。

### `gateway-admin-view.md`

Semantic Admin domain separation、Config ownership、audit、Operation invariant contractは有効。

Transport、serialization、credential/session、permission、health/log/config/audit payloadを「未決定」とする記述はPhase 4 contractへ置換される。

## 14. Implementation-local decisions

次は詳細設計を変更しない範囲でimplementation/deploymentへ委譲できる。

- concrete listen/connect host/port。
- package/generator patch versionのlock。
- private key/certificate automation provider。
- Configで定義済みoperational timeout/backoffのeffective values。
- queue/index/cacheのphysical implementation。
- telemetry exporter/backend deployment。

Implementation-local choiceを理由にwire schema、security minimum、Operation identity、authority、determinismを変更してはならない。

## 15. Completion

Standard Protocol v1の詳細設計上の未決定事項は、implementation-local choiceを除き解決済み。

Protocol-level unresolved blocker: 0件。