# 詳細設計 Phase 4: Numeric Representation / Algorithm / Determinism

Status: In Progress / P4-05  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`  
Predecessors: Phase 1 determinism contract / Phase 3 domain designs / P4-01 registry

## 1. 目的

authoritative world calculationで使用する数値表現、座標、geometry、spatial query、motion/collision/pathfinding、自然環境、Resident、経済、制度、インフラ、queue solverを、platform/thread順に依存しない決定論的algorithmへ固定する。

Presentation/analyticsだけの計算にはbinary floating pointを使用可能だが、authoritative state transitionの入力/結果へそのまま逆流させない。

## 2. Numeric profile

Standard authoritative numeric profileは**integer / fixed-point first**とする。

### 2.1 Checked arithmetic

- int32/int64のoverflowをwrapさせない。
- multiply/divide intermediateはsigned/unsigned 128-bit integerを標準とする。
- 128-bitでもrange超過するschemaはbounded decompositionを使う。
- authoritative calculation overflowは`simulation.numeric-overflow`でStep abort。
- silent saturationはschemaが物理的clampを明示したfieldだけに許可。

### 2.2 Rounding

標準division rounding:

```text
round_ties_to_even
```

integer quotient/remainderから実装し、CPU floating rounding modeへ依存しない。

### 2.3 Fixed-point common types

```text
FixedQ32_32 := int64 / 2^32
RatioQ0_32  := uint32 / 2^32
ProbabilityPpm := uint32, 0..1_000_000
ProgressPpm    := uint32, 0..1_000_000
ConcentrationPpb := uint32, 0..1_000_000_000
```

`1.0`のRatioQ0_32は`0xffffffff`を使用するschemaと、exclusive upper boundaryを使うschemaを混在させない。標準比率fieldは原則ProbabilityPpm/ProgressPpmを使い、Q0.32はvector/math internalのみ。

## 3. Physical scalar units

```text
LengthMm                := int64 millimetres
VelocityUmPerSecond     := int64 micrometres/second
AccelerationUmPerSec2   := int64 micrometres/second^2
MassGram                := int64 grams
VolumeMillilitre        := int64 millilitres
EnergyMillijoule        := int64 millijoules
PowerMilliwatt           := int64 milliwatts
TemperatureMilliKelvin  := int32 millikelvin
PressurePascal          := int32 pascals
AngleMicroradian        := int32 microradians
AngularRateMicroradSec  := int64 microradians/second
CurrencyMicrounit       := int64 10^-6 currency unit
DurationMicrosecond     := uint64 microseconds
```

negativeが物理的に無効なstockはschema validationで>=0。

## 4. World coordinate frame

Authoritative root frame:

```text
frame_kind = world-centered-right-handed
origin = generated planet/world reference center
+Z = world north reference axis
+X = equator/reference-meridian direction
+Y = completes right-handed frame
```

position:

```text
Vec3Mm {
  x: int64,
  y: int64,
  z: int64
}
```

Latitude/longitude/altitudeはderived viewでありroot authorityではない。

### 4.1 Local frame

```text
RigidTransformV1 {
  translation: Vec3Mm,
  rotation: QuaternionQ30
}
```

parent transform chainはSpatial WorldFrame recordのstable orderで評価する。

## 5. Orientation

```text
QuaternionQ30 {
  x: int32,
  y: int32,
  z: int32,
  w: int32
}
```

component scale=`2^30`。

Normalization:

- squared normをInt128で計算。
- integer sqrt + round-to-evenでnormalize。
- canonical sign: `w > 0`。w=0ならx、次y、次zの最初のnon-zeroをpositive。
- zero quaternion invalid。

Direction vectorもQ30 signed int32を使用する。

## 6. Integer square root

`isqrt(n)`はnon-negative integerに対するfloor sqrt。

標準algorithm:

- bitwise restoring square-root、または結果がexactly同じNewton integer iteration。
- final condition `r^2 <= n < (r+1)^2`をassert。

implementation choiceで結果が変わらない。

## 7. Terrain geometry representation

Natural terrain authorityは**Sparse Brick Octree Signed Distance Field (SBO-SDF) v1**。

### 7.1 Brick

```text
TerrainBrickV1 {
  brick_id,
  level: uint8,
  cell_origin: SpatialCellKeyV1,
  sample_spacing_mm: uint32,
  sdf_mm: int32[9*9*9],
  surface_material_id: uint16[8*8*8],
  revision: uint64
}
```

8x8x8 cells、9x9x9 corner SDF samples。

negative SDF=solid、positive=void、zero=boundary。

### 7.2 Standard detail spacing

| detail | sample spacing |
|---|---:|
| D0 | 250 mm |
| D1 | 1000 mm |
| D2 | 8000 mm |
| D3 | 64000 mm |

promotion/demotionでlogical geometry volume/lineageを維持する。

### 7.3 Octree

- each internal node exactly 8 children。
- child index bits: x=bit0, y=bit1, z=bit2。
- child traversal 0..7 ascending。
- empty uniform nodeはsigned distance bound + material summaryでcollapse可能。
- SDF sample interpolationはtrilinear fixed-point Q32.32。

## 8. Spatial cell key

```text
SpatialCellKeyV1 {
  level: uint8,
  x: int32,
  y: int32,
  z: int32
}
```

Canonical order:

```text
(level, signed_bias(x), signed_bias(y), signed_bias(z))
```

signed_bias(v)=uint32(v) XOR 0x80000000。

## 9. Built geometry

Built structure authorityはterrain SDFへ焼き込まず、PhysicalBuilt partitionでprimitive/convex mesh collectionとして保持する。

Standard collision geometry:

```text
SphereV1
CapsuleV1
OrientedBoxV1
ConvexPolytopeV1
TriangleMeshStaticV1
```

- dynamic concave body禁止。concave dynamic objectはconvex decomposition。
- TriangleMeshStaticはstatic built/terrain-derived colliderのみ。
- vertices `Vec3Mm`。
- polygon/vertex orderはschema canonical order。

## 10. Spatial broad-phase index

Derived rebuildable indexは`HierarchicalAabbGridV1`。

Grid cell edge:

```text
L0 = 1 m
L1 = 8 m
L2 = 64 m
L3 = 512 m
L4 = 4096 m
```

Entity AABBを「AABBが最大8 cell以内へ収まる最小level」へ登録する。

Query:

1. relevant grid keysをcanonical orderで列挙。
2. candidate RecordIdをcollect。
3. RecordId bytewise ascendingでsort+unique。
4. exact narrow test。

hash table iteration orderをquery result orderへ使用しない。

## 11. Motion integration

Authoritative rigid-body linear integrationはsemi-implicit Euler。

For StepRate `n/d` steps/sec:

```text
dt = d/n seconds
v1 = v0 + round_even(a * d / n)
pos_delta_mm = round_even(v1_um_per_sec * d / (n * 1000))
pos1 = pos0 + pos_delta_mm
```

all intermediate Int128。

angular integrationもmicroradian/sec + QuaternionQ30 deterministic updateを使用する。

## 12. Collision solver

### 12.1 Broad phase

HierarchicalAabbGridV1。

pair key:

```text
(min(record_id), max(record_id))
```

bytewise ascendingでdedup/sort。

### 12.2 Narrow phase

- sphere/capsule/box: closed-form fixed-point。
- convex polytope pair: GJK distance/intersection, max 32 iterations。
- penetration: EPA max 32 iterations。
- static triangle mesh: deterministic BVH query + triangle tests。
- terrain: SDF conservative advancement, max 16 iterations。

GJK support tie: vertex canonical index最小。

iteration convergence threshold: 1 mm equivalent。

non-convergenceは`physical.solver-nonconvergent`として当該candidate rejectまたはStep abort（commit-blocking interaction）し、nondeterministic fallbackを使用しない。

### 12.3 Contact solve

Sequential impulse、max 12 iterations。

contact ordering:

```text
(body_pair_key, contact_feature_key)
```

friction/restitutionはppm fixed-point。

iteration countをperformance pressureでruntime変更しない。変更する場合SIMULATION Config schema change。

## 13. Pathfinding

標準:

- local: A* on deterministic navigation graph。
- long distance: hierarchical A* over region/transport graph。

Edge cost:

```text
TravelCostMicrosecond := uint64
```

Priority key:

```text
(f_cost, g_cost, node_id)
```

node_id bytewise ascending tie-break。

heuristicはadmissible integer distance / configured maximum speed floor。

closed/open set container iteration orderに依存しない。

## 14. Weather / atmosphere

Atmosphere D0/D1はlayered finite-volume grid。

state per cell:

- pressure Pa
- temperature mK
- humidity ppm
- wind VelocityUmPerSecond vector
- water vapor/liquid mass grams
- gas composition ppb

Update:

1. source/sink forcing
2. pressure-gradient acceleration
3. conservative advection flux
4. condensation/evaporation
5. precipitation generation
6. bounded diffusion

All flux values fixed-point/int64 with Int128 accumulation。

Flux across shared faceを一度計算し、cell A減少/cell B増加へ同一flux identityで適用する。

## 15. Climate

Climateはweather historyからdeterministic exponential/bucket aggregateを更新する。

floating exponentialを使用せず、integer recurrence:

```text
avg_next = avg + round_even((sample - avg) * alpha_num / alpha_den)
```

alphaはSIMULATION Config rational。

## 16. Surface water / groundwater

Surface hydrology:

- terrain SDFからflow graphを構築。
- cell outflow directionはminimum hydraulic head neighbor。
- equal head tieはSpatialCellKey canonical order。
- conservative volume transfer。
- overflow/floodはcapacity超過をadjacent cellへcanonical order allocation。

Groundwater:

- regional Darcy-like conductance graph。
- fixed-point head/conductivity。
- bounded Jacobi iteration 16回。
- iteration read basisをprevious iteration bufferに固定しparallel completionを無視。

## 17. Ocean

D0/D1 coastal/local:

- layered shallow-water finite-volume approximation。
- volume/momentum/salinity/thermal stock conservation。

D2/D3:

- regional cell stock/flow graph。

shared face fluxをsingle calculationしてopposite signへ適用。

## 18. Geology / erosion

Geology materialはstratum/voxel material stock。

- erosion/deposition: surface cell sediment capacity model。
- material transferはMassGram conservation。
- excavationはPhysicalBuilt→Spatial/Environment semantic transaction。
- collapse triggerはfixed-point support/stress threshold。

Geometry changeだけ、resource stockだけのpartial commit禁止。

## 19. Ecology

D0 individual identity-bearing animals/plants where required。

D1-D3 aggregate cohort:

```text
PopulationCount uint64
BiomassGram int64
birth_rate_ppm
death_rate_ppm
migration_rate_ppm
```

Update uses integer binomial expectation + addressable deterministic random for stochastic discrete realization。

random context includes species/cohort/step/process token, not loop consumption order。

## 20. Contaminant / hazard

Contaminant transport uses conserved MassGram + concentration ppb field。

Hazard intensity uses schema-specific fixed scale:

```text
HazardIntensityPpm 0..1_000_000
```

hazard domain emits event/intent; building damage/Resident injury/network outageを直接writeしない。

## 21. Resident physiology / health

Continuous scalar state is bounded integer scale。

```text
NeedLevelPpm
HealthCapacityPpm
PainPpm
StressPpm
FatiguePpm
```

Disease/injury progression:

- condition-specific state machine
- deterministic rate + addressable random event threshold
- all random checks keyed by condition_id/resident_id/step/event kind

same condition iteration orderでrandom stream consumptionしない。

## 22. Resident cognition

Standard model:

```text
Perception
 -> BeliefUpdate
 -> Need/GoalScore
 -> GoalSelection
 -> BoundedPlanSearch
 -> ActionIntent
