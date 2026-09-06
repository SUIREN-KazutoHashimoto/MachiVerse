# 詳細設計 Phase 3: Cross-Domain因果・Aggregation設計

Status: Complete / P3-08  
Tracking: Issue #15  
Parent: `phase3-world-domain-design.md`  
Common contract: `phase3-domain-common-contract.md`

## 1. 目的

本書はPhase 3各domainのstate ownershipを横断し、same-Step dependency、DomainEvent / MutationIntent、shared invariant、detail boundary exchangeを一貫した因果グラフへ統合する。

狙いは次の3点である。

1. domain間の循環依存を、`State(S)` readと明示的same-Step edgeへ分解する。
2. 複数domainへ同時に意味を持つworld eventを、片側だけcommitされないsemantic transactionとして扱う。
3. detail promotion/demotionやregion boundaryでidentity・stock・obligation・flowを失わない。

## 2. Stable domain token / rank

same-Step canonical orderのtie-breakに使用するPhase 3標準domain rankを固定する。

| rank | domain_token | role |
|---:|---|---|
| 10 | `spatial` | 3D scope / terrain geometry |
| 20 | `environment` | natural field / stock / hazard |
| 30 | `physical_built` | physical presence / built world |
| 40 | `participation` | Diver binding/control context |
| 50 | `resident` | individual life / cognition / action |
| 60 | `society_economy` | organization / contract / economy |
| 70 | `governance_security` | institution / law / security / military authority |
| 80 | `infrastructure_information` | network/service/delivery/capacity |

rankはsemantic tie-breakであり、全Stepをこの順番でsingle-thread実行する指定ではない。

新domain/addonは既存rankの意味を変更せず、互換性を保つ登録規則が必要となる。具体extension registryは後続Phase。

## 3. Global logical Step graph

Phase 2 Step pipeline内でPhase 3 domainを次のlogical graphへ配置する。

```text
State(S) finalized
  |
  v
PREPARE
  - effective Config
  - scheduled Operations
  - internal events
  - detail/cadence decisions
  |
  v
ENVIRONMENT
  - environment natural transition
  - environment -> spatial geometry intents where needed
  |
  v
PHYSICAL
  - stable spatial geometry basis
  - physical movement/work/damage/fire
  |
  v
AGENT_INTERNAL
  - participation control context
  - resident body/perception/cognition/goal update
  |
  v
AGENT_ACTION
  - Resident/Diver/organization actions -> intents
  |
  +---------------------+
  |                     |
  v                     v
SOCIAL_INSTITUTIONAL   INFRASTRUCTURE_SERVICE prerequisites
  - society/economy      - prior service/network state reads
  - governance/security
  |
  +----------+----------+
             v
INFRASTRUCTURE_SERVICE
  - transport/utility/communication/facility allocation
             |
             v
CONSEQUENCE
  - cross-domain result propagation
  - shared semantic transaction assembly
             |
             v
VALIDATE
  - global invariant validation
             |
             v
DeterministicMerge / ApplyCandidate State(S+1)
```

実際には独立partitionを並列化できる。edgeがないdomain workはconcurrent calculation可能である。

## 4. Cycle elimination rule

worldには自然なfeedback loopが多数存在する。

例:

```text
weather -> economy -> land use -> environment -> weather
health -> work -> income -> food access -> health
power -> communication -> repair dispatch -> power repair
```

これらを同一Stepのmutable call graphとして実装しない。

原則:

```text
A(S) reads B(S)
B(S) reads A(S)
 -> A_candidate(S+1), B_candidate(S+1)
```

同一Step内で結果が必要な場合だけ、producerがmerge済みfactを発行し、consumerへ`same_step_dependency` edgeを追加する。

edgeを追加する条件:

- same-Stepでなければsemantic invariantを破る
- producer outputがimmutable merged factにできる
- dependency graphがacyclic、またはbounded coupled-resolutionとして明示される

## 5. Cross-domain interaction category

各interactionは次のいずれかへ分類する。

### 5.1 `state_read`

State(S)のpublished read model参照。

例:

- Resident reads local weather truth for thermal exposure calculation
- Infrastructure reads physical asset condition

### 5.2 `same_step_dependency`

同一Stepのmerge済みfactを後phaseへ渡す。

例:

- precipitation fact -> hydrology
- Participation control context -> Resident action

### 5.3 `event`

既に発生したfactを通知する。

例: `ResidentDied`, `StructureCollapsed`。

### 5.4 `intent`

別ownerへmutationを要求する。

例: PhysicalBuilt -> Spatial `geometry.carve`。

### 5.5 `shared_invariant`

複数candidate stateをcommit前に同時検証する。

### 5.6 `aggregate_exchange`

