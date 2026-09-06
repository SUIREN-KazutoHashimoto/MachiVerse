# Simulation Core設計

## 1. 目的

Simulation CoreはMachiVerseにおける権威あるWorld State、Simulation Step、world rule、決定論的更新、保存・復旧を所有する。

標準構成ではCoreは1つだけ存在する。General View / Admin Viewとは直接通信せず、Gatewayを通じて外部Operationを受け付ける。

## 2. 主な責務

- Authoritative World Stateの保持
- 整数ベースのSimulation Step進行
- 標準30Hzを基準とする固定Step計算
- 1〜16 threadを用いた決定論的parallel update
- resident、environment、organization、economy等のworld subsystem更新
- world-state invariantとsimulation ruleの維持
- deterministic random contextの提供
- persistent Entity IDの一貫性維持
- General View由来final batchの世界状態・simulation rule上の最終可否判定と適用
- Gatewayから受けたAdmin Operationのうち、一般的world-state invariantに照らした状態遷移確認と適用
- external Operationの最終有効application Stepの確定
- Gatewayへ外部公開可能なauthoritative-derived stateを提供
- Master Gatewayの選出・generation管理
- save / replay / recoveryのworld側意味論
- `docs/protocols/core-gateway.md` の所有

## 3. 権威ある時間

### 3.1 Simulation Step

- 権威あるWorld Timeは整数ベースのSimulation Stepとする。
- 標準進行頻度は30Hz。
- 30Hzは外部Configから変更可能であり、source codeへ運用固定値として埋め込まない。
- 秒、日時、社会的暦等は必要に応じSimulation Stepから変換する。
- residentの時刻認識、社会的calendar、View表示時刻はauthoritative Stepと分離する。

Simulation Stepのinteger type、epoch、overflow方針、date/time変換精度は詳細設計で決定する。

### 3.2 Overrun

- Core計算が標準30Hzへ追いつかなくても、処理遅延だけを理由にworld Stepをskipしない。
- real-timeへの追従は目標であり、determinismと因果的連続性を優先する。
- 遅延時のdetail reduction、load policy、warning等の調整可能値はConfig化する。

### 3.3 Pause / speed change

- Pause、slow/fast、time multiplier等をConfig/Admin操作で可能にする。
- Pause中はSimulation Stepを進めない。
- Pause中にexternal Operationを受信・認証・queue保持することは可能とする。
- simulation-affecting OperationはPause中の停止Stepへ曖昧に適用せず、resume後の明示的な有効Stepへ決定論的に割り当てる。
- simulation-non-affecting operational actionは別扱い可能とする。

## 4. Parallel execution

Coreはmultithread executionを前提とする。

- 有効thread数は1〜16。
- 実使用thread数はCore Configから変更可能。
- thread数、thread completion order、OS scheduling、task schedulingによってworld outcomeを変えない。

概念的なupdate modelは次とする。

```text
World State(T)
  ↓
parallel read / calculation
  ↓
deterministic merge / reduction / conflict resolution
  ↓
authoritative apply
  ↓
World State(T+1)
```

具体的なtask graph、lock、partitioning、work stealing等の実装方式は現時点では固定しない。

## 5. Determinism

同一の次条件からは同一の論理的world outcomeを得る。

1. World Seed
2. simulation-affecting Configとその変更履歴
3. accepted external/admin Operation集合
4. Operationのdeterministic order
5. Operationのapplication Simulation Step
6. 同じ内部因果状態

世界結果を次へ依存させない。

- CPU処理速度
- OS scheduler
- thread ID / thread completion order
- wall clock
- Gateway数
- Master個体
- network arrival race
- retry回数

異なるCPU、OS、runtimeを跨ぐ全floating-point operationのbit完全一致は標準要件とはしない。ただし、制御可能なordering、random、ID、reduction、conflictの非決定性は排除する。

## 6. Random

- World SeedとWorld Time / Simulation Stepを乱数生成のbase inputとする。
- target、purpose、event、Entity等のdeterministic logical contextを追加する。
- shared stateful PRNGをthread/call orderで消費し、その順序にworld outcomeを依存させない。
- OS時刻、thread ID、task completion order、非決定論的call countをentropyとして使わない。

