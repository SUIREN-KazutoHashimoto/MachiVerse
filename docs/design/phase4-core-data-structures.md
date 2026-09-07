# 詳細設計 Phase 4: 共通Data Structure / State Layout

Status: Draft / P4-01  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`  
Predecessors: Phase 1 common/determinism/persistence contracts, Phase 2 Simulation Core design, Phase 3 domain common/cross-domain contracts

## 1. 目的

Phase 1〜3で論理構造として定義したWorldState、domain partition、Operation、event、intent、shared invariant、detail transition、CrossDomainTransactionを、実装class/recordへ機械的に写像できる共通data structureへ具体化する。

本書ではdomain固有payload内部の数値modelやalgorithmはまだ固定しない。P4-01の責務は、全domainが共有する型幅、stable identity、state directory、candidate transition、index key、collection ordering、ownership/mutation boundaryを固定することである。

## 2. Normative primitive type table

| Logical type | Exact representation | Constraint |
|---|---|---|
| `SimulationStep` | `uint64` | wrap禁止 |
| `HistorySequence` | `uint64` | 1以上、appendごとに単調増加 |
| `MasterGeneration` | `uint64` | initial 1 |
| `ConfigGeneration` | `uint64` | generation 0は未初期化用途のみ |
| `RateGeneration` | `uint32` | wrap前にworld migration required |
| `ProtocolMajor` | `uint16` | version tuple major |
| `ProtocolMinor` | `uint16` | version tuple minor |
| `SchemaMajor` | `uint16` | schema version major |
| `SchemaMinor` | `uint16` | schema version minor |
| `OrderPhase` | `uint8` | Phase 1 registry値のみ |
| `DomainRank` | `uint16` | Phase 3 stable rankを格納可能 |
| `SemanticPriority` | `int32` | ascending order |
| `LocalOrdinal` | `uint64` | source内stable ordinal |
| `WorldId` | 16 octets | ZERO invalid |
| `EntityId` | 16 octets | ZERO invalid |
| `OperationId` | 16 octets | ZERO invalid |
| `BatchId` | 16 octets | ZERO invalid |
| `EventId` | 16 octets | ZERO invalid |
| `IntentId` | 16 octets | ZERO invalid |
| generic `OpaqueId128` | 16 octets | schemaがZERO可否を定義 |
| `Hash256` | 32 octets | SHA-256 digest |
| `StableToken` | ASCII string | `[a-z0-9][a-z0-9._/-]{0,63}` |

implementation language上のGUID/UUID native text formattingをcanonical representationに使用しない。128-bit identityのwire/persistence canonical binaryは16 octets、human-readable formは32桁lowercase hexとする。

## 3. 共通version / schema identity

```text
SchemaVersionV1 {
  major: uint16,
  minor: uint16
}

