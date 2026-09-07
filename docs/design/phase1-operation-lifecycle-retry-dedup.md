# 詳細設計 Phase 1: Operation lifecycle・Pause・late・retry・dedup 契約

Status: Draft / P1-06 complete  
Tracking: Issue #13  
Parent: `docs/design/phase1-common-foundation-contracts.md`

## 1. 目的

本書は Phase 1 の P1-06 として、world-affecting Operation が Gateway へ入ってから Core で terminal result になるまでの共通意味論を具体化する。

対象は次の通り。

- candidate / authoritative effective Step の分離
- deadline / grace / late defer / reject
- Pause 中 Operation の受理・保留・Resume 後 assignment
- stable OperationId を維持した retry
- durable acceptance と duplicate handling
- dedup retention / terminal tombstone
- Batch identity / partial completion / retry
- MasterGeneration 切替時の custody
- ACK loss / reconnect / status recovery

本書は `docs/architecture/deterministic-update-execution.md`、`docs/architecture/gateway-operation-delivery.md`、`docs/design/phase1-protocol-envelope.md`、`docs/design/phase1-persistence-replay-recovery.md` を横断して具体化する。

## 2. 基本原則

1. network arrival timestamp を authoritative application Step に変換しない。
2. Gateway / Master の candidate Step は advisory であり、Core が authoritative effective Step を確定する。
3. same logical Operation の retry / reconnect / failover で `OperationId` と immutable payload digest を変更しない。
4. Core が durable `ACCEPTED` を返した Operation は crash / Master failover / Gateway reconnect で失わない。
5. Core が一度 terminal にした Operation を同一 WorldId 内で再度 world mutation へ作用させない。
6. Pause の wall-clock duration を deadline 消費や replay 条件にしない。
7. durable accepted Operation を queue pressure / timeout / process restart を理由に silent eviction しない。
8. Batch は標準では transport aggregation であり、暗黙の atomic world transaction としない。
9. MasterGeneration の変更は transport authority の変更であり、Operation identity を変更しない。
10. retry count / retry interval / ACK timing / network route を same-Step order、random context、EntityId derivation に使用しない。

## 3. Operation lifecycle

Core authoritative lifecycle を次で定義する。

```text
UNSEEN
  -> ACCEPTED_DURABLE
  -> SCHEDULED_DURABLE
  -> TERMINAL_DURABLE
```

world mutation を伴わない reject は次も許可する。

```text
UNSEEN
  -> TERMINAL_DURABLE
```

`RECEIVED_UNDURABLE` 等の process-local 状態を実装してよいが、protocol 上の durable acceptance として公開しない。

### 3.1 `UNSEEN`

Core world persistence に対象 `OperationId` の accepted / terminal fact が存在しない状態。

- sender は request を送信または retry できる。
- Core crash 前に request bytes を受信していても durable record がなければ `UNSEEN` として recovery できる。

### 3.2 `ACCEPTED_DURABLE`

P1-05 の `OperationAcceptedRecordV1` が durable になった状態。

- Core はこの状態以降 Operation を失ってはならない。
- sender へ `ACCEPTED` を返せる最初の authoritative custody boundary。
- retry で同じ OperationId / digest が届いても新しい Operation として追加しない。

### 3.3 `SCHEDULED_DURABLE`

P1-05 の `OperationScheduledRecordV1` により authoritative effective Step が durable に確定した状態。

- crash recovery 後に別 effective Step へ黙って再割当てしない。
- same-Step order key も recovery 可能にする。

### 3.4 `TERMINAL_DURABLE`

P1-05 の `TransitionCommitRecordV1.operation_outcomes` または `OperationTerminalRecordV1` が durable になった状態。

- terminal success / reject / no-change 等を再構成できる。
- duplicate retry で world mutation を再実行しない。
- terminal result の rich details が compaction されても dedup tombstone は保持する。

## 4. Core scheduling policy

Operation scheduling に使う cross-component policy は Core が所有し、必要な effective information を Gateway へ protocol で配布する。

Config file 自体は共有しない。

```text
OperationSchedulingPolicyV1 {
  owner_config_generation: ConfigGeneration,
  min_lead_steps: uint32,
  default_deadline_window_steps: uint32 | NONE,
  grace_steps: uint32,
  late_policy: LatePolicy
}
```

```text
LatePolicy :=
  REJECT
  | DEFER_WITHIN_GRACE
```

