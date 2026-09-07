# プロトコル設計方針

Status: Complete / Standard Protocol v1 index

## 1. 目的

本書はMachiVerseのcomponent間通信に共通する契約原則を定義する。

Simulation Core、Gateway、General View、Admin Viewはcode/build/deploy/runtime単位まで独立し、component間通信はprotocolだけを通じて行う。shared DTO libraryや内部型共有をprotocolの代替にしない。

Phase 1共通契約の正本:

- envelope / version / Capability / result: `docs/design/phase1-protocol-envelope.md`
- persistence / recovery / continuity: `docs/design/phase1-persistence-replay-recovery.md`
- Operation scheduling / retry / dedup / Batch / failover: `docs/design/phase1-operation-lifecycle-retry-dedup.md`
- Phase 1最終整合レビュー: `docs/design/phase1-cross-cutting-review.md`

Phase 4 exact contract:

- envelope / validation / transport / compatibility: `docs/design/phase4-protocol-schema.md`
- payload / message semantics: `docs/design/phase4-protocol-payload-catalog.md`
- browser auth / session / permission: `docs/design/phase4-auth-session-protocol.md`
- internal component authentication: `docs/design/phase4-internal-component-auth-profile.md`
- Standard Protocol v1 final resolution: `docs/protocols/phase4-resolution.md`
- final cross-consistency: `docs/design/phase4-cross-consistency-resolution.md`

Wire declaration:

- `docs/protocols/schema/common.proto`
- `docs/protocols/schema/auth.proto`
- `docs/protocols/schema/payloads.proto`
- `docs/protocols/schema/message-registry-v1.md`

## 2. 基本原則

### 2.1 Code dependencyを持たない

禁止する例:

- 別component project / DLL参照
- shared DTO libraryを唯一のcontract正本にすること
- 別component内部class/interface参照
- direct method call
- same process前提communication
- protocol documentに存在しない暗黙仕様への依存

各componentは相手implementationなしでも独立build/test可能な境界を維持する。

### 2.2 Contract sourceを責務分離する

各protocolは必要に応じ少なくとも次を明示する。

- communication purpose / owner
- sender / receiver
- message type
- field semantics / required / optional
- data type / range / unit
- success / error semantics
- ordering / idempotency / dedup / retry
- timeout / disconnect / resync
- authentication / authorization
- version / Capability
- World Time / SimulationStep
- Operation / Batch identity
- durability / custody scope

Standard Protocol v1ではPhase 4でphysical transport / serializationまで確定済みである。

- semantic validation、authority、security、ordering、retry/dedup: Phase 4 design文書。
- protobuf field number/type、enum number、service signature: `docs/protocols/schema/*.proto`。
- exact MessageType → payload mapping: `docs/protocols/schema/message-registry-v1.md`。
- component境界overview: 本directoryのboundary文書。

Generated C#/JavaScript/TypeScript等をcontract正本にしない。

## 3. Protocol owner

| 境界 | owner | 利用側 | ProtocolId |
|---|---|---|---|
| Simulation Core ↔ Gateway | Simulation Core | Gateway | `mv.core-gateway` |
| Gateway ↔ Gateway | Gateway | Gateway | `mv.gateway-gateway` |
| Gateway ↔ General View | Gateway | General View | `mv.gateway-view` |
| Gateway ↔ Admin View | Gateway | Admin View | `mv.gateway-admin-view` |

標準構成にCore↔Core protocolは存在しない。

Ownerは公開message semantics、compatibility、version changeを管理し、利用側はownerのinternal implementationへ依存しない。

Standard Protocol v1 transport profile:

| ProtocolId | Transport | Serialization | Production authentication |
|---|---|---|---|
| `mv.core-gateway` | HTTP/2 gRPC bidirectional streaming | Protocol Buffers proto3 | mutual TLS |
| `mv.gateway-gateway` | HTTP/2 gRPC bidirectional streaming | Protocol Buffers proto3 | mutual TLS |
| `mv.gateway-view` | TLS WebSocket binary | Protocol Buffers proto3 | OIDC/BFF Gateway session |
| `mv.gateway-admin-view` | TLS WebSocket binary | Protocol Buffers proto3 | OIDC/BFF Gateway session |

