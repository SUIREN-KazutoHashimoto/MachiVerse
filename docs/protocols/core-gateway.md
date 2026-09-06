# Simulation Core・Gateway間Protocol設計書

## 1. 所有者

本protocolのownerはSimulation Coreです。

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

Concrete wire schema、transport、serializationは本段階では固定しません。

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

## 4. Version / Capability

- Protocol versionはMajor.Minor。
- Major mismatchはconnection reject。
- same Majorではnewer Minorがbackward compatibilityを維持する。
- connect時にsupported / required Capabilityを交換する。
- required Capability不足はsilentに続行しない。
- Major mismatch時はreject reason、双方version、必要なupdate directionをdiagnostic可能にする。

Addonについてstandard protocolで交換できるのは、connection safety / compatibility判定に必要なmeta informationに限定します。Addon固有function payloadやcommandを本protocolへ載せません。

## 5. State synchronization

CoreはGatewayが外部公開stateを構築できるよう、authoritative-derived stateを提供します。

State communicationには少なくとも意味上、次を判別できる必要があります。

- どのSimulation Stepをbasisとするstateか
- full / delta等を採用する場合の適用basis
- reconnect/resyncでcontinuityを検証するための情報
- 必要なprotocol / Capability context

Gatewayはold cacheをblind trustせず、reconnect時にCoreまたはprotocol上のauthoritative sync basisから再同期します。

具体的なPush/Pull、full/delta/snapshot、sequence fieldは詳細設計で決定します。

## 6. Gateway接続とMaster eligibility

CoreはGateway connectionを管理し、Master候補として安全かを判断できる情報を持ちます。

Master candidateは単なるconnected状態だけでなく、少なくとも次の意味を満たす必要があります。

- responsive
- compatible protocol Major/Minor
- required Capability
- Masterとして必要なsync state
- その他、安全にfinal batchを形成・送信できる状態

Exact health signal、threshold、timeoutはConfigと詳細protocolで定義します。

## 7. Master selection / generation

- Master Gatewayのselection authorityはCore。
- Safe candidateからrandomに1台を選出する。
- Master selection結果そのものをWorld Seedから再現する標準要件はない。
- 選択されたMaster identity、generation/epoch、切替理由等をdiagnostic可能にする。
- current generationをCoreがauthoritativeに決定する。
- stale old-generation final batchをcurrent outputとして受理しない。
- Master identityがworld outcomeへ影響してはならない。

Concrete election message、random algorithm、Gateway identifier、generation formatは詳細設計で決定します。

## 8. General View由来final Operation batch

General View Operationは各Gatewayでlocal authn/authz・aggregation・external-request conflict mediationを受け、Masterでdeterministic merge/cross-Gateway mediationされたfinal batchとしてCoreへ送られます。

Final batchには少なくとも意味上、次を追跡できる必要があります。

- Master generation
- Batch ID
- Operation ID
- source Gateway / result routing context
- Operation type / target / content
- deterministic orderingに必要なlogical information
- candidate application time/Stepに必要なlogical information

正式fieldは詳細設計で定義します。

## 9. Operation ID / Batch ID / idempotency

- Operation IDはGateway hop、Master transfer、retry、failover、reconnectを跨いでstable。
- same Operation IDがworldへ二度影響しない。
- retry時にnew Operationとして再採番しない。
- Batch IDとMaster generationでACK loss、retry、old-generation outputを追跡可能にする。
- duplicate requestに対しworld mutationをrepeatしない意味論を持つ。

Dedup retention period、storage/data structure、Batch ID format等は詳細設計で決定します。

## 10. Deterministic ordering

Coreへ渡されるfinal batch orderは、network raceやthread completion orderだけで決めません。

Same effective Operation setでは、Gateway数、Master identity、network timing等が異なってもsame logical Core orderになる必要があります。

具体的ordering key、same-Step tie-break ruleは詳細設計で決定します。

## 11. Candidate / final application Step

Q203/Q223/Q224/Q276に従います。