SchemaRefV1 {
  schema_id: StableToken,
  version: SchemaVersionV1
}
```

`schema_id`例:

- `core.world-state`
- `core.partition-descriptor`
- `core.step-candidate`
- `domain.spatial.world-frame`

tokenは例示ではなく、実際に永続化するregistry entryについて個別specで固定する。

比較規則:

1. `schema_id`: ASCII bytewise ascending
2. `major`: numeric ascending
3. `minor`: numeric ascending

## 4. Partition identity

```text
PartitionId := StableToken
```

Phase 3で定義済みのpartition tokenをそのまま安定identityとして利用する。

例:

```text
spatial.world_frame
spatial.terrain_geometry
resident.identity_lifecycle
participation.binding
```

規則:

- world内で一意。
- persisted historyへ出たtokenを別意味へ再利用しない。
- owner domain変更はmajor schema migration扱い。
- partition split/mergeは新PartitionIdを作りmigration recordを残す。

## 5. Authoritative WorldState layout

logical rootを次で固定する。

```text
WorldStateV1 {
  header: WorldStateHeaderV1,
  partitions: OrderedPartitionDirectoryV1,
  scheduler_state: SchedulerStateRefV1,
  operation_state: OperationStateRefV1,
  detail_state: DetailDirectoryRefV1,
  domain_registry_state: DomainRegistryRefV1,
  diagnostic: StateDiagnosticV1
}
```

### 5.1 WorldStateHeaderV1

```text
WorldStateHeaderV1 {
  schema: SchemaRefV1,
  world_id: WorldId,
  step: SimulationStep,
  world_seed_digest: Hash256,
  config_generation: ConfigGeneration,
  master_generation: MasterGeneration,
  rate_generation: uint32,
  previous_state_digest: Hash256 | NONE
}
```

`world_seed_digest`はseed値そのものの代替authorityではない。Snapshot/Recovery manifest側でWorldSeedを保持し、state diagnosticでworld取り違えを検出する補助値として使用する。

### 5.2 OrderedPartitionDirectoryV1

```text
OrderedPartitionDirectoryV1 = ordered map<PartitionId, PartitionStateRefV1>
```

canonical iterationは `PartitionId` のASCII bytewise ascending。

runtime implementationがhash tableを使うことは許容するが、次にhash table iteration orderを直接使用してはならない。

- state digest
- persistence serialization
- merge order
- replay diagnostic
- protocol full publication canonical order

## 6. Partition descriptor / state header

```text
PartitionDescriptorV1 {
  partition_id: PartitionId,
  owner_domain: StableToken,
  schema: SchemaRefV1,
  persistence_class: PersistenceClassV1,
  detail_capabilities: bitset,
  invariant_ids: ordered list<StableToken>
}
```

```text
PartitionStateHeaderV1 {
  partition_id: PartitionId,
  owner_domain: StableToken,
  schema: SchemaRefV1,
  revision: uint64,
  basis_step: SimulationStep,
  detail_level: DetailLevelV1,
  item_count: uint64,
  canonical_digest: Hash256
}
```

```text
DetailLevelV1 :=
  0 = D0_ENTITY
  1 = D1_LOCAL_AGGREGATE
  2 = D2_REGIONAL_AGGREGATE
  3 = D3_BOUNDARY_SUMMARY
```

`revision`はpartitionごとの単調増加logical revisionとし、同一Stepで変更がなければ維持してよい。DB row versionやmemory pointerをrevisionとして使用しない。

## 7. PersistenceClassV1

```text
PersistenceClassV1 :=
  0 = AUTHORITATIVE_ALWAYS
  1 = AUTHORITATIVE_RECONSTRUCTABLE_WITH_RECIPE
  2 = DERIVED_CACHE_REBUILDABLE
  3 = DIAGNOSTIC_ONLY