Compression baselineは`NONE`、`wire.gzip.v1`はnegotiated optional capabilityとする。

## 4. Common envelope

全標準protocolのnormal messageは論理的に `ProtocolEnvelopeV1` / wire上の `WireEnvelopeV1` の意味を持つ。

共通field:

- envelope version
- ProtocolId / negotiated ProtocolVersion
- NegotiationGeneration
- MessageType
- MessageId
- CorrelationId / CausationId
- sender ComponentInstanceId
- optional WorldContextV1
- optional OperationContextV1
- payload schema id/version
- compression
- protocol-owned payload

MessageId / CorrelationId / sender instance identityをworld ordering、dedup、random、EntityId生成へ使用しない。

## 5. Versioning

```text
ProtocolVersion {
  major: uint16,
  minor: uint16
}
```

- incompatible semantic changeはMajor更新。
- same Major compatible changeはMinor更新。
- handshakeは双方supported rangeから共通Major最大値、そのMajorの共通Minor範囲最大値を選ぶ。
- 共通version不在はnormal connection reject。
- normal messageはnegotiated versionを明示する。
- negotiated Minorを超えるsemanticを無条件送信しない。
- `.proto`のpublished field/enum numberをsame Major内で再利用・renumberしない。

## 6. Capability Negotiation

```text
CapabilityId := StableToken
```

- provided / required Capabilityを分離する。
- 双方required setが相手provided setのsubsetであることを確認する。
- required不足をsilent degradationしない。
- optional effective setは双方providedのintersection。
- incompatible capability semantic revisionは新tokenとする。

connection中のCapability changeはreconnectを基本とする。双方が `protocol.live-renegotiation.v1` を提供し、個別protocolが安全なbarrierを定義した場合のみlive renegotiation可能。

## 7. NegotiationGeneration

```text
NegotiationGeneration := uint32
```

- handshake前: 0
- initial success後: 1
- safe live renegotiation成功ごとに+1
- reconnectは新connectionとして1から開始
- stale generation messageをcurrent semanticsで解釈しない

world orderingへ使用しない。

## 8. Addon metadata境界

standard protocolで交換できるAddon情報はconnection safety / compatibility用metadataに限定する。

許可例:

- addon identity
- enabled state
- version
- required / provided Capability
- dependency range

標準protocolに載せない:

- addon固有function payload
- addon固有command
- world-specific generic extension payload
- addon都合で標準message semanticを書き換える仕組み

addon固有cross-component通信はadditional protocol / framework addonの責務。

## 9. Operation共通要件

world-affecting Operationを扱うprotocolは次を維持する。

- stable OperationId
- immutable Operation payload digest
- immutable `OperationSchedulingAdmissionV1`
- BatchId
- MasterGeneration
- retry時same logical identity
- End-to-End dedup / idempotency
- stale generation handling
- deterministic orderingに必要なlogical information
- candidate / final effective Step分離
- deadline / grace / late handling
- durable custody boundary

same OperationId + different immutable digestは `protocol.operation-payload-mismatch` としてrejectする。

## 10. immutable digest boundary

`mv.operation-payload.v1`へ含める:

- operation type
- logical target
- immutable semantic content / arguments
- origin固定semantic constraints
- `OperationSchedulingAdmissionV1`
  - admission_basis_step
  - scheduling_policy_generation
  - requested_not_before_step
  - requested_deadline_step

含めない:

- ProtocolEnvelopeV1 / WireEnvelopeV1
- MessageId / CorrelationId / CausationId
- BatchId
- MasterGeneration / NegotiationGeneration
- retry count / retry timing
- routing information
- network arrival timestamp
- Gateway / Master candidate Step
- Core final/effective Step
- ACK / result metadata

protobuf wire bytesをauthoritative immutable digestのcanonical sourceにしない。

## 11. World Time / generation context

```text
WorldContextV1 {
  world_id,
  basis_step,
  effective_step,
  master_generation,
  config_generation
}
```

- `basis_step`: state / publication / resyncの基準 `State(S)`。
- `effective_step`: Core確定済み `State(S) -> State(S+1)` transition Step。
- candidate Stepはpayloadのcandidate fieldとして表現する。
- `master_generation`: authority / routing validity。
- `config_generation`: sender ownerのeffective Config generation。

