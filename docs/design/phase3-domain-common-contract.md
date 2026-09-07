# 詳細設計 Phase 3: Domain共通契約

Status: Draft / P3-01  
Tracking: Issue #15  
Parent: `phase3-world-domain-design.md`  

## 1. 目的

Phase 3の全domainが共有するstate ownership、event/intent、Step更新、dependency、detail level、promotion/demotion、aggregate exchangeのsemantic contractを定義する。

本書は個別domainの内部modelを統一するものではない。各domainが異なるstate structureや更新algorithmを採用しても、Simulation Coreの決定論的Step transitionとcross-domain整合性を壊さないための共通境界を定める。

## 2. Phase 1 / 2から継承する契約

本書では次を再定義しない。

- authorityとなるWorld Timeは`SimulationStep`
- simulation-affecting randomはaddressable deterministic random
- same-Step orderingはPhase 1のcanonical orderingへ写像する
- world-affectingOperationはeffective Stepへscheduleされる
- finalized stateの唯一のlogical ownerは`WorldStateStore`
- `StepCoordinator`が`State(S) -> State(S+1)` commitをcoordinatorする
- `DomainRuntime`はshared mutable WorldStateへ直接書き込まない
- domainはread viewを読み、mutation/event intentを返す
- parallel completion orderをworld outcomeへ使わない
- durability完了前のcandidate stateをconfirmed publishしない

## 3. Phase 3 DomainDefinition

Phase 2の`DomainDefinition`を、Phase 3ではsemantic上次の情報を持つものとして扱う。

```text
DomainDefinitionV1 {
  domain_token,
  domain_family,
  state_partitions,
  dependencies,
  input_operation_kinds,
  update_phases,
  emitted_event_kinds,
  accepted_intent_kinds,
  emitted_intent_kinds,
  invariant_ids,
  detail_policy_id,
  diagnostic_partition_version,
  publication_projection_capabilities,
  requirement_trace_refs
}
```

これはPhase 3の論理schemaであり、serialization field名やbinary layoutを固定しない。

### 3.1 domain_token

`domain_token`はworld内で一意かつstableなlogical identityとする。renameが必要な場合、replay/snapshot compatibilityを破壊しないalias/migrationをPhase 4で定義する。

### 3.2 state_partitions

各partitionは最低限次を宣言する。

```text
StatePartitionDescriptor {
  partition_id,
  owner_domain,
  semantic_scope,
  identity_policy,
  detail_capabilities,
  persistence_class,
  invariant_ids
}
```

同一authoritative fieldを複数domainがownerとして宣言してはならない。

## 4. State ownership

### 4.1 単一owner原則

authoritative stateの各semantic fieldには必ず1つのowner domainを割り当てる。

他domainは次だけを行える。

- finalized `State(S)`からreadする
- ownerへMutationIntentを送る
- ownerが発行したDomainEventを受ける
- shared invariantへ検証材料を提供する

他domainのprivate stateへdirect writeする契約は作らない。

### 4.2 shared conceptの分割

1つの現実概念が複数domainに跨る場合、stateを意味ごとに分割する。

例:

- 道路の3D geometryと損傷: `physical_built`
- 道路network上のservice capacity/運行状態: `infrastructure_information`
- 道路の制度上の所有権・通行規制: `governance_security`または`physical_built`が参照する制度state
- 道路建設契約・支払: `society_economy`

「道路」という単語だけを理由に1domainへ全責務を集約しない。

## 5. Step内update model

### 5.1 logical update phases

Phase 3では、個別domainの処理を少なくとも次のlogical phaseへ写像可能にする。

```text
PREPARE
ENVIRONMENT
PHYSICAL
AGENT_INTERNAL
AGENT_ACTION
SOCIAL_INSTITUTIONAL
INFRASTRUCTURE_SERVICE
CONSEQUENCE
VALIDATE
```

phase名はsemantic orderingを示し、数値rankのexact encodingはPhase 4で確定してよい。

1 domainが複数phaseへ参加してもよい。例えば`resident`は`AGENT_INTERNAL`で身体・認知stateを評価し、`AGENT_ACTION`で行動intentを生成できる。

### 5.2 same-Step visibility

原則としてdomain計算は`State(S)`をread basisとする。

同一Step内で前phase結果を必要とする場合は、次のいずれかを明示する。

- phase-local committed intermediate fact
- deterministic event batch
- merge済みderived view

worker completion timingによるimplicit visibilityは禁止する。

### 5.3 Step failure

次の場合、そのStepをpartial commitしない。

- domain runtime failure
- malformed world-affecting intent
- unresolved mandatory conflict
- shared invariant failure
- deterministic merge contract violation

failure時はfinalized `State(S)`をauthorityとして維持する。

## 6. Dependency種類

### 6.1 state_read

```text
A --state_read--> B
```

AがBのfinalized `State(S)`を読む関係。通常は同一Step execution DAG edgeを作らない。