```

- `AUTHORITATIVE_ALWAYS`: snapshot/recovery cutに必須。
- `AUTHORITATIVE_RECONSTRUCTABLE_WITH_RECIPE`: authoritative意味を持つが、source state + versioned deterministic recipeからlossless semantic reconstructionできる場合のみ省略可能。
- `DERIVED_CACHE_REBUILDABLE`: authorityではなくsnapshotから省略可能。
- `DIAGNOSTIC_ONLY`: world resultへ利用禁止。

Phase 3 authoritative domain stateを、単に再計算できそうという理由で `DERIVED_CACHE_REBUILDABLE` に分類してはならない。

## 8. Domain registry state

```text
DomainRegistryStateV1 {
  registry_generation: uint32,
  domains: ordered map<DomainToken, DomainRuntimeDescriptorV1>
}
```

```text
DomainRuntimeDescriptorV1 {
  domain_token: StableToken,
  domain_rank: uint16,
  domain_schema: SchemaRefV1,
  owned_partitions: ordered list<PartitionId>,
  state_read_dependencies: ordered list<StableToken>,
  same_step_dependencies: ordered list<StableToken>,
  accepted_intent_kinds: ordered list<StableToken>,
  emitted_intent_kinds: ordered list<StableToken>,
  emitted_event_kinds: ordered list<StableToken>,
  invariant_ids: ordered list<StableToken>
}
```

Phase 3標準rank:

```text
10 spatial
20 environment
30 physical_built
40 participation
50 resident
60 society_economy
70 governance_security
80 infrastructure_information
```

rankはcanonical tie-break用でありphysical single-thread execution順を強制しない。

## 9. Read boundary

DomainRuntimeへ渡すread authorityはimmutable viewで表現する。

```text
WorldReadViewV1 {
  world_id: WorldId,
  basis_step: SimulationStep,
  config_generation: ConfigGeneration,
  partition_views: ordered map<PartitionId, PartitionReadView>
}
```

禁止:

- foreign domain mutable collectionの共有
- `ref`/pointerによるforeign partition更新
- lazily generated valueがread順でworld stateを変更する実装
- read iteratorのiteration orderをsemantic orderとして利用

## 10. Mutation intent exact common header

Phase 1/3 logical contractを統合し、common headerを次へ固定する。

```text
MutationIntentHeaderV1 {
  intent_id: IntentId,
  phase: uint8,
  source_domain: StableToken,
  target_domain: StableToken,
  basis_step: SimulationStep,
  source_kind: uint8,
  source_id: OpaqueId128,
  local_ordinal: uint64,
  mutation_kind: StableToken,
  target_scope: ConflictScopeV1,
  conflict_scope_digest: Hash256,
  semantic_priority: int32,
  causality_refs: ordered list<CausalityRefV1>
}
```

payloadは `mutation_kind` ごとのversioned schema。

validation順:

1. common header structural validation
2. `basis_step`一致
3. target owner validation
4. payload schema validation
5. conflict scope digest再計算一致
6. domain precondition
7. deterministic conflict resolution
8. shared invariant

## 11. ConflictScopeV1

```text
ConflictScopeV1 {
  domain: StableToken,
  target_kind: StableToken,
  target_id: byte string,
  resource: StableToken,
  subkey: byte string | NONE
}
```

constraints:

- `target_id`: 1..64 octets
- `subkey`: 0..128 octets
- `domain`はauthoritative target domain
- canonical digestはPhase 1 `DomainHash("mv.scope.v1", ...)`

physical database keyやmemory addressを含めない。

## 12. DomainEvent exact common header

```text
DomainEventHeaderV1 {
  event_id: EventId,
  event_kind: StableToken,
  source_domain: StableToken,
  basis_step: SimulationStep,
  subjects: ordered list<CausalityRefV1>,
  spatial_scope_ref: OpaqueId128 | NONE,
  causality_refs: ordered list<CausalityRefV1>,
  detail_provenance: DetailProvenanceV1
}
```

```text
DetailProvenanceV1 {
  source_detail_level: DetailLevelV1,
  source_partition_id: PartitionId,
  source_partition_revision: uint64,
  materialization_generation: uint32 | NONE
}
```

Eventは成立済みfactであり、Event object自体をforeign state mutation handleとして使わない。

## 13. CausalityRefV1

```text
CausalityRefV1 {
  kind: CausalityRefKindV1,
  id: byte string,
  basis_step: SimulationStep | NONE
}
```

```text
CausalityRefKindV1 :=
  0 = OPERATION
  1 = EVENT
  2 = INTENT
  3 = ENTITY
  4 = TRANSACTION
  5 = CONFIG_GENERATION
  6 = HISTORY_RECORD
  7 = PARTITION_REVISION
```

`id`のexact lengthはkind別schemaで固定する。

## 14. StepCandidateV1

`State(S) -> State(S+1)`のcommit前作業状態を次で表す。

```text
StepCandidateV1 {
  candidate_id: OpaqueId128,
  world_id: WorldId,
  basis_step: SimulationStep,
  target_step: SimulationStep,
  config_generation: ConfigGeneration,
  domain_outputs: ordered map<DomainToken, DomainCandidateOutputV1>,
  ordered_intents: ordered list<MutationIntentRefV1>,
  emitted_events: ordered list<DomainEventRefV1>,
  transaction_candidates: ordered list<CrossDomainTransactionCandidateV1>,
  partition_candidates: ordered map<PartitionId, PartitionCandidateV1>,
  invariant_results: ordered list<InvariantResultV1>,
  diagnostic: CandidateDiagnosticV1
}
```

constraints:

- `target_step = basis_step + 1`
- candidateはauthoritative stateではない。
- candidate IDをworld outcome orderingへ使用しない。
- candidate stateをGeneral View/Admin Viewへconfirmed publishしない。
- commit failure時はcandidateを破棄し `State(S)`をauthorityとして維持する。

## 15. DomainCandidateOutputV1

```text
DomainCandidateOutputV1 {
  domain_token: StableToken,
  basis_step: SimulationStep,
  intents: ordered list<MutationIntentRefV1>,
  events: ordered list<DomainEventRefV1>,
  local_partition_candidates: ordered list<PartitionCandidateRefV1>,
  diagnostics: DomainDiagnosticV1
}
```

DomainRuntimeは他domain partition candidateを返してはならない。foreign state変更はintentを通す。

## 16. PartitionCandidateV1

```text
PartitionCandidateV1 {
  partition_id: PartitionId,
  owner_domain: StableToken,
  basis_revision: uint64,
  candidate_revision: uint64,
  basis_step: SimulationStep,
  target_step: SimulationStep,
  change_set: PartitionChangeSetV1,
  candidate_digest: Hash256
}
```

`candidate_revision`は変更ありなら `basis_revision + 1`、no-opならpartition candidate自体を省略する。

`PartitionChangeSetV1`はdomain schema固有だが、canonical iteration/orderを持たなければならない。

## 17. CrossDomainTransactionCandidateV1

Phase 3 semantic transactionを実装可能な共通containerへ具体化する。

```text
CrossDomainTransactionCandidateV1 {
  transaction_id: OpaqueId128,
  transaction_kind: StableToken,
  basis_step: SimulationStep,
  participants: ordered list<TransactionParticipantV1>,
  required_invariants: ordered list<StableToken>,
  causality_refs: ordered list<CausalityRefV1>,
  status: TransactionCandidateStatusV1
}
```

```text
TransactionParticipantV1 {
  domain_token: StableToken,
  partition_id: PartitionId,
  intent_ids: ordered list<IntentId>,
  required: bool
}
```

```text
TransactionCandidateStatusV1 :=
  0 = ASSEMBLING
  1 = READY_FOR_VALIDATION
  2 = VALID
  3 = INVALID