### 4.1 Config classification

上記 policy は Operation の effective Step / terminal result を変え得るため `SIMULATION` Config とする。

runtime change を許可する場合は `RUNTIME_SAFE` とし、P1-03 の explicit effective Step / Config history 契約に従う。

### 4.2 数値値

Phase 1 共通契約は deployment 固有の数値 default を固定しない。

各 Core Config schema は `min_lead_steps`、deadline window、grace の型・範囲・default を明示し hidden default を持たない。

## 5. scheduling admission context

Gateway が world-affecting Operation を logical Operation として受理する際、次の immutable scheduling admission context を確定する。

```text
OperationSchedulingAdmissionV1 {
  admission_basis_step: SimulationStep,
  scheduling_policy_generation: ConfigGeneration,
  requested_not_before_step: SimulationStep | NONE,
  requested_deadline_step: SimulationStep | NONE
}
```

### 5.1 `admission_basis_step`

Gateway が request を Operation として受理した時点で、その Gateway が confirmed authoritative basis として使用した Core `State(B)` の `B` を記録する。

- wall clock を代替にしない。
- Gateway cache の未確認予測 Step を使用しない。
- resync 中で confirmed basis を持てない Gateway は新規 world-affecting Operation を authoritative admission しない。

### 5.2 `scheduling_policy_generation`

`admission_basis_step` に対して Gateway が使用した Core scheduling policy の owner ConfigGeneration。

- Core は historical Config history から当該 generation を検証可能にする。
- current generation と異なるだけで reject しない。
- 当該 generation が `admission_basis_step` で有効でなかった場合は invalid scheduling context とする。

### 5.3 requested constraints

`requested_not_before_step` / `requested_deadline_step` は origin request が意味上指定した場合だけ設定する。

- `requested_deadline_step < requested_not_before_step` は invalid。
- requested deadline を後段 Gateway / Master が延長しない。
- requested not-before を後段が早めない。

### 5.4 immutable digest boundary

`OperationSchedulingAdmissionV1` は一度 Operation が Gateway admission を完了した後は immutable とする。

- `admission_basis_step`
- `scheduling_policy_generation`
- origin requested not-before / deadline

は protocol schema が immutable と宣言する Operation meaning context として `mv.operation-payload.v1` digest へ含める。

一方、後段で再計算可能な `candidate_step` は digest へ含めない。

これにより retry / Master failover で candidate transport field が再構成されても、元の scheduling basis は変化しない。

## 6. canonical candidate Step

Core は Gateway / Master が送信した candidate 値を authoritative truth とせず、admission context と historical scheduling policy から canonical candidate を再計算する。

```text
policy = policy_at(
  admission_basis_step,
  scheduling_policy_generation
)

base_candidate = admission_basis_step + policy.min_lead_steps

canonical_candidate = max(
  base_candidate,
  requested_not_before_step if present
)
```

integer overflow は validation error とし wrap しない。

Gateway / Master は routing / batching / early validation のため `candidate_step` を送信できる。

```text
CandidateSchedulingV1 {
  candidate_step: SimulationStep
}
```

- Core は canonical candidate と一致するか検証できる。
- mismatch は authoritative scheduling 入力として採用せず `request.invalid` として返せる。
- candidate field を修正して retry する場合も immutable OperationId / payload digest は維持する。

## 7. canonical deadline / grace

### 7.1 policy deadline

`default_deadline_window_steps` が存在する場合:

```text
policy_deadline = canonical_candidate + default_deadline_window_steps
```

### 7.2 effective deadline

origin requested deadline と policy deadline の両方が存在する場合、厳しい方を採用する。

```text
effective_deadline = min(requested_deadline_step, policy_deadline)
```

片方だけ存在する場合はその値、両方 NONE の場合は deadline NONE とする。

Gateway / Master が retry / failover 時に deadline を延長しない。

### 7.3 grace

`grace_steps` は effective deadline を過ぎた後に `DEFER_WITHIN_GRACE` を許す最大 Step 幅。

```text
grace_limit = effective_deadline + grace_steps
```

- deadline NONE の場合 grace は適用しない。
- overflow は validation error。
- grace を wall-clock duration として解釈しない。

## 8. Core scheduling barrier

Core は各 transition の external input set を deterministic control boundary で freeze する。

`next_schedulable_step` を次で定義する。

