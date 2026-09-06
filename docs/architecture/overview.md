# 全体アーキテクチャ

## 1. 目的

本書はMachiVerseの最上位アーキテクチャと、複数設計書へ共通する責務境界を定義する。

要件の決定記録は `docs/requirements`、横断矛盾の解消記録は `consistency-audit.md` を参照する。本書と古い個別設計書の記述が食い違う場合は、後続の確定要件を優先する。

MachiVerseはC#で開発する大規模なエージェントベース世界シミュレーターであり、標準構成では単一Simulation Coreが世界状態の正本を保持する。

## 2. 最上位コンポーネント

| コンポーネント | 主目的 | 主な責務 |
|---|---|---|
| Simulation Core | 世界シミュレーションの実行 | 正本状態、Simulation Step、世界ルール、決定論的更新、保存・復旧、Gatewayから受けたOperationの世界状態への適用 |
| Gateway | 外部との接続・認証・認可・緩衝・調停 | cache、公開遅延buffer、認証・認可、Operation集約、Master役割、再送・重複排除、再同期、Admin要求仲介 |
| General View | 一般利用者向けWeb UI | Diver参加、Spectator参照、Moderator/Admin操作、Three.jsによるフル3D表示 |
| Admin View | システム運用者向けUI | component状態、log、metrics、Config、運用command、Admin Operation |

General View上のAdministratorとAdmin Viewの運用権限は別の認証・認可ドメインであり、一方の権限を他方へ自動付与しない。

## 3. コンポーネント完全分離

4コンポーネントは、コード、build、配布、実行単位まで独立させる。

- コンポーネント間でproject参照を持たない。
- DLL、内部型、共通DTO libraryを通信契約として共有しない。
- 他コンポーネント内部への直接method callを行わない。
- 各コンポーネントは独立してbuild・実行・deploy可能とする。
- コンポーネント間の連携はprotocolのみを通じて行う。
- 標準通信契約の正本は `docs/protocols` とする。

## 4. 標準トポロジ

標準構成ではSimulation Coreは1つとし、Core : Gatewayは1:Nとする。

```text
General View ──> Gateway A ─┐
                            │
General View ──> Gateway B ─┼──> Master Gateway ──> Simulation Core
                            │
Admin View ─────> Gateway C ─┘
```

General ViewとAdmin ViewはCoreへ直接接続しない。

複数Coreによる世界分割は標準構成に含めない。将来、独立アドオンとして複数Core化する可能性は残すが、その場合に標準構成の完全再現性を維持しない設計を許容する可能性がある。標準protocolにCore↔Core通信は存在しない。

## 5. Protocol所有責任

| 境界 | 所有者 | 正本 |
|---|---|---|
| Simulation Core ↔ Gateway | Simulation Core | `docs/protocols/core-gateway.md` |
| Gateway ↔ Gateway | Gateway | `docs/protocols/gateway-gateway.md` |
| Gateway ↔ General View | Gateway | `docs/protocols/gateway-view.md` |
| Gateway ↔ Admin View | Gateway | `docs/protocols/gateway-admin-view.md` |

ProtocolはMajor.Minorでversioningする。Major不一致は接続拒否、同一Major内の新Minorは後方互換を維持する。接続時は必要なCapabilityを交換し、意味的不整合を黙って許容しない。

## 6. 権威ある世界状態と時間

- 標準構成ではSimulation Coreだけが世界状態の正本を持つ。
- Gateway cache、View表示状態、予測状態は非権威な派生状態である。
- 権威あるWorld Timeは整数ベースのSimulation Stepとする。
- 標準計算頻度は30Hzで、外部Configから変更可能とする。
- 30Hzに処理が追いつかなくても、処理遅延だけを理由に世界Stepを飛ばさない。
- Pause、速度変更等を可能にするが、世界結果へ影響するOperationは明示的な有効Stepへ決定論的に割り当てる。

社会的な暦・時計・住人の時刻認識と、Coreの権威あるSimulation Stepは別概念とする。

## 7. 決定論

同じWorld Seed、同じシミュレーション影響Config、同じ有効Operation集合・順序・適用Stepからは同じ論理的な世界結果を得なければならない。

世界結果を次へ依存させない。

- 処理速度
- OS scheduling
- thread実行順・task完了順
- Gateway数
- Master Gateway個体
- network到着競争
- wall clockやOS時刻を暗黙に使った世界乱数

Coreは最大16 threadを利用可能とし、実使用数は1〜16の外部Configとする。thread数を変えても同一再現条件では世界結果を変えない。