region/detail boundaryを跨ぐstock/entity/flow handoff。

## 6. Semantic transaction model

複数domainへ不可分の意味を持つ変更は次のlogical envelopeへまとめる。

```text
CrossDomainTransaction {
  transaction_id,
  basis_step,
  cause_refs,
  participant_domains,
  intent_refs,
  candidate_effect_refs,
  required_invariants,
  status
}
```

これはdatabase transaction implementationを固定するものではない。

`VALIDATE`でrequired invariantが1つでも失敗した場合、world-facing participant effectを部分commitしない。

## 7. Mining / excavation transaction

```text
Resident/Organization work decision
 -> PhysicalBuilt actual mining work
 -> Spatial carve/deform intent
 -> Environment geology/resource extraction intent
 -> PhysicalBuilt extracted material handoff candidate
 -> SocietyEconomy inventory/ownership consequence
 -> VALIDATE
```

必須invariant:

- terrain solid/void consistency
- geology/material stock continuity
- resource deposit non-double-consumption
- extracted physical material quantity consistency
- economic inventory physical link

禁止:

- geometryだけ削れてmaterialが消える
- depositだけ減って採掘物が存在しない
- workなしにresource inventoryが増える

## 8. Construction transaction

```text
Society contract / Governance permit state
 -> Resident workers + Physical equipment/material availability
 -> PhysicalBuilt construction work
 -> optional Spatial fill/carve
 -> optional Environment soil/water effect
 -> BuiltStructure stage progress
 -> Infrastructure service onboarding when complete
 -> Society fulfillment/payment consequence
```

permit/contractはphysical progressの必要条件になり得るが、それだけでbuildingを生成しない。

## 9. Demolition / ruin transaction

```text
Demolition authority/decision
 -> Physical work
 -> structure component removal
 -> salvage/waste physical stock
 -> Society inventory/ownership updates
 -> optional Spatial geometry revision
 -> Infrastructure topology invalidation
```

structure history/ruin identityを理由なく削除しない。

## 10. Birth transaction

```text
Resident pregnancy/body state
 -> birth condition/event
 -> deterministic Resident creation context
 -> new ResidentId
 -> family/parent relations
 -> PhysicalPresence creation
 -> Society/Governance registration process may follow separately
```

必須invariant:

- one birth creation context -> one ResidentId
- new ALIVE Residentの必要physical presence
- parent refs validity

公的出生登録はbirth truthと別event/processであり、自動的なperfect recordにしない。

## 11. Death transaction

```text
Resident health/physical consequence
 -> Resident lifecycle DECEASED
 -> Physical body/remains state transition
 -> Participation binding becomes non-operable if bound
 -> Society inheritance/employment/contract consequences
 -> Governance case/record consequences where applicable
 -> burial/handling later world processes
```

ResidentId/history/family relationを削除しない。

Diver-bound Residentでも通常死亡を適用する。

## 12. Disease transmission transaction

```text
Environment pathogen condition / infected Resident
 + Physical contact/proximity
 + Resident susceptibility/immunity
 -> exposure fact
 -> Resident infection transition candidate
```

contactだけで自動感染、またはinfection stateだけで全近隣Residentへ自動伝播しない。

## 13. Food / consumption transaction

```text
Resident decision
 -> economic access/purchase or household claim
 -> Physical item access
 -> actual consume action
 -> physical stock decrement
 -> Resident nutrition/hydration consequence
 -> waste/byproduct candidate
```

「購入した」と「摂取した」を同一にしない。

## 14. Market sale / physical delivery transaction

```text
market/contract agreement
 -> payment obligation/settlement
 -> shipment/delivery obligation
 -> Infrastructure route/service allocation
 -> Physical cargo movement
 -> arrival/transfer
 -> property/inventory fulfillment
```

remote sale時に商品をteleportしない。

paymentとdeliveryは別lifecycleであり、contractが両方のfulfillment stateを結ぶ。

## 15. Information transmission transaction

```text
World fact / actor belief
 -> Society InformationClaim
 -> communication/media delivery request
 -> Infrastructure channel routing/capacity/delay
 -> delivered fact
 -> Resident perception/receipt
 -> Resident interpretation/belief update
```

必須separation:

- Core truth
- claim content
- delivery
- receipt
- belief

## 16. Public record transaction

```text
world/institutional event
 -> actor/authority creates record content
 -> RecordArtifact persisted/distributed
 -> later retrieval
 -> Resident/Organization interprets record
```

recordをCore truthのautomatic mirrorにしない。

recordの誤り、遅延、欠落、紛失を許容する。

## 17. Crime / justice transaction

```text
Resident/Organization action
 -> Physical/world fact
 -> possible witness/perception/report
 -> Governance applicable-law classification
 -> incident/investigation
 -> evidence collection
 -> judicial process
 -> enforcement order
 -> actual Physical/Resident execution
```

