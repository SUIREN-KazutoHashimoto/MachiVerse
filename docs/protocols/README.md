# プロトコル設計方針

Status: Complete / Standard Protocol v1 index

## 1. 目的

本directoryはMachiVerseのcomponent間protocol contractの入口である。

Simulation Core、Gateway、General View、Admin Viewはcode/build/deploy/runtime単位まで独立し、component間通信はprotocolだけを通じて行う。shared DTO libraryや内部型共有をprotocolの代替にしない。

## 2. Contract source of truth

Protocol contractは責務ごとに次を正本とする。

### 2.1 Requirements / semantic foundation

- requirements: `docs/requirements`
- common envelope/version/Capability/result: `docs/design/phase1-protocol-envelope.md`
- persistence/recovery/continuity: `docs/design/phase1-persistence-replay-recovery.md`
- Operation scheduling/retry/dedup/Batch/failover: `docs/design/phase1-operation-lifecycle-retry-dedup.md`

### 2.2 Phase 4 exact semantic contract

- envelope/transport/validation/compatibility: `docs/design/phase4-protocol-schema.md`
- payload/message semantics: `docs/design/phase4-protocol-payload-catalog.md`
- browser auth/session/permission: `docs/design/phase4-auth-session-protocol.md`
- internal component authentication: `docs/design/phase4-internal-component-auth-profile.md`
- completion: `docs/design/phase4-protocol-completion-review.md`
- final cross-consistency: `docs/design/phase4-cross-consistency-resolution.md`

### 2.3 Wire declaration

- `schema/common.proto`
- `schema/auth.proto`
- `schema/payloads.proto`
- `schema/message-registry-v1.md`

`.proto` はprotobuf field number/type、enum number、service signatureの正本である。

### 2.4 Boundary overview

- `core-gateway.md`
- `gateway-gateway.md`
- `gateway-view.md`
- `gateway-admin-view.md`

これらはcomponent境界とsemantic intentのoverviewとして有効である。Phase 1/2時点の「詳細設計へ残す事項」「component実装へ残す事項」は `phase4-resolution.md` でResolvedとされたものについてhistorical handoff記録として扱う。

## 3. Final Standard Protocol v1 profile

| Boundary | ProtocolId | Transport | Serialization | Production auth |
|---|---|---|---|---|
| Simulation Core ↔ Gateway | `mv.core-gateway` | HTTP/2 gRPC bidirectional streaming | Protocol Buffers proto3 | mutual TLS |
| Gateway ↔ Gateway | `mv.gateway-gateway` | HTTP/2 gRPC bidirectional streaming | Protocol Buffers proto3 | mutual TLS |
| Gateway ↔ General View | `mv.gateway-view` | TLS WebSocket binary | Protocol Buffers proto3 | OIDC/BFF Gateway session |
| Gateway ↔ Admin View | `mv.gateway-admin-view` | TLS WebSocket binary | Protocol Buffers proto3 | OIDC/BFF Gateway session |

Standard internal service:

```proto
rpc Connect(stream WireEnvelopeV1) returns (stream WireEnvelopeV1);
```

Standard WebSocket path:

```text
/ws/v1/view
/ws/v1/admin
```

Compression baselineは`NONE`。`wire.gzip.v1`はnegotiated optional capability。

## 4. Protocol owner

| 境界 | owner | 利用側 | ProtocolId |
|---|---|---|---|
| Simulation Core ↔ Gateway | Simulation Core | Gateway | `mv.core-gateway` |
| Gateway ↔ Gateway | Gateway | Gateway | `mv.gateway-gateway` |
| Gateway ↔ General View | Gateway | General View | `mv.gateway-view` |
| Gateway ↔ Admin View | Gateway | Admin View | `mv.gateway-admin-view` |

標準構成にCore↔Core protocolは存在しない。

Ownerは公開message semantics、compatibility、version changeを管理し、利用側はownerのinternal implementationへ依存しない。

## 5. Common envelope / identity

全normal messageは `WireEnvelopeV1` の意味を持つ。

主要field:

