# アーキテクチャ整合性監査

## 1. 目的

本書は、対話ベースで確定した Q001〜Q279 の要件を基準に、`docs/architecture` と `docs/protocols` の横断的な整合性を整理するための監査記録です。

要件定義の決定記録は `docs/requirements` を正本とします。個別設計書に古い記述が残っていた場合は、後続の確定要件で明示的に変更・補足された内容を優先します。

## 2. 解釈の優先順位

1. 後続Qで以前の要件を明示的に変更・補足・解消した場合は、後続Qを優先する。
2. 確定要件と個別設計書の古い記述が食い違う場合は、`docs/requirements` の確定要件を優先する。
3. プロトコルの具体的な通信契約は、確定要件に反しない範囲で `docs/protocols` を正本とする。
4. `TBD` / `今後決定が必要な事項` は詳細設計の未決事項であり、後続Qですでに確定した事項をTBDとして扱わない。
5. 将来アドオンの可能性は、標準実装の確定要件を弱める根拠にしない。

## 3. 解消した主な矛盾・古い記述

### 3.1 Q083 と Q190/Q193/Q265: 世界規模の個体保持

旧記述には「詳細領域外では集約し、世界全域の全住人を個体として保持する方式は標準必須ではない」という表現がありました。一方、Q190/Q193ではデフォルトCとして世界規模で可能な限り個体状態を保持する方向が採用され、Q265で意味が明確化されました。

現在の有効要件は次のとおりです。

- 個体の存在・永続ID・重要な永続状態を保持することと、高頻度・高詳細度で更新することを分離する。
- デフォルトでは世界規模で可能な限り個体・物品・建物等の同一性と重要状態を保持する。
- 遠隔・低重要度対象について更新頻度や計算詳細度を下げることは可能であり、全世界を30Hzで一律詳細更新することは要求しない。
- Configで別の集約方式を選択できる場合も、因果・同一性・保存・復元の整合性を壊さない。

### 3.2 Q193 と Q266: 未来Entity ID

旧Q193の「世界生成時点ですべての将来個体へ恒久IDを割り当てる」という表現は、Q266で具体化されました。

現在は、未来に出生・生成される全Entity IDを世界生成時に列挙・予約する必要はありません。出生・生成事象を一意に識別できる決定論的コンテキストから永続IDを生成し、スレッド順、処理完了順、OSスケジューリング等へ依存させません。

### 3.3 Q209 と Q252: 決定論診断の標準範囲

Q209で状態ハッシュ等による反復実行検証を要求した後、Q252で標準範囲を明確化しました。

- 標準: World State等の整合性ハッシュ比較による不一致検出。
- 将来アドオン: 不一致箇所の詳細な自動局所化、Operation履歴・Config・World Timeとの高度な相関解析。

標準設計へC相当の高度診断を必須化しません。

### 3.4 Q235 と Q275: Admin妥当性チェックとCore責務

Admin操作固有の妥当性確認はGatewayの責務です。一方、Coreは全操作に共通する世界状態の不変条件と状態遷移整合性を維持します。

- Gateway: Admin認証・認可、操作形式、対象、Admin操作としての許可条件等。
- Core: UI上のAdminロールを解釈せず、一般的な世界状態不変条件・状態遷移整合性を維持。

GatewayがAdmin操作として許可したことを理由に、Coreが不整合な世界状態を生成してはなりません。

### 3.5 Q120 と Q279: 通貨供給・金融政策

Q120時点で未確定だった貨幣発行、通貨供給、基本的な金融政策はQ279で標準範囲へ確定しました。

- 通貨発行、供給量、発行主体、基本的な金融政策を標準で扱う。
- 現代的な中央銀行制度を普遍的前提としない。
- 発行・供給調整の制度自体が社会・時代・歴史によって形成・変化・競合・衰退し得る。
- 高度な金融市場・高度な金融政策モデルは将来アドオンで拡張可能。

### 3.6 Q246 と Q255: 標準プロトコル上のアドオン情報

Q246の「アドオンに関する情報交換」は、Q255により範囲が明確化されました。

標準プロトコルで交換してよいアドオン情報は、接続安全性・互換性・Capability判定に必要なメタ情報です。標準プロトコルにアドオン固有の機能ペイロード、追加コマンド、汎用拡張データ領域を設けません。

アドオン固有機能をコンポーネント間で通信する場合は、標準プロトコルではなく、プロトコル拡張用の前提フレームワークアドオン等と、その上に成立する追加プロトコルを使用する方向とします。具体APIや通信方式は未確定です。

### 3.7 Q200/Q203 と Q276/Q277/Q278: World TimeとOperation適用

現在の有効要件を次のように統一します。

- 権威あるWorld Timeは整数ベースのSimulation Step。
- 標準は30Hzの固定Stepを基準とする。
- 30Hzに処理が追いつかなくても世界Stepを飛ばさない。
- Gateway/Masterはプロトコル規則に従いOperationの候補適用時刻を形成し、Coreが現在Step、deadline、Master generation、順序規則等から最終有効Stepを確定する。
- Pause中は受信・認証・認可・キュー保持を可能とするが、シミュレーション影響OperationはResume後の明示的な有効Stepへ決定論的に割り当てる。