行為発生時に即「犯罪確定+penalty」を付与しない。

## 18. Border crossing / smuggling transaction

```text
Resident/vehicle physical journey
 -> Spatial boundary crossing candidate
 -> Governance border rule/effective-control context
 -> Infrastructure checkpoint capacity/service
 -> Resident/Organization action choice
 -> admitted/denied/evaded physical result
 -> Governance/Society consequence
```

territorial claim、effective control、actual crossingを分離する。

## 19. Natural disaster cascade

```text
Environment hazard driver
 -> Spatial geometry / water / atmosphere change
 -> PhysicalBuilt damage/blockage/fire
 -> Resident injury/perception/action
 -> Infrastructure outage/capacity reduction
 -> Society production/logistics/market disruption
 -> Governance emergency/security response
 -> recovery actions
```

hazard sourceが被害を全domainへ直接書き込まない。各ownerが自身stateのconsequenceを計算する。

## 20. Infrastructure cascading outage

```text
Physical asset failure
 -> Infrastructure service outage
 -> dependency propagation
 -> downstream facility capacity reduction
 -> Resident/Society/Governance consequence
 -> repair dispatch/resource allocation
 -> Physical repair
 -> service recovery
```

power <-> communication等のfeedbackはnext-step feedbackまたはbounded explicit coupled resolverへ制限する。

## 21. Medical service transaction

```text
Resident health need
 -> Resident seek-care decision
 -> Society/Governance eligibility/payment context
 -> Infrastructure facility queue/capacity
 -> Physical staff/equipment/medicine access
 -> treatment action
 -> Resident health response
```

施設を選択しただけで治療完了にしない。

## 22. Employment / physical work transaction

```text
Employment relation
 -> work schedule/opportunity
 -> Resident decision/availability
 -> Physical movement to workplace
 -> tools/material/equipment access
 -> actual work
 -> production/service result
 -> wage/payment obligation
 -> Resident skill/fatigue/health consequence
```

## 23. Military operation transaction

```text
Governance mission/order
 -> Infrastructure command communication / transport
 -> Society supply/logistics obligation
 -> Resident soldier decision/morale/skill
 -> Physical movement/combat/equipment use
 -> injury/damage/material consumption
 -> territorial/effective-control consequence
 -> political/economic/social feedback
```

war outcomeをsingle abstract military scoreだけでcommitしない。

## 24. Detail boundary ownership

各boundary exchangeはsource authorityを一意にする。

| Exchange | primary owner |
|---|---|
| terrain/detail interface | `spatial` |
| air/water/ocean/ecological flux | `environment` |
| person/vehicle/item physical crossing | `physical_built` |
| Diver binding | `participation` persistent/global |
| Resident identity/lifecycle | `resident` persistent/global |
| goods/money/contract economic flow | `society_economy` |
| jurisdiction/order/case boundary | `governance_security` |
| transport/utility/communication service flow | `infrastructure_information` |

## 25. Boundary handoff protocol semantics

identity-bearing handoff:

```text
SOURCE_AUTHORITY
 -> TRANSFER_PREPARED
 -> transfer fact committed
 -> TARGET_AUTHORITY
```

aggregate stock flow:

```text
source stock decrement
 + boundary in-flight/flow record
 + target stock increment
```

同一logical exchangeへ結び付ける。

boundaryの実装が1 region storeから別storeへ移動するかはPhase 4/implementation detail。

## 26. Universal conservation classes

Phase 3では次をconservation classとして扱う。

### 26.1 `IDENTITY`

Resident、persistent organization、building、vehicle、contract、binding等。

禁止: duplicate / silent disappearance / identity replacement during detail transition。

### 26.2 `STOCK`

water、resource、material、goods、money等。

禁止: reasonless create/destroy/double count。

### 26.3 `OBLIGATION`

contract、debt、court order、shipment、reservation、employment duty等。

禁止: detail demotionでsilent completion/cancel。

### 26.4 `FLOW`

water/air/goods/traffic/information delivery等。

禁止: boundaryでdouble count/loss without modeled loss cause。

### 26.5 `PROVENANCE`

information/record/evidence/history cause chain。

禁止: truth/claim/record/beliefの混同。

## 27. Global shared invariant catalog