異なるgenerationを相互代用しない。

## 12. Result / error / retry

共通status:

```text
SUCCESS
ACCEPTED
PENDING
NO_CHANGE
DUPLICATE
REJECTED
FAILED
```

machine behaviorはStableToken codeで分岐し、diagnostic textの文字列比較へ依存しない。

主要common code:

```text
ok
accepted
pending
no-change
duplicate

protocol.malformed
protocol.wrong-protocol
protocol.version-incompatible
protocol.capability-missing
protocol.unknown-message-type
protocol.negotiation-stale
protocol.operation-payload-mismatch
protocol.batch-payload-mismatch

auth.unauthenticated
auth.unauthorized
auth.session-expired
auth.session-revoked
auth.component-untrusted
auth.component-identity-mismatch

request.invalid
request.conflict
request.stale
request.timeout

operation.accepted
operation.scheduled
operation.result-details-expired

world.not-found
world.invalid-state
world.late-operation
world.deadline-exceeded
world.late-deferred
world.pause-deferred
world.resync-required

master.stale-generation
config.stale-generation
config.invalid

batch.partial
batch.complete

component.unavailable
component.resyncing
internal.failure
```

RetryAdvice:

```text
DO_NOT_RETRY
RETRY_SAME_IDENTITY
RECONNECT_THEN_RETRY
RESYNC_THEN_RETRY
RENEGOTIATE_THEN_RETRY
```

wall-clock retry delayはoperational advisoryでありauthoritative effective Stepへ使用しない。

## 13. ACK / custody / terminal result

ACKはhop上の受理・custody stateであり、Core authoritative world mutationのterminal successとは限らない。

world-affecting OperationについてCoreが `ACCEPTED` を返す場合、Operation acceptanceを先にdurable化する。

applied terminal resultは対応transition commitのdurability前に返さない。

Gateway delivery custody:

```text
SOURCE_HELD
 -> MASTER_RECEIVED
 -> CORE_ACCEPTED
 -> TERMINAL
```

Master receipt ACKだけを理由にsourceが唯一のretry可能copyを捨てない。

## 14. retry / dedup

same logical Operation retryは常に:

- same OperationId
- same immutable payload digest
- same scheduling admission context

を維持する。

Core terminal OperationはWorldId lifecycle中、minimum `OperationDedupTombstoneV1` を保持する。

rich result detailsは有限保持可能だが、tombstone expiryによりsame OperationIdのdouble applyを可能にしてはならない。

Gateway/View等のlocal request cache retentionはこのCore dedup contractの代替ではない。

## 15. Batch

Batchはtransport aggregation identity。

```text
BatchProcessingMode := PER_OPERATION
BatchStatus := RECEIVED | PARTIAL | COMPLETE | REJECTED
```

- Batchを暗黙all-or-nothing transactionとしない。
- exact same logical batch retryのみsame BatchIdを維持可能。
- contents変更 / subset retry / re-mergeはnew BatchId。
- contained OperationIdは維持する。
- Batch historyがexpireしてもOperation dedup安全性を失わない。

## 16. Pause / late

Pause中もOperation受信 / validation / durable acceptanceは可能。

worldが `State(P)` でPause中:

- Pause前にeffective_step=Pへschedule済みOperationはtransition Pに残す。
- Pause中新規accept Operationは最速 `P+1`。
- Pause durationだけでSimulationStep deadlineを消費しない。
- Pause arrival orderをsame-Step orderへ使用しない。

late policy:

```text
REJECT | DEFER_WITHIN_GRACE
```

finalized past stateをretroactive rewriteしない。

## 17. State continuity / resync

Core-derived confirmed state chainは `StateContinuityToken` で識別する。

- process restartでtokenを再採番しない。
- delta base token mismatch時はblind applyせずresync。
- Gatewayが独自authoritative-looking tokenを生成しない。
- View predictionへconfirmed tokenを付けない。

Reconnect後はversion / Capability negotiationを再実行し、current confirmed basisへ同期してからnormal publicationへ戻る。

FULL/DELTA/chunk/projection exact schemaは `docs/protocols/schema` と `docs/design/phase4-protocol-payload-catalog.md` を参照する。

