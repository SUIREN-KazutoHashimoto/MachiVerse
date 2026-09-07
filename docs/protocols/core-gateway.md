# Simulation Core・Gateway間Protocol設計書

## 1. 所有者

本protocolのownerはSimulation Core。

```text
ProtocolId = mv.core-gateway
```

共通契約の正本:

- envelope / version / Capability / result: `docs/design/phase1-protocol-envelope.md`
- persistence / durability / recovery: `docs/design/phase1-persistence-replay-recovery.md`
- Operation scheduling / retry / dedup / custody: `docs/design/phase1-operation-lifecycle-retry-dedup.md`

## 2. 目的

単一Simulation Coreと複数Gatewayの間で次を成立させる。

- confirmed authoritative-derived state同期
- Gateway cache / publication bufferのbasis Step提供
- Master Gateway selection / generation通知
- General View由来final Operation batch受理
- Admin View由来Core Operation受理
- Operation scheduling / result / status query
- retry / dedup / idempotency
- reconnect / resync / recovery continuity
- protocol version / Capability negotiation
- Core health / persistence / Config diagnostic

Physical transport、serialization、compressionはcomponent実装詳細とする。

## 3. 基本原則

- Coreだけがauthoritative World Stateを所有する。
- Gateway cacheは非権威な派生state。
- authoritative timeは整数SimulationStep。
- Coreがfinalizedとして公開するStateはdurable transition frontierを越えない。
- Gateway/Masterのcandidate Stepをauthoritative effective Stepとしてblind acceptしない。
- network arrival timing、Gateway数、Master identity、retry countをworld outcomeの暗黙入力にしない。
- Gatewayはauthn/authz・external-request mediationを担い、Coreはworld-state invariantを担う。

## 4. Common envelope / Version / Capability

normal messageは `ProtocolEnvelopeV1` の共通意味を持つ。

- negotiated ProtocolVersion / NegotiationGenerationを明示する。
- WorldContextV1をworld-related messageで使用する。
- OperationContextV1をOperation / Batch messageで使用する。
- required / provided Capabilityを接続時に相互検証する。
- Capability変更はreconnectを基本とする。
- standard protocol上のaddon情報はcompatibility metadataに限定する。

## 5. State synchronization

Core confirmed publicationは少なくとも次を識別可能にする。

```text
world_id
basis_step
state_continuity_token
base_state_continuity_token | NONE
```

- `basis_step=S` はcomplete finalized `State(S)` をbasisとする。
- transition commit durability前のStateをconfirmed publicationしない。
- delta base token mismatch時はblind applyせずresyncする。
- StateContinuityTokenはCore committed causal historyから導出する。
- process restartでtokenを再採番しない。

Gateway reconnect時はcurrent finalized basisから再同期する。

## 6. Master selection / generation

- Master selection authorityはCore。
- `MasterGeneration := uint64`。
- initial generationは1、reassignmentごとに+1。
- stale generationのfinal batchをcurrent outputとして受理しない。
- stale batch rejectはcontained Operationのterminal rejectを意味しない。
- Master identity自体をworld outcomeへ使用しない。

Recovery直後はpre-crash Master authorityを無条件に再利用せず、connection / generation authorityを再確立する。

## 7. Scheduling policy publication

CoreはGatewayがOperation admission contextを形成できるよう、effective scheduling policyをprotocolで配布する。

```text
OperationSchedulingPolicyV1 {
  owner_config_generation: ConfigGeneration,
  min_lead_steps: uint32,
  default_deadline_window_steps: uint32 | NONE,
  grace_steps: uint32,
  late_policy: REJECT | DEFER_WITHIN_GRACE
}
```

- Config fileそのものをGatewayへ共有しない。
- policyはSIMULATION Config。
- runtime changeはexplicit effective Step / Config historyを持つ。
- Gatewayはconfirmed basis Stepに対応するpolicy generationを使用する。

## 8. Gateway admission context

Core-facing Operationは次のimmutable scheduling contextを持つ。

```text
OperationSchedulingAdmissionV1 {
  admission_basis_step: SimulationStep,
  scheduling_policy_generation: ConfigGeneration,
  requested_not_before_step: SimulationStep | NONE,
  requested_deadline_step: SimulationStep | NONE
}
```