```

### 22.1 Goal utility

utility=`int64` fixed score。

weighted sum uses Int128, final checked int64。

tie:

```text
utility descending
semantic_priority ascending
goal_id ascending
```

### 22.2 Planning

bounded GOAP/A*。

- max expanded node standard 256 per planning activation。
- node key `(f_cost, g_cost, action_token, stable_state_digest)`。
- no solution within bound: fallback behavior registry, not random arbitrary action。

planning bound is SIMULATION Config candidate for P4-06 validation。

## 23. Knowledge / belief

Belief confidence:

```text
ProbabilityPpm 0..1_000_000
```

Evidence update uses rational Bayesian-like odds approximation with lookup/rational arithmetic; binary64 log odds禁止。

Information delivery ≠ belief acquisition。delivery event→perception→belief update。

## 24. Skill / aptitude

Skill level/aptitude/practice:

```text
0..1_000_000 ppm
```

learning increment uses diminishing integer curve:

```text
increment = round_even(base_gain * (1_000_000 - skill) / 1_000_000)
```

skill cannot silently exceed bounds。

## 25. Market matching

Standard spot market usesdeterministic **batch call auction** per market cadence。

Order fields:

```text
side BUY|SELL
limit_price CurrencyMicrounit per unit
quantity int64 >0
owner ref
order_id 128-bit
eligible_step
```

Clearing:

1. buy sort: price descending, eligible_step ascending, order_id ascending。
2. sell sort: price ascending, eligible_step ascending, order_id ascending。
3. find price maximizing executable quantity。
4. quantity tie: minimizes absolute imbalance。
5. remaining tie: lowest candidate price。
6. allocation at clearing price by canonical order, pro-rata only where schema explicitly requests。

Money and quantity integer; no binary64 price。

## 26. Ledger / finance

Double-entry ledger invariant:

```text
sum(debits) == sum(credits)
```

per currency, per transaction。

CurrencyMicrounit checked int64。

ledger transaction entries canonical `(account_id, entry_kind, entry_id)`。

insufficient funds/credit is deterministic validation result, not negative-wrap。

## 27. Production / logistics allocation

Production recipe is integer stoichiometric recipe:

```text
inputs: material grams/count
outputs: material grams/count
work_requirement: integer work units
energy_requirement: millijoule
```

resource allocation uses canonical request order unless explicit market/priority policy。

Logistics path uses transport graph hierarchical A* + capacity reservation queue。

## 28. Law representation

Law/rule authority usesversioned declarative AST, arbitrary executable code禁止。

```text
RuleV1 {
  rule_id,
  jurisdiction_ref,
  priority: int32,
  effective_from_step,
  effective_until_step?,
  predicate_ast,
  effect_ast
}
```

Predicate node kinds:

```text
AND OR NOT
FACT_EQUALS
FACT_RANGE
SUBJECT_HAS_STATUS
RELATION_EXISTS
SPATIAL_WITHIN
TIME_STEP_RANGE
```

Effect kinds:

```text
CLASSIFY
PERMIT
PROHIBIT
CREATE_CLAIM
CREATE_OBLIGATION
AUTHORIZE_ENFORCEMENT
```

Rule resolution order:

```text
explicit jurisdiction applicability
priority ascending
specificity descending
rule_id ascending
```

conflicting terminal effects without declared resolver -> legal resolution conflict event, not last-writer-wins。

## 29. Governance selection / voting

Vote tally integer counts/weights。

candidate tie uses election law-defined tie resolver; standard fallback is deterministic seeded draw only if law explicitly declares random tie-break。

random context includes election_id and tie candidate set digest。

## 30. Security / enforcement

Institutional order does not directly move/damage bodies。

Governance emits authorized intent; Resident/PhysicalBuilt executes subject to physical state。

investigation/evidence list canonical evidence id order。

## 31. Infrastructure graph

Network topology uses stable node/edge IDs and integer capacity/cost。

Common shortest path:

- Dijkstra for non-negative exact cost。
- A* where admissible spatial heuristic available。

Tie key `(distance, node_id)`。

## 32. Capacity allocation / queues

Standard queue key:

```text
(eligible_step, semantic_priority, request_id)
```

arrival wall-clock/orderを使わない。

Capacity divisible resource:

- canonical sequential allocation default。
- fairness-required service may use deterministic weighted deficit round robin with integer credits and stable participant order。

## 33. Power network

D0/D1 electrical service uses linearized DC-like flow approximation on connected graph where applicable。

- conductance/susceptance fixed-point Q32.32。
- nodal solve: bounded Jacobi iteration 32回。
- each iteration reads previous full vector and writes next vector。
- node iteration canonical ID order for diagnostic/reduction。
- non-convergence -> service degraded/failure state according to schema; no platform-specific solver fallback。

D2/D3 use capacity/energy stock flow aggregate。

## 34. Water service

Pipe/service network:

- capacity integer ml/step equivalent derived by StepRate rational。
- pressure/head fixed LengthMm。
- bounded Jacobi flow balance 32 iterations。
- demand allocation by queue/canonical priority。

natural hydrology stateはEnvironment ownerのまま。

## 35. Communication / information delivery

Delivery uses graph path + queue/capacity。

message truth/claim semanticsをdelivery networkが変更しない。

Delivery lifecycle ordering by:

```text
eligible_step, priority, delivery_id
```

## 36. Deterministic reduction rules

### Integer sum

- Int128 accumulator。
- inputs SameStepOrderKey or RecordId canonical order。
- final checked cast。

### min/max

- value compare。
- equal tie by record/id canonical order。

### average

```text
round_even(sum / count)
```

### weighted average

Int128 numerator/denominator, denominator >0, round-even。

### vector sum

component-wise Int128, same input order。

### histogram

bucket index pure integer function; bucket counts uint64 checked。

## 37. Parallel solver rule

parallelization allowed when:

- each work item reads frozen input。
- outputs keyed by stable target identity。
- merge sorted by canonical key。
- iterative solver uses explicit double buffer/barrier per iteration。

parallel tree floating reduction禁止。

## 38. Addressable random

All stochastic domain algorithm usesPhase 1 RandomContext。

Minimum context:

```text
world_id
step
domain_token
process_token
subject_id / aggregate_id
local semantic ordinal
```

random resultをsequence-consume shared PRNGから得ない。

## 39. Algorithm version registry

```text
AlgorithmId := StableToken
AlgorithmVersion := SchemaVersion
```

Initial standard ids:

```text
spatial.sbo-sdf.v1
spatial.hierarchical-aabb-grid.v1
physical.semi-implicit-euler.v1
physical.gjk-epa.v1
physical.sequential-impulse.v1
path.hierarchical-astar.v1
environment.finite-volume.v1
environment.hydrology-graph.v1
resident.utility-goap.v1
society.batch-call-auction.v1
governance.rule-ast.v1
infrastructure.graph-capacity.v1
```

simulation-affecting algorithm major changeはsaved world compatibility/migration対象。

## 40. Failure codes

```text
simulation.numeric-overflow
simulation.numeric-invalid
simulation.iteration-limit
spatial.geometry-invalid
spatial.sdf-invalid
physical.solver-nonconvergent
physical.contact-overflow
path.no-route
path.search-budget-exceeded
environment.solver-nonconvergent
resident.plan-budget-exceeded
market.no-clearing
market.ledger-unbalanced
governance.rule-conflict
infrastructure.flow-nonconvergent
```

## 41. P4-05 current acceptance status

確定済み:

- integer/fixed-point standard profile
- authoritative unit scales
- root coordinate/orientation representation
- SDF terrain representation/detail spacing
- built geometry/collision shape boundary
- spatial broad-phase
- motion/collision/contact/pathfinding algorithm
- atmosphere/hydrology/ocean/geology/ecology algorithm family
- Resident physiology/cognition/skill algorithm
- deterministic market/ledger/production policy
- law AST/resolution
- infrastructure graph/queue/power/water algorithms
- deterministic reduction/parallel/random rules

残作業:

- 97 partition record payload schema
- domain operation/event/intent catalog
- Config keys for planner/solver iteration where operator-tunable
- P4-06 performance budget cross-review

blocker: なし。