```

candidate phaseでは `COMMITTED` を持たない。commit済みfactはhistory/event側で表す。

required participant/invariant失敗時、transaction参加effectを部分finalizeしない。

## 18. InvariantResultV1

```text
InvariantResultV1 {
  invariant_id: StableToken,
  severity: InvariantSeverityV1,
  outcome: InvariantOutcomeV1,
  participant_refs: ordered list<CausalityRefV1>,
  diagnostic_code: StableToken | NONE
}
```

```text
InvariantSeverityV1 :=
  0 = DIAGNOSTIC
  1 = COMMIT_BLOCKING
  2 = FATAL_AUTHORITY
```

```text
InvariantOutcomeV1 :=
  0 = PASS
  1 = FAIL
```

`COMMIT_BLOCKING + FAIL`はstep abort。`FATAL_AUTHORITY + FAIL`はState(S)を維持したままCoreをfatal停止させる。

## 19. SchedulerStateV1

world-affecting schedulerの論理indexを次へ固定する。

```text
SchedulerStateV1 {
  by_effective_step: ordered map<SimulationStep, OrderedOperationBucketV1>,
  next_schedulable_step: SimulationStep,
  freeze_step: SimulationStep | NONE
}
```

```text
OrderedOperationBucketV1 {
  effective_step: SimulationStep,
  operations: ordered list<ScheduledOperationRefV1>
}
```

同一effective Stepのworld-affecting operationはOperation schemaが定義するcanonical scheduling/order keyでsortする。network arrival orderやenqueue sequenceは使用しない。

physical priority queue implementationは自由だが、serialization/replay時のlogical orderは上記で固定する。

## 20. Operation dedup index

```text
OperationStateV1 {
  active_by_id: map<OperationId, OperationLifecycleStateV1>,
  terminal_tombstones: map<OperationId, OperationDedupTombstoneV1>
}
```

lookup keyは `OperationId`。

same OperationId + different immutable payload digestは `protocol.operation-payload-mismatch`。

terminal tombstoneはworld lifecycle中expiryしない。rich result detailのretentionとは分離する。

canonical persistence iterationはOperationId 16 octets bytewise ascending。

## 21. Detail directory

```text
DetailDirectoryV1 {
  regions: ordered map<OpaqueId128, DetailRegionStateV1>,
  pending_transitions: ordered list<DetailTransitionCandidateV1>
}
```

```text
DetailRegionStateV1 {
  detail_region_id: OpaqueId128,
  spatial_scope_ref: OpaqueId128,
  level_by_domain: ordered map<DomainToken, DetailLevelV1>,
  lineage_generation: uint32,
  last_transition_step: SimulationStep,
  active_guards: ordered list<StableToken>
}
```

View camera/FPSを `level_by_domain` のauthoritative inputにしない。

## 22. Deterministic collection rule

Phase 4共通containerを次の3種へ分類する。

### 22.1 `ordered list`

順序自体がsemanticであり、schema指定keyで完全順序を持つ。

### 22.2 `ordered map`

keyのcanonical ascendingでiterationする。

### 22.3 `unordered semantic set`

runtime上setでよいが、hash/persistence/wireへ出す前にschema指定stable keyでsortする。

禁止:

- hash-table iteration orderの永続化
- task completion orderのappend
- process-local sequence numberによるtie-break
- locale依存string sort

string/token sortはASCII bytewise。

## 23. Index contract

P4-01で論理indexの存在とkeyを固定し、physical data structure選定は用途別に後続P4で最適化できる。

最低限必要なauthoritative/derived index:

| Index | Key | Value | Authority |
|---|---|---|---|
| partition directory | `PartitionId` | partition state ref | authoritative root |
| operation dedup | `OperationId` | lifecycle/tombstone | authoritative |
| scheduler | `effective_step` + canonical operation key | operation ref | authoritative |
| entity/reference lookup | domain-defined stable identity | state ref | owner-dependent |
| transaction lookup | `transaction_id` | candidate/history ref | candidate/history |
| spatial lookup | spatial schema key | geometry/entity refs | Spatial/derived splitはP4-05で確定 |
| event causality lookup | `EventId` | event/history ref | history/derived |
| partition revision lookup | `(PartitionId, revision)` | snapshot/history ref | persistence |

index cacheを再構築可能にする場合、authoritative sourceとrebuild algorithm/versionを明示する。

## 24. Ownership / builder model

1 partitionのcandidate builderは同一Stepで1つのlogical ownerだけが保持する。

```text
PartitionBuilderAuthorityV1 {
  partition_id,
  owner_domain,
  basis_step,
  basis_revision
}
```

実装上複数workerが局所changeを計算してよいが、mergeはowner domainのdeterministic merge contractへ集約する。

foreign domainはbuilderを取得しない。

## 25. Candidate apply sequence

標準logical sequence:

```text
State(S)
 -> freeze input/read view
 -> domain calculation
 -> collect Event / Intent
 -> canonical order
 -> owner validation / conflict resolution
 -> assemble CrossDomainTransaction candidates
 -> build owner PartitionCandidates
 -> shared invariant validation
 -> candidate diagnostic digest
 -> persistence transition commit
 -> State(S+1) finalize
 -> confirmed publication / terminal result
