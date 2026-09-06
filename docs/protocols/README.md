# プロトコル設計方針

## 1. 目的

本書はMachiVerseのcomponent間通信に共通する契約原則を定義する。

Simulation Core、Gateway、General View、Admin Viewはcode/build/deploy/runtime単位まで独立し、component間通信はprotocolだけを通じて行う。shared DTO libraryや内部型共有をprotocolの代替にしない。

Phase 1 の共通 message envelope、version / Capability negotiation、result / error、correlation / causation、World Time / generation context の詳細契約は `docs/design/phase1-protocol-envelope.md` を正本とする。

## 2. 基本原則

### 2.1 Code dependencyを持たない

禁止する例:

- 別componentのproject参照
- 別component DLL参照
- shared DTO libraryによるcontract共有
- 別component内部class/interface参照
- direct method call
- same processであることを前提としたcommunication
- protocol documentに存在しない暗黙仕様への依存

各componentは相手componentのimplementationなしでも独立build/test可能な境界を目指す。

### 2.2 Protocol documentを契約正本とする

各protocol設計書では、必要に応じ少なくとも次を明示する。

- communication purpose
- sender / receiver
- message / request / event type
- field semantics
- required / optional
- data type / range / unit
- success / error semantics
- ordering
- idempotency / dedup
- retry
- timeout / disconnect
- synchronization basis
- authentication / authorization handling
- version / backward compatibility
- Capability negotiation
- World Time / Simulation Stepとの関係
- Operation / Batch identityが関係する場合の意味

transportやserializationの具体技術は個別protocol詳細設計で決定する。ただし共通 envelope の field semantics を失ってはならない。

## 3. Protocol所有責任

Protocol ownerは、接続する2componentのうちよりSimulation Coreに近いcomponentとする。

| 境界 | owner | 利用側 | ProtocolId |
|---|---|---|---|
| Simulation Core ↔ Gateway | Simulation Core | Gateway | `mv.core-gateway` |
| Gateway ↔ Gateway | Gateway | Gateway | `mv.gateway-gateway` |
| Gateway ↔ General View | Gateway | General View | `mv.gateway-view` |
| Gateway ↔ Admin View | Gateway | Admin View | `mv.gateway-admin-view` |

標準構成にCore↔Core protocolは存在しない。

Ownerは公開機能、message semantics、compatibility、変更方針を定義する。利用側はownerのinternal implementationへ依存せずprotocol contractだけを基準に実装する。

## 4. Common envelope

全標準 protocol の normal message は論理的に `ProtocolEnvelopeV1` の意味を持つ。

最低限次を共通化する。

- envelope version
- ProtocolId / negotiated Major.Minor
- NegotiationGeneration
- MessageType
- MessageId
- CorrelationId / CausationId
- sender ComponentInstanceId
- optional WorldContextV1
- optional OperationContextV1
- protocol-owned payload

MessageId / CorrelationId / sender instance identityをworld operation ordering、dedup、乱数、Entity ID生成の入力にしない。

WorldContextでは `basis_step` とCore確定済み `effective_step` を区別する。Gateway/Masterのcandidate Stepをauthoritative `effective_step` として表現しない。

OperationContextではstable OperationId、immutable payload digest、BatchIdをmessage transport identityから分離する。

## 5. Versioning

### 5.1 Major.Minor

各protocolはMajor.Minorを `uint16` の組として識別する。

- backward-compatibleでないsemantic changeはMajorを更新する。
- same Majorのcompatible changeはMinorを更新する。
- handshakeでは双方のsupported rangeから共通Majorの最大値を選び、そのMajorの共通Minor範囲で最大Minorをnegotiated versionとする。
- 共通versionが存在しない場合はnormal connectionを拒否する。
- normal messageはsender実装最新版ではなくnegotiated versionを明示する。

### 5.2 Minor compatibility

Minor updateで既存必須fieldを削除したり、既存fieldの意味・型・unitを互換不能に変更したりしない。

new fieldはabsent時に旧Minorと同じ意味になるoptional fieldとし、新message typeや新機能はnegotiated MinorまたはCapabilityで送信可否を制御する。

newer Minor側は、older peerが理解できない内容を無条件送信しない。

## 6. Capability Negotiation

Connection確立時にprotocol versionとCapabilityを交換する。

```text
CapabilityId := StableToken
```

incompatible semantic revisionは別tokenとし、例として `state.delta.v1` のようにversionをtokenへ含める。

- provided Capabilityとrequired Capabilityを区別する。
- 双方のrequired setが相手provided setのsubsetであることを確認する。
- effective optional setは双方providedのintersection。
- required Capability不足はconnectionまたは対象featureを明示的に拒否する。
- required Capability不足をsilent degradationで隠さない。