- transition `S` の external input set がまだ open なら `S`。
- transition `S` の external input set を freeze 済みなら `S+1` 以降の最小 open Step。

physical receive interrupt / thread race そのものを ordering key にしない。

ある Operation が barrier 前に authoritative admission されたか後かは protocol上の accepted set の違いであり、同じ accepted set 内の順序は P1-02 の canonical order で決める。

## 9. final effective Step algorithm

通常 running 時の target を次で計算する。

```text
target_step = max(
  canonical_candidate,
  next_schedulable_step
)
```

Pause 中 admission については 10 節の `pause_floor_step` をさらに適用する。

### 9.1 on-time

`effective_deadline == NONE` または `target_step <= effective_deadline` なら:

- `effective_step = target_step`
- `OperationScheduledRecordV1` を durable にする。
- result code は `operation.scheduled` とできる。

### 9.2 late / reject

`target_step > effective_deadline` かつ `late_policy = REJECT` なら:

- world mutation を行わない。
- terminal `REJECTED / world.deadline-exceeded` とする。
- terminal record を durable にしてから final result を返す。

### 9.3 late / defer within grace

`late_policy = DEFER_WITHIN_GRACE` かつ:

```text
target_step <= effective_deadline + grace_steps
```

なら:

- `effective_step = target_step`
- scheduling result code を `world.late-deferred` とする。
- original deadline を書き換えず、late だった事実を diagnostic / history で追跡可能にする。

### 9.4 grace exceeded

`target_step > grace_limit` なら terminal `REJECTED / world.deadline-exceeded`。

### 9.5 past rewrite 禁止

いかなる場合も:

```text
effective_step < next_schedulable_step
```

となる assignment を行わない。

finalized past state を retroactive rewrite しない。

## 10. Pause semantics

### 10.1 Pause boundary

world が finalized `State(P)` で Pause 中の場合、authoritative SimulationStep は `P` で停止する。

Pause が有効になる前に既に `effective_step = P` と durable scheduling 済みの Operation は transition P に属したまま保持する。

- Pause 中に apply しない。
- Resume 後の最初の transition `State(P) -> State(P+1)` で通常規則に従い処理する。

### 10.2 Pause 中に新規 admission した Operation

Pause active 中に Core が durable acceptance した simulation-affecting Operation は停止中 transition P の frozen input setへ追加しない。

```text
pause_floor_step = P + 1
```

その Operation の target は:

```text
target_step = max(
  canonical_candidate,
  P + 1
)
```

とする。

これにより Pause 中受信Operationを「停止しているStep Pへ後付け」しない。

### 10.3 Resume

Resume によりまず transition P が再開可能になる。

- Pause前に P へ scheduled 済みの Operation は transition P に参加できる。
- Pause中 admission Operation は最速でも transition `P+1`。
- Pause queue の arrival order を same-Step order として使用しない。
- P+1 へ複数 Operation が集まる場合、P1-02 の canonical same-Step order を使用する。

### 10.4 Pause と deadline

Pause 中は SimulationStep が進まないため、wall-clock Pause duration だけで deadline を消費しない。

ただし Pause 中 admission の `pause_floor_step = P+1` により target が deadline を超える場合、9節の late / grace rule を適用する。

### 10.5 Pause queue expiry

Core durable accepted Operation に wall-clock based expiry を設けない。

- session timeout
- client disconnect
- Gateway reconnect delay
- Pause duration

だけを理由に accepted Operation を破棄しない。

Operation を取り消す必要がある domain は、取消可能条件を持つ **新しい logical Operation** として定義する。

### 10.6 capacity / backpressure

Core は durable accepted Operation を queue capacity 超過で evict しない。

resource pressure 時は durable acceptance **前** に `component.unavailable` 等で backpressure / temporary reject できる。

一度 `ACCEPTED_DURABLE` へ入った Operation を resource pressure で silent drop しない。

## 11. retry semantics

### 11.1 identity

same logical Operation の retry は常に:

- same OperationId
- same immutable payload digest
- same immutable scheduling admission context

を維持する。

retry envelope の MessageId / CorrelationId / routing metadata は protocol rule に従って変化できる。

### 11.2 RetryAdvice

P1-04 の `RetryAdviceV1` を使用する。

