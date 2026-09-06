# プロトコル設計方針

## 1. 目的

本書はMachiVerseのcomponent間通信に共通する契約原則を定義する。

Simulation Core、Gateway、General View、Admin Viewはcode/build/deploy/runtime単位まで独立し、component間通信はprotocolだけを通じて行う。shared DTO libraryや内部型共有をprotocolの代替にしない。

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

transportやserializationの具体技術は個別protocol詳細設計で決定する。

## 3. Protocol所有責任

Protocol ownerは、接続する2componentのうちよりSimulation Coreに近いcomponentとする。

| 境界 | owner | 利用側 |
|---|---|---|
| Simulation Core ↔ Gateway | Simulation Core | Gateway |
| Gateway ↔ Gateway | Gateway | Gateway |
| Gateway ↔ General View | Gateway | General View |
| Gateway ↔ Admin View | Gateway | Admin View |

標準構成にCore↔Core protocolは存在しない。

Ownerは公開機能、message semantics、compatibility、変更方針を定義する。利用側はownerのinternal implementationへ依存せずprotocol contractだけを基準に実装する。

## 4. Versioning

### 4.1 Major.Minor

各protocolはMajor.Minorを識別可能にする。

- Major mismatch: normal connectionを拒否する。
- same Major / same Minor: compatibility成立を前提とする。
- same Major / different Minor: backward-compatibleな範囲でconnectionを許可する。
- newer Minorはsame Majorのolder Minorとのbackward compatibilityを維持する。
- backward compatibilityを維持できないsemantic changeはMajorを更新する。

具体的なfield name、numeric/string representationは個別protocolで定義する。

### 4.2 Minor compatibility

Minor updateで既存必須fieldを削除したり、既存fieldの意味・型・unitを互換不能に変更したりしない。

newer Minor側は、older peerが理解できない新機能・新内容を無条件送信しない。peer capabilityを確認し、safe-to-ignore unknown dataとsemantic incompatibilityを区別する。

## 5. Capability Negotiation

Connection確立時に、protocol versionだけでなく対応Capabilityを交換可能にする。

- required Capabilityとoptional Capabilityを区別する。
- required Capability不足はconnection全体または対象機能を明示的に拒否する。
- capability mismatchをsilent degradationで隠さない。
- live migration、Master Gateway切替、addon状態変更等でeffective Capabilityが変化し得る場合、安全なrenegotiationまたはreconnectを行う。
- reconnectはCapability negotiationをやり直す基本境界とする。

具体的Capability identifier、negotiation message、error codeは個別protocol詳細設計で決定する。

## 6. Addon関連情報の標準Protocol境界

Q246とQ255を次のように統一する。

### 6.1 標準protocolで交換可能な情報

標準protocolは、接続安全性・compatibility確認に必要なaddonの**meta information**を交換できる。

例として意味上含み得るもの:

- addon install / enable状況
- addon identity
- addon version
- required / provided Capability
- compatibility判断に必要なdependency information

具体fieldは現時点では定義しない。

### 6.2 標準protocolに載せないもの

標準protocolには次を設けない。

- addon固有function payload
- addon固有command
- addonのworld-specific extra dataを運ぶgeneric extension payload
- addon都合で標準message semanticsを書き換える仕組み

つまり、標準protocol上のaddon情報は「その接続・構成が安全かを判定するためのmeta情報」であり、「addon機能そのものを標準protocolで通信する仕組み」ではない。

### 6.3 Addon固有の追加Protocol

Addonがcomponent境界を越えて固有情報を交換する必要がある場合は、標準protocolへ混在させず、protocol拡張の前提framework addon等と、そのaddon間で成立するadditional protocolを用意する方向とする。

このframework addon、additional protocol、transport、API、package形式等は未確定であり、本書では先取りしない。

## 7. Addon不整合と接続安全性

