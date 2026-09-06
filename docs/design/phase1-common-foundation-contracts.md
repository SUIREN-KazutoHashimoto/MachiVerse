# 詳細設計 Phase 1: 共通基盤・契約

Status: Complete / Phase 1 complete  
Tracking: Issue #13  
Source of truth: `docs/requirements` / `docs/architecture` / `docs/protocols`

## 1. 目的

本書はMachiVerse全体の詳細設計に先立ち、全コンポーネントと全シミュレーション領域が共有する横断契約を整理する。

Phase 1対象:

1. Simulation Step / World Time
2. Entity ID / Operation ID / Batch ID / MasterGeneration
3. deterministic ordering / conflict / random context
4. Config schema / classification / apply / history
5. Protocol common envelope / version / Capability / result-error
6. Snapshot / replay / recovery consistency
7. Pause / late / retry / dedup / failover semantics
8. cross-cutting consistency review

P1-01〜P1-07はすべて完了した。

## 2. 正本文書

詳細契約は次を正本とする。

- P1-02 deterministic contract: `docs/design/phase1-determinism-ordering-random.md`
- P1-03 Config contract: `docs/design/phase1-config-contract.md`
- P1-04 Protocol envelope: `docs/design/phase1-protocol-envelope.md`
- P1-05 persistence/replay/recovery: `docs/design/phase1-persistence-replay-recovery.md`
- P1-06 Operation lifecycle/retry/dedup: `docs/design/phase1-operation-lifecycle-retry-dedup.md`
- P1-07 final cross-cutting review: `docs/design/phase1-cross-cutting-review.md`

各 `docs/protocols` 文書は上記共通意味論を境界固有payloadへ適用する。

古いsubphase文書末尾に残る「後続で決める」「P1-06へ引き渡す」等の記述は、そのsubphase完了時点の履歴である。現在の未決定一覧はP1-07文書を正本とする。

## 3. 共通設計原則

- authoritative World Timeはwall clockではなくSimulationStep。
- network arrival race、thread completion order、retry timing、Master identityをworld outcomeの暗黙入力にしない。
- world-affecting logical identityとtransport identityを分離する。
- retry / failover / reconnectでsame logical Operationのidentityを変更しない。
- save / replay / recoveryを跨いでWorldId、WorldSeed、EntityId、Operation identity、Config historyを維持する。
- component間でshared DTO libraryを契約正本にしない。
- protocol / Config / persistence不整合をsilent degradationしない。
- finalized state / terminal resultはdurability frontierを越えて公開しない。

## 4. Simulation Step / World Time

```text
SimulationStep := uint64
```

- initial authoritative world stateは `State(0)`。
- `effective_step = S` のinputは `State(S) -> State(S+1)` transitionに参加する。
- transition完了ごとにStepを1増加する。
- Pause中はStepを進めない。
- overrun時もStep skipしない。
- wrap-around禁止。

Simulation rateは有理数で保持する。

```text
StepRate {
  numerator: uint32,
  denominator: uint32
}
```

標準30Hzは `30/1`。

```text
WorldTime {
  step: SimulationStep,
  rate_generation: uint32
}
```

wall clockは運用・表示補助に限定し、authoritative scheduling / ordering / random / ID generationへ使用しない。

## 5. 共通identity

```text
WorldId     := 128-bit opaque value
EntityId    := 128-bit opaque value
OperationId := 128-bit opaque value
BatchId     := 128-bit opaque value
```

- binaryは16 octets。
- human-readable canonical formは32桁lowercase hex。
- ZEROはinvalid / unassigned。

```text
MasterGeneration := uint64
```

- initial 1。
- Coreがauthority。
- Master reassignmentごとに+1。
- stale generation outputはreject。

EntityIdはdeterministic creation contextからdomain-separated SHA-256を用いて128 bit導出する。

OperationIdはGateway hop、retry、failover、reconnectを通して不変。

## 6. deterministic encoding / hash / random

