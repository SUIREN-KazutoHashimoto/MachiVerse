# Gateway間Protocol設計書

## 1. 所有者

本protocolのownerはGatewayです。

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

## 4. Version / Capability

- Major.Minor versioningを行う。
- Major mismatchはconnection reject。
- same Majorではnewer Minorがbackward compatibilityを維持する。
- connect時にrequired/optional Capabilityを交換する。
- Master candidateとして必要なCapability不足をsilentに許容しない。
- Master切替やaddon状態変更でeffective Capabilityが変わり得る場合、安全にrenegotiateまたはreconnectする。

Standard protocol上のaddon情報はconnection safety / compatibility用meta informationに限定し、addon functional payload/commandを本protocolへ載せません。

## 5. Master identity / generation

Gateway間通信は少なくとも意味上、次を識別できる必要があります。

- Gateway identity
- current Master identity
- Master generation / epoch

Coreがcurrent generationのauthorityです。

- old generation宛てのmessageをcurrentとして扱わない。
- stale Masterから遅れて到着したoutput/resultをblind acceptしない。
- Master不明時にnon-Masterが独断でCoreへGeneral View batchをdirect submitしない。

Exact identifier / generation formatは詳細設計で定義します。

## 6. Local Operation batch transfer

Non-Master Gatewayはlocal authn/authz・aggregation・conflict mediation済みbatchをMasterへ送ります。

Local batchには少なくとも意味上、次を追跡できる必要があります。

- source Gateway
- target Master generation
- local Batch ID
- stable Operation ID
- Operation type / target / content
- deterministic orderingに必要なlogical information
- candidate application time/Stepに必要なlogical information
- result routing context

正式fieldは詳細設計で定義します。

## 7. Batch receipt / acknowledgement

Masterはlocal batchについて少なくとも次の状態を返却可能にします。

- accepted
- rejected
- duplicate
- stale generation
- incompatible capability/protocol
- retryable temporary failure

ACK lossでsenderがsame Batch/Operationをretryしてもworld outcomeを変えない契約にします。

## 8. Stable Operation ID / idempotency

- Operation IDはconnected Gateway→Master→Core、retry、failover、reconnectを跨いで不変。
- retry時にnew Operation IDを発行しない。
- same Operation IDがworldへ二度影響しない。
- Batch IDを用いてtransfer/ACKを追跡する。
- Masterがduplicate local batchを受けてもduplicate Operationをnew requestとしてmergeしない。

Exact dedup retention/data structureは詳細設計で決定します。

## 9. Deterministic merge

Masterは自身を含む全Gatewayのlocal batchをdeterministicにmergeします。

- same effective Operation setならGateway count、source arrival timing、network latency、thread order、Master identityによらずsame logical merged orderを得る。
- Gateway-level conflict mediationはexternal-request levelに限定する。
- authoritative World StateをGatewayへ複製しsimulation ruleを再実装しない。
- simulation-affecting Admin Operationを「Adminだから」という理由だけでGeneral Operationに対し無条件最優先にしない。

Exact ordering key、priority relation、same-target merge/reject ruleは詳細設計で決定します。

## 10. Candidate application time/Step

Gateway/Masterはprotocol ruleに従いcandidate application time/Stepに必要なlogical informationを扱います。

- physical arrival wall-clockをauthoritative application timeにしない。
- reception deadline / grace / late statusを必要に応じ追跡する。
- final valid application StepをCoreが確定する前提を壊さない。
- late Operationはpast finalized Stepをretroactive rewriteしない。

Wire fieldとdefer/reject semanticsの詳細はCore↔Gateway protocolと整合させて定義します。

## 11. Master failover

Master利用不能時はCoreがnew Masterを選出します。

Gateway↔Gateway protocolは次のsafe handoffを可能にします。

- old Masterへsent済み / ACK不明local batch
- old Masterがaccepted済み / Core submit status不明batch
- retrying Operation
- result未返却Operation
- stale old-generation message

New Masterへsame Operation ID/Batch contextで再送し、loss・duplicate applyを防ぎます。

Live migrationでも同じ意味論を維持します。

## 12. Failure detectionとの関係

Master failure decisionのauthorityはCore側ですが、Gateway間通信は必要なhealth/response informationを提供可能にします。

- heartbeat / response delay等の具体方式は未確定。
- monitor interval、timeout、grace等の調整数値はConfig。
- transient delayとfailureを区別する。

## 13. Result routing

Core resultをMasterからsource Gatewayへ返却し、source Gatewayがoriginating user/session requestへ対応付けられる契約を持ちます。

Result routingでもOperation ID、Batch ID、generation等のcontextを利用可能にします。

Stale generation resultをcurrent requestへ誤対応させないようにします。

## 14. Login proxy

Q241に従い、General View / Admin View等からconnected Gatewayへ届いたlogin requestはMaster Gatewayへproxyし、Masterでlogin処理を確定します。

- non-Masterが独立に同じloginを最終確定しない。
- Master change/live migrationでsession consistencyを壊さない。
- old Master generationのauth stateをcurrent authorityとして誤使用しない。

Credential/token/IdP/session storageの具体方式は未確定です。

## 15. Addon meta information

Connection safety/compatibility判定に必要なaddon identity/version/required Capability等のmeta情報はstandard protocolで交換可能です。

Addon固有function dataをGateway間standard protocolへ載せません。必要な場合はaddon/framework側のadditional protocolの責務です。

## 16. 禁止事項

- non-MasterのGeneral View batch Core direct submission
- Major mismatchでnormal batch transfer
- stale generationをcurrentとして扱うこと
- stable Operation IDなしのretry
- duplicate Operationのnew request化
- network arrival orderだけでdeterministic mergeを決めること
- Gatewayへauthoritative simulation ruleを複製すること
- old Master auth/result stateのsilent authority化
- addon functional payloadをstandard protocolに埋め込むこと

## 17. 詳細設計へ残す事項

- physical transport / connection establishment
- Gateway identity / generation representation
- local batch wire schema
- ACK/result message format
- deterministic merge ordering key
- retry timeout / interval / queue capacity
- failover handoff messages
- health signal
- candidate Step / deadline fields
- auth mutual verification
- login proxy message/session handoff
- Capability / addon meta schema
- compression
