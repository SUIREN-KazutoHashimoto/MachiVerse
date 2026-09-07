# Gateway操作配信・重複排除設計

## 確定方針

第215〜219問およびQ222〜Q225のOperation delivery / retry / failover意味論をPhase 1詳細設計へ反映する。

詳細契約の正本:

- `docs/design/phase1-operation-lifecycle-retry-dedup.md`
- `docs/design/phase1-protocol-envelope.md`
- `docs/design/phase1-persistence-replay-recovery.md`

## Operation identity

- `OperationId` は128-bit opaque value。
- same logical OperationのGateway hop、Master切替、retry、reconnect、ACK lossを通して不変。
- immutable payload digestも不変。
- same OperationIdでdigestが異なる場合は `protocol.operation-payload-mismatch` としてrejectする。
- MessageId / CorrelationId / BatchIdをOperation dedup primary keyにしない。

Operationのimmutable scheduling admission contextもretry/failoverで変更しない。

## End-to-End dedup

Coreがworld mutationの最終dedup authority。

Core lifecycle:

```text
UNSEEN
 -> ACCEPTED_DURABLE
 -> SCHEDULED_DURABLE
 -> TERMINAL_DURABLE
```

- Core `ACCEPTED` はOperation accepted recordのdurability後のみ返す。
- terminal resultはdurable terminal/transition commit後に返す。
- duplicate retryでworld mutationを再実行しない。
- same id / different digestはduplicateではなくprotocol violation。

### terminal tombstone

strict no-double-applyを維持するため、terminal Operationの最小tombstoneをWorldIdのlifecycle中保持する。

```text
OperationDedupTombstoneV1 {
  operation_id,
  operation_payload_digest,
  terminal_status,
  result_code,
  effective_step,
  terminal_history_sequence
}
```

rich result detailsはConfigで有限保持としてよいが、dedup tombstoneはexpiryしない。

## retry

retryではsame OperationId / immutable digest / immutable scheduling admission contextを維持する。

- retry interval / timeout / backoffはOPERATIONAL Config。
- retry timing / countをworld orderingやrandomへ使用しない。
- Core durable acceptance後はretryが遅れたことを理由にeffective Stepを再計算しない。
- terminal result受領後はworld mutation用retryを停止する。

RetryAdviceはP1-04共通契約を使用する。

## Master generation / failover

MasterGenerationはCore authorityの`uint64`。

old generation final batchは `master.stale-generation` としてrejectするが、それだけでcontained Operationをterminal rejectとしない。

new Masterへの再送ではsame Operation identityを維持する。

Core acceptance不明時はsame identityでretryし、Core dedup stateにより:

- UNSEEN: normal acceptance
- ACCEPTED/SCHEDULED: duplicate pending/current state
- TERMINAL: stored terminal result

として解決する。

exactly-once network deliveryではなくeffectively-once world mutationを成立させる。

## custody

Operation delivery responsibilityを次で扱う。

```text
SOURCE_HELD
 -> MASTER_RECEIVED
 -> CORE_ACCEPTED
 -> TERMINAL
```

### SOURCE_HELD

source GatewayはOperationId / digest / scheduling contextを保持し、Master switchやreconnect後もretry可能にする。

### MASTER_RECEIVED

Master hop ACKはCore acceptanceではない。

source GatewayはこのACKだけを理由に唯一の再送可能copyを破棄しない。

### CORE_ACCEPTED

Core durable `ACCEPTED` が確認できた状態。

Coreがauthoritative custodyを持ち、Gatewayは「Coreへ未達かもしれない」という理由のretry loopを停止できる。

### TERMINAL

Core terminal result確認済み。

world mutation用retryを停止し、result routing / client deliveryだけが残り得る。

## Batch identity

Batchはtransport aggregation identityであり、Operation identityの代替ではない。

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

MasterGeneration、routing、MessageId、retry metadataはBatchDigestに含めない。

- exact same logical batchのretry/failoverはsame BatchId / BatchDigestを維持できる。
- same BatchIdでdigestが異なる場合は `protocol.batch-payload-mismatch`。
- Operation追加/削除やsubset retryはnew BatchId。
- contained OperationIdは維持する。

## Batch processing

標準Batchは `PER_OPERATION` processing。

Batchそのものを暗黙all-or-nothing world transactionにしない。

```text
BatchStatus := RECEIVED | PARTIAL | COMPLETE | REJECTED
```

- `RECEIVED`: hop receipt。
- `PARTIAL`: entry lifecycleが混在。
- `COMPLETE`: 全entry terminalまたはknown duplicate terminal。
- `REJECTED`: wrapper不正でentry処理未開始。

一部Operationがterminalになった後に他entryが失敗してもrollbackしない。

transaction semanticsが必要な機能はcomposite Operationまたは明示transaction protocolとして別定義する。

## scheduling情報

Gateway admission時にconfirmed Core basisとCore配布scheduling policy generationを固定する。

```text
OperationSchedulingAdmissionV1 {
  admission_basis_step,
  scheduling_policy_generation,
  requested_not_before_step,
  requested_deadline_step
}
```

このcontextはOperation immutable digestへ含める。

Gateway/Masterが算出する `candidate_step` はadvisoryでありdigestへ含めない。Coreがhistorical policyからcanonical candidateを再計算しfinal effective Stepを決定する。

## Pause / deadline

- Pause中もrequest受信・auth・validation・durable acceptanceは可能。
- Pause中にacceptedしたsimulation-affecting Operationをstopped Stepへ後付けしない。
- Pause中accepted Operationのearliest targetは `paused_step + 1`。
- Pause wall-clock durationだけでSimulationStep deadlineを消費しない。
- deadline/grace/late policyはCore-owned SIMULATION Configとして配布する。

## disconnect / reconnect

non-Master Gateway切断中でもSOURCE_HELD Operationを保持する。

reconnect / new Master確立後、same identityでretry可能にする。

Core acceptanceが不明な場合はOperationId status queryまたはsame identity retryで状態を収束させる。

client/session disconnectだけでCore accepted Operationを自動cancelしない。

## 禁止事項

- retryでOperationIdを再発行すること
- same OperationIdでimmutable payload/scheduling contextを変更すること
- Master hop ACKをCore durable acceptanceと同一視すること
- stale Master batch rejectをcontained Operation terminal rejectと同一視すること
- BatchをOperation dedup keyにすること
- Batchを暗黙transactionとして扱うこと
- terminal tombstoneをWorldId継続中にexpiryしてsame ID再適用を可能にすること
- network arrival order / retry timingをworld orderとして利用すること

## component実装へ残す事項

- physical retry/backoff algorithm
- Gateway durable queueのstorage/data structure
- Master handoff messageのphysical schema
- Gateway identity
- transport compression / serialization
- operational timeout数値

これらは本書のidentity / custody / dedup / scheduling意味論を変更してはならない。