connection中のCapability変化はPhase 1標準ではreconnectして再negotiationする。双方が `protocol.live-renegotiation.v1` を提供し、個別protocolが安全なquiesce/barrierを定義した場合のみlive renegotiationを許可する。

## 7. NegotiationGeneration

- handshake前は0。
- initial handshake成功後は1。
- safe live renegotiation成功ごとに1増加する。
- reconnect時は新connectionとして1から開始する。
- stale NegotiationGeneration messageをcurrent semanticsで解釈しない。

NegotiationGenerationをworld orderingへ使用しない。

## 8. Addon関連情報の標準Protocol境界

Q246とQ255を次のように統一する。

### 8.1 標準protocolで交換可能な情報

標準protocolはconnection safety / compatibility確認に必要なaddon metadataのみ交換できる。

共通metadataは少なくとも次を表現可能にする。

- addon identity
- enabled state
- major/minor/patch version
- required / provided Capability
- compatibility判断に必要なaddon dependency range

### 8.2 標準protocolに載せないもの

標準protocolには次を設けない。

- addon固有function payload
- addon固有command
- addon world-specific extra dataを運ぶgeneric extension payload
- addon都合で標準message semanticsを書き換える仕組み

### 8.3 Addon固有の追加Protocol

Addonがcomponent境界を越えて固有情報を交換する必要がある場合は、標準protocolへ混在させず、protocol拡張の前提framework addon等と、そのaddon間で成立するadditional protocolを利用する。

## 9. Addon不整合と接続安全性

- required addon / version / Capabilityが不足・非互換ならunsafe featureをenableしない。
- component startup時のaddon構成・dependency・Capability・Configに不整合がある場合は、重大度に関係なくstartupを拒否する。
- saved worldが依存するaddon条件に不整合があれば、明示migrationが完全成功しない限りworld startupを拒否する。

## 10. Operationを扱うProtocolの共通要件

World Stateへ影響するOperationを扱うprotocolは次を契約化する。

- stable Operation ID
- immutable Operation payload digest
- Batch ID
- Master generation / epoch
- retry時のsame logical identity
- dedup / idempotency
- stale generation handling
- deterministic orderingに必要なlogical information
- candidate / final application Simulation Step semantics
- deadline / late behavior

同一OperationIdのdigestへはlogical Operation meaningを含め、MessageId、CorrelationId、BatchId、MasterGeneration、retry情報、routing情報、candidate/final Step等の可変metadataを含めない。

同一OperationIdで異なるimmutable digestを検出した場合は `protocol.operation-payload-mismatch` としてrejectし、world mutationしない。

Network arrival time、retry count、thread schedulingだけでworld outcomeを変えない。

## 11. World Time / generation context

Protocol上でsimulation timeを扱う場合、authoritativeな時間基準はSimulation Coreの整数Simulation Stepと整合させる。

- `basis_step`: state / publication / resync等の基準State(S)。
- `effective_step`: Coreが確定した `State(S) -> State(S+1)` の適用Step。
- candidate Step / deadline: individual payloadで明示し、effective_stepと混同しない。
- `master_generation`: authority / routing validityに必要なmessageで指定する。
- `config_generation`: sender componentのeffective behavior識別に必要な場合に指定する。

MasterGeneration、ConfigGenerationの大小をbusiness priorityへ使用しない。

## 12. Correlation / causation

- request rootでCorrelationIdを発行する。
- proxy / result routingでも同じinteractionを追跡できる範囲で維持する。
- response / ACK / async resultはrequestと同じCorrelationIdを使用する。
- CausationIdは直接原因となったMessageIdが明確な場合に設定する。
- MessageIdは各envelopeのtrace identityであり、OperationIdの代替ではない。

Tracing identityをworld causalityそのものの根拠として使用しない。

## 13. Result / error / retry

共通resultは `SUCCESS / ACCEPTED / PENDING / NO_CHANGE / DUPLICATE / REJECTED / FAILED` を区別する。

machine behaviorはStableTokenのresult/error codeで分岐し、diagnostic messageの文字列比較へ依存しない。

標準codeには少なくとも次を含む。

- `protocol.version-incompatible`
- `protocol.capability-missing`
- `protocol.malformed`
- `protocol.negotiation-stale`
- `protocol.operation-payload-mismatch`
- `auth.unauthenticated`
- `auth.unauthorized`
- `request.invalid`
- `request.stale`
- `world.invalid-state`
- `world.late-operation`
- `master.stale-generation`
- `config.stale-generation`
- `component.unavailable`
- `component.resyncing`
- `internal.failure`

