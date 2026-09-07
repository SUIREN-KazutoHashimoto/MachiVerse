# 詳細設計 Phase 4: Algorithm / Domain Schema Completion Review

Status: Complete / P4-05 Completion Review  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`

## 1. 目的

P4-05で具体化したauthoritative numeric representation、geometry、solver、97 partition payload schema、Operation/Event/Intent/Transaction registryを横断監査し、performance/test設計へ移行可能か判定する。

本書をP4-05 completion判定の正本とする。

## 2. 成果物

- `phase4-algorithm-determinism.md`
- `phase4-domain-payload-schema.md`
- `phase4-domain-operation-event-intent-catalog.md`
- 本書

## 3. Numeric determinism audit

authoritative calculationはinteger/fixed-point firstとし、標準scalar unitを固定した。

- position: millimetre int64
- velocity/acceleration: micrometre based int64
- mass: gram int64
- volume: millilitre int64
- energy/power: milli-unit int64
- temperature: millikelvin int32
- pressure: pascal int32
- probability/progress/concentration: bounded integer scale
- money: 10^-6 currency unit int64
- multiply/divide intermediate: signed/unsigned 128-bit integer
- division: round ties to even

binary floating resultをauthoritative stateへ直接commitしない。

overflow/non-finite相当状態をsilent saturationせずStep abort/validation rejectする。

判定: PASS。

## 4. Coordinate / geometry audit

- world-centered right-handed root frame
- `Vec3Mm`
- `QuaternionQ30`
- terrain: Sparse Brick Octree SDF v1
- built geometry: primitive/convex/static mesh separation
- hierarchical AABB grid broad phase
- stable child/grid/record traversal order

single heightmapへauthoritative terrainを縮退させず、cave/tunnel/overhangを保持可能。

判定: PASS。

## 5. Physical solver audit

- semi-implicit Euler
- fixed iteration GJK/EPA
- deterministic terrain conservative advancement
- sequential impulse contact solve
- contact/pair stable order
- hierarchical A* pathfinding
- explicit non-convergence failure

thread completion、container iteration、CPU floating behaviorにresultを依存させない。

判定: PASS。

## 6. Natural environment audit

- atmosphere finite-volume
- climate integer recurrence
- hydrology flow graph/Jacobi
- ocean layered finite-volume/regional aggregate
- geology erosion/deposition material conservation
- ecology cohort + addressable deterministic random
- contaminant conserved stock
- hazard event/intent separation

shared-face fluxをsingle factとして両cellへ反対符号適用し、mass/volumeの片側commitを禁止する。

判定: PASS。

## 7. Resident audit

- physiology/health bounded integer state
- disease/injury condition state machine
- addressable random threshold
- perception -> belief -> goal -> bounded GOAP -> action intent
- stable utility/plan tie-break
- skill/aptitude bounded learning curve
- physical resultはPhysicalBuilt owner

Diver controlでもResident identity/health/world ruleを別systemへ分離しない。

判定: PASS。

## 8. Society / economy audit

- deterministic batch call auction
- integer price/quantity
- double-entry ledger
- integer production recipe
- logistics path/capacity reservation
- property/contract/payment/physical handoffを別ownerとしてtransaction連携

arrival orderやfloating-price roundingでmarket resultを変えない。

判定: PASS。

## 9. Governance / security audit

- law/ruleはversioned declarative AST
- arbitrary executable law code禁止
- jurisdiction/priority/specificity/rule-id stable resolution
- unresolved terminal conflictはexplicit conflict result
- institutional orderとphysical resultを分離

判定: PASS。

## 10. Infrastructure audit

- stable graph ID
- Dijkstra/A*
- canonical queue order
- deterministic weighted deficit round robin option
- bounded Jacobi power/water solver
- communication delivery graph/queue
- outage dependency causality

physical facility conditionとlogical service availabilityを分離する。

判定: PASS。

## 11. 97 partition payload audit

`phase4-domain-payload-schema.md`でP4-01全97 partitionへminimum authoritative payloadを割り当てた。

| domain | expected | payload schema |
|---|---:|---:|
| spatial | 8 | 8 |
| environment | 13 | 13 |
| physical_built | 11 | 11 |
| participation | 5 | 5 |
| resident | 13 | 13 |
| society_economy | 16 | 16 |
| governance_security | 17 | 17 |
| infrastructure_information | 14 | 14 |
| total | 97 | 97 |

owner変更・欠落なし。

判定: PASS。

## 12. Secondary index audit

P4-05で追加したdomain secondary indexは初期標準すべて`DERIVED_REBUILDABLE`。

- authoritative recordを置換しない。
- canonical record orderから再構築可能。
- index corruptionでworld authorityを失わない。

判定: PASS。

## 13. Operation/Event/Intent registry audit

standard registry:

- OperationKind: 69
- EventKind: 129
- IntentKind: 63
- CrossDomainTransactionKind: 17

Intentはtarget ownerとallowed source pairを検証し、generic foreign mutable write escape hatchを設けない。

判定: PASS。

## 14. Cross-domain transaction audit

Phase 3主要transactionを全17 stable tokenへ固定した。

- mining/excavation
- construction/demolition
- birth/death/disease/food
- market sale + delivery
- information/public record
- crime/justice/border
- disaster/outage
- medical/employment
- military

required participant/invariant失敗時、参加partition effectを部分commitしない。

判定: PASS。

## 15. Detail level audit

D0〜D3はalgorithm/payload双方で同じidentity/reference/conservation ruleを使用する。

- terrain spacingはdetail levelごとに固定
- persistent identity-bearing recordをdemotionだけで削除しない
- aggregate materializationはstable lineage/context
- promotion/demotionはcamera/FPS/worker availabilityをworld inputにしない

判定: PASS。

## 16. Parallel execution audit

parallel workは:

1. frozen input read
2. stable target key付きoutput
3. canonical merge
4. iterative solverはdouble-buffer/barrier

を要求する。

worker count 1〜16でlogical resultを同一化可能。

判定: PASS。

## 17. Random audit

stochastic処理はPhase 1 addressable randomを使用する。

contextへworld/step/domain/process/subject/local semantic ordinalを含め、shared mutable PRNG consumption順を使用しない。

判定: PASS。

## 18. Algorithm version audit

simulation-affectingalgorithmへstable AlgorithmId/versionを付与した。

algorithm semantic変更でsaved world resultが変化する場合、Configだけでsilent切替せずcompatibility/migration対象とする。

iteration上限等、determinismに影響する標準solver constantはalgorithm versionの一部とし、P4-05初期版ではoperator runtime tuning対象にしない。domain update cadence/detail thresholdだけをP4-03 SIMULATION Configで調整する。

判定: PASS。

## 19. P4-06 handoff

P4-06はalgorithm semanticを変更せず、次をbudget化する。

- 30Hz標準Step wall-clock budget
- domain compute share
- memory/record budget
- solver work/promotion limits
- snapshot/history throughput
- publication bandwidth
- queue/backpressure

予算超過時もsolver iteration数やworld resultをoperational pressureで変えない。

## 20. Completion criteria

| criterion | result |
|---|---|
| exact authoritative numeric representation | PASS |
| coordinate/geometry structure | PASS |
| collision/motion/path algorithm | PASS |
| natural environment solver family | PASS |
| Resident algorithm | PASS |
| society/economy algorithm | PASS |
| governance/law algorithm | PASS |
| infrastructure/queue algorithm | PASS |
| 97 partition payload schema | PASS |
| Operation/Event/Intent catalog | PASS |
| deterministic reduction/random | PASS |
| cross-domain atomicity | PASS |
| unresolved P4-05 blocker | 0 |

## 21. Completion decision

P4-05を`Complete`と判定する。

P4-06 performance budgetでdefault cadence/budget値を検証するが、P4-05 semantic algorithm/ownershipを変更するblockerはない。