具体的なRNG/hash algorithmは詳細設計で決定する。

## 7. Persistent Entity ID

- Entityはsave/restart/replayを跨いで同一Entityを識別できるpersistent IDを持つ。
- ID assignmentをmemory address、thread順、task完了順、非決定論的creation orderへ依存させない。
- 未来に生まれるすべてのIDをworld generation時に事前列挙する必要はない。
- birth/creation eventのdeterministic logical contextからIDを生成可能にし、同一再現条件では同じEntityに同じIDを割り当てる。

ID formatとgeneration algorithmは詳細設計で決定する。

## 8. Gatewayとの関係

Core : Gatewayは1:N。

- CoreはGeneral View / Admin Viewへdirect connectionしない。
- Gateway cacheはauthoritative stateではない。
- CoreはGatewayがcache/publication bufferを構築できるauthoritative-derived stateをprotocol経由で提供する。
- Core内部のmutable data structureを外部へ直接公開しない。

Core→GatewayのPush/Pull、full/delta、snapshot等の具体state delivery方式は未確定。

## 9. General View由来final batch

General View OperationはGateway側でauthn/authz、local aggregation、local conflict mediationを受け、Master Gatewayでdeterministic merge/cross-Gateway mediationされたfinal batchとしてCoreへ到達する。

CoreはUI role名やnon-Master Gatewayのindividual requestを直接解釈しない。

Coreはfinal batch内Operationについて、少なくとも次をauthoritative world stateに照らして判定する。

- target Entity/stateが存在・有効か
- 現在world stateから状態遷移可能か
- simulation rule/world invariantに反しないか
- deterministic apply order
- same-target conflictの最終state transition

Gateway/Masterでexternal-request conflictが整理済みでも、authoritative world state上成立しないOperationをCoreは拒否できる。

## 10. Admin Operationの責務境界

Admin View→Gateway→Coreの経路を使用する。

Q235/Q275に従い責務を分ける。

### Gatewayの責務

- Admin authentication / authorization
- Admin operation format
- targetとscope
- Admin操作としてのallowed condition
- protocol-level validity

### Coreの責務

- UI上のAdmin roleを解釈しない。
- Adminだからという理由で特別なauthorizationを再判定しない。
- すべてのOperation共通のworld-state invariant、reference consistency、状態遷移として成立するかを維持する。

つまり、GatewayがAdmin操作として許可したOperationでも、一般的world-state invariantを破壊するならCoreが状態遷移を拒否できる。拒否理由はAdmin権限ではなくworld/state-transition上の理由である。

## 11. External Operationのapplication Step

Q203/Q223/Q224/Q276を次のように統一する。

- physical network arrival timeをそのままapplication timeにしない。
- Gateway/Masterはprotocol規則に従いcandidate application time/Stepに必要な情報を形成する。
- Coreはcurrent Simulation Step、deadline、Master generation、deterministic ordering rules等に基づき最終有効application Stepを確定する。
- late Operationでpast finalized Stepをretroactiveにrewriteしない。
- late Operationはprotocol ruleに従いfuture valid Stepへdeferまたはrejectする。
- same effective Operation set / same logical conditionsならnetwork timingだけで結果を変えない。

candidate field、deadline表現、tie-break key等はprotocol詳細設計で決定する。

## 12. Master Gateway

- Master GatewayはCoreが選出する。
- candidateはconnectedだけでなくresponsive、protocol-compatible、required Capability、sync state等の安全条件を満たす必要がある。
- selectionはrandomとする。
- Master selection結果自体のdeterministic replayは標準要件としない。
- selection resultとgenerationをdiagnostic可能にする。
- Master identityがworld outcomeへ影響してはならない。
- Coreはcurrent Master generationをauthoritativeに管理し、stale old-generation outputを拒否する。
- failure時はsafe candidateから再選出する。

具体的なoperational random source、health threshold、selection algorithmは未確定。

## 13. Operation idempotency

