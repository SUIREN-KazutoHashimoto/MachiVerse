# Gateway設計

## 1. 目的

GatewayはSimulation CoreとGeneral View / Admin Viewの間に位置する、接続、認証・認可、緩衝、Operation集約・調停、cache、再同期の境界である。

Gatewayは世界状態の正本ではない。標準構成では単一Simulation Coreが正本を保持し、Gatewayは外部接続を水平scaleさせる。

本書は `gateway-operation-delivery.md`、`gateway-master-failover.md`、`gateway-cache-resynchronization.md`、`authentication-authorization-session.md`、`protocol-compatibility-capability.md` の確定要件を上位説明へ反映する。

## 2. 基本構成

- Simulation Core : Gateway = 1:N。
- General View / Admin ViewはCoreへ直接接続しない。
- 複数Gateway構成では、Coreが安全にMaster役割を担えるGatewayから1台をMaster Gatewayとして選出する。
- Masterは固定nodeではなく役割である。
- General View由来のCore干渉Operationは各Gatewayで処理した後、Masterへ集約する。
- 外部参照要求は可能な限り各Gatewayのcacheから処理し、すべてのreadをMasterへ集中させない。

```text
General View ──> Gateway A ─┐
                            │ local batch
General View ──> Gateway B ─┼──> Master Gateway ── final batch ──> Simulation Core
                            │
General View ──> Gateway C ─┘
```

Admin ViewもGatewayを経由する。login要求は接続先GatewayからMasterへproxyするが、login以外のAdmin Core OperationをすべてMaster経由へ統一するかは未確定である。

## 3. Component境界

Gatewayは他componentとcode、DLL、内部型、shared DTO libraryを通信契約として共有しない。

| 境界 | protocol owner | 正本 |
|---|---|---|
| Simulation Core ↔ Gateway | Simulation Core | `docs/protocols/core-gateway.md` |
| Gateway ↔ Gateway | Gateway | `docs/protocols/gateway-gateway.md` |
| Gateway ↔ General View | Gateway | `docs/protocols/gateway-view.md` |
| Gateway ↔ Admin View | Gateway | `docs/protocols/gateway-admin-view.md` |

## 4. 主な責務

Gatewayは少なくとも次を担当する。

- General View / Admin Viewからの接続受付
- authn / authzとsession制御
- General View roleに応じたOperation認可
- Admin Operation固有の妥当性確認
- 外部参照用cache
- 約1秒を標準とするlogical publication buffer
- General View Operationのlocal aggregate / local conflict mediation
- local batch形成
- 非Master時のMasterへのbatch転送
- Master時の全Gateway batchのdeterministic merge
- Master時のcross-Gateway external-request conflict mediation
- Core向けfinal batch形成
- Operation ID / Batch IDによるretry・dedup・idempotency
- Master generation / epochへの追従
- Core結果の適切な返却・配信
- reconnect時のcache再同期
- resync状態のconnected userへの通知
- protocol / Capability negotiation
- flow control、timeout、retry等の外部接続運用

## 5. 責務外

Gatewayは次を正本責務として持たない。

- Simulation World Stateの正本
- World Time / Simulation Stepの進行
- residentやworld subsystemの内部simulation rule
- UI roleと無関係な一般的world-state invariantの定義
- Master選出そのものの最終決定
- 複数Core間のstate sync、ownership transfer、region split

Gatewayはsimulation ruleを複製してGeneral View Operationの世界上の最終可否を決めない。

一方、Admin OperationについてはQ235/Q275に従い、Admin認証・権限・操作形式・対象・Admin操作としての許可条件等の妥当性確認をGatewayが担当する。Coreはその後も全Operation共通のworld-state invariantを維持する。

## 6. General View Operationの経路

```text
General View
  ↓
connected Gateway
  ↓ authn / authz
local aggregation
  ↓
local external-request conflict mediation
  ↓ local batch
Master Gateway
  ↓ deterministic merge
cross-Gateway external-request conflict mediation
  ↓ final batch
Simulation Core
  ↓ common world-state / simulation-rule validity
authoritative state
```

### 6.1 Gateway内の責務

- unauthorized requestをCoreへ送らない。
- 同一Gateway内の外部要求レベル競合を決定論的に整理する。
- local batch内順序をnetwork arrival raceやthread completion orderだけで決めない。
- Operationのstable IDを保持する。