- `DO_NOT_RETRY`: terminal / semantic reject。
- `RETRY_SAME_IDENTITY`: same OperationId / digest で再送。
- `RECONNECT_THEN_RETRY`: connection再確立後 same identity で再送。
- `RESYNC_THEN_RETRY`: authoritative basisを再同期後、same immutable Operationで再送可能な場合のみ再送。
- `RENEGOTIATE_THEN_RETRY`: capability semanticsを確立後再送。

immutable scheduling admission context 自体を変更する必要がある場合、それは同一 Operation の retry ではなく new OperationId を持つ新しい logical request とする。

### 11.3 retry timing

retry delay / timeout / exponential backoff / operational jitter は `OPERATIONAL` Config とする。

- world random を使用しない。
- retry timing を effective Step / same-Step orderへ直接使用しない。
- durable Core acceptance 後は retryが遅れたことを理由に effective Step を再計算しない。

### 11.4 terminal後

terminal resultを受領した sender は通常 retryを停止する。

同じ OperationId を再度送っても duplicate query として扱い mutationを再実行しない。

## 12. Core dedup state

### 12.1 primary key

Core dedup primary key は `OperationId` とする。

world persistence は WorldId ごとに分離できるが、origin は別 WorldId であっても OperationId を再利用しないことを標準契約とする。

### 12.2 duplicate cases

#### same id / same digest / pending

- new mutation requestとして追加しない。
- `DUPLICATE` として current lifecycle stateを返せる。
- scheduling済みなら同じ effective Step を返せる。

#### same id / same digest / terminal

- mutationを再実行しない。
- retained tombstoneから同じ terminal status / result code / effective Step を返す。

#### same id / different digest

- `REJECTED / protocol.operation-payload-mismatch`。
- existing Operation の state を変更しない。
- security / audit diagnostic 対象とする。

## 13. dedup retention

### 13.1 pending state

`ACCEPTED_DURABLE` から terminal までの Operation identity / payload / scheduling state は削除しない。

### 13.2 terminal tombstone

strict end-to-end no-double-apply を維持するため、terminal Operation について world lifetime の最小 tombstone を保持する。

```text
OperationDedupTombstoneV1 {
  operation_id: OperationId,
  operation_payload_digest: Hash256,
  terminal_status: ResultStatus,
  result_code: ResultCode,
  effective_step: SimulationStep | NONE,
  terminal_history_sequence: HistorySequence
}
```

P1-06 で domain separation label を追加する。

```text
mv.dedup-tombstone.v1
```

- tombstone は WorldId の lifecycle 中に expiry しない。
- history compaction で raw terminal record を削除する場合も tombstone semantics を RecoveryState / compact indexへ移す。
- tombstone の存在を Bloom filter 等の false-positive 構造だけに依存しない。

### 13.3 rich result detail retention

large diagnostic details / domain-specific response payload は Config により有限保持としてよい。

これは `OPERATIONAL` Config とする。

期限後 duplicate request へは:

- `DUPLICATE`
- original terminal status
- stable result code
- effective Step（存在する場合）
- `operation.result-details-expired`

を返せる。

world mutation prevention に必要な tombstone は削除しない。

### 13.4 history floor

P1-05 history compaction floor は、全 terminal Operation の tombstone が recovery可能な別 compact stateへ移された後にのみ進められる。

「古いOperationだから忘れる」ことで同一IDの再適用可能性を作らない。

## 14. Batch identity

Batch は transport / aggregation identity とし、Operation identity の代替にしない。

### 14.1 `BatchDigest`

P1-06 で次の label を追加する。

```text
mv.batch.v1
```

```text
BatchDigest = DomainHash(
  "mv.batch.v1",
  {
    batch_kind,
    ordered_entries: [
      { operation_id, operation_payload_digest }, ...
    ]
  }
)
```

- MasterGeneration
- MessageId / CorrelationId
- retry count / timing
- hop routing

は BatchDigest へ含めない。

### 14.2 same BatchId

same logical batch の retry / reconnect / MasterGeneration切替では、内容が同一なら same BatchId / BatchDigest を維持する。

same BatchId で BatchDigest が異なる場合:

- `REJECTED / protocol.batch-payload-mismatch`
- contained Operation を新規batch内容として黙って処理しない。

### 14.3 batch 内容変更

次は new BatchId を必要とする。

- Operation追加 / 削除
- Operation entry順のsemantic変更
- immutable Operation digest変更
- batch_kind変更

