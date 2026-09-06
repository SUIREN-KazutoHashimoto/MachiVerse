# Gateway・Admin View間Protocol設計書

## 1. 所有者

本protocolのownerはGatewayです。

## 2. 目的

Admin ViewがMachiVerse各componentを運用・診断し、許可されたConfig change、operational command、simulation Admin Operationを要求するための外部契約です。

Admin ViewはGeneral Viewの上位roleではなく、system operator向けの別auth/authz domainです。

## 3. 設計原則

- Admin ViewとGatewayはcode、DLL、internal type、shared DTO libraryを共有しない。
- General View roleとAdmin View permissionを分離する。
- General View AdministratorをAdmin View operatorとして自動認可しない。
- Admin Viewからcomponent internal implementationへ直接アクセスしない。
- Admin requestのauthn/authz、operation format、target、Admin operationとしてのallowed conditionをGatewayが検証する。
- Target component自身が所有するstate invariantやConfig consistencyは、そのcomponentの責務として維持する。
- UI表示制御だけでmanagement permissionを完結させない。
- Admin Operationは追跡・監査可能にする。
- generic Undoを設けない。元へ戻す場合もnew Operation/change requestとして実行する。

## 4. Auth / login / session

Admin ViewはGeneral Viewとは別domainでauthentication/authorizationします。

- userはconnected Gatewayへlogin requestを送る。
- Q241に従いGatewayはlogin requestをMaster Gatewayへproxyし、login処理をMasterで確定する。
- non-Master Gatewayが独立に同じloginをfinalizeしない。
- Master switch/live migrationでsession consistencyを壊さない。
- authorization outage時もpermission checkをbypassしない。
- severe privilege revokeではexisting session/credentialをinvalidate可能にする。

具体credential、token、IdP、session storageは未確定です。

## 5. Communication category

### 5.1 Component health / status

Admin Viewは各componentの公開可能なhealth、processing state、diagnostic stateを参照できます。

少なくともarchitecture上、次の状態を表示可能にする方向です。

- Core current Simulation Step / lag
- Master Gateway identity / generation
- resyncing Gateway
- protocol / Capability mismatch
- Config validation error
- Operation retry growth / relevant dedup diagnostics
- save / recovery state
- generic CPU / memory / connection metrics等

Exact metrics/schemaはobservability詳細設計で定義します。

### 5.2 Log

各componentの公開可能なstructured logを検索・参照できる契約を持ちます。

Log retention、capacity、rotation等はcomponent Configで管理し、audit logとhigh-volume diagnostic logを同じretentionへ固定しません。

Exact query/stream mechanismは未確定です。

### 5.3 Config read

各componentが所有するConfigのうちAdmin Viewへ公開可能な項目、current effective value、必要なclassification/validation stateを参照できるようにします。

Admin ViewやGatewayが他componentのConfig fileを直接読むことをcontractにしません。

### 5.4 Config change

Config changeはrequestとしてGatewayへ送ります。

- GatewayがAdmin permission、format、target、operationとしてのallowed conditionを検証する。
- Target componentが自身のConfig type/range/cross-constraintとstate consistencyを検証する。
- startup Config不整合を抱えたcomponentは起動しない。
- runtime change可能項目はsafe explicit boundaryでatomicにapplyする。
- simulation-affecting Config changeはSimulation Stepとhistoryへ結び付ける。
- invalid runtime change setはpartial applyせずrejectする。
- 元の値へ戻す場合もgeneric Undoではなくnew change requestとして実行する。

Admin ViewがConfig fileを直接editすることはstandard contractとしません。

### 5.5 Operational command

Defined component operational commandをrequestできます。

Commandごとにpermission、target、parameter、idempotency、timeout、simulation-affecting classification等を定義する必要があります。

具体command一覧は詳細設計で決定します。

### 5.6 Simulation Admin Operation

Admin ViewからSimulation Coreへ影響するOperationはAdmin View→Gateway→Core経路を使用します。

責務分離:

- Gateway: Admin authn/authz、format、target、Admin operationとしてのvalidity/allowed condition。
- Core: UI roleを解釈せず、全Operation共通のworld-state invariant / state-transition consistency。