```

worker parallelismはこのlogical resultを変更しない範囲で自由。

## 26. State diagnostic

```text
StateDiagnosticV1 {
  state_digest: Hash256,
  partition_digests: ordered map<PartitionId, Hash256>,
  schema_registry_digest: Hash256,
  config_digest: Hash256
}
```

`state_digest`はPhase 1 `mv.state-diagnostic.v1` domain-separated hashを使用し、canonical partition orderとauthority stateだけから生成する。

以下を含めない。

- wall-clock timestamp
- process ID
- memory address
- worker count
- cache statistics
- View connection state

## 27. Runtime implementation mapping constraints

C#等のimplementation languageで次は許容する。

- immutable record/struct
- array/list/dictionary
- persistent collection
- arena/chunk storage
- ECS-like domain-local storage
- memory-mapped persistence cache

ただし、logical schemaとcanonical orderingを満たす限りに限る。

shared DTO/class assemblyをprotocol/persistenceの唯一の契約正本にしない。

## 28. P4-01 acceptance criteria

P4-01完了時には次を満たす必要がある。

- Phase 1 common identity/type幅と矛盾しない。
- Phase 3全domain partitionを `PartitionDescriptorV1` へ登録可能。
- foreign domain direct writeがdata structure上不要。
- StepCandidateからpartial commitなしでState(S+1)を構築可能。
- CrossDomainTransactionのrequired participantを1 logical candidateへ束ねられる。
- scheduler/dedup stateがsnapshot/recovery対象として表現可能。
- hash map iteration/thread completionに依存せずcanonical serialization可能。
- partition/state diagnostic digestを再現可能。
- protocol/persistence/config後続schemaが共通型を参照できる。

## 29. P4-01未決定事項

P4-01開始時点で次を後続作業として残す。

- world-global coordinate exact numeric representation
- terrain geometry storage（voxel/SDF/CSG/octree等）の選定
- domain固有partition内部field layout
- protocol wire encoding exact choice
- snapshot physical chunk layout/compression
- domain algorithm/data-oriented layout最適化
- exact memory/performance budget

これらはP4-02〜P4-06で具体化し、本書のidentity/ownership/canonical orderingを変更しない。
