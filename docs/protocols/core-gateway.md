# Simulation Core・Gateway間Protocol設計書

## 1. 所有者

本protocolのownerはSimulation Coreです。

ProtocolIdは `mv.core-gateway` とする。

共通 envelope / version / Capability / result / error / correlation contractは `docs/design/phase1-protocol-envelope.md` を正本とする。

Persistence / replay / recovery / durability contractは `docs/design/phase1-persistence-replay-recovery.md` を正本とする。

## 2. 目的

本protocolは、単一Simulation Coreと複数Gatewayの間で、少なくとも次を成立させる契約です。

- 外部公開用stateの同期
- Gateway cache / logical publication bufferに必要なSimulation Step基準の提供
- Gateway接続状態・同期状態の把握
- Master Gatewayの選出・generation通知
- MasterからのGeneral View由来final Operation batch受理
- Admin View由来Core Operationの受理
- Operation resultの返却
- retry / dedup / idempotency
- protocol version / Capability negotiation
- Coreの公開可能なhealth / diagnostic stateの通知

Physical transport、serialization、compressionは個別protocol詳細設計事項として残す。

## 3. 基本原則

- CoreとGatewayは互いのcode、DLL、internal type、shared DTO libraryへ依存しない。
- Coreだけがauthoritative World Stateを所有する。
- Gateway cacheは非権威な派生state。
- 権威あるWorld Timeは整数Simulation Step。
- Core standard frequencyは30Hzだが、Gateway state publication frequencyと同一ではない。
- General View由来Core writeはcurrent Master Gatewayからfinal batchとして受理する。
- Gateway/Masterのexternal-request conflict mediationと、Coreのworld-state/simulation-rule validityを分離する。
- Admin Operation固有のauth/permission/format/target/allowed-condition validationはGateway責務。Coreはcommon world-state invariantを維持する。
- network arrival timing、Gateway数、Master identity、retry countをworld outcomeの暗黙入力にしない。
- Coreがexternally finalizedとして公開するstate / resultはP1-05のdurable frontierを越えてはならない。

## 4. Common envelope / Version / Capability

normal messageは `ProtocolEnvelopeV1` の共通意味を持つ。

- protocol id: `mv.core-gateway`
- negotiated ProtocolVersion: `uint16 major + uint16 minor`
- NegotiationGenerationを明示する。
- MessageId / CorrelationId / CausationIdをtraceに利用できる。
- world-related messageはWorldContextV1を利用できる。
- Operation/Batch関連messageはOperationContextV1を利用する。
- connect時にprovided / required Capabilityを交換する。
- required Capability不足はsilentに続行しない。
- connection中のCapability変化はreconnectを基本とする。

Addonについてstandard protocolで交換できるのはconnection safety / compatibility判定に必要なmetadataに限定し、Addon固有function payloadやcommandを載せない。

## 5. State synchronization

CoreはGatewayが外部公開stateを構築できるよう、authoritative-derived stateを提供する。

State communicationには少なくとも次を判別できる必要がある。

- WorldId
- basis Simulation Step
- `StateContinuityToken`
- full / delta等を採用する場合のbase continuity token
- reconnect/resyncでcontinuityを検証するための情報
- protocol / Capability context

共通WorldContextではstate basisを `basis_step` として表現する。

P1-05に従い、Coreはtransition commitがdurableになる前のStateをauthoritative confirmed stateとしてpublishしない。

### 5.1 continuity

confirmed state publicationは必要に応じ次を持つ。

```text
basis_step
state_continuity_token
base_state_continuity_token | NONE
```

- `state_continuity_token` はCoreのcommitted causal historyから導出する。
- process restartでtokenをprocess-local sequenceとして再採番しない。
- deltaのbase tokenとGateway保持tokenが一致しなければblind applyしない。
- mismatch時はfull resync / protocol-defined rebuildへ移行する。
- tokenはworld-state equality hashではなくcausal continuity tokenである。

Gatewayはold cacheをblind trustせず、reconnect時にCoreのcurrent finalized basisから再同期する。

Push/Pull、full/delta payload schemaは個別protocol詳細設計で決定する。

## 6. Gateway接続とMaster eligibility

CoreはGateway connectionを管理し、Master候補として安全かを判断できる情報を持つ。

Master candidateは少なくとも次を満たす。

- responsive
- compatible negotiated protocol version
- required Capability
- Masterとして必要なsync state
- その他、安全にfinal batchを形成・送信できる状態

Exact health signal、threshold、timeoutはConfigと詳細protocolで定義する。

## 7. Master selection / generation

