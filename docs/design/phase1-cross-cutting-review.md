# 詳細設計 Phase 1: 横断整合性レビュー

Status: Complete / P1-07 complete / Phase 1 complete  
Tracking: Issue #13  
Parent: `docs/design/phase1-common-foundation-contracts.md`

## 1. 目的

本書は Phase 1 の P1-07 として、P1-01〜P1-06で確定した共通契約を `docs/architecture` / `docs/protocols` / `docs/design` 横断で再確認し、Phase 2〜4が新たなcross-cutting仮定を置かず開始できることを確認する最終レビュー記録である。

Phase 1終了時点の判断について、本書は各P1詳細文書の「当時の次作業」「後続へ引き渡す事項」「未決定事項」節より新しい最終状態を表す。

## 2. レビュー対象

主対象:

- `docs/design/phase1-common-foundation-contracts.md`
- `docs/design/phase1-determinism-ordering-random.md`
- `docs/design/phase1-config-contract.md`
- `docs/design/phase1-protocol-envelope.md`
- `docs/design/phase1-persistence-replay-recovery.md`
- `docs/design/phase1-operation-lifecycle-retry-dedup.md`
- `docs/architecture/deterministic-update-execution.md`
- `docs/architecture/gateway-operation-delivery.md`
- `docs/architecture/persistence-replay-recovery.md`
- `docs/architecture/persistence-save-recovery-semantics.md`
- `docs/architecture/protocol-compatibility-capability.md`
- `docs/architecture/configuration.md`
- `docs/architecture/config-semantics.md`
- `docs/protocols/README.md`
- `docs/protocols/core-gateway.md`
- `docs/protocols/gateway-gateway.md`
- `docs/protocols/gateway-view.md`
- `docs/protocols/gateway-admin-view.md`

## 3. 正本優先順位

同一事項について古いP1文書の「後続で決める」記述と、後続P1文書の確定契約が並存する場合は、**後続で確定した専用詳細契約を現行正本**とする。

具体的には:

1. deterministic encoding / ordering / random / ID / diagnostic hash: `phase1-determinism-ordering-random.md`
2. Config: `phase1-config-contract.md`
3. common protocol envelope / version / Capability / result: `phase1-protocol-envelope.md`
4. persistence / replay / recovery / durability: `phase1-persistence-replay-recovery.md`
5. Operation scheduling / Pause / late / retry / dedup / Batch / custody: `phase1-operation-lifecycle-retry-dedup.md`
6. P1-07で追加確定した横断補正: 本書

従って、P1-04/P1-05文書末尾に残る「P1-05/P1-06へ引き継ぐ」「P1-06で確定する」等の節は、そのsubphase完了時点の履歴記録として読む。現在の未決定事項一覧ではない。

## 4. 用語・field consistency確認

### 4.1 World Time

統一済み:

```text
SimulationStep := uint64
WorldTime = { step, rate_generation }
```

- authoritative timeはSimulationStep。
- wall clockはauthoritative ordering / scheduling / randomへ使用しない。
- `basis_step` はstate basis。
- `effective_step` はCore確定済み `State(S) -> State(S+1)` transition Step。
- candidate Stepを `effective_step` として表現しない。

### 4.2 identity

統一済み:

- `WorldId / EntityId / OperationId / BatchId`: 128-bit opaque
- `MasterGeneration`: uint64 / Core authority
- `MessageId / CorrelationId / ComponentInstanceId`: operational 128-bit identity
- `HistorySequence`: uint64 persistence sequence
- `ConfigGeneration`: uint64 effective Config revision
- `NegotiationGeneration`: uint32 negotiated connection semantics generation

異なるgenerationを相互代用しない。

### 4.3 immutable Operation boundary

P1-06確定後の最終ルール:

`mv.operation-payload.v1` digestへ含める:

- operation type
- logical target
- immutable semantic payload / parameters
- originが固定したsemantic constraint
- `OperationSchedulingAdmissionV1`
  - `admission_basis_step`
  - `scheduling_policy_generation`
  - `requested_not_before_step`
  - `requested_deadline_step`

含めない:

- ProtocolEnvelopeV1
- MessageId / CorrelationId / CausationId
- BatchId
- MasterGeneration / NegotiationGeneration
- retry count / retry timing
- routing metadata
- network arrival timestamp
- Gateway / Masterが算出したcandidate Step
- Core確定effective Step
- ACK / result metadata

同一OperationIdでdigest不一致は `protocol.operation-payload-mismatch`。

## 5. common result code最終集合

P1-04のcommon codeへ、P1-06で次を追加済みの現行contractとする。

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

既存 `world.late-operation` はgeneric late categoryとして利用できるが、deadline超過terminal rejectには `world.deadline-exceeded` を使用する。

## 6. persistence / P1-06同期の最終解釈

P1-05の抽象field:

```text
deterministic_scheduling_constraints
```