このcontextはOperation immutable digestへ含める。

Coreはhistorical Config historyから、指定policy generationがadmission basisで有効だったことを検証する。

## 9. Candidate Step

Gateway/Masterはadvisory fieldとしてcandidate Stepを送信できる。

```text
CandidateSchedulingV1 {
  candidate_step: SimulationStep
}
```

Coreはcandidateをauthoritative inputとせず再計算する。

```text
canonical_candidate = max(
  admission_basis_step + policy.min_lead_steps,
  requested_not_before_step if present
)
```

candidate mismatchはrequest validation errorとして扱え、correct candidateでsame OperationId / digestをretry可能。

## 10. deadline / grace / final effective Step

Coreはpolicy deadlineとrequested deadlineからeffective deadlineを決定する。

```text
policy_deadline = canonical_candidate + default_deadline_window_steps

effective_deadline = min(
  requested_deadline_step if present,
  policy_deadline if present
)
```

Coreの `next_schedulable_step` はexternal input setがまだfreezeされていない最小Step。

```text
target_step = max(canonical_candidate, next_schedulable_step)
```

- deadline以内: targetをeffective Stepにする。
- deadline超過 + REJECT: `REJECTED / world.deadline-exceeded`。
- deadline超過 + DEFER_WITHIN_GRACE + grace内: targetへdeferし `world.late-deferred`。
- grace超過: terminal reject。
- finalized past Stepへretroactive applyしない。

Core final schedulingは `OperationScheduledRecordV1` としてdurableにし、recovery後に別Stepへsilent reassignmentしない。

## 11. Pause

worldが `State(P)` でPause中の場合:

- Pause前に `effective_step=P` とschedule済みのOperationはtransition Pに残す。
- Pause中はapplyしない。
- Resume後の最初のtransition Pで処理する。

Pause中に新規Core acceptanceしたsimulation-affecting Operationは:

```text
pause_floor_step = P + 1

target_step = max(canonical_candidate, P + 1)
```

とする。

- stopped Step Pへ後付けしない。
- Pause durationだけでSimulationStep deadlineを消費しない。
- Pause中arrival orderをsame-Step orderにしない。
- durable accepted Operationをwall-clock expiryで破棄しない。

## 12. final Operation batch

Master→Core final batchは少なくとも次を追跡可能にする。

- WorldId
- current MasterGeneration
- BatchId / BatchDigest
- 各OperationId / immutable digest
- OperationSchedulingAdmissionV1
- advisory candidate Step
- source/result routing context
- Operation type / target / content

共通規則:

- submit時 `WorldContext.effective_step = NONE`。
- candidate Stepをeffective_stepへ格納しない。
- same OperationId + different digestは `protocol.operation-payload-mismatch`。
- same BatchId + different BatchDigestは `protocol.batch-payload-mismatch`。

## 13. Batch semantics

標準Batchは `PER_OPERATION` processing。

```text
BatchStatus := RECEIVED | PARTIAL | COMPLETE | REJECTED
```

- Batchは暗黙all-or-nothing transactionではない。
- entryごとにaccepted / scheduled / terminal / duplicateが混在できる。
- partial completionで既にterminalのOperationをrollbackしない。
- subset retryはnew BatchId、contained OperationIdは維持する。

all-or-nothing意味論が必要な機能はcomposite Operationまたはexplicit transaction protocolを定義する。

## 14. Core Operation lifecycle

```text
UNSEEN
 -> ACCEPTED_DURABLE
 -> SCHEDULED_DURABLE
 -> TERMINAL_DURABLE
```

world mutationなしでterminal rejectする場合は `UNSEEN -> TERMINAL_DURABLE` を許可する。

### 14.1 authoritative ACCEPTED

Coreは `OperationAcceptedRecordV1` durability後にのみ `ACCEPTED` を返す。

ACK直後のcrashでもOperationId / immutable payload / scheduling contextをrecoveryできる。

### 14.2 scheduling

final effective Step確定後は `OperationScheduledRecordV1` をdurableにする。

### 14.3 terminal

- applied terminal resultはTransitionCommitRecord durability後に返す。
- non-applied terminal rejectはOperationTerminalRecord durability後に返す。
- terminal後はsame OperationIdでmutationを再実行しない。

## 15. dedup

