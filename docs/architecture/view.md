# General View設計

## 1. 目的

General Viewは、MachiVerseの一般利用者がworldを閲覧し、roleに応じて参加・操作するためのWeb applicationです。

- Simulation Coreへ直接接続しない。
- 状態参照、login、Operation送信、reconnectはGatewayを通じて行う。
- full-3D world renderingにはThree.jsを使用する。
- Three.jsはpresentation technologyであり、authoritative World Stateやworld physicsを所有しない。

## 2. 利用者role

| Role | Simulation interference | 参照範囲 |
|---|---|---|
| Diver | 通常residentと同程度 | participantとして許可された情報 |
| Spectator | 不可 | system vitalに関係しない範囲の公開status |
| Moderator | simulation / lower userへ限定的に可能、critical operationは禁止 | Spectator相当 |
| Administrator | General Viewで定義されたsimulation interference | General View向け全公開status |

General View AdministratorとAdmin View operatorは別auth/authz domainです。

Exact permission matrix、public status set、critical operation定義は詳細設計で決定します。

## 3. Diver体験の上位原則

Diverには「自分がこのworldを構成する一人の住人である」と感じられる体験を最大化します。

これはVR/HMD等の特定技術を必須にする意味ではありません。Diverをworld外部の管理者として特別扱いするのではなく、通常residentと同じworld rule、時間、制約、関係、健康、経済、社会、歴史的因果の中へ参加させます。

Diverだけに都合のよい結果を演出することを目的とせず、深いworld simulationの因果をDiver自身の経験として感じられることを重視します。

VR等は将来addonで体験を拡張できる可能性がありますが、standard requirementではありません。

## 4. Diverとresidentのbinding

Q260〜Q264を確定要件とします。

### 4.1 Existing residentのみ

- Diver joinのためにnew residentを生成しない。
- worldに既に存在する通常residentへbindする。
- Diver-controlled residentも通常のphysics、social、economy、law、health等のworld ruleに従う。

### 4.2 希望条件

- Diverはbind対象residentについてbroad preferenceをrequestできる。
- 条件を満たすresidentが割り当てられることは保証しない。
- arbitrary residentを無条件にtake overする機能にはしない。

Exact preference/matching ruleは詳細設計で決定します。

### 4.3 1 resident / 1 Diver

- 原則1residentにつき1Diver。
- disconnectを理由に別Diverへresident controlを自動移譲しない。
- reconnect後もsame Diver identityを使用する。

### 4.4 Resident death

Controlled residentが死亡した場合、そのresidentは通常のworld death、postmortem、inheritance、history等のruleに従います。

Diver identityは維持し、参加ruleに従い後に別のexisting residentへbind可能にできます。Diver join用new residentは生成しません。

## 5. Disconnect中のDiver resident

- normal disconnectとerror disconnectを同じ基本semanticsで扱う。
- residentをworldからremoveしない。
- disconnect時点へworldをrewindしない。
- residentはabsence中もworld内で行動を続ける。
- Diverはabsence中にresidentへ優先させるbehavior/action方針を事前設定可能にする。

Exact absence behavior engine/schemaは未確定です。

## 6. State displayのauthoritative basis

General ViewはGatewayがpublishしたconfirmed stateを表示basisにします。

- display stateには対応するWorld Time / Simulation Stepを識別できる必要がある。
- incompatibleな異なるStepのstateを1つのconfirmed snapshotとして混在させない。
- Gateway cacheはauthoritativeではないが、Gatewayがprotocol上confirmed publicationとして提供したsequenceをdisplay basisに使う。
- old View-local stateをcurrent authoritative-looking stateとしてblind reuseしない。

## 7. Interpolation / prediction

Smooth displayのため、View側でinterpolationやshort predictionを行えます。

- presentation-only / non-authoritative。
- confirmed stateとpredictionを内部的に区別する。
- predictionがworld outcomeへ影響してはならない。
- confirmed result到着時にreconcileする。

## 8. Diver操作のreal-time感

