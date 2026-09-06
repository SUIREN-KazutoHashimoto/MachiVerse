# Gateway・General View間Protocol設計書

## 1. 所有者

本protocolのownerはGatewayです。

## 2. 目的

General Viewが利用者roleに応じてworldを参照・参加・操作し、Gatewayのconfirmed publication state、同期状態、Operation resultを安全に扱うための契約です。

General ViewはWeb UIであり、world renderingにはThree.jsを使用します。ただしThree.jsはView側presentationであり、authoritative World StateはSimulation Coreにあります。

## 3. 利用者role

本protocolは次の4roleを扱います。

1. Diver
2. Spectator
3. Moderator
4. Administrator

- Diver: existing residentとしてworldへ参加し、通常residentと同程度のsimulation interferenceを持つ。
- Spectator: simulationへinterferenceせず、system vitalに関係しない範囲の公開statusを参照する。
- Moderator: simulationおよびlower userへ限定的にinterfere可能だがcritical operationは禁止。
- Administrator: General View上でsimulationへ広範にinterfere可能で、General View向け全公開statusを参照可能。

General View AdministratorはAdmin View operatorとは別auth/authz domainです。

各roleのexact API/operation list、「vitalではないstatus」「critical interference」の具体定義は詳細設計で決定します。

## 4. 設計原則

- General ViewとGatewayはcode、DLL、internal type、shared DTO libraryを共有しない。
- requestはGatewayでauthn/authzする。
- UI hide/showだけでauthorizationを完結させない。
- unauthorized OperationをCoreへ送らない。
- ViewはGateway cache/internal structureへ依存しない。
- View display predictionをauthoritative stateとして扱わない。
- protocol version / Capability mismatchをsilentに無視しない。
- addon functional payloadをstandard protocolへ載せない。

## 5. Auth / login / session

General View userはconnected Gatewayへlogin requestを送ります。

Q241に従い、Gateway側ではそのlogin requestをMaster Gatewayへproxyし、login処理はMasterで確定します。General Viewから見た外部contractはconnected Gatewayとの間に維持します。

Protocolは少なくとも次の状態を扱える必要があります。

- unauthenticated
- authentication in progress
- authenticated session
- authorization/role information
- session invalid/revoked
- reconnect/resync中のsession state

具体credential、token、IdP、session formatは未確定です。

## 6. Role change / revoke

- connection中にroleが変わり得る。
- role changeには明示的なeffective pointを持たせる。
- change前にaccepted済みOperationとchange後のnew Operationを区別できるようにする。
- privilege revoke後にold privilegeでnew Operationを送信・成立させない。
- severe revokeではexisting session/credential invalidationをViewへ通知可能にする。

## 7. State publication

General ViewはGatewayがpublishするconfirmed stateを基準に表示します。

State publicationには意味上少なくとも次を判別できる必要があります。

- display basisとなるWorld Time / Simulation Step
- confirmed stateであること
- continuity / freshness判断に必要な情報
- resync state

互換性のない異なるWorld Time/Stepのstateを1つのauthoritative-looking displayとして混在させません。

## 8. Interpolation / prediction

Smooth displayのため、Viewはpresentation layerでinterpolationやshort predictionを行えます。

- prediction/interpolationはnon-authoritative。
- Core/Gateway confirmed stateと区別する。
- predictionがworld outcomeへ影響してはならない。
- confirmed stateと異なる場合はreconcile/correctする。

## 9. Diver操作のreal-time体験

Q232に従い、Gateway publication delayが存在してもDiverからはreal-timeに操作しているように感じられる体験を目指します。

- View側local prediction / immediate feedbackを許容する。
- predicted resultをauthoritative mutationとして扱わない。
- Core confirmed result到着時にreconcileする。
- correctionが必要な場合でもworld outcomeはCoreのauthoritative resultに従う。

Exact prediction model、correction UI、animationは詳細設計で決定します。

## 10. Diverとresidentのbinding

Q260〜Q264に従います。

### 10.1 新規residentを生成しない

- Diver joinのために専用new residentを生成しない。
- worldにexistingな通常residentへbindする。
- Diver-controlled residentも通常のphysics、social、economy、law、health等のworld ruleに従う。

### 10.2 希望条件

