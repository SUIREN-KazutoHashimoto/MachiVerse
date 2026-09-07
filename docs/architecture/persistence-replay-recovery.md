# 永続化・再開・リプレイ・障害復旧設計

## 確定方針

MachiVerseの保存は単なる状態保存ではなく、同一条件から同一世界進行を再現可能にするための基盤とする。

Phase 1 の concrete contract は `docs/design/phase1-persistence-replay-recovery.md` を正本とする。本書はarchitecture上の意味を要約する。

## 永続化方式

永続化方式は外部Configで設定可能とする。

- 少なくとも、全状態スナップショット中心、スナップショット＋操作・イベント履歴、任意時点への高精度リプレイを重視した履歴保持、という複数の運用方針を選択可能にする。
- デフォルトは、スナップショットと必要な入力・履歴を組み合わせ、任意時点への高精度リプレイを標準的に可能にするC相当とする。
- Configで方式を変更しても、選択した方式に対応する再現性・復旧保証を明確にする。
- シミュレーション結果へ影響するConfig変更そのものも、再現条件として扱う。
- 永続化周期、保持期間、粒度等の調整可能な数値は外部Configとする。

physical database / file layout は本architectureでは固定しない。実装は `State(S)` Snapshot、durable append history、atomic commit / integrity verification の意味契約を満たす必要がある。

## Snapshot boundary

Snapshotは必ず完全な `State(S)` を表す。

- `effective_step < S` のcommitted effectを含む。
- `effective_step >= S` のmutationを含まない。
- 並列計算途中、deterministic merge途中、apply途中を保存しない。
- Snapshotは `(SnapshotStep=S, HistoryAnchor=H)` のconsistent cutを持つ。
- Snapshot内のRecoveryStateとhistory `H+1` 以降を接続して同一causal lineを継続できるようにする。

原則はrunning snapshotとする。

- Step boundaryで短いconsistency barrierを取り、immutable view / copy-on-write rootをfreezeする。
- frozen viewの保存I/O中はsimulationを継続可能とする。
- safe immutable viewを作れない場合は、整合したStep boundaryで一時停止して保存してよい。
- 保存方式の違い・I/O completion timingでworld outcomeを変えない。

## durable finalized Step

`State(S+1)` はtransition `S` のdurable commit後にexternally finalized / publishableとする。

- in-memory計算完了だけではfinalizedとしない。
- durable commit前にauthoritative state publicationを行わない。
- applied Operationのterminal successも対応commitより先に返さない。
- crash後はlast durable finalized stateから継続し、外部へ確定公開済みのWorld Timeを理由なく巻き戻さない。

logical commit recordはStep、有効Config generation/digest、適用Operationのcanonical order、terminal outcome、state continuityを再構成可能にする。

## durable Operation acceptance

world-affecting OperationをCoreが`ACCEPTED`として返す場合、その前にOperationId、immutable payload digest、logical request、recoveryに必要なscheduling contextをdurable historyへ保存する。

- ACK直後にcrashしてもaccepted Operationを復元できる。
- retryでは同じOperationId / digestを維持する。
- acceptedだが未適用のOperationはSnapshotまたは後続historyから再構成する。
- auth failureやmalformed requestなどCore authoritative acceptance境界へ到達していないrequestはworld historyへ必須ではない。

Gateway / Masterのhop ACKとCore authoritative acceptanceを同一視しない。

## durable history

world persistence historyはappend-only logical sequenceを持つ。

```text
HistorySequence := uint64
```

- 1から単調増加し、0はgenesis sentinel。
- SHA-256 domain-separated record digestでprevious recordをbindし、missing / reorder / replacementを検出可能にする。
- HistorySequenceそのものをsimulation orderingへ利用しない。
- physical storageはcomplete recordとtorn recordを区別できなければならない。

共通record categoryは少なくとも次を扱う。

- world genesis
- Operation durable acceptance
- Operation final scheduling
- terminal non-applied result
- simulation Config change
- Step transition commit
- Snapshot commit
- persistence migration

## State continuity

process restartやGateway reconnectを跨いでconfirmed state chainを識別するため `StateContinuityToken` を使用する。

- genesis tokenから開始する。
-各transition commitをprevious tokenへchainして次tokenを導出する。
- same committed causal historyではsame tokenを得る。
- tokenはworld-state equality hashではない。
- divergence検出はstate diagnostic hashを使用する。

Core→Gateway state/deltaでbase tokenがreceiverの保持tokenと一致しない場合、blind delta applyせずresyncする。

## RecoveryState

Snapshotはpublic World Stateだけでなく、同一worldを継続するために必要なauthoritative recovery stateを保持する。

少なくとも次を保存または再構成可能にする。

- WorldId / WorldSeed
- SimulationStep
- StepRate history / rate generation
- simulation Config generation / digest / history cursor
- enabled domain set / dependency state
- deterministic scheduler / future event state
- pending accepted Operation
- retained Operation dedup / terminal result state
- Entity identity continuityに必要なstate
- current Master generation
- StateContinuityToken

P1-02のrandomはstateless context方式なので共有PRNG cursorを保存対象としない。

## 再開時の再現性