### 6.2 same_step_dependency

```text
A --same_step_dependency--> B
```

BがAの同一Step前phase出力を必要とする関係。execution DAGへ反映し、cycleを禁止する。

### 6.3 event dependency

Aで確定したfactをBが後続処理へ使う関係。event自体はimmutable factであり、Bのstate mutationではない。

### 6.4 intent dependency

AがB ownerのstate変更を要求する関係。Bはintentを必ず採用する必要はなく、validation/conflict policyに従う。

### 6.5 shared invariant

複数domainが関与するcommit条件。owner不明の「共同state」を作る代わりに、ownerは一意に保ちつつcross-domain整合性を検証する。

## 7. DomainEvent

DomainEventはworld内で成立したfactを表す。

```text
DomainEvent {
  event_id,
  kind,
  source_domain,
  basis_step,
  subjects,
  spatial_scope,
  payload,
  causality_refs,
  detail_provenance
}
```

### 7.1 event_id

Phase 1のdeterministic identity規則に従い、worker sequenceやwall clockから生成しない。

### 7.2 basis_step

そのfactがどのauthoritative Step transitionに基づくかを表す。

### 7.3 causality_refs

必要に応じてsource Operation、parent Event、Entity、Config generation等へのstable referenceを持てる。

### 7.4 detail_provenance

factが`D0_ENTITY`等の詳細stateから得られたか、aggregate transitionから得られたかを追跡可能にする。ただしdetail levelの違いだけで同一world factの意味を変えない。

## 8. MutationIntent

MutationIntentはowner domainへstate transition候補を要求する。

```text
MutationIntent {
  intent_id,
  source_domain,
  target_domain,
  basis_step,
  target_scope,
  kind,
  payload,
  conflict_scope,
  semantic_priority,
  causality_refs
}
```

### 8.1 owner validation

`target_domain`はintent kindごとのauthoritative ownerでなければならない。owner不一致はcontract violationとして扱う。

### 8.2 conflict

同一target_scopeへ複数intentが競合する場合、Phase 1のdeterministic conflict resolutionへ写像する。

arrival order、worker completion order、Gateway identityをtie-breakerにしない。

## 9. SharedInvariant

shared invariantは次の形式で登録可能にする。

```text
SharedInvariantDefinition {
  invariant_id,
  participating_domains,
  read_requirements,
  severity,
  validation_phase,
  failure_policy
}
```

world-affecting整合性で`severity = commit_blocking`の場合、失敗時はcandidate `State(S+1)`をfinalizeしない。

Phase 3で少なくとも次の横断invariantを持つ。

- identity uniqueness
- spatial containment consistency
- owner/reference validity
- domain-defined conserved stock continuity
- cross-boundary transfer uniqueness
- Diver-resident binding uniqueness

## 10. Detail level contract

### 10.1 共通level

```text
D0_ENTITY
D1_LOCAL_AGGREGATE
D2_REGIONAL_AGGREGATE
D3_BOUNDARY_SUMMARY
```

これはsimulation semantic detailであり、render LODとは別物である。

### 10.2 domain detail descriptor

各domainはlevelごとに次を宣言する。

```text
DomainDetailLevelDescriptor {
  level,
  retained_state,
  omitted_or_aggregated_state,
  update_cadence_class,
  conserved_quantities,
  preserved_identity_classes,
  promotion_recipe,
  demotion_recipe,
  promotion_triggers,
  demotion_guards,
  publication_capability
}
```

### 10.3 update cadence

update frequencyはdetail levelに応じて低下させられるが、世界の権威ある時間基準自体を変更しない。

例えばD2が30 Stepごとに内部rate更新する場合でも、その更新はauthoritative `SimulationStep`上のdeterministic scheduleとして表現する。

## 11. Promotion

promotionは低detail stateから高detail stateをmaterializeするtransitionである。

### 11.1 promotion triggerの入力

triggerは原則として次から決定する。

- finalized `State(S)`
- scheduled world-affecting Operation
- effective simulation Config
- domain-owned deterministic trigger
- authoritative participation state

render camera位置、FPS、worker負荷、wall clockだけを直接triggerにしない。

### 11.2 promotion時の保持事項

promotionで次を捏造しない。

- persistent identity
- 人口
- domain-defined resource stock
- inventory/value/financial obligation
- family/ownership/contract relationship
- territory/legal status
- causal history anchor

### 11.3 deterministic materialization

aggregate-only populationから個体等を生成する必要がある場合、少なくとも次のstable contextを使う。

```text
MaterializationContext {
  world_id,
  source_aggregate_id,
  aggregate_lineage_generation,
  promotion_step,
  materialization_role,
  ordinal_or_semantic_key
}
```

具体hash/encodingはPhase 1の共通規則へ従う。

## 12. Demotion

### 12.1 demotion condition

各domainは次を満たす場合にのみdemotion可能とする。