- required addon / version / Capabilityが不足・非互換ならunsafe featureをenableしない。
- Q257に従い、component startup時のaddon構成・dependency・Capability・Configに不整合がある場合は、重大度に関係なくstartupを拒否する。
- Q267に従い、saved worldが依存するaddon条件に不整合があれば、明示migrationが完全成功しない限りworld startupを拒否する。

Protocol negotiationはこの安全判定に必要な情報を伝えるが、addon runtime implementationそのものをstandard protocolへ取り込まない。

## 8. Operationを扱うProtocolの共通要件

World Stateへ影響するOperationを扱うprotocolは、必要な境界で次を契約化する。

- stable Operation ID
- Batch ID
- Master generation / epoch
- retry時のsame identity
- dedup / idempotency
- stale generation handling
- deterministic orderingに必要なlogical information
- candidate / final application Simulation Step semantics
- deadline / late behavior

Network arrival time、retry count、thread schedulingだけでworld outcomeを変えない。

具体field schemaは各protocolで定義する。

## 9. World Time

Protocol上でsimulation timeを扱う場合、authoritativeな時間基準はSimulation Coreの整数Simulation Stepと整合させる。

display time、wall clock、resident calendar等とauthoritative Simulation Stepを混同しない。

Gateway/Masterがcandidate application timeを扱う場合も、Coreが最終有効Stepを確定する意味論を壊さない。

## 10. Auth / Authorization

- General View / Admin Viewのauth domainは分離する。
- Gateway-owned protocolでは、unauthorized requestをCoreへ到達させない意味論を持つ。
- Admin Operation固有のvalidity checkはGateway責務。
- Core-facing protocolでは、CoreがUI roleを解釈せずcommon world-state invariantを維持する責務と両立させる。
- loginはconnected GatewayからMaster Gatewayへproxyし、Masterで確定する要件をGateway関連protocolで表現可能にする。

具体credential/token/IdPは未確定。

## 11. Failure / reconnect / resynchronization

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

Failure handlingをimplementationの暗黙挙動に任せない。

## 12. Error diagnostics

Compatibility・safety上のrejectは、可能な範囲でoperator/userが原因を診断できるようにする。

少なくともMajor mismatchでは、reject reason、双方version、必要なupdate directionを確認可能にする。

Required Capability / addon compatibility mismatch等についても、原因をsilentに隠さない。

## 13. Protocol変更の流れ

1. protocol ownerが変更要求を整理する。
2. protocol設計書を先に更新する。
3. same Majorのcompatible Minor changeか、Major changeが必要なsemantic breakかを判定する。
4. Capability impactを確認する。
5. addon meta informationに影響する場合もstandard/additional protocol境界を確認する。
6. 各component implementationが独立してcontractへ追従する。

Shared code変更によって暗黙に複数componentを同時変更させない。

## 14. Independent testing

各componentは相手implementation自体を必要とせずprotocol boundaryをtest可能にする方向とする。

少なくとも次を検証可能にする。

- same Major / same Minor compatibility
- same Major / different Minor backward compatibility
- Major mismatch reject
- required Capability mismatch
- retry / duplicate / idempotency semanticsがある場合のcontract
- stale Master generation rejectがある場合のcontract

具体test framework/code generation方式は未確定。

## 15. 禁止事項

- component間code sharingをcommunication contractとすること
- shared internal type / DTO library dependency
- direct method call
- standard protocolにないimplicit behaviorへの依存
- Minor updateでsemantic compatibilityを壊すこと
- Major mismatchをnormal connectionとして許容すること
- required Capability不足を黙って無視すること
- standard protocolへaddon functional payload / commandを埋め込むこと
- network arrival orderをauthoritative world operation orderとして利用すること

## 16. 詳細設計へ残す事項

- concrete network transport
- serialization format
- version field representation
- handshake sequence
- Capability identifier/schema
- compatibility error code
- Operation/Batch wire schema
- Simulation Step field representation
- reconnect/resync message set
- addon compatibility meta schema
- additional addon protocol frameworkの具体仕様
- schema management / code generation policy
