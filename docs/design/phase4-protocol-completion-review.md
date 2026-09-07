# 詳細設計 Phase 4: Protocol Completion Review

Status: Complete / P4-02 Completion Review  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`

## 1. 目的

P4-02で定義したcommon wire schema、4 protocol message registry、auth/session、publication、Operation/Batch、Admin management payloadをPhase 1/2および`docs/protocols`と横断照合し、P4-02を完了可能か判定する。

本書をP4-02 completion判定の正本とする。

## 2. P4-02成果物

| Artifact | Status |
|---|---|
| `phase4-protocol-schema.md` | common wire/schema complete |
| `phase4-auth-session-protocol.md` | auth/session/permission complete |
| `phase4-protocol-payload-catalog.md` | protocol-specific payload catalog complete |
| 本書 | completion review |

## 3. Transport / serialization decision audit

確定:

- Protocol Buffers proto3をstandard binary serializationとする。
- Core↔Gateway / Gateway↔GatewayはgRPC bidirectional streaming。
- Gateway↔General View / Gateway↔Admin ViewはTLS WebSocket binary message。
- browser uint64はlossless BigInt/wrapperを要求する。
- protobuf wire bytesはauthoritative digest正本にしない。
- protobuf map iteration orderをworld orderingにしない。
- standard envelope hard limitは8 MiB。
- state publicationは1 MiB以下のchunkへ分割可能。

Phase 1のMV-DCBOR-v1 digest contractと矛盾なし。

## 4. Common envelope audit

`WireEnvelopeV1`はPhase 1 `ProtocolEnvelopeV1`の全semantic fieldを保持する。

- envelope version
- ProtocolId / ProtocolVersion
- NegotiationGeneration
- MessageType
- MessageId / CorrelationId / CausationId
- sender ComponentInstanceId
- WorldContext
- OperationContext
- protocol-owned payload

追加したpayload schema id/versionはPhase 1 semanticを変更せず、formal payload validationを可能にする。

判定: PASS。

## 5. Version / Capability audit

- handshake前 generation 0、成功後1。
- common major/minor selectionはPhase 1 ruleを維持。
- required Capability不足はreject。
- unknown required semanticをminorでsilent downgradeしない。
- message typeごとのrequired Capabilityをregistry化。
- reconnectをCapability changeの標準barrierとする。

判定: PASS。

## 6. Operation identity / scheduling audit

- stable OperationIdをwire containerへ保持。
- immutable payload digestを保持。
- `OperationSchedulingAdmissionWireV1`をimmutable digest対象として維持。
- candidate Stepとauthoritative effective Stepを分離。
- BatchIdとOperationIdを分離。
- same id/different digest errorを維持。
- Operation status queryをformal schema化。

判定: PASS。

## 7. ACK / custody audit

`GatewayBatchAckV1`で次を区別する。

```text
SOURCE_HELD
MASTER_RECEIVED
CORE_ACCEPTED
TERMINAL
```

Master receipt ACKをCore durable acceptanceと同一視しない。

Batch statusはPER_OPERATION semanticsを維持し、Batchを暗黙transactionにしない。

判定: PASS。

## 8. State continuity / publication audit

- FULL / DELTAを明示enum化。
- DELTA base continuity token required。
- chunk digest/assembly validationを定義。
- continuity mismatchはblind applyせずresync。
- confirmed projection recordとView predictionを分離。
- projection schemaとauthoritative persistence schemaを分離。
- View slow consumerでintermediate publication coalesce/dropを許容するがOperation result/custodyをlossyにしない。

判定: PASS。

## 9. Auth / session audit

- Gatewayをbrowser Backend-for-Frontendとする。
- OpenID Connect + Authorization Code + PKCE S256 profile。
- access/refresh tokenをbrowser JavaScriptへ露出しない。
- secure HttpOnly session cookieを使用。
- WebSocket Origin validationを要求。
- General View / Admin View auth domainを分離。
- connected non-Master Gatewayがloginを独立finalizeしない。
- MasterGeneration切替中のold login authorityをcurrent化しない。
- credential/session secretをWorldStateへ保存しない。
- logout/revokeでParticipation bindingを暗黙解除しない。

判定: PASS。

## 10. Permission audit

General View role:

```text
view.spectator
view.diver
view.moderator
view.administrator
```

をpermission setへmappingした。

Admin Viewは別permission namespaceを持つ。

General View AdministratorからAdmin permissionへのautomatic promotionを禁止。

OperationKindごとのrequired permissionはdomain Operation registryへ登録する。

判定: PASS。

## 11. Core↔Gateway remaining item resolution

旧`docs/protocols/core-gateway.md`の未決定事項を次へ解消/引き渡す。

| Item | Resolution |
|---|---|
| physical transport | P4-02 gRPC bidi stream |
| serialization | P4-02 protobuf |
| compression | NONE required / optional gzip capability |
| Gateway identity | `GatewayLogicalId` |
| Master heartbeat physical message | P4-02 payload catalog |
| state full/delta | P4-02 chunk/projection schema |
| timeout/backoff | P4-03 OPERATIONAL Config |
| Core dedup physical structure | P4-04 persistence / implementation |
| status query transport | P4-02 message registry |

Protocol-level blocker: 0。

## 12. Gateway↔Gateway remaining item resolution

| Item | Resolution |
|---|---|
| physical transport | gRPC bidi stream |
| serialization/compression | protobuf / optional gzip |
| Gateway logical identity | `GatewayLogicalId` |
| local/cross-Gateway merge domain fields | P4-05 Operation/domain schema |
| heartbeat/election messages | P4-02 payload catalog |
| retry timeout/backoff | P4-03 |
| durable custody queue | P4-04 / implementation work item |
| login session handoff | P4-02 auth sub-spec |

Protocol-level blocker: 0。

## 13. Gateway↔General View remaining item resolution

| Item | Resolution |
|---|---|
| transport/serialization | binary WebSocket + protobuf |
| auth/session | P4-02 auth sub-spec |
| role permission matrix | P4-02 auth sub-spec |
| public projection profile transport | P4-02 payload catalog |
| critical OperationKind list | P4-05 domain Operation registry |
| Diver preference/matching | P4-05 participation payload/algorithm |
| binding wire state | P4-02 payload catalog |
| absence behavior payload semantics | P4-05 participation schema |
| interpolation/prediction implementation | General View implementation / P4-06 budget |
| full/delta | P4-02 publication schema |
| world Operation payload | P4-05 owner-domain registry |
| resync status | P4-02 payload catalog |
| result retention | P4-03/P4-06 |

Protocol-level blocker: 0。

## 14. Gateway↔Admin View remaining item resolution

| Item | Resolution |
|---|---|
| transport/serialization | binary WebSocket + protobuf |
| auth/session | P4-02 auth sub-spec |
| permission base registry | P4-02 auth sub-spec |
| health payload | P4-02 payload catalog |
| log query/page | P4-02 payload catalog; event registry P4-07 |
| Config read/change wire | P4-02 payload catalog; key/value spec P4-03 |
| operational command container | P4-02; command catalog P4-07/P4-09 |
| high-impact category/audit | P4-07 |
| audit query/page | P4-02 payload catalog; retention/event kinds P4-07 |
| component management reachability | Gateway external owner; existing internal/peer boundaries only |
| timeout/idempotency retention | P4-03/P4-06 |

Protocol-level blocker: 0。

## 15. Error code audit

Common Phase 1 codesを維持し、P4-02 structural codeを追加した。

追加codeはprotocol validation/routing failureを表し、world semantic failureを上書きしない。

unknown message/schema/capabilityをgeneric pass-throughしない。

判定: PASS。

## 16. Addon boundary audit

- standard payload registryにaddon functional generic extension slotを設けない。
- addon compatibility metadataのみstandard handshakeで交換可能。
- addon functional cross-component dataはadditional protocolへ分離。

判定: PASS。

## 17. Independent implementation testability

各componentは相手implementationなしで次をcontract test可能。

- protobuf fixture decode/encode
- fixed length/range/size validation
- version/capability negotiation
- stale NegotiationGeneration
- stale MasterGeneration
- same OperationId different digest
- BatchDigest mismatch
- ACK/custody state separation
- FULL/DELTA continuity mismatch
- chunk digest mismatch
- auth session generation/revoke
- cross-origin WebSocket rejection
- Admin permission separation
- unknown message/schema reject

判定: PASS。

## 18. P4-02 handoff

P4-03へ:

- transport timeout / heartbeat interval / retry / result/session retention
- IdP/endpoints/deployment configuration
- protocol queue/size operational tunables where not hard structural limit

P4-05へ:

- owner-domain Standard OperationKind catalog
- Operation payload field schema
- View projection record payload schemas
- participation preference/absence policy semantics

P4-07へ:

- MetricName registry
- structured LogEventKind registry
- AuditEventKind / retention / redaction
- high-impact operation confirmation/audit policy

P4-08へ:

- protocol compatibility/malformed/property tests
- auth/session security contract tests
- full/delta/resync tests

## 19. Completion criteria

| Criterion | Result |
|---|---|
| formal envelope field/type/number | PASS |
| 4 protocol transport/serialization | PASS |
| version/capability schema | PASS |
| result/error registry | PASS |
| Operation/Batch/status schema | PASS |
| state publication full/delta schema | PASS |
| auth/session/permission schema | PASS |
| health/log/config/audit wire container | PASS |
| stale/malformed handling | PASS |
| addon boundary | PASS |
| protocol-level unresolved blocker = 0 | PASS |

## 20. Completion decision

P4-02をCompleteと判定する。

World-domain固有Operation payloadやConfig/observabilityのowner-specific内容は後続P4へ明示引き渡しており、Protocol schema自体のblockerではない。
