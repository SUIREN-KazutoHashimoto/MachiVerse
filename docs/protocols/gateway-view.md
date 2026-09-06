# Gateway・General View間Protocol設計書

## 1. 所有者

本protocolのownerはGatewayです。

ProtocolIdは `mv.gateway-view` とする。

共通 envelope / version / Capability / result / error / correlation contractは `docs/design/phase1-protocol-envelope.md` を正本とする。

State continuity / recovery basisは `docs/design/phase1-persistence-replay-recovery.md` を正本とする。

## 2. 目的

General Viewが利用者roleに応じてworldを参照・参加・操作し、Gatewayのconfirmed publication state、同期状態、Operation resultを安全に扱うための契約です。

General ViewはWeb UIであり、world renderingにはThree.jsを使用する。ただしThree.jsはView側presentationであり、authoritative World StateはSimulation Coreにある。

## 3. 利用者role

本protocolは次の4roleを扱う。

1. Diver
2. Spectator
3. Moderator
4. Administrator

- Diver: existing residentとしてworldへ参加し、通常residentと同程度のsimulation interferenceを持つ。
- Spectator: simulationへinterferenceせず、system vitalに関係しない範囲の公開statusを参照する。
- Moderator: simulationおよびlower userへ限定的にinterfere可能だがcritical operationは禁止。
- Administrator: General View上でsimulationへ広範にinterfere可能で、General View向け全公開statusを参照可能。

General View AdministratorはAdmin View operatorとは別auth/authz domain。

各roleのexact API/operation list、「vitalではないstatus」「critical interference」の具体定義は詳細設計で決定する。

## 4. 設計原則

- General ViewとGatewayはcode、DLL、internal type、shared DTO libraryを共有しない。
- requestはGatewayでauthn/authzする。
- UI hide/showだけでauthorizationを完結させない。
- unauthorized OperationをCoreへ送らない。
- ViewはGateway cache/internal structureへ依存しない。
- View display predictionをauthoritative stateとして扱わない。
- protocol version / Capability mismatchをsilentに無視しない。
- addon functional payloadをstandard protocolへ載せない。

## 5. Common envelope / Version / Capability

normal messageは `ProtocolEnvelopeV1` の共通意味を持つ。

- protocol id: `mv.gateway-view`
- negotiated ProtocolVersionとNegotiationGenerationを明示する。
- MessageId / CorrelationId / CausationIdをtraceに使用できる。
- world state/publicationではWorldContextV1を使用する。
- world-affecting request/resultではOperationContextV1を使用する。
- connect時にrequired / provided Capabilityをnegotiationする。
- required Capability不足をsilentに無視しない。
- reconnect時はnegotiationをやり直す。

Addon関連はinstall/identity/version/required-provided Capability/dependency等のcompatibility metadataだけをstandard protocolで扱い、Addon functional data/commandは載せない。

## 6. Auth / login / session

General View userはconnected Gatewayへlogin requestを送る。

Q241に従い、Gateway側ではそのlogin requestをMaster Gatewayへproxyし、login処理はMasterで確定する。General Viewから見たexternal contractはconnected Gatewayとの間に維持する。

Protocolは少なくとも次の状態を扱える必要がある。

- unauthenticated
- authentication in progress
- authenticated session
- authorization/role information
- session invalid/revoked
- reconnect/resync中のsession state

login request/resultはCorrelationIdで追跡可能にする。

MessageId、CorrelationId、sender ComponentInstanceIdをcredentialとして扱わない。

具体credential、token、IdP、session formatはauth詳細設計で決定する。

## 7. Role change / revoke

- connection中にroleが変わり得る。
- role changeには明示的なeffective pointを持たせる。
- change前にaccepted済みOperationとchange後のnew Operationを区別できるようにする。
- privilege revoke後にold privilegeでnew Operationを送信・成立させない。
- severe revokeではexisting session/credential invalidationをViewへ通知可能にする。

Role/permission変更に伴いrequired Capabilityやconnection semanticsが変化する場合、Phase 1標準ではreconnectして再negotiationする。

## 8. State publication

General ViewはGatewayがpublishするconfirmed stateを基準に表示する。