- Master Gatewayのselection authorityはCore。
- Safe candidateからrandomに1台を選出する。
- Master selection結果そのものをWorld Seedから再現する標準要件はない。
- 選択されたMaster identity、generation/epoch、切替理由等をdiagnostic可能にする。
- current generationをCoreがauthoritativeに決定する。
- stale old-generation final batchをcurrent outputとして受理しない。
- Master identityがworld outcomeへ影響してはならない。

MasterGenerationはPhase 1共通契約の `uint64` を使用する。authority/routing validityに依存するmessageではWorldContextV1の `master_generation` を使用する。

Recovery直後はpre-crash Master authorityを無条件に再信頼せず、protocol handshake / Core authorityを再確立してからnormal writeを受理する。

Concrete election message、Master identity、health/election algorithmは個別詳細設計で決定する。

## 8. General View由来final Operation batch

General View Operationは各Gatewayでlocal authn/authz・aggregation・external-request conflict mediationを受け、Masterでdeterministic merge/cross-Gateway mediationされたfinal batchとしてCoreへ送る。

Final batchは少なくとも次を追跡できる必要がある。

- WorldId
- current MasterGeneration
- BatchId
- 各OperationId / immutable payload digest
- source Gateway / result routing context
- Operation type / target / content
- deterministic orderingに必要なlogical information
- candidate application Step / deadlineに必要なlogical information

共通規則:

- Core確定前のcandidate StepをWorldContext `effective_step`へ入れない。
- final batch submit時の `effective_step` はNONE。
- same OperationIdでimmutable digestが異なる場合は `protocol.operation-payload-mismatch` としてrejectする。
- BatchId単独をOperation dedup keyにしない。

## 9. Operation ID / Batch ID / idempotency

- Operation IDはGateway hop、Master transfer、retry、failover、reconnectを跨いでstable。
- same Operation IDがworldへ二度影響しない。
- retry時にnew Operationとして再採番しない。
- Batch IDとMaster generationでACK loss、retry、old-generation outputを追跡可能にする。
- duplicate requestに対しworld mutationをrepeatしない。
- MessageId / CorrelationIdをOperation dedup keyにしない。

Coreのdedup stateはP1-05のSnapshot / durable historyからrecovery可能にする。

Exact dedup retention period、retention expiry後のduplicate resultはP1-06で決定する。

## 10. Deterministic ordering

Coreへ渡されるfinal batch orderはnetwork raceやthread completion orderだけで決めない。

Same effective Operation setではGateway数、Master identity、network timing等が異なってもsame logical Core orderになる必要がある。

Core authoritative same-Step orderingは `docs/design/phase1-determinism-ordering-random.md` のSameStepOrderKeyに従う。

Gateway側external-request mergeの具体keyは本protocol / Gateway↔Gateway protocol詳細で定義するが、physical arrival orderをauthorityにしない。

## 11. Candidate / final application Step

Q203/Q223/Q224/Q276に従う。

- Gateway/Masterはprotocol ruleに従いcandidate application Step / deadline情報を形成する。
- Coreがcurrent Simulation Step、deadline、Master generation、deterministic order等からfinal valid application Stepを確定する。
- network arrival wall-clockをauthoritative application timeにしない。
- late Operationでpast finalized Stepをretroactive rewriteしない。
- late Operationはdefined ruleに従いfuture valid Stepへdeferまたはrejectする。

WorldContextV1では次を区別する。

- `basis_step`: state basis。
- `effective_step`: Core確定済みauthoritative apply Stepのみ。

Coreがfinal effective Stepをdurable scheduling factとして確定した場合、recovery後に別Stepへsilent reassignmentしない。

Exact candidate/deadline/grace fieldsとdefer/reject algorithmはP1-06で定義する。

## 12. Core側のGeneral Operation validity

Coreはfinal batchをauthoritative stateへ適用する前に少なくとも次を確認できる。

- target existence / validity
- current stateからのstate transition成立性
- common world-state invariant
- simulation rule
- deterministic apply order

Masterでexternal-request conflictが整理済みでもauthoritative world上成立しないOperationはreject可能。

Batch全件atomicかpartial successを許可するかはP1-06のBatch state machineで決定する。

## 13. Admin Operation

Admin View由来Core OperationもGatewayから本protocolを通じてCoreへ到達する。

責務:

- Gateway: Admin authn/authz、operation format、target、Admin operationとしてのallowed condition。
- Core: UI roleを解釈せず、全Operation共通のworld-state invariant / state-transition consistency。

Gateway-approved Admin Operationであってもcommon invariantを破壊するstate transitionをCoreが無条件適用してはならない。

Login処理はGateway↔Gateway側でMasterへproxyして確定する。Login以外のAdmin Core OperationをMaster経由へ統一するかは個別routing詳細で決定する。

## 14. Operation result / error / durability

Operation/Batch resultは元requestへ対応付け可能なCorrelationIdとOperationContextを持つ。