### 3.8 Q220〜Q229/Q269: Master・再送・再同期

Masterの選択結果自体を世界の決定論対象として再現する必要はありません。ただし次は必須です。

- Coreが安全に役割を担えるGatewayだけからMasterを選出する。
- Master generation/epochを持ち、旧世代出力を拒否する。
- Operation ID/Batch IDを再送・failover・reconnectで維持し、重複適用しない。
- 同じ有効Operation集合では、Gateway数、Master個体、通信速度、スレッド順に依存せず同じ世界結果になる。
- reconnectしたGatewayは古いcacheを信用せず再同期し、再同期中であることを利用者へ通知する。
- Master切替はlive migrationに耐える設計とする。

### 3.9 Q260〜Q264: Diverと住人

- Diver参加時に専用の新規住人を生成しない。
- 既存の通常住人へ紐付く。
- 大まかな希望条件は指定できるが、条件充足を保証しない。
- 原則1住人につき1Diverで、切断を理由に別Diverへ自動変更しない。
- reconnectしても必ず同じDiver識別を使う。
- 操作住人が死亡した場合、その住人は通常の死亡処理に従い、Diver識別自体は維持する。

### 3.10 Q270〜Q274: 保存・復旧

- 保存は特定Simulation Stepの論理的一貫状態として取得する。
- 通常は進行中の保存を許容するが、負荷・整合性上必要なら安全境界で停止して保存してよい。
- 破損保存を部分的に読み込んで起動しない。
- 有効Snapshotと履歴から可能な限り最新確定状態へ決定論的に復旧する。
- 古い保存形式は明示的・決定論的migrationで変換し、変換不能なら起動拒否する。
- 復旧後も同一世界の因果系列、Entity ID、World Time、適用済みOperationを維持する。

## 4. Configの統一解釈

Config関連文書は次の意味で統一します。

- 各コンポーネントが自身のConfigファイルを所有し、他コンポーネントのConfigファイルを直接参照しない。
- 他コンポーネントへ影響する必要情報は、Coreに近い側の責任コンポーネントがプロトコルで配布する。
- シミュレーション影響Configと表示・運用のみのConfigを区別する。
- 各項目に再起動必須、世界再生成必須、実行時変更可能等の変更可能性を定義する。
- 実行時変更は明示的な安全境界で原子的に適用し、シミュレーション影響変更はWorld Time/Stepと履歴に結び付ける。
- 起動時Configに不整合があれば起動しない。
- 古いConfigで新しい項目が欠ける場合は既定値を補い、その既定値をConfigファイルへ追加する。

## 5. 標準プロトコルの統一解釈

- プロトコルはMajor.Minorで管理する。
- Major不一致は接続拒否。
- 同一Major内の新Minorは後方互換を維持する。
- 接続時に対応Capabilityを交換し、必要Capability不足を明示する。
- reconnect、Master切替、live migration、アドオン状態変更等で有効Capabilityが変化し得る場合、安全に再交渉または接続再確立する。
- 標準プロトコル上のアドオン情報はメタ情報に限定し、アドオン固有機能データを載せない。

## 6. 確認済みの欠落疑義

過去に途中切れの可能性があった以下の文書を再確認しましたが、現ブランチ上では要件内容が成立する形で完結しており、欠落修復は不要です。

- `dynamic-water-simulation.md`（Q086）
- `item-damage-repair.md`（Q088）
- `material-simulation.md`（Q115）

## 7. 詳細設計へ残すTBD

次のような項目は、要件矛盾ではなく意図的に詳細設計へ持ち越します。

- 実際の通信transport・serialization形式
- Operationの具体的な順序キー、候補適用時刻のwire表現
- Master選出乱数アルゴリズム、各timeout等の具体値
- Core→Gateway状態配信方式（Push/Pull、全量/差分等）
- Admin操作のうちlogin以外をMaster経由へ統一するかどうか
- 認証token/IdP/sessionの具体技術
- Configファイル形式・配置・具体キー
- 保存媒体・serialization・archive形式
- RNG、Entity ID、状態hashの具体アルゴリズム
- Simulation Stepの整数型・epoch・日時変換規則
- Three.jsを用いたGeneral Viewの具体的scene/LOD/shader/update/UI構成
- 各世界サブシステムの具体データ構造・アルゴリズム・精度

これらは、確定要件を変更するものではなく、以後の詳細設計で決定します。

## 8. 関連する横断設計書

- `requirements-consistency-resolution.md`
- `final-cross-cutting-semantics.md`
- `deterministic-update-execution.md`
- `deterministic-random-id-numerics.md`
- `config-semantics.md`
- `gateway-operation-delivery.md`
- `gateway-master-failover.md`
- `gateway-cache-resynchronization.md`
- `authentication-authorization-session.md`
- `protocol-compatibility-capability.md`
- `addon-boundary-safety.md`
- `persistence-replay-recovery.md`
- `persistence-save-recovery-semantics.md`
- `diver-resident-binding.md`

## 9. 非変更範囲

この整合性整理は設計・要件ドキュメントの整備であり、Simulation Core、Gateway、General View、Admin Viewの実装コードや具体的なアドオンAPIを新規定義するものではありません。