- Gateway/Masterはprotocol ruleに従いcandidate application time/Stepに必要な情報を形成する。
- Coreがcurrent Simulation Step、deadline、Master generation、deterministic order等からfinal valid application Stepを確定する。
- network arrival wall-clockをauthoritative application timeにしない。
- late Operationでpast finalized Stepをretroactive rewriteしない。
- late Operationはdefined ruleに従いfuture valid Stepへdeferまたはrejectする。

Exact candidate field、deadline/grace field、reject/defer codeは詳細設計で定義します。

## 12. Core側のGeneral Operation validity

Coreはfinal batchをauthoritative stateへ適用する前に、common world/simulation semanticsとして少なくとも次を確認できます。

- target existence / validity
- current stateからのstate transition成立性
- common world-state invariant
- simulation rule
- deterministic apply order

Masterで外部要求競合が整理済みでも、authoritative world上成立しないOperationはreject可能です。

Batch全件atomicかpartial successを許可するかは詳細設計で決定します。

## 13. Admin Operation

Admin View由来Core OperationもGatewayから本protocolを通じてCoreへ到達します。

責務は次のように分離します。

- Gateway: Admin authn/authz、operation format、target、Admin operationとしてのallowed condition等。
- Core: UI roleを解釈せず、全Operation共通のworld-state invariant / state-transition consistencyを維持。

Gateway-approved Admin Operationであってもcommon invariantを破壊するstate transitionをCoreが無条件適用してはなりません。

Login処理はGateway↔Gateway側でMasterへproxyして確定します。Login以外のAdmin Core OperationをMaster経由へ統一するかは未確定です。

## 14. Operation result

CoreはOperation/Batch resultを、元requestへ対応付け可能なidentity contextと共に返却できる契約を持ちます。

Resultには成功/拒否だけでなく、必要に応じreason、final application Step、duplicate/stale generation等を識別できる意味が必要です。

具体message/codeは詳細設計で定義します。

## 15. Master failover / live migration

- CoreはMaster failureを検出した場合safe candidateからnew Masterを選出する。
- unfinished batch、ACK-waiting Operation、retrying Operationをloss/duplicateなくnew generationへ引き継げる意味論を持つ。
- live migrationに耐える。
- old generation outputをrejectする。
- failover timingそのものがworld outcomeを変えない。

Exact handoff message、heartbeat、timeout等は詳細設計で定義します。

## 16. Gateway reconnect / resync

- reconnect時にold cacheをauthoritativeとして扱わない。
- basis Simulation Step / generation等を確認してresyncする。
- missing/reorder/sync mismatch時にrefetch/rebuild可能にする。
- Gatewayはresync中のinconsistent state sequenceをnormal publishしてはならない。

Userへのresync表示はGateway↔View protocolの責務です。

## 17. Gatewayが0台の場合

Gateway connectionが0台でもCoreのSimulation Stepはそれ自体を理由に停止しません。

Protocol上、新規external requestが存在しないだけであり、Core internal eventと既にaccepted済みOperationは通常規則に従って進行します。

Gateway復旧後にabsence期間をrewindしません。

## 18. Diagnostic / operational state

Gatewayが外部運用へ必要な範囲で、次のようなCore状態をprotocol化可能にします。

- current Simulation Step
- real-time target lag等のhealth
- current Master / generation
- save / recovery state
- compatibility / Capability error
- relevant Config validation error

具体metrics/schemaはobservability詳細設計で定義します。

## 19. 禁止事項

- non-MasterからGeneral View final batchをnormal writeとして受理すること
- stale Master generationをcurrentとして受理すること
- stable Operation IDなしのretry
- duplicate Operationのdouble apply
- network arrival orderのauthoritative ordering化
- Gateway cacheをauthoritative stateとして扱う契約
- Admin UI roleをCore authzへ持ち込むこと
- standard protocolへaddon functional payloadを載せること
- Major mismatchでnormal communicationを継続すること

## 20. 詳細設計へ残す事項

- physical transport / connection direction
- serialization / compression
- handshake / Capability schema
- state sync method and fields
- Gateway identifier / Master generation representation
- Master health/election messages and algorithm
- final batch wire schema
- deterministic ordering key
- candidate/final Step fields
- Batch atomicity / partial success
- dedup retention and storage
- result message/error codes
- Admin login以外のMaster path
- timeout / reconnect / resync message set