### 6.2 Masterの責務

- 自身と他Gatewayのlocal batchを受け取る。
- 同じ有効Operation集合からdeterministicなmerge結果を形成する。
- cross-Gateway external-request conflictを整理する。
- final Core-facing batchを形成しCoreへ送る。
- resultを元Gatewayへ返却可能にする。

Masterはsimulation world ruleの正本ではない。

## 7. Operation ID・Batch ID・retry・dedup

- 各外部Operationはhop、retry、Master failover、Gateway reconnectを跨いで不変のstable Operation IDを持つ。
- 同一Operation IDは世界へ一度だけ影響する。
- retry時は同じOperation IDを使用する。
- batchにも識別子を持たせ、ACK lossやMaster切替時に同一処理を追跡可能にする。
- retry回数、network delay、ACK loss、thread順序だけでworld outcomeを変えない。
- exact ID format、retention window、dedup data structure等は詳細設計で決定する。

## 8. Operation順序と適用候補Step

- Gateway内順序とMaster merge順序を決定論化する。
- 同じ有効Operation集合なら、Gateway数、Master個体、network timing、thread orderに依存せず同じCore-facing orderを得る。
- Gateway / Masterはprotocol規則に従いOperationの候補適用時刻/Stepに必要な情報を形成する。
- Coreが現在Simulation Step、deadline、Master generation、ordering rules等から最終有効Stepを確定する。
- physical arrival timeをそのままauthoritative application orderにしない。

具体的なordering key、candidate Step field、same-step tie-breakerは詳細設計で決定する。

## 9. Master Gateway選出

Master選出責任はSimulation Coreが持つ。

### 9.1 eligibility

単にTCP等で接続中であるだけでは候補としない。少なくとも、接続・応答、protocol互換、required Capability、必要なsync state等、安全にMasterとして動作できる条件を満たす必要がある。

具体的なhealth check方式・数値thresholdは外部Configと詳細protocolで決定する。

### 9.2 random selection

- 選出方式はrandomとする。
- Master選択結果そのものをWorld Seedから決定論的に再現する標準要件はない。
- 選択結果、generation、切替理由等をdiagnostic可能にする。
- どのGatewayがMasterでも、同じ有効Operation集合ならworld outcomeを変えない。

### 9.3 generation / epoch

- Master generation / epochを持つ。
- Coreが現在有効なgenerationを決定する。
- old Master generationから遅れて到着したfinal outputを通常のcurrent outputとして受理しない。

## 10. Master障害・failover・live migration

- heartbeat、response delay等からMaster利用不能を判断できる。
- transient delayとfailureを区別する。
- monitor interval、failure threshold、reselection条件等の調整数値は外部Config化する。
- unfinished batch、ACK待ち、retry中Operationを新generationへ引き継ぎ、loss・duplicate applyを防ぐ。
- live migrationに耐える設計とする。
- split-brainを防止する。

OperationにはWorld Time / application Step上の受付deadlineを設けられる。late Operationは過去の確定状態をretroactiveに書き換えず、protocol規則に従って後続有効Stepへdeferするかrejectする。

## 11. Authn / Authz / session

General ViewとAdmin Viewはauth/authz domainを分離する。

### 11.1 General View

Gatewayはrole、session、Operation type、target等に応じて認可する。

- Spectatorのsimulation mutation requestをCoreへ送らない。
- Moderatorに許可されないcritical requestをCoreへ送らない。
- General View Administratorの権限をAdmin Viewへ自動流用しない。

### 11.2 login

- Userは接続先Gatewayへlogin requestを送る。
- connected Gatewayはlogin requestをMaster Gatewayへproxyする。
- login処理の確定はMasterで行う。
- 非Master Gatewayが独立に同じ認証を最終確定しない。
- Master切替/live migrationでもsession整合性を壊さない設計にする。

具体的なcredential、token、IdP、session storage方式は未確定。

### 11.3 role change / revoke

- connection中のrole変更には明示的なeffective pointを持たせる。
- privilege revoke後の新規Operationへ古い権限を適用しない。
- severe revokeではexisting session/credentialをinvalidate可能にする。
- auth outage時もauthorizationをbypassしない。

## 12. Admin Operation