- 保存済みWorld Seed、シミュレーション影響Config、世界状態、必要な操作・イベント履歴から決定論的に再開できることを標準要件とする。
- 同一保存点から複数回再開した場合、以後に同一の有効操作・順序・適用時刻が与えられれば結果は一致しなければならない。
- 再開結果はOSスケジューリング、スレッド完了順、処理速度、Gateway数等に依存してはならない。
- 再開処理でも通常実行と同じ乱数決定論・状態適用順序の原則を維持する。
- recovery中のwall-clock経過をSimulation Stepへ自動加算しない。

## クラッシュ復旧

標準recoveryは、latest usable committed Snapshotとそのhistory anchor以後のcontiguous valid historyからlast durable finalized stateまで再計算する。

- snapshot manifest / section digestを検証する。
- history hash chain / sequence continuityを検証する。
- Operation acceptance / scheduling / Config changeを再構成する。
- transition commitのcanonical Operation order / Config generation / continuity tokenと再計算結果を照合する。
- state diagnostic hashが記録されているcheckpointでは一致を検証する。
- recovery完了までnew external inputをauthoritative processingへ混在させない。

## torn tail / corruption

crashにより最後のrecordがpartialで、durable completion前であることを判定できる場合、そのuncommitted tailはtruncate可能とする。

一方、committed regionにhash mismatch / history gap等がある場合:

- corrupted itemだけを読み飛ばして後続historyをnormal applyしない。
- redundancyや以前の正常Snapshot＋intact historyで同じdurable factsを復元できる場合は利用できる。
- durable accepted Operationまたはfinalized stateを失う復旧しかできない場合、silent data-loss startupをせず起動拒否する。

latest Snapshotだけが破損していても、以前のSnapshotからintact historyでsame latest finalized stateへ到達できるならfallback可能とする。

## リプレイ

リプレイは表示録画ではなく、保存されたRecoveryStateと決定論的入力履歴からSimulation Coreが世界を再計算する仕組みとする。

- recovery replay: latest durable finalized stateまで復旧する。
- historical replay: target State(T)まで再計算する。
- verification replay:同一入力を複数回再生しstate diagnostic hash等を比較する。

replayでoriginal wall clock、network latency、thread schedule、Gateway数、Master identity、retry countを再現条件にしない。

## Configと保存データの整合性

P1-03に従い、saved simulation Configをcontinuationの正本とする。

- saved ConfigGeneration / ConfigDigestを検証する。
- current local Configとの差異をSIMULATION / OPERATIONAL / PRESENTATIONとmutabilityで分類する。
- replay過去区間へcurrent Configをsilent overrideしない。
- WORLD_REGENERATION_REQUIRED値の不整合は既存WorldIdで継続しない。
- 必要なmigrationはdeterministicかつ明示的に行う。

## 保存形式migration

保存形式のmigrationはnon-destructiveとする。

1. source saveをread-only inputとする。
2. targetをstaging生成する。
3. Snapshot / history / Config / addon dependencyを全体検証する。
4. successful targetのみatomic publishする。
5. publish成功後にsource retentionを適用できる。

migration failure時はsourceを維持し、partial targetをactive saveとして扱わない。

正常だが互換不能なsaveを「corruption」とみなし、古いworld stateへsilent fallbackしない。

## history compaction

Snapshot作成後もhistoryを即時削除しない。

compactionは少なくとも次を保持できる場合のみ許可する。

- configured replay guarantee
- latest required recovery checkpoint
- pending accepted Operation
- dedup retention中のOperation identity / terminal result
- simulation Config history
- migration / audit上必要なfact
- state continuity validation

exact dedup retention / history floorはP1-06で確定する。

## publication / resync

recovery後のnormal publication前に:

- last durable finalized stateまでreplayを完了する。
- current basis_step / StateContinuityTokenを確定する。
- protocol negotiationを再成立させる。
- peer continuityを確認する。
- mismatch時はfull resync / rebuildする。

Gateway cacheをCore recovery sourceとして扱わない。

## persistence failure

persistence subsystemがdurable writeを継続できない場合:

- 新しいauthoritative finalized Stepを増やし続けない。
- durabilityを保証できないworld-affecting Operationへauthoritative `ACCEPTED`を返さない。
- last durable finalized stateを保持する。
- failureをhealth / Admin Viewへ診断可能にする。
-必要に応じsafe pause / stopする。

storage性能不足を理由にStep skipやnon-durable authoritative publishを行わない。

## 詳細度可変シミュレーションとの関係

- 詳細領域の昇格・降格、個体化・集約、完全復元アーカイブ等を使用する場合、そのstateもRecoveryState / historyから再現可能にする。
- 同じ保存状態を再開・リプレイした結果、詳細化境界や集約処理のtiming差によってworld resultが変わってはならない。
- 外部Configで詳細度方針を変更した場合は、保存データとの互換性・再現性をConfig差異として扱う。

## 実装詳細へ残す事項

P1-05でcross-cutting semanticsは確定した。次はcomponent implementation設計で以下を選定する。

- physical storage product
- concrete binary serialization / file layout
- compression / encryption
- replication / backup topology
- Snapshot section / chunk strategy
- large-world state diagnostic hashのslice / tree granularity
- storage throughput / fsync / group-commit implementation

P1-06ではexact dedup retention、late Operation、retry / failover custodyを本durability contractへ接続する。