は、P1-06確定後は `OperationSchedulingAdmissionV1` と、それを検証するために必要なnormalized scheduling policy contextを意味する。

`OperationAcceptedRecordV1` は少なくとも:

```text
operation_id
operation_payload_digest
normalized_immutable_operation
scheduling_admission: OperationSchedulingAdmissionV1
accepted_master_generation
accepted_config_generation
```

をrecovery可能にする。

`OperationScheduledRecordV1` は:

```text
operation_id
effective_step
same_step_order_key
scheduling_result_code
```

をdurableに保持し、recovery後のsilent reassignmentを禁止する。

## 7. dedup retention最終契約

P1-05内の「dedup retention window」はP1-06で次のように確定した。

### pending

`ACCEPTED_DURABLE` / `SCHEDULED_DURABLE` のOperation identity・payload・scheduling stateはterminalまで削除しない。

### terminal

CoreはWorldId lifecycle中、少なくとも次のtombstoneを保持する。

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

- tombstoneはWorldId継続中expiryしない。
- raw history compaction時もRecoveryState / compact indexへsemanticsを移す。
- rich result detailsのみ有限retentionとしてよい。
- result details expiry後もduplicate mutationを再実行しない。

従って「exact dedup retention window」はPhase 1の未決定事項ではない。

## 8. Batch / retry / failover最終契約

### Batch

```text
BatchProcessingMode := PER_OPERATION
BatchStatus := RECEIVED | PARTIAL | COMPLETE | REJECTED
```

- Batchはtransport aggregation identity。
- 暗黙all-or-nothing transactionではない。
- exact same batch retryのみsame BatchIdを維持可能。
- contents変更 / subset retry / re-mergeはnew BatchId。
- contained OperationIdは維持する。

### custody

```text
SOURCE_HELD
 -> MASTER_RECEIVED
 -> CORE_ACCEPTED
 -> TERMINAL
```

- Master receipt ACKはCore acceptanceではない。
- stale Master batch rejectはcontained Operationのterminal rejectではない。
- Core acceptance不明時はsame identity retry / Operation status queryで収束する。

## 9. Pause / late最終契約

worldがfinalized `State(P)` でPause中:

- Pause前に `effective_step=P` へschedule済みOperationはtransition Pに残す。
- Pause中に新規durable acceptanceしたsimulation-affecting Operationは `pause_floor_step=P+1`。
- Pause durationだけでSimulationStep deadlineを消費しない。
- Pause queue arrival orderをsame-Step orderへ使用しない。
- durable accepted Operationにwall-clock expiryを設けない。

late:

- on-time: target Stepへschedule
- `REJECT`: deadline超過でterminal `world.deadline-exceeded`
- `DEFER_WITHIN_GRACE`: grace内ならfuture targetへdefer
- grace超過: terminal reject
- finalized past Stateはrewriteしない

## 10. large-world state diagnostic hash最終契約

P1-02 / P1-05で残っていたlarge-world slice/tree granularityをP1-07で確定する。

### 10.1 hierarchy

```text
StateDiagnosticRootV1
  -> DomainDiagnosticHashV1[]
       -> StateDiagnosticSliceHashV1[]
```

domain / slice partitionはauthoritative stateの**論理partition**であり、thread、process shard、DB shard、memory page、physical chunkへ依存させない。

### 10.2 stable slice key

```text
DiagnosticPartitionVersion := uint32
DiagnosticSliceKey := StableToken
```

各DomainTokenは自身のdiagnostic partition schemaを所有する。

- partition versionを明示する。
- 同一partition versionでは同一authoritative stateから同じslice key集合を生成する。
- slice keyの意味を変更する場合はpartition versionを増加する。
- partition方式はEntityId prefix、stable spatial cell、ledger partition等を利用できるが、物理storage配置を意味にしない。

### 10.3 slice hash

```text
SliceHash = DomainHash(
  "mv.state-diagnostic-slice.v1",
  {
    world_id,
    step,
    domain_token,
    partition_version,
    slice_key,
    canonical_authoritative_slice
  }
)
```

### 10.4 domain hash

slice entryを `DiagnosticSliceKey` ASCII bytewise ascendingで並べる。

```text
DomainHashValue = DomainHash(
  "mv.state-diagnostic-domain.v1",
  {
    world_id,
    step,
    domain_token,
    partition_version,
    slices: [
      { slice_key, slice_hash }, ...
    ]
  }
)
```

### 10.5 root hash

domain entryを `DomainToken` ASCII bytewise ascendingで並べる。

```text
StateDiagnosticHash = DomainHash(
  "mv.state-diagnostic-root.v1",
  {
    world_id,
    step,
    domains: [
      {
        domain_token,
        partition_version,
        domain_hash
      }, ...
    ]
  }
)
```

`StateDiagnosticHash` はP1-05 `TransitionCommitRecordV1.state_diagnostic_hash` / Snapshot verificationで使用するroot hashを意味する。