State publicationは少なくとも次を判別可能にする。

- WorldId
- display basisとなるSimulation Step
- confirmed stateであること
- Core-derived `StateContinuityToken`
- deltaの場合のbase continuity token
- resync state

WorldContextV1の `basis_step` をconfirmed state basisとして使用する。

### 8.1 StateContinuityToken

P1-05の `StateContinuityToken` をconfirmed publication chainのcontinuity識別に使用できる。

- GatewayはCoreから確認したtokenをViewへ伝播する。
- Gatewayが独自にauthoritative-looking continuity tokenを生成しない。
- process restart / Gateway changeを理由に同一committed stateのtokenを再採番しない。
- deltaのbase tokenとViewが保持するconfirmed tokenが一致しない場合、そのdeltaをblind applyしない。
- mismatch時はGatewayへresync / full state取得を要求できる。
- tokenはstate equality proofではなくcausal continuity識別子である。

互換性のない異なるWorld Time/Step/tokenのstateを1つのauthoritative-looking displayとして混在させない。

## 9. Interpolation / prediction

Smooth displayのため、Viewはpresentation layerでinterpolationやshort predictionを行える。

- prediction/interpolationはnon-authoritative。
- Core/Gateway confirmed stateと区別する。
- predictionがworld outcomeへ影響してはならない。
- confirmed stateと異なる場合はreconcile/correctする。
- predicted presentation stateのStepをWorldContext `basis_step`としてauthoritativeに見せない。
- predicted stateにconfirmed `StateContinuityToken`を付与してauthoritativeに見せない。

Exact prediction model、correction UI、animationはView詳細設計で決定する。

## 10. Diver操作のreal-time体験

Q232に従い、Gateway publication delayが存在してもDiverからはreal-timeに操作しているように感じられる体験を目指す。

- View側local prediction / immediate feedbackを許容する。
- predicted resultをauthoritative mutationとして扱わない。
- Core confirmed result到着時にreconcileする。
- correctionが必要な場合でもworld outcomeはCoreのauthoritative resultに従う。

## 11. Diverとresidentのbinding

### 11.1 新規residentを生成しない

- Diver joinのために専用new residentを生成しない。
- worldにexistingな通常residentへbindする。
- Diver-controlled residentも通常のphysics、social、economy、law、health等のworld ruleに従う。

### 11.2 希望条件

- Diverはbind対象についてbroad preferenceをrequest可能にする方向とする。
- 希望条件を満たすresidentが割り当てられることは保証しない。
- arbitrary residentを無条件にtake overできるprotocolにはしない。

Preference fieldやmatching ruleは詳細設計で決定する。

### 11.3 1 resident / 1 Diver

- 原則1residentにつき1Diver。
- disconnectを理由に別Diverへ自動的にbindingを移さない。
- concurrent duplicate controlを成立させない。

### 11.4 same Diver identity

- reconnectしても必ず同じDiver identityを使う。
- normal disconnect / error disconnectでDiver identityの扱いを変えない。

### 11.5 resident death

- controlled residentが死亡した場合、そのresidentは通常のworld death semanticsに従う。
- Diver identityは消滅させない。
- next participationで別existing residentへbind可能なruleを持てるが、new resident生成はしない。

## 12. Disconnect中のresident

- Disconnectしてもresidentをworldからremoveしない。
- World Timeをrewindしない。
- normal disconnectとerror disconnectを同じ基本semanticsで扱う。
- Diverは不在時にresidentへ優先させるbehavior/action方針を事前設定可能にする。

Exact absence-policy schemaやAI behavior implementationは未確定。

## 13. Reconnect / resync

Reconnect時は可能な限りsame session / same Diver identityを復元し、Gatewayのcurrent publication basisへ同期してからnormal displayへ戻る。

- reconnectでversion / Capability negotiationを再実行する。
- Viewはsyncing/resyncing状態を明示表示できる。
- GatewayがCore recovery / resync中の場合、それをconnected userへ通知する。
- inconsistent state sequenceをnormal confirmed stateとして表示しない。
- old cached View stateをcurrent authoritative-looking stateとしてblind reuseしない。
- new connectionのNegotiationGenerationは1から開始する。
- old confirmed `StateContinuityToken` とGateway current tokenが一致すればprotocol固有ruleに従いcontinuation可能。
- token不一致、basis gap、unknown baseの場合はfull resync / confirmed rebuildを行う。