Core dedup primary keyはOperationId。

### same id / same digest / pending

- duplicateとしてcurrent lifecycle stateを返す。
- new mutation requestとして追加しない。

### same id / same digest / terminal

- tombstone / retained resultからterminal semanticsを返す。
- mutationを再実行しない。

### same id / different digest

- `REJECTED / protocol.operation-payload-mismatch`。
- existing Operation stateを変更しない。

## 16. dedup retention

terminal Operationの最小tombstoneはWorldId lifecycle中保持する。

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

rich result detailsは有限保持としてよいが、double-apply防止に必要なtombstoneはexpiryしない。

history compaction時もRecoveryState / compact indexへtombstone semanticsを移す。

## 17. Operation status query

reconnect / ACK loss / failover後、GatewayはOperationIdでauthoritative stateを確認できる。

```text
OperationStatusV1 {
  operation_id,
  state: UNKNOWN | ACCEPTED | SCHEDULED | TERMINAL,
  operation_payload_digest,
  effective_step,
  result_status,
  result_code,
  rich_result_details_available
}
```

- UNKNOWNはCore persistenceにidentity factがないことを意味する。
- status queryはworld mutationではない。
- query timingをworld outcomeへ使用しない。

## 18. result / retry

P1-04 ResultStatus / ResultCode / RetryAdviceを使用する。

P1-06追加common code:

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

retryではsame OperationId / immutable digest / scheduling admission contextを維持する。

network timeout / retry interval / backoffはOPERATIONAL Configであり、effective Stepへ直接使用しない。

## 19. Master failover / custody

Gateway delivery custodyは:

```text
SOURCE_HELD
 -> MASTER_RECEIVED
 -> CORE_ACCEPTED
 -> TERMINAL
```

- Master receipt ACKだけではCore custody成立ではない。
- Core acceptance不明時はsame identityでnew Master経由retryできる。
- CoreでUNSEENならaccept、pendingならduplicate current state、terminalならterminal resultを返す。
- Core accepted済みOperationをMaster切替理由で失わない。

## 20. reconnect / resync

- reconnect時にProtocol handshakeを再実行する。
- Gateway old cacheをauthoritativeとして扱わない。
- basis Step / StateContinuityTokenを確認してresyncする。
- continuity mismatch時はfull rebuildへ移る。
- Operation custody不明時はstatus queryまたはsame identity retryで収束させる。

## 21. client disconnect

client/session disconnectだけでCore accepted Operationを自動cancelしない。

- accepted Operationはscheduled / terminalまで継続する。
- resultはreconnect後query可能にできる。
- cancelが必要ならexplicit cancellation Operationをdomain contractとして定義する。

## 22. Admin Operation

Admin View由来Core Operationも本protocolを使用する。

- Gateway: Admin authn/authz、format、target、allowed condition。
- Core: common world-state invariant / transition consistency。
- simulation-affecting Admin Operationも同じOperation scheduling / durability / retry / dedup意味論へ従う。

## 23. diagnostics

公開可能なdiagnostic例:

- current / last durable finalized SimulationStep
- current MasterGeneration
- scheduling policy ConfigGeneration
- pending accepted / scheduled Operation件数
- duplicate / payload mismatch件数
- persistence/recovery state
- resync state
- protocol / Capability mismatch

## 24. 禁止事項

- non-Master final batchをnormal writeとして受理すること
- stale MasterGenerationをcurrent authorityとして扱うこと
- candidate Stepをauthoritative effective_stepにすること
- retryでOperationIdを再発行すること
- same OperationIdでimmutable payload/scheduling contextを変更すること
- terminal Operationのdouble apply
- tombstone expiryによりsame ID再適用を可能にすること
- Batchを暗黙transactionにすること
- hop ACKをCore terminal successと同一視すること
- durable acceptance前のauthoritative ACCEPTED
- transition commit前のconfirmed State / applied terminal success publication

## 25. component実装へ残す事項

- physical transport / connection direction
- serialization / compression
- Gateway identity
- Master election/heartbeat physical messages
- state full/delta payload schema
- exact operational timeout/backoff values
- Core dedup index physical data structure
- status query endpoint transport

これらは本書のauthoritative scheduling / durability / dedup / custody semanticsを変更してはならない。