異なるCPU、OS、runtimeを跨ぐすべての浮動小数演算のbit完全一致は標準要件とはしないが、制御可能な非決定性は排除する。

## 8. 乱数・Entity ID

- 世界乱数はWorld Seed、World Time/Simulation Step、対象・用途・event等の決定論的contextから導出する。
- shared stateful PRNGの消費順をworld outcomeへ依存させない。
- 永続Entity IDは保存・再開・replayを跨いで同じEntityを識別する。
- 未来に生まれる全Entity IDを世界生成時に事前列挙する必要はなく、出生・生成eventの決定論的contextから生成できる。

## 9. General View由来Operation

General View由来のCore干渉Operationは概念的に次の経路を通る。

```text
General View
  ↓
接続先Gateway
  ↓ 認証・認可
local aggregate / local conflict mediation
  ↓ local batch
Master Gateway
  ↓ deterministic merge / cross-Gateway mediation
final batch
  ↓
Simulation Core
  ↓ world-state invariant / simulation-rule validity
authoritative world state
```

- 非Master GatewayはGeneral View由来のlocal batchをCoreへ直接送らない。
- Operation IDはhop、retry、failover、reconnectを跨いで維持する。
- 同じOperation IDを世界へ二重適用しない。
- Master generation/epochを持ち、旧世代Masterの遅延出力を拒否する。
- 同じ有効Operation集合はGateway数やMaster個体に関係なく決定論的なCore向け順序へ変換される。

Gateway/Masterはprotocol規則に従って候補適用時刻を形成し、Coreが現在のSimulation Step、deadline、Master generation、順序規則等から最終的な有効適用Stepを確定する。

## 10. Master Gateway

Master GatewayはCoreが安全に役割を担えるGatewayから選出する。

候補には、接続・応答、protocol互換、必要Capability、同期状態その他の安全条件が必要である。

- 選出方式はランダム。
- Master選択結果そのものの再現性は標準要件としない。
- 選択結果とgenerationは診断・監査可能にする。
- Master個体が異なっても同じ有効Operation集合なら世界結果を変えない。
- Master障害時はCoreが再選出する。
- failoverはunfinished batch、ACK待ち、retry中Operationを欠落・重複なく引き継ぎ、live migrationに耐える設計とする。
- split-brain、stale generation、duplicate applyを防止する。

## 11. Gateway cache・公開遅延・再同期

- Gatewayは参照用cacheを持つが、正本ではない。
- cache喪失・不整合は世界喪失ではなく、権威ある同期元から再構築する。
- Gatewayは標準約1秒の論理的な公開遅延bufferを持ち、状態到着のjitter・順序揺らぎを吸収する。
- cacheと公開遅延bufferは別責務とする。
- reconnect時は古いcacheをそのまま信頼せず、基準Simulation Step/generation等を確認して再同期する。
- 再同期中は不整合な状態列を通常状態として公開せず、接続利用者へ再同期中であることを通知する。

Gatewayが0台でも、それ自体を理由にCoreのSimulation Step進行を止めない。Coreは受理済みOperationと内部eventを処理し続け、新規外部Operationだけ受けられない。Gateway復旧後に不在期間を巻き戻さない。

## 12. General View同期とDiver

- ViewはGatewayが公開した確認済み状態を明示的な表示World Time/Stepと共に扱う。
- 表示補間・短期予測を許容するが、presentation-onlyで非権威とする。
- Diver操作は利用者からリアルタイムに感じられるようlocal prediction/correctionを許容し、Core確定状態へreconcileする。
- reconnect中は同期状態を明示する。

Diverは参加時に新規専用住人を生成しない。

- 世界に既に存在する通常住人へ紐付く。
- 大まかな希望条件は指定できるが、条件に合う住人の割当てを保証しない。
- 原則1住人につき1Diver。
- 切断しても別Diverへ自動的に操作権を移さない。
- reconnectしても同じDiver識別を使う。
- 切断中も住人は世界内で行動し続け、Diverは不在中に優先させる行動方針を事前設定できる。

## 13. Admin View・Admin Operation

Admin ViewはGeneral Viewの上位ロールではなく、別の運用境界である。

- Admin View → Gateway → Core の経路を使用する。
- Admin Operation固有の認証・認可、形式、対象、許可条件等の妥当性はGatewayが確認する。
- CoreはUI上のAdmin roleを解釈しない。
- Coreは全操作共通の世界状態不変条件・状態遷移整合性を維持する。
- シミュレーションへ影響しないAdmin操作に限り最優先にしてよい。
- 高影響Admin操作は追加確認・監査対象とする。
- generic Undoは設けず、元へ戻す場合も新しいOperationとして実行する。

