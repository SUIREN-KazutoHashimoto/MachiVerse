# Gateway間Protocol設計書

## 1. 所有者

本protocolのownerはGatewayです。

ProtocolIdは `mv.gateway-gateway` とする。

共通 envelope / version / Capability / result / error / correlation contractは `docs/design/phase1-protocol-envelope.md` を正本とする。

## 2. 目的

複数Gateway構成で、Coreが選出したMaster GatewayへGeneral View由来local Operation batchを安全・決定論的に集約し、Master切替、retry、result routing、login proxyを成立させるための契約です。

本protocolはWorld Stateの正本同期を目的としません。正本はSimulation Coreにあります。

## 3. 基本原則

- Gateway同士でcode、DLL、internal type、shared DTO libraryを共有しない。
- 非Master GatewayはGeneral View由来local batchをCoreへ直接送らない。
- local Gatewayはauthn/authzとlocal external-request conflict mediationを行う。
- Masterは全Gatewayのlocal batchをdeterministic mergeし、cross-Gateway external-request conflictを整理する。
- simulation rule/world-state validityはCoreの責務でありGatewayへ複製しない。
- stable Operation ID、Batch ID、Master generationをretry/failover/reconnectで維持する。
- network arrival raceやthread completion orderだけでmerge orderを決めない。
- Master failoverはlive migrationに耐える。

## 4. Common envelope / Version / Capability

normal messageは `ProtocolEnvelopeV1` の共通意味を持つ。

- protocol id: `mv.gateway-gateway`
- negotiated ProtocolVersionを明示する。
- NegotiationGenerationを明示する。
- MessageId / CorrelationId / CausationIdをtraceに使用できる。
- Master authorityに依存するmessageではWorldContextV1のMasterGenerationを使用する。
- Operation/Batch messageではOperationContextV1を使用する。
- connect時にrequired / provided Capabilityを交換する。
- Master候補として必要なCapability不足をsilentに許容しない。
- connection中のCapability changeはreconnectを基本とする。

Standard protocol上のaddon情報はconnection safety / compatibility用metadataに限定し、addon functional payload/commandを載せない。

## 5. Master identity / generation

Gateway間通信は少なくとも次を識別できる必要がある。

- Gateway identity
- current Master identity
- MasterGeneration

Coreがcurrent generationのauthority。

- old generation宛てのmessageをcurrentとして扱わない。
- stale Masterから遅れて到着したoutput/resultをblind acceptしない。
- Master不明時にnon-Masterが独断でCoreへGeneral View batchをdirect submitしない。
- MasterGenerationは共通 `uint64` 契約を使用する。

Gateway identityのconcrete representationは個別詳細設計で定義する。ComponentInstanceIdをGateway identityそのものとして扱わない。

## 6. Local Operation batch transfer

Non-Master Gatewayはlocal authn/authz・aggregation・conflict mediation済みbatchをMasterへ送る。

Local batchには少なくとも次を追跡できる必要がある。

- source Gateway
- target MasterGeneration
- local BatchId
- stable OperationId / immutable payload digest
- Operation type / target / content
- deterministic orderingに必要なlogical information
- candidate application Step / deadlineに必要なlogical information
- result routing context

共通規則:

- target generationをWorldContext `master_generation` で明示する。
- candidate Stepをauthoritative `effective_step` として表現しない。
- retry時もOperationId / BatchId / immutable digestを維持する。

## 7. Batch receipt / acknowledgement

Masterはlocal batchについて少なくとも次を返却可能にする。

- accepted
- rejected
- duplicate
- stale generation
- incompatible capability/protocol
- retryable temporary failure

共通ResultStatus / stable code / RetryAdviceを使用する。

ACKはGateway hop上の受理状態であり、Core authoritative world mutation成功を意味しない。

ACK lossでsenderがsame Batch/Operationをretryしてもworld outcomeを変えない。

## 8. Stable Operation ID / idempotency

- Operation IDはconnected Gateway→Master→Core、retry、failover、reconnectを跨いで不変。
- retry時にnew Operation IDを発行しない。
- same Operation IDがworldへ二度影響しない。
- Batch IDを用いてtransfer/ACKを追跡する。
- Masterがduplicate local batchを受けてもduplicate Operationをnew requestとしてmergeしない。
- MessageId / CorrelationIdをOperation dedup keyにしない。
- same OperationIdで異なるimmutable digestは `protocol.operation-payload-mismatch` としてrejectする。

Exact dedup retention/data structureとexpiry後のresult semanticsはP1-06で定義する。

## 9. Deterministic merge

Masterは自身を含む全Gatewayのlocal batchをdeterministicにmergeする。