partial retry のため subset を作る場合も new BatchId とし、各 OperationId は元のまま維持する。

## 15. Batch processing semantics

### 15.1 default mode

Phase 1 標準 Batch は **PER_OPERATION** processing とする。

```text
BatchProcessingMode := PER_OPERATION
```

Batch受理そのものを全Operationのatomic world transactionとしない。

all-or-nothing world semanticsが必要な機能は:

- 1つの composite Operationとしてdomain contract化する、または
- 将来 explicit transaction Capability / protocolを定義する。

暗黙にBatchへtransaction性を付与しない。

### 15.2 batch result

```text
BatchResultV1 {
  batch_id,
  batch_digest,
  status: BatchStatus,
  entries: [
    {
      operation_id,
      operation_status,
      result_code,
      effective_step: SimulationStep | NONE
    }, ...
  ]
}
```

```text
BatchStatus :=
  RECEIVED
  | PARTIAL
  | COMPLETE
  | REJECTED
```

- `RECEIVED`: hop-level receipt。
- `PARTIAL`: entryごとの lifecycleが混在。
- `COMPLETE`: 全entryが terminal または既知duplicate terminal。
- `REJECTED`: batch wrapper自体が無効でentry処理を開始していない。

### 15.3 partial completion

一部 Operation が terminal / duplicate、他が pending でもよい。

Batch `PARTIAL` は既にterminalになった Operationをrollbackしない。

retry は per-Operation dedupにより安全に行う。

## 16. Batch retention

Batch dedup / ACK state は transport continuity 用であり、Operation tombstoneのような world lifetime 永続保持を必須としない。

- retention期間は `OPERATIONAL` Configで管理可能。
- Batch recordがexpireしても、contained OperationIdのCore dedup安全性を失わない。
- expiry後に同じ BatchId を受けてBatch historyが不明な場合でも、各 OperationIdを必ずdedup確認する。
- same BatchId再利用を新しいlogical batch作成方法として認めない。

## 17. custody model

Gateway / Master failover時のloss防止のため、Operation custodyを次の意味で扱う。

```text
SOURCE_HELD
  -> MASTER_RECEIVED
  -> CORE_ACCEPTED
  -> TERMINAL
```

これは world state machine ではなく delivery responsibility state。

### 17.1 `SOURCE_HELD`

originating / connected Gateway が downstream durable acceptance を確認していない状態。

- stable OperationId / digest / scheduling admission contextを保持する。
- disconnect / Master switch後も再送可能にする。

### 17.2 `MASTER_RECEIVED`

Master が local batch を receipt ACKした状態。

- Master hop ACKはCore acceptanceではない。
- source GatewayはこのACKだけを理由に唯一の再送可能copyを破棄しない。
- Master failure時、sourceはnew Masterへsame identityで再送できる。

### 17.3 `CORE_ACCEPTED`

Core durable `ACCEPTED` が確認できた状態。

- Coreがauthoritative custodyを持つ。
- source / Masterは「Coreへ未達かもしれない」という理由の再送loopを停止できる。
- terminal result不明ならOperationIdでstatus/resultを再確認できる。

### 17.4 `TERMINAL`

Core terminal resultが確認できた状態。

- result routing / client delivery責務のみ残り得る。
- world mutation用再送は停止する。

## 18. MasterGeneration failover

### 18.1 stale generation batch

Core が old MasterGeneration の final batch を受けた場合:

- batch wrapperを `REJECTED / master.stale-generation` とする。
- old generationだからという理由だけで contained OperationId を terminal reject としない。
- 既にCore accepted済みOperationのstateを変更しない。

### 18.2 new Masterへの再送

source Gateway / new Masterは same logical Operation を:

- same OperationId
- same immutable digest
- same scheduling admission context

で再送する。

exact same logical Batchを再送する場合は same BatchIdを維持できる。

new Masterがmerge結果を変更した場合は new BatchIdを発行するが、contained OperationIdは維持する。

### 18.3 ACK unknown

old Master障害時にCore acceptanceが不明なOperationは new pathからsame identityでretryする。

Core側で:

- UNSEENならnormal acceptanceへ進む。
- ACCEPTED/SCHEDULEDならduplicateとしてcurrent stateを返す。
- TERMINALならterminal tombstone/resultを返す。

これによりexactly-once network deliveryを要求せず、effectively-once world mutationを成立させる。