Login要求は接続先GatewayからMaster Gatewayへproxyし、login処理はMasterで確定する。login以外のAdmin Core操作をMaster経由へ統一するかは未確定とする。

## 14. Auth / session

- General ViewとAdmin Viewのauth/authz domainを明確に分離する。
- GatewayはOperation type、target、current role/session等から認可し、未認可OperationをCoreへ送らない。
- role変更は明示的な有効時点を持つ。
- privilege revoke後に古い権限で新規Operationを継続させない。
- auth基盤障害時もauthorizationをbypassしない。

具体的なtoken、IdP、session storage技術は未確定。

## 15. Config

- 調整可能な数値・しきい値・時間・件数等は外部Config化する。
- 各componentが自身のConfig fileを所有し、他componentのConfig fileを直接参照しない。
- 他componentへ必要な設定・状態は、Coreに近い責任componentがprotocolで配布する。
- simulation-affecting Configとdisplay/ops-only Configを区別する。
- runtime changeは安全な明示境界でatomicに適用し、simulation-affecting changeはSimulation Stepと履歴へ結び付ける。
- startup Configに不整合があれば起動しない。
- 古いConfigで新項目が欠ける場合はdefault値を適用し、その項目をConfig fileへ追加する。

## 16. Addon境界

Addonはcomponent単位で設定可能とする。

- 標準protocolにはaddon固有機能のpayload、command、汎用拡張領域を載せない。
- addonのinstall状況、identity、version、required/provided Capability等、接続安全性・互換性確認に必要なmeta情報は標準protocolで交換可能。
- addon固有機能をcomponent間で通信する場合は、protocol拡張用framework addon等と追加protocolを使用する方向とする。
- addon構成・依存・Capability・Configに不整合があれば、重大度に関係なく対象componentを起動しない。
- 保存世界が依存するaddonに不整合がある場合も、明示migrationが完全成功しない限り起動しない。

具体的addon API、package format、runtime loading方式等は未確定。

## 17. 保存・replay・復旧

標準デフォルトはSnapshot＋Operation/Event履歴＋高精度replayを前提とする。

- replayは動画ではなくCoreによる決定論的再計算。
- saveは特定Simulation Stepに対応する論理的一貫状態として取得する。
- 進行中saveを許容するが、負荷・整合性上必要なら安全境界で一時停止してよい。
- crash recoveryで受理済み重要Operationを失わず、duplicate applyしない。
- corrupt saveを部分的に読み込んで起動しない。
- old formatは明示的・決定論的migrationを行い、変換不能なら起動拒否。
- restore後も同じ世界のEntity ID、World Time、適用済みOperation、因果系列を維持する。

保存媒体、serialization、archive形式は未確定。

## 18. Full 3D世界

Simulation Coreの権威ある空間モデルはfull 3Dとする。Three.jsはGeneral Viewの描画技術であり、Coreの権威ある空間状態を置き換えない。

単一XYにつき単一Zしか持てないpure heightmapを権威ある地形表現にはしない。洞窟、坑道、地下室、切通し、overhang、同一XY上の複数surface/spaceを表現できる必要がある。

具体的にVoxel、SDF、CSG、mesh、octree等のどれを採用するかは未確定。

## 19. 世界規模の詳細度

- デフォルトでは世界規模で可能な限り個体・物品・建物等の存在・永続ID・重要状態を保持する。
- それらを全世界一律30Hzで詳細更新することは要求しない。
- 遠隔・低重要度対象では更新頻度・計算詳細度を下げられる。
- detail promotion/demotion、aggregation、archive、boundary causalityは決定論的に行う。

## 20. 現時点で詳細設計へ残す主な事項

- 具体的なnetwork transport・serialization形式
- Operationのdeterministic ordering keyと候補適用時刻のwire表現
- Core→Gateway状態配信方式
- Master選出random algorithmとtimeout等の具体値
- Login以外のAdmin操作をMaster経由へ統一するか
- auth token / IdP / sessionの具体技術
- Config file形式・配置・具体key
- save storage・serialization・archive形式
- RNG / Entity ID / state hashの具体algorithm
- Simulation Stepのinteger type・epoch・date/time変換
- General ViewのThree.js scene、LOD、shader、browser/device対応等
- 各世界subsystemの具体data model・algorithm・精度

これらは要件未決ではなく、確定した横断要件の上で以後の詳細設計により決定する事項である。