GatewayでAdmin operationとして許可したことだけを理由にCoreがinconsistent World Stateを作ってはなりません。

Simulation-affecting Admin OperationをAdmin由来というだけで無条件最優先にしません。Simulation-non-affecting Admin Operationに限り最優先としてよいものとします。

### 5.7 High-impact operation confirmation

World destruction、大量変更、time control、大規模Config change等のhigh-impact operationは追加確認・audit対象とします。

Concrete high-impact category、confirmation UX、multi-person approval有無は未確定です。

### 5.8 Result

Config change、operational command、simulation Admin Operation等について、少なくとも意味上次を区別可能にします。

- accepted / pending
- success
- authorization reject
- format/target/allowed-condition reject
- target component state/config invariant reject
- duplicate/already processed
- temporarily unavailable
- version/Capability incompatibility

Exact result/error codeは詳細設計で決定します。

## 6. Admin Operation identity / audit

Worldまたはsystem stateへ影響するAdmin actionをtraceable Operation/change requestとして扱います。

Auditでは少なくとも次を追跡可能にします。

- actor
- Operation ID / request identity
- operation type
- target
- requested content
- request time
- application Simulation Step / effective boundary（該当する場合）
- result
- reject reason
- related Config change

Audit historyをgeneral Undoで消しません。

## 7. Config ownership

- 各componentが自身のConfig fileを所有する。
- Admin View / Gatewayはother component Config fileへdirect dependencyを持たない。
- Cross-componentに必要なsetting/stateは、owner componentがprotocolを介して公開する。
- Old Configにnew fieldが不足する場合、owner componentはdefaultを有効化し、そのfieldをConfig fileへ追加するQ214要件に従う。

## 8. Component reachability

Admin Viewとのexternal boundary ownerはGatewayです。

ただし、Core以外のcomponent自身に対するすべてのmanagement requestをGatewayがproxyするか、component-specific management protocolを別途設けるかは未確定です。

この未確定事項は「Admin Viewがcomponent internal APIへ直接依存してよい」という意味ではありません。どの方式でもprotocol contractとcomponent independenceを維持します。

Coreへのsimulation Admin OperationはGatewayを経由します。

## 9. Pause / World Timeとの関係

- simulation-affecting Admin Operationはauthoritative Simulation Stepのsemanticsへ従う。
- Pause中に受信・auth/queueすることは可能だが、simulation-affecting Operationをstopped Stepへ曖昧applyしない。
- Resume後のexplicit valid Stepへdeterministicにassignmentする。
- simulation-non-affecting operational commandはPause中でも実行可能なcategoryを持てる。

## 10. Version / Capability

- Major mismatchはconnection reject。
- same MajorではMinor backward compatibilityを維持する。
- connect時にrequired/optional Capabilityをnegotiationする。
- required Capability不足をsilentに無視しない。
- reconnect / Master switch / addon state change等でeffective Capabilityが変わる場合、安全にrenegotiate/reconnectする。

Addonについてstandard protocolで交換するのはcompatibility/safety meta informationだけです。Addon functional payload/commandを本protocolへ載せません。

## 11. Forbidden

- General View AdministratorのAdmin View permissionへのautomatic promotion
- Admin Viewからcomponent internal implementationへのdirect access
- Admin View/Gatewayによるother component Config fileのdirect edit
- UI-only authorization
- unauthorized Admin Operation forwarding
- generic Undoによるhistory消去
- simulation-affecting Admin Operationのunconditional highest priority
- Major mismatchでnormal management communication
- standard protocolへのaddon functional payload
- shared DTO library dependency

## 12. 詳細設計へ残す事項

- physical transport / serialization
- Admin View credential/session technology
- permission model / operation matrix
- health/metrics/log message schema
- Config read/change schema
- operational command set
- high-impact operation category/confirmation flow
- Operation audit schema/retention
- result/error code
- each component management reachability architecture
- retry / timeout / idempotency
- version/Capability handshake
- addon compatibility meta schema