| ID | invariant |
|---|---|
| `INV-XD-01` | semantic field has one authoritative owner |
| `INV-XD-02` | persistent identity is not recreated by promotion |
| `INV-XD-03` | source/target boundary authority is unique |
| `INV-XD-04` | material/resource/water/money stock changes have cause-linked source/sink |
| `INV-XD-05` | active obligation is preserved across detail transition |
| `INV-XD-06` | Core truth, social claim, delivery, Resident receipt/belief remain distinct |
| `INV-XD-07` | institutional permission/order is distinct from physical capability/execution |
| `INV-XD-08` | physical possession/location is distinct from legal/economic ownership |
| `INV-XD-09` | territorial claim/jurisdiction/effective control/physical presence remain distinct |
| `INV-XD-10` | Diver binding cannot bypass normal Resident/world rules |
| `INV-XD-11` | same-Step output order does not depend on worker completion/network arrival |
| `INV-XD-12` | promotion/demotion cannot make region overlap into dual authority or gap into no authority |

## 28. Cross-domain causality matrix

| Producer | Consumer | Type | Main meaning |
|---|---|---|---|
| spatial | environment | state_read/event | terrain/boundary for natural fields |
| environment | spatial | intent | erosion/deposition/collapse geometry |
| spatial | physical_built | state_read/event | terrain/support/connectivity |
| physical_built | spatial | intent | excavation/fill/deform |
| environment | physical_built | state_read/event | weather/water/hazard forcing |
| physical_built | environment | intent/event | emission, structure effect, intake/use cause |
| participation | resident | same_step context | Diver control / absence policy |
| resident | physical_built | intent | movement/action/work |
| physical_built | resident | event | movement/contact/injury exposure |
| resident | society_economy | intent | transaction/work/social choices |
| society_economy | resident | event | income/contract/education/reputation exposure |
| resident | governance_security | intent/event | institutional action/report/world act |
| governance_security | resident | event/intent | law/order/service/legal consequence |
| resident | infrastructure_information | intent | service/delivery/transport request |
| infrastructure_information | resident | event | delivered info/service/queue result |
| society_economy | physical_built | intent | production/delivery/material use |
| physical_built | society_economy | event | actual production/transfer/damage |
| society_economy | governance_security | intent/state | tax/legal/property/treaty/economic request |
| governance_security | society_economy | event/state | rule/tax/sanction/permission |
| society_economy | infrastructure_information | intent | shipment/service/info distribution request |
| infrastructure_information | society_economy | event | delivery/logistics/service result |
| governance_security | infrastructure_information | intent/state | public service/border/dispatch/command requirement |
| infrastructure_information | governance_security | event | capacity/delivery/execution support result |
| physical_built | infrastructure_information | event | asset condition / actual movement |
| infrastructure_information | physical_built | intent | service movement/repair/operation request |
| environment | infrastructure_information | state/event | source resource/weather/hazard |
| infrastructure_information | environment | intent | intake/discharge/emission-linked service action |

## 29. Detail promotion order

複数domainを同時promotionする場合のsemantic restore順:

```text
1. Spatial scope/geometry
2. Environment natural fields/stocks
3. PhysicalBuilt structures/presence
4. Resident persistent individual state
5. Participation binding/control context
6. SocietyEconomy obligations/market/organization
7. GovernanceSecurity institutions/cases/control
8. InfrastructureInformation network/service/deliveries
9. Cross-domain invariant validation
```

これはrestore dependency順であり、runtime domain rankと同義ではない。

archiveが存在するstateをdeterministic generationで上書きしない。

## 30. Detail demotion order

```text
1. freeze boundary/in-flight exchanges
2. identify persistent identities/obligations
3. produce domain aggregates
4. persist detail archive anchors
5. reconcile stock/flow totals
6. install lower-detail authority
7. release higher-detail transient state
```

途中で失敗した場合はdemotion前authorityを維持する。

## 31. Cross-domain demotion guard

次のいずれかがactiveなら関係domainのdemotionを延期できる。

- Diver active control
- birth/death transition
- excavation/construction/demolition
- active fire/flood/collapse/disaster
- medical emergency/procedure
- physical boundary crossing
- unsettled payment/delivery/contract
- arrest/trial/enforcement
- battle/military transfer
- critical infrastructure outage cascade
- in-flight communication/record transaction requiring detail

## 32. Determinism review

cross-domain outcomeへ次を使用してはならない。

- wall clock
- thread completion order
- queue physical order without canonical key
- Gateway identity
- Master identity
- View camera/FPS
- storage partition order
- hash-map iteration order

すべてのsame-Step conflictはPhase 1 canonical ordering/conflict policyへ写像する。

## 33. Phase 4 handoff

Phase 4で確定する事項:

- CrossDomainTransaction wire/internal schema
- invariant validation execution graph
- rollback/candidate state implementation strategy
- boundary exchange concrete schema per conservation class
- coupled subsystem numerical iteration rules
- aggregate/promotion archive storage structures
- exact conflict-scope normalization

これらは本書で固定したowner、cycle elimination、conservation class、semantic transactionを変更してはならない。