意味digest / ID derivation / deterministic randomには `MV-DCBOR-v1` を使用する。

```text
Hash256(data) = SHA-256(data)
DomainHash(label, value) =
  SHA-256(ASCII(label) || 0x00 || MV-DCBOR-v1(value))
```

World randomはshared stateful PRNG consumption orderへ依存させず、WorldSeed + SimulationStep + logical contextからstatelessに導出する。

## 7. same-Step ordering

canonical total order:

```text
SameStepOrderKey = (
  phase,
  domain_rank,
  conflict_scope_digest,
  semantic_priority,
  intent_id
)
```

- domain rankはdependency DAGのdeterministic topological sortで決定。
- thread completion / network arrival / Master identity / retry countをkeyへ含めない。
- parallel calculation結果はmutation intentとしてdeterministic mergeする。

## 8. Config

標準operator-editable Configはcomponent-owned UTF-8 TOML 1.0。

```text
ConfigSchemaVersion { major:uint16, minor:uint16 }
ConfigGeneration := uint64
ConfigDigest := Hash256
```

field classification:

```text
ConfigImpact := SIMULATION | OPERATIONAL | PRESENTATION

ConfigMutability :=
  RUNTIME_SAFE
  | RESTART_REQUIRED
  | WORLD_REGENERATION_REQUIRED
```

- old compatible Configはdeterministic migration / default completionを行う。
- migration/default補完後はatomic write-back。
- invalid startup Configは起動拒否。
- runtime changeはatomic change set。
- simulation-affecting runtime changeはexplicit effective Stepを持つ。
- saved worldのsimulation Config/historyをrestore continuationの正本とする。

## 9. Protocol common envelope

標準ProtocolId:

```text
mv.core-gateway
mv.gateway-gateway
mv.gateway-view
mv.gateway-admin-view
```

normal messageは `ProtocolEnvelopeV1` の共通意味を持つ。

protocol version:

```text
ProtocolVersion { major:uint16, minor:uint16 }
NegotiationGeneration := uint32
```

- compatible highest common versionをdeterministicに選択する。
- required / provided Capabilityを相互検証する。
- Capability changeはreconnectを基本とする。

tracing identity:

```text
MessageId
CorrelationId
CausationId
ComponentInstanceId
```

はいずれもoperational identityでありworld orderingへ使用しない。

```text
WorldContextV1 {
  world_id,
  basis_step,
  effective_step,
  master_generation,
  config_generation
}
```

`effective_step` はCore確定済みauthoritative Stepだけに使用する。

```text
OperationContextV1 {
  operation_id,
  operation_payload_digest,
  batch_id
}
```

ACKはhop receipt / custodyでありterminal world successとは限らない。

## 10. immutable Operation digest

`mv.operation-payload.v1` digestへ含める:

- operation type
- logical target
- immutable semantic payload / parameters
- originが固定したsemantic constraints
- `OperationSchedulingAdmissionV1`

含めない:

- ProtocolEnvelopeV1
- MessageId / CorrelationId / CausationId
- BatchId
- MasterGeneration / NegotiationGeneration
- retry / routing / network timing
- Gateway / Master candidate Step
- Core final/effective Step
- ACK / result metadata

same OperationId + different digestは `protocol.operation-payload-mismatch`。

## 11. persistence / replay / recovery

authoritative Snapshotは完全な `State(S)` boundaryだけを表す。

```text
HistorySequence := uint64
```

append-only durable historyはSHA-256 hash chainでcontinuityを検証する。

重要record:

```text
world.genesis.v1
operation.accepted.v1
operation.scheduled.v1
operation.terminal.v1
config.changed.v1
transition.committed.v1
snapshot.committed.v1
persistence.migrated.v1
```

- Core `ACCEPTED` はOperationAcceptedRecord durability後のみ返す。
- `State(S+1)` はtransition S commit durability後にfinalized / publishable。
- applied terminal resultもtransition commit durability後に返す。