## 18. Auth / Authorization

- General View / Admin View auth domainを分離する。
- sender ComponentInstanceId / MessageId / CorrelationIdはcredentialではない。
- unauthorized requestをGatewayからCoreへforwardしない。
- Admin Operation固有permissionはGateway責務。
- CoreはUI roleを解釈せずcommon world-state invariantを維持する。
- loginはconnected GatewayからMaster GatewayへproxyしMasterでfinalizeする。

Browser user authentication/sessionは `docs/design/phase4-auth-session-protocol.md` を正本とし、OIDC Authorization Code + PKCE S256 + Gateway BFFをstandard profileとする。

Core↔Gateway / Gateway↔Gateway production component authenticationは `docs/design/phase4-internal-component-auth-profile.md` を正本とし、mutual TLSとservice identity bindingをrequiredとする。

mTLS identityだけでMaster authorityを付与せず、MasterGeneration/role stateを別途検証する。

## 19. Failure / reconnect / recovery

Protocolは必要に応じ次を明示する。

- confirmed / unconfirmed boundary
- retry ownership
- ACK loss
- duplicate message
- reconnect sync basis
- resync state
- Master failover / generation handoff
- Operation status recovery

Core acceptance不明時はsame identity retryまたはOperationId status queryで収束させる。

Certificate/trust rotationやinternal connection reauthenticationでもaccepted Operation identityを変更せず、同じ収束規則を使用する。

## 20. Independent testing

各componentは相手implementationを必要とせず、少なくとも次をcontract test可能にする。

- same/different Minor compatibility
- no common version reject
- required Capability mismatch
- stale NegotiationGeneration
- stale MasterGeneration
- same OperationId + digest mismatch
- same BatchId + BatchDigest mismatch
- retry / duplicate / idempotency
- ACKとterminal resultの分離
- candidate/effective Step混同拒否
- continuity mismatch resync
- `.proto` schema compile / registry mapping
- internal mTLS required / untrusted / identity mismatch / no downgrade

P4-08 exact acceptanceは `docs/design/phase4-test-acceptance.md` と `docs/design/phase4-test-acceptance-addendum.md` を正本とする。

## 21. 禁止事項

- component間code sharingをcommunication contractとすること
- shared internal/generated DTOへの依存
- direct method call
- undocumented implicit behavior
- Minor updateでsemantic compatibilityを破壊すること
- common version不在でnormal connection
- required Capability不足のsilent degradation
- stale NegotiationGeneration / MasterGenerationのcurrent化
- addon functional payloadをstandard protocolへ埋め込むこと
- MessageId / CorrelationIdをOperation dedup keyにすること
- network arrival orderをauthoritative world orderにすること
- candidate Stepをauthoritative effective_stepにすること
- ACKをterminal world successと同一視すること
- retryでOperationIdを再採番すること
- terminal tombstoneをWorldId継続中にexpiryしてdouble apply可能にすること
- generated codeだけを変更して`.proto`とwire contractを分岐させること
- production internal auth失敗時にplaintext/server-only TLSへsilent downgradeすること

## 22. Phase 4 resolution / implementation-local事項

Phase 1/2時点で「詳細設計へ残す事項」とされていた次はPhase 4で解決済み。

- concrete network transport
- serialization / compression baseline
- protocol-specific payload schema
- state publication full/delta payload strategy
- browser auth credential / session technology
- internal component authentication
- exact role/permission matrix
- heartbeat/role payload
- Admin health/log/config/audit payload
- schema tooling / code generation policy

詳細なresolution tableは `docs/protocols/phase4-resolution.md` を正本とする。

Implementation/deploymentへ残せるのは、protocol semanticsを変更しない次のphysical/local choiceである。

- endpoint host/port。
- exact operational timeout/backoff effective values（Config contract内）。
- physical durable queue / dedup index layout。
- package/generator patch version lock。
- certificate issuer/private key storage/revocation provider。
- telemetry backend/exporter deployment。
- additional addon protocol frameworkの具体implementation。

これらはPhase 1〜4のauthority、identity、security、determinism、retry/dedup、wire compatibilityを変更してはならない。

Standard Protocol v1 unresolved design blocker: 0件。