RetryAdviceは `DO_NOT_RETRY / RETRY_SAME_IDENTITY / RECONNECT_THEN_RETRY / RESYNC_THEN_RETRY / RENEGOTIATE_THEN_RETRY` を共通意味として持つ。

retry_after_millis等のwall-clock retry adviceは運用情報であり、authoritative application Stepを決める入力にしない。

## 14. ACKとterminal result

ACKはprotocol hop上の受理・配送状態であり、Core authoritative world mutationのterminal successと同一視しない。

world-affectingOperationはOperationId単位で二重mutationしない。保持期間内のduplicateには可能な限り同じterminal semantic resultを返す。

具体的dedup retentionとexpiry後の挙動はP1-06で定義する。

## 15. Auth / Authorization

- General View / Admin Viewのauth domainは分離する。
- sender ComponentInstanceId / MessageId / CorrelationIdはcredentialではない。
- Gateway-owned protocolではunauthorized requestをCoreへ到達させない。
- Admin Operation固有のvalidity checkはGateway責務。
- Core-facing protocolではCoreがUI roleを解釈せずcommon world-state invariantを維持する。
- loginはconnected GatewayからMaster Gatewayへproxyし、Masterで確定する要件をGateway関連protocolで表現する。

具体credential/token/IdPは個別auth詳細設計で決定する。

## 16. Failure / reconnect / resynchronization

Protocolは必要に応じ次を明示する。

- disconnect時にconfirmed / unconfirmedとみなすもの
- retry ownership
- ACK loss
- duplicate message
- missing / reorder detection
- reconnect時のsync basis
- cacheがauthoritativeでないこと
- resync中のpublication behavior
- Master failover / generation handoff

Reconnect後はversion / Capability negotiationを再実行する。Operation retryではstable OperationId / BatchIdを維持する。

## 17. Error diagnostics

Compatibility・safety上のrejectは、可能な範囲でoperator/userが原因を診断できるようにする。

Version incompatibilityでは双方のsupported versionと必要なupdate directionを確認可能にする。Required Capability / addon compatibility mismatchでも原因をsilentに隠さない。

## 18. Protocol変更の流れ

1. protocol ownerが変更要求を整理する。
2. protocol設計書を先に更新する。
3. same Majorのcompatible Minor changeかMajor changeが必要なsemantic breakか判定する。
4. Capability impactを確認する。
5. common envelope / result / error contractとの整合を確認する。
6. addon meta informationに影響する場合standard/additional protocol境界を確認する。
7. 各component implementationが独立してcontractへ追従する。

Shared code変更によって暗黙に複数componentを同時変更させない。

## 19. Independent testing

各componentは相手implementation自体を必要とせずprotocol boundaryをtest可能にする。

少なくとも次を検証可能にする。

- same Major / same Minor compatibility
- same Major / different Minor backward compatibility
- no common version reject
- required Capability mismatch
- stale NegotiationGeneration reject
- retry / duplicate / idempotency
- stale Master generation reject
- same OperationId + payload digest mismatch reject
- ACKとterminal resultの区別

具体test framework/code generation方式は個別component設計で決定する。

## 20. 禁止事項

- component間code sharingをcommunication contractとすること
- shared internal type / DTO library dependency
- direct method call
- standard protocolにないimplicit behaviorへの依存
- Minor updateでsemantic compatibilityを壊すこと
- common version不在をnormal connectionとして許容すること
- required Capability不足を黙って無視すること
- stale NegotiationGenerationをcurrent semanticsで解釈すること
- standard protocolへaddon functional payload / commandを埋め込むこと
- MessageId / CorrelationIdをOperation dedup keyにすること
- network arrival orderをauthoritative world operation orderとして利用すること
- ACKをauthoritative world successと同一視すること

## 21. 詳細設計へ残す事項

P1-04で次は共通契約として確定済み。

- ProtocolEnvelopeV1
- ProtocolId / version representation
- handshake version selection
- Capability identifier / required-provided判定
- NegotiationGeneration
- addon compatibility metadata
- WorldContextV1 / OperationContextV1
- correlation / causation
- common result/error/retry taxonomy
- immutable Operation digestのprotocol inclusion/exclusion

後続または個別protocol詳細設計へ残す事項:

- concrete network transport
- concrete serialization / compression
- protocol-specific message payload schema
- reconnect/resync message set
- candidate Step / deadline具体field
- dedup retention
- state continuity sequence/token
- additional addon protocol framework
- schema tooling / code generation policy