Snapshot consistent cut:

```text
(snapshot_step = S, history_anchor = H)
```

RecoveryStateはworld stateだけでなくpending accepted Operation、dedup state、scheduler state、Config、StepRate、domain metadataを保持する。

State publication continuityには `StateContinuityToken` を使用する。

## 12. Operation scheduling admission

Gatewayはconfirmed Core stateとCore配布scheduling policyを用いてimmutable admission contextを確定する。

```text
OperationSchedulingAdmissionV1 {
  admission_basis_step: SimulationStep,
  scheduling_policy_generation: ConfigGeneration,
  requested_not_before_step: SimulationStep | NONE,
  requested_deadline_step: SimulationStep | NONE
}
```

このcontextはOperation immutable digestへ含め、retry / failoverで変更しない。

Core scheduling policy:

```text
OperationSchedulingPolicyV1 {
  owner_config_generation,
  min_lead_steps,
  default_deadline_window_steps,
  grace_steps,
  late_policy
}
```

```text
LatePolicy := REJECT | DEFER_WITHIN_GRACE
```

policyはSIMULATION Config。

## 13. candidate / final effective Step

Gateway / Masterのcandidate Stepはadvisory。

Coreはhistorical scheduling policyから再計算する。

```text
canonical_candidate = max(
  admission_basis_step + policy.min_lead_steps,
  requested_not_before_step if present
)
```

Core input freeze後の最小open Stepを `next_schedulable_step` とする。

```text
target_step = max(canonical_candidate, next_schedulable_step)
```

- deadline以内: targetをeffective Step。
- deadline超過 + REJECT: `world.deadline-exceeded`。
- deadline超過 + DEFER_WITHIN_GRACE + grace内: targetへdefer。
- grace超過: reject。
- finalized past Stepへretroactive applyしない。

final schedulingはdurable historyへ保存する。

## 14. Pause / resume

worldが `State(P)` でPause中の場合:

- Pause前に `effective_step=P` へschedule済みのOperationはtransition Pに残す。
- Pause中はapplyしない。
- Resume後の最初のtransition Pで処理する。

Pause中にCoreが新規acceptしたsimulation-affecting Operationは:

```text
pause_floor_step = P + 1
```

を持ち、stopped Step Pへ後付けしない。

- Pause durationだけでSimulationStep deadlineを消費しない。
- Pause中arrival orderをresume後orderへ使用しない。
- durable accepted Operationにwall-clock expiryを設けない。

## 15. Operation lifecycle / retry

Core authoritative lifecycle:

```text
UNSEEN
 -> ACCEPTED_DURABLE
 -> SCHEDULED_DURABLE
 -> TERMINAL_DURABLE
```

retryはsame:

- OperationId
- immutable payload digest
- immutable scheduling admission context

を維持する。

retry interval / timeout / backoffはOPERATIONAL Configでありworld Stepへ直接使用しない。

client disconnect / session timeoutだけでCore accepted Operationをcancelしない。

## 16. dedup retention

Core dedup primary keyはOperationId。

same id / different digestは `protocol.operation-payload-mismatch`。

terminal OperationはWorldId lifecycle中、最小tombstoneを保持する。

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

rich result detailsは有限保持としてよいが、double-apply防止tombstoneはexpiryしない。

## 17. Batch

Batchはtransport aggregation identity。

```text
BatchDigest = DomainHash(
  "mv.batch.v1",
  {
    batch_kind,
    ordered_entries:[{operation_id, operation_payload_digest}, ...]
  }
)
```

- exact same logical batch retryはsame BatchIdを維持できる。
- same BatchIdでcontents変更は禁止。
- subset retry / re-merge contents変更はnew BatchId。
- contained OperationIdは維持する。

標準processing:

```text
BatchProcessingMode := PER_OPERATION
BatchStatus := RECEIVED | PARTIAL | COMPLETE | REJECTED
```

Batchは暗黙all-or-nothing transactionではない。