## 19. status recovery

reconnect / failover / ACK loss後、senderは OperationId によるstatus queryを利用可能にする。

論理 response:

```text
OperationStatusV1 {
  operation_id,
  state: UNKNOWN | ACCEPTED | SCHEDULED | TERMINAL,
  operation_payload_digest: Hash256 | NONE,
  effective_step: SimulationStep | NONE,
  result_status: ResultStatus | NONE,
  result_code: ResultCode | NONE,
  rich_result_details_available: bool
}
```

- `UNKNOWN` は「このCore world persistenceにidentity factがない」を意味する。
- UNKNOWNをterminal rejectと解釈しない。
- query自体はworld mutationではない。
- status queryのMessageId / timingをworld outcomeへ使用しない。

## 20. timeout / client disconnect

### 20.1 pre-Core acceptance

Gateway / client側request timeoutが発生し、Core durable acceptanceが未確認の場合、senderはprotocol RetryAdvice / custody stateに従いsame identityでretryできる。

### 20.2 post-Core acceptance

Core durable acceptance後にclient/sessionがdisconnectしてもOperationを自動cancelしない。

- Operationはscheduled/terminalまで継続する。
- resultはsession recovery / later query / audit経路で取得可能にできる。
- cancelがdomain上必要ならexplicit cancellation Operationを定義する。

### 20.3 timeout values

network timeout / retry intervalはwall-clock operational Configでよい。

Operation deadline / graceはSimulationStep基準であり混同しない。

## 21. result / error code additions

P1-04 common namespaceへ次の stable code を追加する。

```text
operation.accepted
operation.scheduled
operation.result-details-expired

protocol.batch-payload-mismatch

world.deadline-exceeded
world.late-deferred
world.pause-deferred

batch.partial
batch.complete
```

既存 `world.late-operation` はgeneric late categoryとして残せるが、deadline terminal判定は `world.deadline-exceeded` を推奨する。

## 22. persistence integration

P1-05 recordを次のように使用する。

- Gateway admission context / normalized immutable Operationは `OperationAcceptedRecordV1` に保存する。
- Core final effective Step / same-Step orderは `OperationScheduledRecordV1` に保存する。
- applied terminal resultは `TransitionCommitRecordV1.operation_outcomes` に保存する。
- non-applied terminal rejectは `OperationTerminalRecordV1` に保存する。
- compaction時は `OperationDedupTombstoneV1` を RecoveryStateへ保持する。

recovery後に accepted pending Operation を再度Gatewayから受け取らなくても継続可能でなければならない。

## 23. deterministic replay

replayは original retry / failover / ACK timing を再生しない。

authoritative historyに保存された:

- immutable Operation
- scheduling admission context
- final effective Step
- canonical same-Step order
- terminal outcome

を用いる。

MasterGeneration / retry routingの変化はdiagnostic historyとして保持できるが、world replay causal inputとはしない。

## 24. forbidden

- retryでnew OperationIdを発行すること
- same OperationIdでpayload / immutable scheduling contextを変更すること
- MessageId / BatchIdをOperation dedup primary keyにすること
- candidate StepをCore authoritative effective Stepとして扱うこと
- Coreがhistorical scheduling policyを検証せずGateway candidateをblind acceptすること
- wall-clock arrival時刻をdeadline / same-Step orderへ変換すること
- Pause durationでSimulationStep deadlineを消費すること
- Pause中Operationをstopped Step Pへ後付けすること
- durable accepted Operationのqueue expiry / silent eviction
- terminal Operation tombstoneをWorldId継続中に破棄してsame IDを再適用可能にすること
- Batchを暗黙all-or-nothing transactionとして扱うこと
- Master hop ACKをCore durable acceptanceと同一視すること
- stale Master batch rejectをcontained Operationのterminal rejectと同一視すること
- retry timing / countをworld random / orderingへ使用すること

## 25. Phase 1 で残す実装詳細

P1-06完了後も次はcomponent実装詳細として残せる。

- physical transport retry algorithm
- exact wall-clock timeout / backoff 数値
- Gateway durable queue の storage product / data structure
- Core dedup index の physical storage / shard strategy
- Batch transfer messageのphysical serialization
- status query の endpoint / request transport
- operational metrics / alert thresholds

これらは本書のidentity / durability / ordering / scheduling semanticsを変更してはならない。