- envelope/protocol version
- NegotiationGeneration
- MessageType
- MessageId / CorrelationId / CausationId
- sender ComponentInstanceId
- optional WorldContext
- optional OperationContext
- payload schema id/version
- compression
- protocol-owned protobuf payload

MessageId、CorrelationId、ComponentInstanceIdをcredential、Operation dedup key、world ordering、random seed、EntityId生成へ使用しない。

## 6. Version / Capability

- incompatible semantic changeはProtocol Major更新。
- compatible additive changeはsame Major Minor更新可能。
- connection handshakeでhighest common compatible versionを選択する。
- required Capability不足はconnection/message rejectし、silent degradationしない。
- connection中のCapability changeはreconnectを基本とする。
- schema required semantic additionはCapabilityまたはMajor changeでguardする。

## 7. Operation / retry / dedup

world-affecting Operationは次を維持する。

- stable OperationId。
- immutable payload digest。
- immutable scheduling admission context。
- retry時same logical identity。
- candidate StepとCore final effective Stepの分離。
- End-to-End dedup/idempotency。
- durable custody boundary。

same OperationId + different immutable digestはrejectする。

Batchはtransport aggregationであり、暗黙all-or-nothing transactionではない。

Hop ACKをCore terminal successと同一視しない。

## 8. State continuity / View

Confirmed state publicationはCore-derived continuityを維持する。

- FULL/DELTAはexplicit schema。
- DELTA base mismatchはblind applyせずresync。
- View prediction/interpolationはnon-authoritative。
- predicted stateへconfirmed continuity tokenを付けない。
- View camera/FPS/network timingをworld outcomeへ使用しない。

## 9. Authentication / authorization

### Browser boundary

`mv.gateway-view` / `mv.gateway-admin-view`:

- OIDC Authorization Code + PKCE S256。
- Gateway BFF。
- access/refresh tokenをbrowser JavaScriptへ渡さない。
- opaque Secure/HttpOnly session cookie。
- General View/Admin View auth domainを分離する。

### Internal component boundary

`mv.core-gateway` / `mv.gateway-gateway` production:

- mutual TLS required。
- Core/Gateway service identityをcertificate SAN URIで検証する。
- Gateway certificate identityとGatewayLogicalIdを一致検証する。
- mTLS identityだけでMaster authorityを与えない。
- mTLS失敗時にplaintext/server-only TLSへfallbackしない。

## 10. Code dependency / generation

禁止:

- 別component project/DLLへのprotocol目的の直接参照。
- shared generated DTO assemblyを唯一のcontract正本にすること。
- direct method callをprotocol代替にすること。
- generated sourceだけを編集して`.proto`と乖離させること。

各componentは同じversion-controlled `.proto` sourceからlocal code generationし、相手implementationなしでcontract test可能にする。

## 11. Determinism boundary

Protocol Buffersはwire serializationであり、authoritative deterministic digestのcanonical encodingではない。

- Operation immutable digest。
- state diagnostic digest。
- EntityId/IntentId/TransactionId derivation。

これらはPhase 1/4 deterministic encoding/hash contractを使用する。

network arrival order、protobuf map iteration、retry timing、Gateway/Master identityをworld outcomeへ持ち込まない。

## 12. Resolution of historical TODOs

Phase 1/2文書に残る次の項目はPhase 4で解決済み。

- network transport。
- serialization/compression。
- protocol-specific payload schema。
- state FULL/DELTA strategy。
- browser auth/session technology。
- internal component authentication。
- role/permission matrix。
- heartbeat/role payload。
- Admin health/log/config/audit payload。
- schema/code-generation policy。

詳細なresolution tableは `phase4-resolution.md` を参照する。

Implementation-localとして残るのは、host/port、certificate automation provider、package/tool patch lock、physical queue/index layout、Configで所有されるoperational value等であり、protocol semanticsを変更してはならない。

## 13. Change governance

Protocol変更では同一change setで必要に応じ次を更新する。

- semantic design。
- `.proto`。
- message registry。
- version/Capability compatibility decision。
- Config/persistence migration impact。
- P4-08 contract fixture/acceptance。

実装側だけでsilent forkしない。

Standard Protocol v1 unresolved design blocker: 0件。