## 18. Master failover custody

```text
SOURCE_HELD
 -> MASTER_RECEIVED
 -> CORE_ACCEPTED
 -> TERMINAL
```

- Master hop ACKだけでCore custody成立とはしない。
- source GatewayはCore acceptance確認前のOperationを再送可能に保持する。
- stale Master batch rejectだけでcontained Operationをterminal rejectにしない。
- Core acceptance不明時はsame identity retry / status queryで収束させる。
- Core accepted済みOperationはMaster failoverで失わない。

## 19. State diagnostic hash

Phase 1のauthoritative diagnostic rootはP1-07で確定した3段階hierarchyを使用する。

```text
StateDiagnosticRootV1
  -> DomainDiagnosticHashV1[]
       -> StateDiagnosticSliceHashV1[]
```

- domain / sliceはlogical partition。
- thread / process shard / DB shard / physical chunkへ依存しない。
- domainごとに `DiagnosticPartitionVersion` とstable `DiagnosticSliceKey` を定義する。
- sliceは `mv.state-diagnostic-slice.v1`、domainは `mv.state-diagnostic-domain.v1`、rootは `mv.state-diagnostic-root.v1` でdomain-separated SHA-256を計算する。
- root `StateDiagnosticHash` はTransitionCommit / Snapshot verificationへ使用できる。
- domain/slice hashはdivergence localization用に選択保存可能。

詳細は `docs/design/phase1-cross-cutting-review.md` を参照する。

## 20. common result code追加

P1-06までに次をcommon namespaceへ追加した。

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

## 21. Phase 1 作業分解

### P1-01 共通時間・識別子

状態: 完了。

### P1-02 決定論的順序・競合・乱数context

状態: 完了。  
正本: `docs/design/phase1-determinism-ordering-random.md`

### P1-03 Config詳細契約

状態: 完了。  
正本: `docs/design/phase1-config-contract.md`

### P1-04 Protocol共通envelope

状態: 完了。  
正本: `docs/design/phase1-protocol-envelope.md`

### P1-05 persistence / replay / recovery

状態: 完了。  
正本: `docs/design/phase1-persistence-replay-recovery.md`

### P1-06 Pause / late / retry / dedup

状態: 完了。  
正本: `docs/design/phase1-operation-lifecycle-retry-dedup.md`

### P1-07 横断整合性レビュー

状態: 完了。  
正本: `docs/design/phase1-cross-cutting-review.md`

確認済み:

- `docs/architecture` / `docs/protocols`とのsemantic整合
- terminology / field name / stable code consistency
- P1-04/P1-05の後続handoff事項がP1-06/P1-07で解消済み
- large-world diagnostic hash hierarchy確定
- Phase 2〜4が追加cross-cutting仮定なしで開始可能

## 22. Phase 1完了条件

Issue #13の完了条件に対する判定:

- common ID contract: 完了
- common time contract: 完了
- deterministic ordering / conflict / random: 完了
- Config schema / classification / apply / history: 完了
- common Protocol envelope / version / Capability / error-result: 完了
- Snapshot / replay / recovery consistency boundary: 完了
- Pause / late / retry / dedup / failover: 完了
- Phase 2〜4開始に必要なcross-cutting assumption: 追加不要
- unresolved cross-cutting blocker: **0件**

Phase 1は完了と判定する。

## 23. Phase 1後へ残す非blocker実装詳細

次は後続component/domain詳細設計へ委ねる。

- physical network transport
- concrete protocol serialization / compression
- physical persistence product / file layout / encryption
- exact wall-clock timeout / retry backoff数値
- Gateway durable queue / Core dedup index physical data structure
- state publication full/delta payload format
- auth credential / IdP / session technology
- component-specific permission matrix / command list
- observability metrics / alert threshold
- diagnostic treeのdomain-specific slice partition schema
- schema tooling / code generation method

これらはPhase 1で確定したidentity / ordering / durability / compatibility semanticsを変更してはならない。
