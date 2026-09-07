# 詳細設計 Phase 2: Simulation Core内部設計

Status: Complete / Phase 2 reviewed  
Tracking: Issue #14  
Parent: `docs/design/phase2-component-internal-design.md`

## 1. 目的

Simulation Core内部を、authoritative World Stateとdeterministic Step transitionの所有責任が一意になるようmodule分割する。

本書はPhase 3で個別domain simulationを追加するための受け皿を確定し、thread scheduler、database、serialization等の具体技術は固定しない。

## 2. 内部module境界

Coreは論理上、最低限次のmoduleへ分ける。

| module | 主責務 | authoritative mutation |
|---|---|---|
| `CoreLifecycle` | 起動、停止、recovery、Pause/Resume、top-level state transition | lifecycleのみ |
| `ProtocolBoundary` | `mv.core-gateway` negotiation、decode、validation、mapping | なし |
| `OperationIngress` | Operation/Batch受付、digest/dedup照合、durable acceptance要求 | operation lifecycleのみ |
| `OperationScheduler` | policy検証、effective Step確定、same-Step投入 | schedule stateのみ |
| `StepCoordinator` | `State(S) -> State(S+1)` transition全体の唯一のcoordinator | commit authority |
| `DomainRegistry` | DomainToken、dependency、domain metadata管理 | registry metadata |
| `DomainRuntime` | domain read/calculate、intent生成 | direct mutation禁止 |
| `DeterministicMerge` | intent ordering、conflict resolution、reduction | apply candidate生成 |
| `WorldStateStore` | current authoritative stateの論理所有 | StepCoordinator経由のみ |
| `PersistenceCoordinator` | history、snapshot、recovery、durability frontier | durable records |
| `PublicationProjection` | Gateway向けauthoritative-derived projection生成 | なし |
| `MasterCoordinator` | Gateway eligibility、MasterGeneration、Master assignment | master authority state |
| `ConfigCoordinator` | Core Config validation/migration/effective apply | config state/history |
| `Observability` | metric/log/diagnostic projection | なし |

module名は実装上のclass/project名を固定しない。責務境界を表す論理名である。

## 3. state ownership

### 3.1 WorldState

`WorldStateStore` がcurrent finalized `State(S)` の唯一の論理ownerとなる。

- DomainRuntimeはcurrent stateのread viewを受け取る。
- DomainRuntimeがshared mutable WorldStateへ直接書き込まない。
- mutationは`MutationIntent`相当のcomponent-local表現として返す。
- finalized stateの更新は`StepCoordinator`が`DeterministicMerge`結果を適用する一箇所に集約する。

### 3.2 Operation lifecycle state

`OperationIngress` / `OperationScheduler` / `PersistenceCoordinator` が協調して次を保持する。

```text
UNSEEN
 -> ACCEPTED_DURABLE
 -> SCHEDULED_DURABLE
 -> TERMINAL_DURABLE
```

memory上のqueue stateよりdurable recordをauthorityとする。recovery時はdurable recordからqueueを再構成する。

### 3.3 Master state

`MasterCoordinator` が次を所有する。

```text
MasterState {
  generation,
  current_master,
  eligible_gateways,
  assignment_state
}
```

WorldStateとは別のoperational authorityであり、Master identityをsimulation outcomeへ入力しない。

### 3.4 Config state

`ConfigCoordinator` が次を所有する。

- loaded normalized Core Config
- ConfigSchemaVersion
- ConfigGeneration
- ConfigDigest
- pending runtime change set
- simulation-affecting Config history

DomainはConfig fileを直接読まず、Step basisに対応したeffective Config viewだけを受け取る。

## 4. Step update pipeline

1 Stepの標準pipelineを次とする。

```text
finalized State(S)
  -> FreezeStepInputs(S)
  -> BuildDomainExecutionPlan(S)
  -> ParallelDomainCalculate(S)
  -> CollectMutationIntents(S)
  -> DeterministicMerge(S)
  -> ValidateCrossDomainInvariants(S)
  -> ApplyCandidateState(S+1)
  -> BuildPersistenceRecords
  -> DurableCommit
  -> Finalize State(S+1)
  -> BuildPublicationProjection
```

### 4.1 FreezeStepInputs

Step Sへ参加する入力集合をfreezeする。

- schedule済みOperation
- Step Sで有効になるConfig changes
- internal scheduled events
- domain-owned deterministic triggers

freeze後に到着した外部OperationをStep Sへnetwork timingだけで割り込ませない。

### 4.2 BuildDomainExecutionPlan

`DomainRegistry` のdependency declarationから、Phase 1のstable domain orderingに従うexecution planを構築する。

execution planはphysical thread countから独立した論理planとする。