- Config上のquiet/hysteresis条件を満たす
- active detail-required interactionがない
- unresolved world-affecting Operationがない
- commit-blocking invariant violationがない
- identity/obligationを失わずaggregateへ写像できる

### 12.2 demotion guard

少なくとも次はdomain-specific floorまたはdemotion guardを要求する。

- Diver-bound resident
- 進行中の建設・事故・戦闘・災害等で局所因果が高い領域
- active契約履行・輸送handoff・裁判・医療処置等で個体追跡が必要な対象
- boundary crossing中のidentity-bearing entity

## 13. Identity class

Detail controlのためEntityを少なくとも次のsemantic classへ分けられるようにする。

### 13.1 persistent identity-bearing

一度world history上で個体として確定した後、detail levelが下がってもidentityを維持する必要があるもの。

例:

- resident
- 所有・契約・債務・法的記録の対象となるasset
- 明示追跡中のvehicle
- 重要施設
- Diver binding対象

### 13.2 aggregate-native

常時個体identityを保持しなくても要件を満たすpopulation/stock。

例:

- 一部の野生動物population
- 植生cohort
- 粒状resource stock

aggregate-native対象をD0へ展開する場合も、展開結果はdeterministicでなければならない。

## 14. AggregateExchange

Detail境界または外部簡略領域との交換は次で表す。

```text
AggregateExchange {
  exchange_id,
  source_scope,
  target_scope,
  basis_step,
  exchange_kind,
  quantity_or_entities,
  conservation_class,
  handoff_state,
  causality_refs
}
```

### 14.1 identity-bearing handoff

identity-bearing entityは境界通過中にsourceとtargetの双方で同時authorityにならない。

logical state例:

```text
OWNED_BY_SOURCE
 -> TRANSFER_PREPARED
 -> TRANSFER_COMMITTED
 -> OWNED_BY_TARGET
```

同一Step内のexact transition順はPhase 4 schemaへ落とすが、duplicate/lossを許容しない意味論はPhase 3で固定する。

### 14.2 conserved flow

resource等のflowはsource減少とtarget増加を同一logical exchangeへ結び付ける。別々の独立eventとして処理して片側だけcommitしない。

## 15. Cross-domain causality

cross-domain因果は原則として次の流れで表現する。

```text
State(S)
 -> source domain calculation
 -> DomainEvent / MutationIntent
 -> deterministic merge
 -> target owner validation/apply
 -> shared invariant validation
 -> State(S+1)
```

複雑な因果が複数Stepに跨ることは許容する。現実上即時に見える因果でも、同一Stepでmutual direct writeする必要はない。

## 16. Publicationとの境界

Domainのpublication capabilityはauthoritative stateからのprojection可能性だけを宣言する。

- View向けpresentation stateをdomain authorityへ逆流させない
- prediction stateをWorldStateへ書かない
- detail levelをrender LODと混同しない
- publication coalesceでworld eventそのものを失ったことにしない

## 17. Persistence / Replay

Detail transitionはsimulation-affecting state transitionであるため、replayで同じdetail decisionとmaterialization結果へ到達できなければならない。

少なくとも次をreplay可能にする。

- promotion/demotionを決めたauthoritative input
- effective Config generation
- aggregate lineage
- persistent identity continuity
- cross-boundary exchange

physical storage encodingはPhase 4以降でよい。

## 18. Domain設計時の禁止事項

- private mutable stateへのcross-domain direct write
- implicit singleton/shared cacheをworld authorityにする
- wall clockをsimulation state transitionへ直接利用
- render cameraだけでauthoritative detailを変える
- CPU負荷だけで非決定論的にdetailを落とす
- demotion時にactive contract/ownership/family/identityを捨てる
- promotion時にstock不足を無視してentityを生成する
- aggregate flowを片側だけcommitする
- 同一Step cycleをworker timingで解消する

## 19. P3-01で確定した事項

- domain stateは単一ownerとする。
- cross-domain連携を`state_read` / `same_step_dependency` / `event` / `intent` / `shared_invariant` / `aggregate_exchange`へ分類する。
- `D0_ENTITY`〜`D3_BOUNDARY_SUMMARY`の4段階を共通semantic detail levelとする。
- persistent identity-bearing classはdemotionでもidentityを維持する。
- aggregate-native対象のmaterializationはdeterministicにする。
- render LODとsimulation detailを分離する。
- promotion/demotionはauthoritative state/Operation/Config/participation stateから決定し、wall clockやcameraだけへ依存させない。
- boundary exchangeはidentityまたはstockのduplicate/lossを禁止する。

## 20. P3-01残作業

- individual domainごとのstate partition一覧
- individual domainごとのevent/intent catalog
- same-Step dependency DAGの具体化
- detail levelごとのdomain-specific retained state
- promotion/demotion threshold class
- cross-domain shared invariant catalogの拡張
- Q001〜Q279の個別traceability登録

次の設計単位はP3-02 `spatial` / `environment`とする。