### 10.6 記録粒度

- root hashはverification checkpointで保存可能。
- domain hash / slice hashはdivergence localization用に選択保存可能。
- lower-level hashを保存しない場合でもroot algorithmは同一。
- hash treeの計算・保存有無はworld outcomeを変えない。

これによりlarge-worldで全stateを1つの巨大canonical byte streamへ実体化せず、stable logical slicesから同一rootを再構成できる。

## 11. hash domain separation追加

P1-07で次をcommon hash labelへ追加する。

```text
mv.state-diagnostic-slice.v1
mv.state-diagnostic-domain.v1
mv.state-diagnostic-root.v1
```

従来の `mv.state-diagnostic.v1` はsingle-slice compatibility / leaf-level diagnostic用途として残せるが、Phase 1のauthoritative cross-domain diagnostic rootは `mv.state-diagnostic-root.v1` とする。

## 12. protocol境界レビュー

### Core ↔ Gateway

確認済み:

- final batchはMasterGenerationを明示。
- candidateとeffective Stepを分離。
- durable ACCEPTED / terminal result boundaryを区別。
- StateContinuityTokenでrecovery後continuityを識別。
- status queryでACK loss / failoverを収束可能。

### Gateway ↔ Gateway

確認済み:

- stable OperationId / BatchId。
- MasterGeneration切替でOperation identityを変更しない。
- Batch receipt ACKとCore custodyを分離。
- stale generation batchをOperation terminal rejectにしない。

### Gateway ↔ General View

確認済み:

- confirmed basis_stepとpredictionを分離。
- state continuity mismatch時にresync。
- ACK / acceptedとCore terminal resultを分離。
- View local retry/cache retentionはCore world-lifetime dedup tombstoneの代替ではない。

### Gateway ↔ Admin View

確認済み:

- General View AdministratorとAdmin View operatorを分離。
- ConfigGeneration optimistic concurrencyとatomic Config changeを維持。
- simulation-affecting Admin OperationもP1-06 scheduling / retry / dedup契約へ従う。
- Admin-side request/cache timeoutはCore accepted Operationをcancelしない。

## 13. architecture整合確認

確認結果:

- `deterministic-update-execution.md`: P1-06 Step / Pause semanticsと一致。
- `gateway-operation-delivery.md`: stable identity / retry / failover / dedupと一致。
- `persistence-replay-recovery.md`: durable acceptance / commit-before-publicationと一致。
- `persistence-save-recovery-semantics.md`: State(S)境界 / corruption fail-safeと一致。
- `protocol-compatibility-capability.md`: P1-04 version / Capability semanticsと一致。
- `configuration.md` / `config-semantics.md`: P1-03 schema / generation / effective Stepと一致。

## 14. Phase 2〜4への引き渡し条件

Phase 2〜4は次を再定義せず利用する。

- SimulationStep / WorldTime
- WorldId / EntityId / OperationId / BatchId
- MasterGeneration / ConfigGeneration / NegotiationGeneration
- MV-DCBOR-v1 / SHA-256 domain separation
- EntityId / EventId / IntentId derivation
- same-Step ordering / conflict resolver contract
- RandomContextV1
- Config classification / migration / atomic apply
- ProtocolEnvelopeV1 / WorldContextV1 / OperationContextV1
- version / Capability / common result code
- State(S) Snapshot / durable history / StateContinuityToken
- Operation scheduling admission / deadline / Pause semantics
- retry / dedup tombstone / Batch / failover custody
- StateDiagnosticHash root hierarchy

各domain/componentは、上記を壊さない範囲で自身のpayload、state schema、algorithm、storage/transport実装を設計する。

## 15. Phase 1後へ残す非blocker実装詳細

次はcross-cutting semantic blockerではないため後続詳細設計へ委ねる。

- physical network transport
- concrete protocol serialization / compression
- physical persistence product / file layout / encryption
- exact wall-clock timeout / retry backoff数値
- Gateway durable queue / Core dedup indexのphysical data structure
- state publication full/delta payload format
- auth credential / IdP / session technology
- component-specific permission matrix / command list
- observability metrics / alert threshold
- diagnostic treeのdomain-specific slice partition schema
- generated schema tooling / code generation method

これらはPhase 1で確定したidentity / ordering / durability / compatibility semanticsを変更してはならない。

## 16. completion判定

Issue #13の完了条件に対する判定:

- common ID contract: 完了
- common time contract: 完了
- deterministic ordering / conflict / random contract: 完了
- Config contract: 完了
- common Protocol envelope / version / Capability / error-result: 完了
- Snapshot / replay / recovery consistency boundary: 完了
- Pause / late / retry / dedup semantics: 完了
- Phase 2〜4開始に必要なcross-cutting assumption: 追加不要
- unresolved cross-cutting blocker: **0件**

Phase 1は完了と判定する。