### 4.3 ParallelDomainCalculate

各domainへ少なくとも次を提供する。

```text
DomainStepContext {
  world_id,
  step,
  effective_config_view,
  read_view,
  scheduled_operations,
  deterministic_random_context_factory
}
```

domainは0個以上のMutationIntent/EventIntent/DiagnosticContributionを返す。

### 4.4 DeterministicMerge

- intentをPhase 1 `SameStepOrderKey`へ写像する。
- conflict scopeを明示する。
- parallel completion orderを無視する。
- reductionはstable orderingまたはcommutative/associativeであることをcontract化する。
- unresolved conflictをlast-writer-wins timingへ落とさない。

### 4.5 DurableCommit

`State(S+1)` をexternally confirmedとする前に必要なhistory/transition durabilityを完了する。

commit failure時は`State(S)`をfinalized authorityとして維持し、半端な`State(S+1)`をpublishしない。

## 5. Phase 3 DomainRuntime contract

Phase 3の各domainは次を登録する。

```text
DomainDefinition {
  domain_token,
  dependencies,
  state_owner_descriptor,
  input_operation_kinds,
  update_phases,
  diagnostic_partition_version,
  publication_projection_capabilities
}
```

DomainRuntimeの禁止事項:

- 他domainのprivate mutable stateへのdirect write
- Gateway/View protocol typeへのdependency
- wall clockをworld ordering/randomへ利用
- thread id / worker idをidentity生成へ利用
- shared stateful PRNG消費順への依存
- physical storage partitionをdomain semantic orderingへ利用

Cross-domain interactionはread dependency、intent、event、explicit shared invariantのいずれかとして表現する。

## 6. 内部queue

### 6.1 Protocol ingress queue

Producer: `ProtocolBoundary`  
Consumer: `OperationIngress` / control handlers

- 未検証wire messageをWorldState pipelineへ直接入れない。
- capacity超過はprotocol-level overload/retry responseを返せる。
- duplicate retryをqueue entry duplicationとして無制限増幅しない。

### 6.2 Accepted operation queue

Producer: `OperationIngress`  
Consumer: `OperationScheduler`

- durable ACCEPTED後のOperationだけを対象とする。
- lossless logical queue。
- process crash後はdurable stateから再構築可能にする。
- pressureを理由にaccepted Operationをdropしない。

### 6.3 Scheduled-step buckets

Producer: `OperationScheduler`  
Consumer: `StepCoordinator`

keyはauthoritative effective Step。

- bucket内orderはarrival orderではなくcanonical order。
- recovery後もsame effective Stepを維持する。
- Pause中new acceptanceはPhase 1のpause floor semanticsへ従う。

### 6.4 Domain work queue

Producer: `StepCoordinator`  
Consumer: workers/DomainRuntime

- physical execution orderはnon-authoritative。
- work completion順をmerge順へ使わない。
- worker failureを検出した場合、そのStepをpartial commitしない。

### 6.5 Persistence queue

Producer: Step/Operation/Config coordinators  
Consumer: `PersistenceCoordinator`

- durability-required recordはlossyにしない。
- saturation時はStep進行/admissionへbackpressureする。
- publicationよりdurabilityを優先する。

### 6.6 Publication queue

Producer: `PublicationProjection`  
Consumer: `ProtocolBoundary`

- confirmed finalized stateだけを投入する。
- intermediate publicationをcoalesce可能。
- coalesce後もbasis_step / continuity tokenを正確にする。
- Gatewayがdelta base mismatchを検出可能な情報を失わない。

## 7. Parallel calculationとdeterministic merge

Coreは1〜16 threadの実使用を許容するが、論理結果をthread countから独立させる。

### 7.1 read isolation

同一Step計算中のDomainRuntimeは原則`State(S)`のstable read viewを読む。

同一Step内の別domain結果を必要とする場合はdependency phaseを明示し、implicit concurrent visibilityへ依存しない。

### 7.2 intent identity

world-affecting intentはreplay可能なlogical contextからstable intent identityを導出できる必要がある。

worker local sequenceやcompletion sequenceをidentityへ含めない。

### 7.3 merge barrier

各logical phaseにmerge barrierを置ける。

```text
read/calc phase
 -> all required results collected
 -> canonical sort/reduce
 -> phase result
```

barrier数やworker scheduling technologyは実装詳細とする。

## 8. lifecycle

```text
STOPPED
 -> STARTING
 -> RECOVERING | INITIALIZING_WORLD
 -> READY_RUNNING | READY_PAUSED
 -> DEGRADED
 -> STOPPING
 -> STOPPED
```

重大failure:

```text
STARTING/RECOVERING/RUNNING
 -> FAILED_SAFE
```