- Admin View→Gateway→Coreを基本経路とする。
- Admin Operationをstable Operationとして識別・audit可能にする。
- Admin固有のauthn/authz、形式、target、allowed-condition validationはGatewayが担当する。
- CoreはUI role名を解釈せず、全Operation共通のworld-state invariantを維持する。
- simulation-affecting Admin Operationを単にAdmin由来だからという理由で無条件最優先にしない。
- simulation-non-affecting Admin Operationに限り最優先としてよい。

Login以外のAdmin Core OperationをMaster pathへ統一するかは未確定である。

## 13. Cache

Gateway cacheは外部read負荷をCoreから分離するための非権威な派生状態である。

- cacheをauthoritative World Stateとして扱わない。
- cache lossをworld lossとして扱わない。
- cacheのbasisとなるSimulation Step / generation等を識別可能にする。
- stale / inconsistent cacheを検出した場合は破棄・再構築できる。

Core→Gateway state deliveryの具体方式は未確定であり、Push/Pull、full/delta、snapshot等を現段階で固定しない。

## 14. Logical publication buffer

Gatewayは標準約1秒のpublication delay bufferを持つ。

- 単純なsleepではなく、World Time / Simulation Stepの範囲を保持するlogical bufferとして扱う。
- arrival jitter、temporary reorder、publish timingの揺らぎを外部へ直接露出させないために使う。
- cacheとは別責務。
- 30Hz Core updateと30Hz external publishを同一要件にしない。
- buffer durationはGateway Config。
- General View Operation / Admin Operationそのものを一律1秒遅らせる要件ではない。

## 15. Reconnect・resync

Gateway reconnect時はold cacheをblind trustしない。

- Coreまたはprotocol-authoritativeなsync sourceからbasis Simulation Step、generation等を確認する。
- 正常publicationへ戻す前に必要なresyncを完了する。
- missing、reorder、sync mismatchを検出したらrefetch/rebuildする。
- inconsistent state sequenceを通常状態としてpublishしない。
- resync中であることをconnected userへ通知し、General Viewではvisibleなsync stateとして扱えるようにする。

## 16. Gatewayが0台の場合

Gatewayが0台になったこと自体を理由にCoreのSimulation Stepを停止させない。

- Coreのinternal eventは継続する。
- Coreが既に受理済みのOperationは決定済みの規則に従って処理する。
- 新規external Operationだけが入らない。
- Gateway復旧後に不在期間へworldを巻き戻さない。

## 17. Protocol compatibility / Addon metadata

- Major mismatchはconnection reject。
- same MajorではMinor backward compatibilityを維持する。
- connect時にrequired/optional Capabilityをnegotiationする。
- addonについて標準protocolで交換するのはinstall状況、identity、version、required/provided Capability等のcompatibility/safety meta informationに限定する。
- addon固有function payloadやcommandを標準protocolへ載せない。
- addon固有cross-component communicationは別addon/frameworkとadditional protocolの責務とする。

## 18. Config

Gatewayが所有する調整可能値はGateway外部Configから供給する。

例:

- publication buffer duration
- cache retention / size
- timeout
- retry interval / limit
- connection limit
- flow control
- aggregation window / count / size
- Master health threshold
- deadline / grace
- resync threshold

GatewayはCoreやViewのConfig fileを直接読まない。

## 19. 複数Core拡張

標準Gatewayは複数Coreのregion routing、Core-to-Core state ownership、cross-Core proxy、Core boundary handoverを持たない。

将来のmulti-Core addonは標準契約へ暗黙に混在させず、独立拡張として扱う。

## 20. 禁止事項

- ViewからCoreへのdirect connection
- 非Master GatewayによるGeneral View local batchのCore direct submission
- two active Mastersによる同一generationの通常final batch書き込み
- Gateway cacheのauthoritative化
- Gatewayへのsimulation rule複製
- Master generationなしのfailover
- stable Operation IDなしのretry
- network arrival raceをworld orderingとして利用すること
- unauthorized requestをCoreへ送ること
- standard protocolへaddon functional payloadを埋め込むこと

## 21. 詳細設計へ残す事項

- Master election messageとrandom algorithm
- Master healthの具体protocol/threshold
- Operation ordering key
- candidate application Stepのwire表現
- Batch transaction / partial-success semantics
- Core→Gateway state delivery方式
- cache consistencyの具体algorithm
- result return messageの具体format
- auth/token/session技術
- login以外のAdmin Core OperationのMaster path
- network transport / serialization
- Capability / addon metadataの具体identifier・schema