共通ResultStatus / stable code / RetryAdviceはP1-04共通契約に従う。

Applied Operation resultでは該当する場合、WorldContext `effective_step` にCore確定Stepを設定する。

少なくとも次を識別可能にする。

- success
- accepted / pending
- duplicate
- world-state reject
- late Operation
- stale MasterGeneration
- protocol / Capability incompatibility
- temporarily unavailable
- internal failure

### 14.1 authoritative `ACCEPTED`

Coreがworld-affecting Operationについて`ACCEPTED`を返す場合、P1-05の`OperationAcceptedRecordV1`を先にdurableにする。

- ACK直後にcrashしてもOperationId / immutable payload / recovery scheduling contextを失わない。
- durable acceptance前のcrashではsenderがsame identityでretryできる。

### 14.2 applied terminal result

applied Operationのterminal `SUCCESS` / world-state terminal resultは、対応する`TransitionCommitRecordV1`がdurableになる前に返さない。

- terminal resultとeffective Stepをrecovery後に再構成可能にする。
- retention範囲内のduplicateへoriginal semantic resultを返せる。

### 14.3 ACK scope

Gateway↔Master等のhop-local ACKはCore authoritative acceptance / terminal mutation successと同一視しない。

各ACK schemaは「memory receipt」「durable local custody」「Core durable acceptance」等、自身が保証するscopeを明示する。

## 15. Master failover / live migration

- CoreはMaster failureを検出した場合safe candidateからnew Masterを選出する。
- unfinished batch、ACK-waiting Operation、retrying Operationをloss/duplicateなくnew generationへ引き継げる意味論を持つ。
- live migrationに耐える。
- old generation outputをrejectする。
- failover timingそのものがworld outcomeを変えない。
- retry後もOperationId / BatchId / immutable digestを維持する。

Coreでdurably accepted済みのOperationはMaster failoverを理由に失わない。exact custody state machineはP1-06で定義する。

## 16. Gateway reconnect / resync

- reconnect時にversion / Capability handshakeを再実行する。
- old cacheをauthoritativeとして扱わない。
- basis Simulation Step / StateContinuityToken / generation等を確認してresyncする。
- missing/reorder/sync mismatch時にrefetch/rebuild可能にする。
- Gatewayはresync中のinconsistent state sequenceをnormal publishしてはならない。
- recovery後のCore current finalized basisがGateway保持tokenと一致しない場合full resyncする。

## 17. Gatewayが0台の場合

Gateway connectionが0台でもCoreのSimulation Stepはそれ自体を理由に停止しない。

新規external requestが存在しないだけであり、Core internal eventと既にaccepted済みOperationは通常規則に従って進行する。

Gateway復旧後にabsence期間をrewindしない。

## 18. Diagnostic / operational state

Gatewayが外部運用へ必要な範囲で次をprotocol化可能にする。

- current Simulation Step / last durable finalized Step
- real-time target lag等のhealth
- current Master / generation
- save / recovery state
- persistence unavailable / degraded state
- compatibility / Capability error
- relevant Config generation / validation error

ConfigGenerationを含める場合、WorldContextの値はsenderであるCoreのeffective Config generationを意味する。

具体metrics/schemaはobservability詳細設計で定義する。

## 19. 禁止事項

- non-MasterからGeneral View final batchをnormal writeとして受理すること
- stale Master generationをcurrentとして受理すること
- stable Operation IDなしのretry
- duplicate Operationのdouble apply
- MessageId / CorrelationIdをOperation dedup keyにすること
- candidate Stepをauthoritative effective_stepとして送ること
- network arrival orderのauthoritative ordering化
- Gateway cacheをauthoritative state / recovery sourceとして扱う契約
- Admin UI roleをCore authzへ持ち込むこと
- standard protocolへaddon functional payloadを載せること
- negotiated compatibility不成立でnormal communicationを継続すること
- hop ACKをterminal world successと同一視すること
- durable Operation acceptance前のauthoritative `ACCEPTED`
- transition commit前のauthoritative State publication / applied terminal result

## 20. 詳細設計へ残す事項

P1-04 / P1-05で共通化済み:

- common envelope
- version field / handshake
- Capability schema基本形
- common result/error/retry
- correlation/causation
- MasterGeneration / basis/effective Stepの共通context
- Operation immutable digest inclusion/exclusion
- StateContinuityToken semantics
- durable Operation acceptance / terminal result boundary
- finalized Step / recovery checkpoint semantics

残る個別事項:

- physical transport / connection direction
- serialization / compression
- state sync full/delta payload schema
- Gateway identifier / Master election messages
- final batch payload schema
- candidate Step / deadline / grace fields
- Batch atomicity / partial success state machine
- dedup retention / terminal result expiry
- login以外Admin routing
- heartbeat / timeout / reconnect / resync message set
