# 保存・復旧の意味論

## 確定方針

第270〜274問は以下を採用する。

- Q270: C。ただし、保存処理の負荷または同時実行上の制約が大きく、安全かつ一貫した保存を継続実行できない場合は、World Timeの整合した境界でシミュレーションを一時停止して保存してよい。
- Q271: C。
- Q272: C。
- Q273: C。
- Q274: C。

Phase 1の具体的なSnapshot / History / durability / recovery contractは `docs/design/phase1-persistence-replay-recovery.md` を正本とする。

## Q270: 保存中のシミュレーション

- 原則として、シミュレーションを進行させながら保存可能な設計とする。
- 保存データは、必ず特定のWorld Time／Simulation Stepに対応した論理的に一貫した状態でなければならない。
- 並列計算途中、決定論的マージ途中、状態適用途中などの中間状態を保存してはならない。
- Snapshotは完全な `State(S)` と、historyの `HistoryAnchor=H` を組にしたconsistent cutとする。
- running snapshotではStep boundaryでimmutable view / copy-on-write rootをfreezeし、その後のI/O中はsimulationを継続可能とする。
- 一貫性確保のための短時間の内部同期点は許容する。
- 保存処理の負荷、メモリ圧力、I/O待ち、または実装上の整合性確保コスト等により、進行中のまま安全に保存することが困難な場合は、整合したWorld Time／Step境界で一時停止して保存してよい。
- 一時停止の判断基準やしきい値など調整可能な数値は外部Configで設定可能とする。
- 一時停止の有無によって、同じ確定入力系列から得られる世界結果が変化してはならない。

## finalized state と durability

`State(S+1)` はtransition `S` のdurable commitが完了した後にauthoritative finalized stateとなる。

- durable commit前のin-memory stateをnormal authoritative publicationへ出さない。
- applied Operationのterminal successもcommit前に返さない。
- crash後はlast durable finalized stateから継続する。
- 外部へ確定公開済みのWorld Timeを、process crashだけを理由に巻き戻さない。

world-affecting OperationをCoreが`ACCEPTED`と返す場合も、OperationId / immutable payload / recoveryに必要なlogical contextを先にdurable historyへ保存する。

## Q271: 保存データ破損時

- 破損または不整合のある保存点を部分的に読み込んで世界を起動しない。
- Snapshot、Operation履歴、Config、メタデータ等の整合性を検証する。
- Snapshot manifest / section digestとhistory hash chainを検証する。
- crashによる未commitのtorn tailは、durable complete recordより後の部分だと確認できる場合のみtruncate可能とする。
- committed regionのhash mismatchやhistory gapはskipして後続recordを通常適用しない。
- 最新保存点が利用不能な場合、正常な以前の保存点と、その後の確定済みOperation／履歴から同じlatest durable finalized stateまで決定論的に復旧できる場合にfallback可能とする。
- 単に古いSnapshotが読めるだけで、後続のdurable accepted Operationやfinalized stateを捨てて起動しない。
- acknowledged durable factを復元できない場合はsilent data lossで起動せず、復旧不能として起動拒否する。

## Q272: 保存とConfig

- 保存世界には、その世界状態を生成したシミュレーション影響Configを再現できる情報を保持する。
- ConfigGeneration / ConfigDigest / simulation Config historyをrecovery metadataから検証可能にする。
- 復旧時には、保存世界が要求するConfigと現在のConfigとの差異を分類する。
- 表示・運用のみの差異と、世界状態・決定論・再現性へ影響する差異を区別する。
- 再現性または整合性を壊す差異がある場合、暗黙に現在Configへ置換して起動してはならない。
- WORLD_REGENERATION_REQUIRED値が保存世界と不整合な場合、同一WorldIdをその値で継続しない。
- 必要に応じて明示的・決定論的な移行操作を要求する。

## Q273: 古い保存形式の互換性

- 可能な範囲で保存形式の後方互換性を維持する。
- persistence schemaはmajor.minorで識別可能にする。
- 現行版が直接読み込めない旧形式については、明示的なmigration経路を用意できる設計とする。
- 世界結果へ影響する移行は決定論的であり、同一入力に対して同一の変換結果を得なければならない。
- migrationはsource saveをread-only inputとし、targetをstaging生成・全体検証した後にのみpublishするnon-destructive方式を標準とする。
- 必須情報を失う、意味論が不明、アドオンやConfigを含めて整合性を確認できない等の場合は変換失敗として起動拒否する。
- 情報を黙って破棄・補完して起動することを標準動作としない。
- schema incompatibilityをdata corruptionとみなして古いworld stateへsilent fallbackしない。

## Q274: 復旧後の世界同一性

- 保存、クラッシュ復旧、プロセス再起動を経ても、世界は同一の因果系列の継続として扱う。
- 同じ確定状態、World Seed、シミュレーション影響Config、確定入力履歴からは、同じ世界状態系列を継続できなければならない。
- Entity ID、World Time、適用済みOperation、決定済み履歴等の同一性を維持する。
- 復旧を理由にWorld SeedやEntity ID等を再発行し、別世界として扱ってはならない。
- process restartを跨いだstate continuityは `StateContinuityToken` で識別可能にする。
- 保存・復旧処理そのものが世界内の因果へ意図せず影響してはならない。

## recovery checkpoint

committed Snapshotは最低限次の情報からrecovery checkpointを構成する。

- WorldId
- SnapshotId
- SnapshotStep
- HistoryAnchor sequence / digest
- StateContinuityToken
- simulation ConfigGeneration / ConfigDigest

Snapshot load後はHistoryAnchorの次recordからcontiguous valid historyをreplayし、last durable finalized stateまで復元する。

## publication再開

recovery後はnormal publication前に:

1. latest durable finalized stateまでreplayする。
2. current basis_step / StateContinuityTokenを確定する。
3. protocol connection / negotiationを再成立させる。
4. Gateway側continuityと一致しなければresyncする。
5. resync中の不整合delta chainをnormal confirmed stateとして公開しない。

Gateway cacheはauthoritative recovery sourceではない。

## history / dedup

- acceptedだがterminalでないOperationIdをSnapshot compactionで失わない。
- retained dedup window内のterminal identity / resultをSnapshotまたはhistoryから復元可能にする。
- exact dedup retention windowはP1-06で定義する。
- Snapshot作成だけを理由にhistoryを即時削除しない。

## 未確定事項

P1-05でcross-cutting semanticsは確定した。残る具体事項は次の通り。

- 保存中の一時停止判断に用いる具体的な指標・しきい値
- physical storage product / file format / binary serialization
- compression / encryption / backup / replication方式
- 保存世代数・保持期間・rotationの具体値
- large-world state diagnostic hashのslice/tree方式
- P1-06で定義するdedup retention / retry / late Operation / failover custody