## 14. Operation request

General Viewからのworld-affecting requestはstable OperationとしてGatewayへ送る。

Gatewayはrole/session/operation type/target等を検証し、authorized requestだけをdownstreamへ送る。

Protocolは少なくとも次を扱える必要がある。

- CorrelationId
- stable OperationId / immutable payload digest
- requested operation type / target / content
- requested/candidate Step制約がある場合の明示field
- accepted / rejected / pending
- final result
- authoritative effective Step（Core確定後）

規則:

- MessageId / CorrelationIdをOperation dedup keyにしない。
- retry時にOperationIdを再採番しない。
- same OperationId + different immutable digestはrejectする。
- Viewが希望するcandidate/requested StepをWorldContext `effective_step`として送らない。
- applied resultでは該当する場合Core確定 `effective_step` を表示可能にする。

Exact operation-specific payloadは個別詳細設計で定義する。

## 15. Spectator / Moderator / Administrator

### Spectator

- simulation mutation requestは許可しない。
- public non-vital statusのみ参照可能。

### Moderator

- defined lower-user / simulation operationsのみ許可する。
- critical operationはGatewayでrejectする。

### General View Administrator

- General Viewで定義されたsimulation interferenceを行える。
- Admin View専用のcomponent log、Config management、system operational commandを本protocolへ混在させない。

## 16. Operation result / error

共通ResultStatus / stable ResultCode / RetryAdviceを使用する。

少なくとも次を区別可能にする。

- success
- accepted / pending
- authorization reject
- protocol/validation reject
- world-state/simulation-rule reject
- duplicate / already processed
- temporarily unavailable / resyncing
- session expired/revoked
- capability/version incompatibility
- late Operation

Machine behaviorはdiagnostic textではなくstable codeで分岐する。

ACK / acceptedをCore authoritative terminal mutation successと同一視しない。

Core由来terminal successが返ってきた場合、P1-05のdurable transition/result boundaryを通過したauthoritative resultとして扱える。

## 17. Protocol version / Capability

Common handshakeはP1-04正本に従う。

- 共通versionが存在しなければconnection reject。
- same MajorのMinor compatibilityを維持する。
- required Capability mismatchを明示する。
- reconnectはrenegotiationの基本境界。
- optional live renegotiationは双方のCapabilityと個別barrier設計がある場合のみ許可する。

## 18. 禁止事項

- Gateway bypassによるCore direct access
- UI-side authorizationだけでworld Operationを許可すること
- Spectatorのsimulation mutation
- Moderatorのundefined critical operation
- General View AdministratorとAdmin View operatorの同一視
- Diver join時の専用new resident自動生成
- disconnect時のautomatic Diver swap
- View predictionのauthoritative化
- prediction stateをconfirmed basis_step / continuity tokenとして表現すること
- resync中のinconsistent stateをnormal confirmed stateとして表示すること
- shared DTO library dependency
- incompatible negotiated versionでnormal connection
- required Capability不足のsilent degradation
- MessageId / CorrelationIdをOperation dedup keyにすること
- candidate Stepをauthoritative effective_stepとして送ること
- standard protocolへのaddon functional payload
- ACKをauthoritative world successと同一視すること

## 19. 詳細設計へ残す事項

P1-04 / P1-05で共通化済み:

- common envelope / tracing identity
- version / Capability handshake
- NegotiationGeneration
- common result/error/retry
- WorldContext / OperationContext
- immutable Operation digest boundary
- StateContinuityToken semantics
- recovery後のconfirmed publication continuity rule

残る個別事項:

- physical transport / serialization / compression
- auth credential / session representation
- exact role permission matrix
- public status field set
- critical operation definition
- Diver preference/matching schema
- resident binding identifier/schema
- absence behavior policy schema
- interpolation/prediction/correction messages
- state publication full/delta payload strategy
- Operation payload schema
- resync notification/status representation
- pagination/range request
- dedup retention / duplicate result expiry