Gatewayにstandard約1秒のlogical publication delayがあっても、Diverからはreal-timeに操作しているように感じられる体験を目標にします。

- inputへのlocal immediate feedbackを許容する。
- short local predictionを許容する。
- authoritative resultはCore→Gateway confirmed state/resultに従う。
- mismatch時はcorrection/reconcileする。

Prediction algorithm、correction visual、animationは詳細設計で決定します。

## 9. Reconnect / resync

Reconnect時は可能な限りsame session / same Diver identityを復元します。

- Gateway publication basisへ同期してからnormal displayへ戻る。
- syncing/resyncing stateをuserへvisibleにする。
- Gateway自体がresyncingの場合、その状態を明示する。
- inconsistent state sequenceをnormal stateとして表示しない。

## 10. Login / auth / role

- Userはconnected Gatewayへlogin requestを送る。
- Gateway側ではlogin requestがMaster Gatewayへproxyされ、Masterでloginが確定する。
- Viewは具体的なMasterへのdirect connectionを前提にしない。
- General View auth/authz domainとAdmin View auth/authz domainを分離する。
- role changeには明示的なeffective pointを持たせる。
- privilege revoke後にold privilegeでnew Operationを継続させない。

Credential、token、IdP、session technologyは未確定です。

## 11. Operation input

General ViewはGatewayへrole-permitted Operation requestを送ります。

- UIでdisabledにするだけでauthorizationを完結させない。
- Gatewayがrequestごとにauthn/authzする。
- unauthorized requestをCoreへ送らない。
- Operation resultがpending/accepted/rejected/confirmed等の状態を持つ場合、userへ適切に表示する。
- Network arrival raceやView frame timingをworld outcomeの決定要因にしない。

Exact Operation UI、message schemaは詳細設計で決定します。

## 12. Three.js full-3D rendering

General ViewはThree.jsでfull-3D worldを描画します。

Coreのauthoritative full-3D spatial modelには、terrain surfaceだけでなくcave、tunnel、basement、mine working、overhang、same XYの異なるZに存在するspace/surface等が含まれます。

Viewは公開されたauthoritative-derived stateと矛盾しない形で、地下空間のwall/ceiling/floor等も適切に描画できる必要があります。

Three.js version、WebGL/WebGPU、scene graph、LOD、asset、shader、render update rate、UI framework、browser/device target等は詳細設計で決定します。

## 13. 表示専用state

View自身が所有してよいstateは、例えば次です。

- camera
- selected entity
- open/closed panel
- filter/sort
- interpolation/prediction state
- input-in-progress
- local presentation cache

これらをauthoritative World Stateとして扱いません。

## 14. Communication / error display

General Viewは少なくとも次を利用者へ表示できるようにします。

- loading
- disconnected
- reconnecting
- resyncing
- session expired/revoked
- protocol Major mismatch
- required Capability mismatch
- Operation rejected
- temporary unavailable

Exact UI wording/layoutは詳細設計で決定します。

## 15. Addonとの関係

AddonはGeneral View component単位でも設定可能です。

- standard Gateway↔View protocolへaddon functional payload/commandを載せない。
- addon install/version/Capability等のcompatibility/safety meta情報はstandard protocolで交換可能。
- View addonがGateway addon等と固有dataを交換する必要がある場合、addon/framework側のadditional protocolを使用する方向とする。

Concrete addon APIは未確定です。

## 16. 責務外

- Authoritative World State
- simulation ruleの実行
- Coreへのdirect connection
- authzのserver-side final enforcement
- Gateway cacheの内部実装
- component Configのdirect edit
- Admin View運用command
- addon functional protocolをstandard protocolへ追加すること

## 17. 詳細設計へ残す事項

- exact role permission matrix
- Diver preference/matching UI/schema
- absence behavior policy UI/schema
- login/session UI
- state update protocol利用方式
- prediction/reconcile UX
- camera/control scheme
- Three.js scene/LOD/shader/assets
- browser/device support
- status/error presentation
- accessibility/localization
- addon View extensionの具体境界