### READY_RUNNING

- Step transition可能。
- Gateway connectionなしでも継続可能。

### READY_PAUSED

- finalized Stepを維持。
- protocol/authenticated Operation受付とdurable acceptanceは可能。
- simulation Step transitionは進めない。

### DEGRADED

例:

- publication経路の一部 unavailable
- 一部Gateway接続喪失
- diagnostic exporter failure

WorldState integrityとpersistence durabilityを維持できる場合のみ継続する。

### FAILED_SAFE

例:

- persistence continuity破損
- snapshot/history integrity failure
- deterministic invariant violation
- unrecoverable state validation failure

新しいworld mutationをnormal successとして受理せず、operator診断/recoveryへ移行する。

## 9. failure transition

| failure | transition | world処理 |
|---|---|---|
| Gateway disconnect | RUNNING維持 | world継続、Master再評価 |
| Master failure | DEGRADEDまたはRUNNING | generation更新、安全candidateへ切替 |
| Domain calculation failure | Step abort/degraded | 当該Stepをcommitしない |
| deterministic merge invariant failure | FAILED_SAFE | partial apply禁止 |
| persistence write failure | DEGRADED/FAILED_SAFE | durabilityなしpublication禁止 |
| Config validation failure | current state維持 | change set reject、partial apply禁止 |
| publication failure | DEGRADED | finalized worldは継続可能 |
| recovery integrity failure | FAILED_SAFE | world startup拒否 |

## 10. backpressure

優先順位:

1. durability/integrity
2. authoritative Step correctness
3. accepted Operation retention
4. protocol admission
5. publication freshness
6. diagnostics detail

処理能力不足時:

- Step skipをしない。
- new ingress admissionを制限できる。
- durable accepted Operationは保持する。
- publication intermediate stateをcoalesceできる。
- optional diagnostics/detail計算をConfig policyにより削減できる。
- detail reductionがworld semanticsへ影響する場合はdeterministic simulation policyとして扱う。

## 11. MasterCoordinator

MasterCoordinatorはGateway health/compatibility/sync readinessからeligible setを維持する。

- selection authorityはCore。
- reassignment時にMasterGenerationを+1。
- stale generation outputをreject。
- old MasterのOperation identityを変更しない。
- selectionのoperational randomnessをWorldSeedへ結び付けない。

Master切替はStep outcomeのcanonical ordering inputではない。

## 12. PublicationProjection

Coreはinternal WorldState objectをGatewayへ公開しない。

Projection pipeline:

```text
Finalized State(S)
 -> publication eligibility/filter
 -> stable protocol-facing projection model
 -> continuity metadata
 -> serialization boundary
```

full/deltaのphysical representationは固定しない。

projection generation failureはWorldStateをrollbackせず、publication側failureとして扱う。

## 13. Config ownership

Core-owned Config category:

- Step rate
- active worker/thread limit
- deterministic detail policy
- scheduling policy
- persistence/snapshot/recovery policy
- publication projection policy
- Master health/eligibility operational thresholds
- Core queue/admission limits
- observability limits

simulation-affecting Configはexplicit effective Stepで適用する。worker count等がOPERATIONALである場合もworld outcomeを変えないことが前提となる。

## 14. observability

最低限:

- current finalized SimulationStep
- Step calculation/merge/commit duration
- Step overrun/lag
- active worker count
- domain phase duration / failures
- intent count / conflict count
- Operation lifecycle counts
- accepted/scheduled queue depth
- persistence queue depth/durability lag
- current StateContinuityToken metadata
- ConfigGeneration
- MasterGeneration/current Master/eligible count
- publication queue depth/coalesce count
- recovery state
- StateDiagnosticHash checkpoints

metrics値をworld outcomeへ入力しない。

## 15. protocol対応

`mv.core-gateway` message categoryを次へmappingする。

| protocol category | internal owner |
|---|---|
| handshake/Capability | ProtocolBoundary |
| state synchronization | PublicationProjection + ProtocolBoundary |
| scheduling policy | ConfigCoordinator + ProtocolBoundary |
| Operation/Batch submit | OperationIngress |
| status query | OperationIngress/PersistenceCoordinator |
| Master generation | MasterCoordinator |
| Config/health diagnostic | ConfigCoordinator/Observability |
| recovery continuity | PersistenceCoordinator |

## 16. 未確定だがblockerではない実装詳細

- concrete task scheduler
- concrete lock-free/lock/data partition structure
- physical persistence product/file layout
- serialization/compression format
- exact queue capacities
- exact retry/backoff/health threshold
- publication full/delta encoding
- metric exporter/collector technology

これらはPhase 2責務境界を変更せず後続で選定可能である。