- Diverはbind対象についてbroad preferenceをrequest可能にする方向とする。
- 希望条件を満たすresidentが割り当てられることは保証しない。
- arbitrary residentを無条件にtake overできるprotocolにはしない。

Preference fieldやmatching ruleは詳細設計で決定します。

### 10.3 1 resident / 1 Diver

- 原則1residentにつき1Diver。
- disconnectを理由に別Diverへ自動的にbindingを移さない。
- concurrent duplicate controlを成立させない。

### 10.4 same Diver identity

- reconnectしても必ず同じDiver identityを使う。
- normal disconnect / error disconnectでDiver identityの扱いを変えない。

### 10.5 resident death

- controlled residentが死亡した場合、そのresidentは通常のworld death semanticsに従う。
- Diver identityは消滅させない。
- next participationで別existing residentへbind可能なruleを持てるが、new resident生成はしない。

## 11. Disconnect中のresident

- Disconnectしてもresidentをworldからremoveしない。
- World Timeをrewindしない。
- normal disconnectとerror disconnectを同じ基本semanticsで扱う。
- Diverは不在時にresidentへ優先させるbehavior/action方針を事前設定可能にする。

Exact absence-policy schemaやAI behavior implementationは未確定です。

## 12. Reconnect / resync

Reconnect時は可能な限りsame session / same Diver identityを復元し、Gatewayのcurrent publication basisへ同期してからnormal displayへ戻ります。

- Viewはsyncing/resyncing状態を明示表示できる。
- Gatewayがresync中の場合、それをconnected userへ通知する。
- inconsistent state sequenceをnormal confirmed stateとして表示しない。
- old cached View stateをcurrent authoritative-looking stateとしてblind reuseしない。

## 13. Operation request

General Viewからのworld-affecting requestはstable OperationとしてGatewayへ送ります。

Gatewayはrole/session/operation type/target等を検証し、authorized requestだけをdownstreamへ送ります。

Protocolは意味上次を扱える必要があります。

- client request correlation
- Operation identityまたはserver側stable Operation identityとの対応
- requested operation type / target / content
- accepted / rejected / pending
- final result
- applicable World Time/Step informationが必要な場合の表示

Exact fieldは詳細設計で定義します。

## 14. Spectator / Moderator / Administrator

### Spectator

- simulation mutation requestは許可しない。
- public non-vital statusのみ参照可能。

### Moderator

- defined lower-user / simulation operationsのみ許可する。
- critical operationはGatewayでrejectする。

### General View Administrator

- General Viewで定義されたsimulation interferenceを行える。
- Admin View専用のcomponent log、Config management、system operational commandを本protocolへ混在させない。

## 15. Operation result / error

少なくとも意味上次を区別可能にします。

- success
- authorization reject
- protocol/validation reject
- world-state/simulation-rule reject
- duplicate / already processed
- temporarily unavailable / resyncing
- session expired/revoked
- capability/version incompatibility

Exact error codeとuser-facing textは詳細設計で決定します。

## 16. Protocol version / Capability

- Major mismatchはconnection reject。
- same MajorではMinor backward compatibilityを維持する。
- connection時にrequired/optional Capabilityをnegotiationする。
- Major mismatch、required Capability mismatchのdiagnostic reasonを利用者へ表示可能にする。
- reconnectはrenegotiationの基本境界。

Addon関連はinstall/identity/version/required Capability等のcompatibility meta informationだけをstandard protocolで扱います。Addon functional data/commandは載せません。

## 17. 禁止事項

- Gateway bypassによるCore direct access
- UI-side authorizationだけでworld Operationを許可すること
- Spectatorのsimulation mutation
- Moderatorのundefined critical operation
- General View AdministratorとAdmin View operatorの同一視
- Diver join時の専用new resident自動生成
- disconnect時のautomatic Diver swap
- View predictionのauthoritative化
- resync中のinconsistent stateをnormal confirmed stateとして表示すること
- shared DTO library dependency
- Major mismatchでnormal connection
- standard protocolへのaddon functional payload

## 18. 詳細設計へ残す事項

- physical transport / serialization
- auth credential / session representation
- exact role permission matrix
- public status field set
- critical operation definition
- Diver preference/matching schema
- resident binding identifier/schema
- absence behavior policy schema
- interpolation/prediction/correction messages
- state publication/full-delta strategy
- Operation request/result schema
- resync notification/status representation
- pagination/range request
- version/Capability handshake and error code