- same effective Operation setならGateway count、source arrival timing、network latency、thread order、Master identityによらずsame logical merged orderを得る。
- Gateway-level conflict mediationはexternal-request levelに限定する。
- authoritative World StateをGatewayへ複製しsimulation ruleを再実装しない。
- simulation-affecting Admin Operationを「Adminだから」という理由だけでGeneral Operationに対し無条件最優先にしない。

Core authoritative orderingはP1-02のSameStepOrderKeyに従う。Gateway local / cross-Gateway merge keyは個別詳細設計で定義するが、physical arrival orderをauthorityにしない。

## 10. Candidate application Step

Gateway/Masterはprotocol ruleに従いcandidate application Step / deadline情報を扱う。

- physical arrival wall-clockをauthoritative application timeにしない。
- reception deadline / grace / late statusを必要に応じ追跡する。
- final valid application StepをCoreが確定する前提を壊さない。
- late Operationはpast finalized Stepをretroactive rewriteしない。
- WorldContext `effective_step` はCore確定済みresult等にのみ使用する。

Exact candidate/deadline/grace fieldsとdefer/reject semanticsはP1-06でCore↔Gateway protocolと整合させて定義する。

## 11. Master failover

Master利用不能時はCoreがnew Masterを選出する。

Gateway↔Gateway protocolは次のsafe handoffを可能にする。

- old Masterへsent済み / ACK不明local batch
- old Masterがaccepted済み / Core submit status不明batch
- retrying Operation
- result未返却Operation
- stale old-generation message

New Masterへsame OperationId / BatchId / immutable digestで再送し、loss・duplicate applyを防ぐ。

Live migrationでも同じ意味論を維持する。

## 12. Failure detectionとの関係

Master failure decisionのauthorityはCore側だが、Gateway間通信は必要なhealth/response informationを提供可能にする。

- heartbeat / response delay等の具体方式は個別詳細設計で定義する。
- monitor interval、timeout、grace等の調整数値はConfig。
- transient delayとfailureを区別する。
- retry timingをworld orderingへ使用しない。

## 13. Result routing

Core resultをMasterからsource Gatewayへ返却し、source Gatewayがoriginating user/session requestへ対応付けられる契約を持つ。

- CorrelationIdを可能な範囲でend-to-end維持する。
- OperationId / BatchId / MasterGenerationをcontextとして利用する。
- stale generation resultをcurrent requestへ誤対応させない。
- Operation resultとGateway hop ACKを区別する。

## 14. Login proxy

Q241に従い、General View / Admin View等からconnected Gatewayへ届いたlogin requestはMaster Gatewayへproxyし、Masterでlogin処理を確定する。

- non-Masterが独立に同じloginを最終確定しない。
- Master change/live migrationでsession consistencyを壊さない。
- old Master generationのauth stateをcurrent authorityとして誤使用しない。
- login proxy request/resultもCorrelationIdで追跡可能にする。

Credential/token/IdP/session storageの具体方式はauth詳細設計で決定する。

## 15. Addon meta information

Connection safety/compatibility判定に必要なAddonDescriptorV1相当のidentity/version/required-provided Capability/dependency metadataはstandard protocolで交換可能。

Addon固有function dataをGateway間standard protocolへ載せない。必要な場合はaddon/framework側additional protocolの責務。

## 16. 禁止事項

- non-MasterのGeneral View batch Core direct submission
- incompatible negotiated versionでnormal batch transfer
- stale NegotiationGenerationをcurrent semanticsとして扱うこと
- stale MasterGenerationをcurrentとして扱うこと
- stable Operation IDなしのretry
- duplicate Operationのnew request化
- MessageId / CorrelationIdをOperation dedup keyにすること
- candidate Stepをauthoritative effective_stepとして扱うこと
- network arrival orderだけでdeterministic mergeを決めること
- Gatewayへauthoritative simulation ruleを複製すること
- old Master auth/result stateのsilent authority化
- addon functional payloadをstandard protocolに埋め込むこと
- ACKをCore terminal successと同一視すること

## 17. 詳細設計へ残す事項

P1-04で共通化済み:

- common envelope / tracing identity
- version / Capability handshake
- NegotiationGeneration
- common result/error/retry
- MasterGeneration context
- Operation immutable digest boundary

残る個別事項:

- physical transport / connection establishment
- serialization / compression
- Gateway identity
- local batch payload schema
- Gateway local / cross-Gateway merge key
- Batch ACK / partial progress state machine
- retry timeout / interval / queue capacity
- failover handoff messages
- health signal
- candidate Step / deadline / grace fields
- auth mutual verification
- login proxy/session handoff
- dedup retention
