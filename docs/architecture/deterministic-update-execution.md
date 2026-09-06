# 決定論的更新・時間進行設計

## 確定方針

第200〜204問はすべてCを採用する。後続のQ276〜Q278により、Operationの最終適用Step、World Timeの内部表現、Pause中Operationの意味を追加で確定する。

### World Timeと時間進行

- Simulation Coreは固定World Time stepを基礎として進行する。
- Q277により、**権威あるWorld Timeの内部表現は整数ベースのSimulation Stepとする。**
- 標準の計算基準は30Hzとする。ただし30Hzはnetwork配信やView更新を毎Step保証する意味ではない。
- 秒、日時等は必要に応じSimulation Stepから変換する。
- 社会的calendar、residentのtime awareness、View表示時刻はCoreのauthoritative Simulation Stepと分離する。
- 実時間は追従目標であり、世界法則の基準時刻としてOS wall clockへ依存しない。
- time multiplier、Pause、slow/fastを外部Configまたは正規Admin Operationによって制御可能とする。
- 調整可能な数値は外部Configへ置く。

Simulation Stepの具体的integer type、epoch、overflow方針、date/time変換精度は詳細設計へ持ち越す。

### 同一Simulation Step内の処理

- thread/taskの完了順をWorld Stateのapply順序として使用しない。
- 基本原則を `World State(T) -> read / parallel calculation -> deterministic merge -> apply -> World State(T+1)` とする。
- system・Operation間のdependencyとconflictは決定論的に解決する。
- 同一Stepの処理結果はOS scheduling、thread execution order、processing speedに依存してはならない。
- この原則は具体的thread API、job system、ECS等を指定するものではない。

### 同時Event

- 同じSimulation Stepのeventをnetwork arrival orderやparallel completion orderだけで並べない。
- 必要な場合はdeterministic identifier、priority relation、target context等からstable orderを決定する。
- 相互に独立し順序がworld outcomeへ影響しないeventはparallel execution可能とする。
- parallel executionしてもmerge後のWorld Stateは同一入力に対して一致しなければならない。

具体的なsame-Step ordering keyは詳細設計で決定する。

### External Operationの適用Step

Q203とQ276を次のように統一する。

- Gatewayへ到着したwall-clock instantをWorld Stateへの直接application timeとしない。
- Gateway / Master Gatewayはprotocol ruleに従ってcandidate application time/Stepに必要な情報を形成する。
- Coreはcurrent Simulation Step、reception deadline、Master generation、deterministic ordering rule等から**final valid application Step**を確定する。
- same effective Operation set / same logical conditionsでは、Gateway数、Master個体、network arrival timing、thread raceによってworld outcomeを変化させない。
- late Operationで過去のfinalized Stepを書き換えない。protocol ruleに従ってlater valid Stepへdeferまたはrejectする。
- Gateway側external-request conflict mediationとCore側world-state/simulation-rule validityの責務を混同しない。

candidate application informationの具体wire field、deadline representation、tie-breakerは詳細protocol設計で決定する。

### Pause中のOperation

Q278により次を確定する。

- Pause中もexternal requestの受信、authn/authz、validation、queue保持は可能とする。
- Pause中はauthoritative Simulation Stepを進めない。
- simulation-affecting OperationをPause中の停止Stepへ曖昧にapplyしない。
- simulation-affecting OperationはResume後の明示的なvalid Stepへ決定論的に割り当てる。
- simulation-non-affecting operational actionはPause中でも別扱いで実行可能とする。
- Pause duration、受信race、processing speedがworld outcomeの暗黙入力になってはならない。

Queue capacity、expiry、Resume後の具体assignment rule等は詳細設計で決定する。

### 処理遅延

- 30Hzの計算時間を超過したことだけを理由としてworld Step skipを行わない。
- processing capacity不足時はdeterminismを優先し、必要に応じ実時間とのlagを許容する。
- allowed lag、slowdown、load reduction、detail adjustment等の調整可能なbehavior/thresholdは外部Configで制御する。
- load reductionやdetail changeを行う場合も、同一再現条件では同一結果となるよう決定論的でなければならない。

## Gateway不在との関係

Q268により、connected Gatewayが0台になったこと自体を理由にSimulation Stepを停止しない。

- Core internal eventは継続する。
- Coreが既にacceptedしたOperationは決定済みapplication ruleに従い処理する。
- 新規external Operationだけが入らない。
- Gateway recovery後にabsence期間へworldをrewindしない。

## 再現性との関係

- World Seed、simulation-affecting Config、same Operation set/order/application Stepが同一ならworld outcomeを一致させる。
- save/restart/replay時も本設計のStep semanticsとdeterministic orderingを維持する。
- simulation-affecting Config changeはexplicit effective Stepと履歴を持つ。
- Master selection結果そのものはoperational randomでよいが、Master identityがworld outcomeを変えてはならない。

## 今後決定が必要な事項

- Simulation Stepのinteger type、epoch、overflow policy
- time multiplier changeの具体effective boundary
- system間のdeterministic dependency / apply order representation
- same-Step stable ordering key
- Gateway/Master candidate application informationのwire representation
- Core final application Step assignmentの具体algorithm
- deadline / defer / rejectのprotocol詳細
- lag detection / load reduction / detail adjustmentのConfig key
- Pause queue capacity・expiry・Resume assignment詳細