Core/Gateway protocolは、retry・failover・reconnectによって同一Operationが二重適用されない意味論を持つ。

- stable Operation IDを維持する。
- Batch ID / Master generation等からduplicate/stale contextを識別可能にする。
- ACK lossやretry countがworld outcomeを変えない。

具体的dedup retention/data structureは詳細設計で決定する。

## 14. Gatewayが0台の場合

接続Gatewayが0台になっても、それ自体を理由にSimulation Stepを停止しない。

- internal eventは通常規則で進行する。
- 既にCoreが受理済みのOperationは決定済みのapply ruleに従って処理する。
- 新規external OperationはGatewayがないため入らない。
- Gateway復旧後にgateway-absent期間へworldを巻き戻さない。

## 15. World-scale detail

Coreのauthoritative world modelはfull 3Dであり、世界規模のdetail levelを決定論的に制御する。

- defaultでは可能な限りworld-wideにEntityの存在、persistent ID、重要stateを保持する。
- all-world 30Hz detail updateは要求しない。
- remote / low-importance regionはupdate frequency/detailを下げられる。
- detail promotion/demotion、aggregation、archive、boundary causalityでEntity identityと重要因果を壊さない。

## 16. Save / replay / recovery

- defaultはSnapshot＋Operation/Event history＋high-precision replay方向。
- replayはrecorded videoではなくCoreによるdeterministic recalculation。
- saveはspecific Simulation Stepに対応したlogically consistent stateを取得する。
- running saveを許容するが、安全なconsistent saveが高負荷・困難な場合はsafe boundaryで一時停止してよい。
- pause/no-pauseの選択でworld outcomeを変えない。
- crash recoveryでaccepted important Operationをloss/duplicateしない。
- corrupt saveはpartial loadして起動しない。
- old formatはexplicit deterministic migrationを経由し、変換不能なら起動拒否する。
- restore後も同じworld identity、Entity ID、Simulation Step、applied Operation historyを維持する。

storage/serialization形式は未確定。

## 17. Config

Core ConfigはCore自身が所有する。

- 30Hz standard frequency
- active thread count 1〜16
- detail level関連threshold/frequency
- world generation/simulation条件
- save/replay関連の調整数値
- lag/load policy

等の調整可能値を外部Config化する。

他componentはCore Config fileを直接読まない。境界を越えて必要な有効設定・意味はCore-owned protocolで配布する。

startup Configに不整合があればCore/worldを起動しない。simulation-affecting runtime Config changeはexplicit safe Stepでatomicに適用し、historyへ記録する。

## 18. Protocol ownership

Coreは `docs/protocols/core-gateway.md` のownerである。

そのprotocolは少なくとも次の意味を契約化する必要がある。

- state publication/synchronization basis
- Operation / Batch identity
- Master generation
- candidate/final application Step semantics
- retry / idempotency / stale generation behavior
- protocol version / Capability
- operational/admin Operation transport
- reconnect/recovery semantics

具体wire schemaはprotocol詳細設計で決定する。

## 19. 禁止事項

- General View / Admin Viewへのdirect dependency
- Gateway implementation codeへのdependency
- shared DTO libraryによるcomponent coupling
- non-Master GatewayのGeneral View local batch direct accept
- Gateway authorization結果だけを理由にworld-invalid state transitionを無条件適用すること
- Admin UI roleをCore authorization logicへ持ち込むこと
- thread completion order、wall clock、network raceをworld outcomeへ利用すること
- overrun時にworld Stepをskipすること
- Gateway不在だけを理由にworldを巻き戻すこと
- single-Z-only terrainをauthoritative full-3D worldとして扱うこと

## 20. 詳細設計へ残す事項

- internal system/task dependency representation
- deterministic merge/reduction algorithm
- same-Step event ordering key
- RNG/hash algorithm
- persistent Entity ID format/generation algorithm
- Simulation Step integer type/epoch
- candidate application Step wire fields
- Core→Gateway state delivery method
- save storage/serialization/archive format
- numeric determinism guarantee boundary by supported platform
- multi-Core addonの具体仕様（標準外）
