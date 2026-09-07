# 詳細設計 Phase 3: Physical / Built Domain設計

Status: Complete / P3-03  
Tracking: Issue #15  
Parent: `phase3-world-domain-design.md`  
Common contract: `phase3-domain-common-contract.md`  
Spatial dependency: `phase3-spatial-domain-design.md`  
Environment dependency: `phase3-environment-domain-design.md`

## 1. 目的

`physical_built` domainは、MachiVerseのfull 3D worldに存在する動的physical presence、移動、占有、基本衝突、建築・室内・開口部、施工、解体、物品所在、損傷、保守、built-material combustionをauthoritative stateとして所有する。

本domainは「物理エンジンそのもの」を意味しない。標準要件で必要な3D因果と実体性をsemantic contractとして定義し、exact rigid-body solver、collision library、navmesh等はPhase 4以降へ持ち越す。

## 2. Responsibility / Non-responsibility

### 2.1 PhysicalBuiltが所有する責務

- movable physical presenceのauthoritative pose/location
- local movement feasibilityとbasic collision/occupancy
- vehicle/item/equipment等のphysical condition
- building/room/passages/openingsのbuilt geometryとphysical state
- door/window/gateのopen/close/lock等physical mechanism state
- container/shelf/vehicle/carried等のphysical containment
- construction/demolition worksiteとphysical progress
- building/item degradation、damage、repair state
- built/item materialのfire/combustion state
- construction/demolitionによるmaterial physical handoff
- Spatial terrainへのexcavation/fill/deform intent生成

### 2.2 PhysicalBuiltが所有しない責務

- natural terrain solid/void geometry: `spatial`
- geology、soil、water、weather、smoke transport、natural resource deposit: `environment`
- resident health/decision/perception: `resident`
- ownership、price、contract、employment、commercial inventory valuation: `society_economy`
- law、permit、institutional access authority: `governance_security`
- road/rail/water/power/communication service network operation: `infrastructure_information`
- Diver session/control admission: Gateway / `participation`
- render transform interpolation/prediction: General View

## 3. DomainDefinition

```text
DomainDefinitionV1 physical_built {
  domain_token = "physical_built"
  domain_family = "physical_built"
  state_partitions = [
    physical.presence,
    physical.occupancy,
    built.structure,
    built.space,
    built.opening,
    physical.container_location,
    built.worksite,
    physical.condition,
    physical.combustion,
    physical.material_handoff,
    physical.lineage
  ]
  update_phases = [PREPARE, PHYSICAL, AGENT_ACTION, CONSEQUENCE, VALIDATE]
}
```

## 4. PhysicalPresence

### 4.1 state

```text
PhysicalPresence {
  presence_id,
  subject_ref,
  subject_class,
  spatial_frame_ref,
  pose_state,
  motion_state,
  occupancy_shape_ref,
  containment_ref?,
  support_or_contact_refs,
  presence_mode,
  detail_level,
  revision
}
```

`subject_ref`はresident、vehicle、item、animal、equipment等のowner identityを参照する。

PhysicalBuiltはidentityそのものを奪わず、「worldのどこにどうphysicalに存在するか」をownerする。

### 4.2 presence mode

例:

- `FREE_MOVING`
- `SUPPORTED`
- `CONTAINED`
- `CARRIED`
- `MOUNTED`
- `INSTALLED`
- `IN_TRANSIT_AGGREGATE`
- `STORED_AGGREGATE`

### 4.3 residentとの分離

resident:

- 行動意思
- 身体状態
- 目標/知覚

physical_built:

- authoritative 3D presence
- 移動結果
- occupancy/collision result

residentが「移動したい」と判断しただけでposeを書き換えない。

## 5. Motion model

### 5.1 `MotionIntent`

```text
MotionIntent {
  actor_ref,
  presence_id,
  basis_step,
  desired_motion,
  destination_or_direction?,
  movement_mode,
  capability_context_ref,
  causality_refs
}
```

source例:

- resident
- vehicle control logic
- animal/environment behavior
- forced motion event

### 5.2 validation

PhysicalBuiltは少なくとも次を検証する。

- Spatial terrain/built geometryで通過可能か
- opening state
- occupancy/collision constraint
- movement modeに必要なsupport/pathがあるか
- actor physical capability condition
- container/mount constraint
- active hazard/damageによるobstruction

法的に許可されているかはgovernance stateであり、physical impossibilityとは分離する。

### 5.3 local movement vs route planning

長距離route choiceはresident/infrastructure側が計画可能だが、各segmentでphysicalに移動可能か、実際のposition transitionを成立させる責務はPhysicalBuiltにある。

瞬間teleportを標準移動として使わない。

D2/D3では詳細軌跡を省略しても、travel duration、route continuity、boundary crossing、vehicle/item/resident identityを維持する。

## 6. Basic collision / occupancy

### 6.1 `OccupancyState`

```text
OccupancyState {
  occupancy_scope,
  occupying_presence_refs,
  capacity_or_exclusion_class,
  congestion_summary?,
  revision
}
```

標準はbasic collision/占有を扱うが、最高詳細の剛体物理を要求しない。

### 6.2 collision result

```text
PhysicalContactEvent {
  participants,
  contact_scope,
  relative_motion_class,
  severity_input,
  resulting_intents,
  causality_refs
}
```

負傷はresident、damageはPhysicalBuiltがそれぞれownerする。

## 7. BuiltStructure

```text
BuiltStructureState {
  structure_id,
  structure_class,
  spatial_anchor_ref,
  geometry_state,
  material_state_refs,
  support_state,
  condition_state,
  lifecycle_state,
  space_refs,
  opening_refs,
  equipment_refs,
  construction_ref?,
  detail_level,
  lineage_id
}
```

対象例:

- building
- road physical body
- bridge
- tunnel lining/constructed tunnel
- wall/fence
- rail physical track
- dam/levee physical body
- utility physical structure
- ruins

service/network意味はInfrastructureへ分離する。

## 8. Interior / BuiltSpace

### 8.1 `BuiltSpaceState`

```text
BuiltSpaceState {
  built_space_id,
  parent_structure_id,
  geometry_ref,
  space_class,
  connection_refs,
  opening_refs,
  installed_asset_refs,
  physical_capacity_summary,
  condition_state,
  detail_level
}
```

room、corridor、stairs、platform等をactual 3D spaceとして扱える。

### 8.2 interior detail

D0/D1では住人が実際に内部移動・利用できるgeometry/connectivityを持つ。

低detail時もbuilding interior existence、主要space connectivity、capacity、重要asset locationを失わない。

## 9. Door / Window / Gate

### 9.1 `OpeningState`

```text
OpeningState {
  opening_id,
  parent_structure_id,
  geometry_ref,
  opening_class,
  aperture_state,
  lock_mechanism_state,
  damage_state,
  permeability_state,
  physical_access_state,
  detail_level
}
```

### 9.2 physical vs institutional access

PhysicalBuilt:

- 開いているか
- 閉じているか
- 施錠mechanismがengagedか
- physical key/tool等で解除可能か

Governance/Society:

- 入ってよい権限があるか
- credentialが制度上有効か
- ownership/permission

「許可されていない」と「物理的に通れない」を同一booleanにしない。

### 9.3 environment coupling

opening stateはair/smoke/temperature/water exchange boundaryへ影響するためEnvironmentへfactをpublishする。

## 10. Physical item / container location

### 10.1 `PhysicalItemState`

```text
PhysicalItemState {
  physical_item_ref,
  item_identity_or_lot_ref,
  physical_form_class,
  quantity_if_aggregate,
  condition_state,
  containment_ref,
  presence_ref?,
  storage_condition_refs,
  detail_level
}
```

### 10.2 containment

```text
PhysicalContainment {
  contained_ref,
  container_ref,
  containment_slot_or_scope?,
  quantity?,
  accessibility_state,
  revision
}
```

所在例:

- room
- shelf
- container
- machine
- vehicle
- carried by resident
- stockpile

全物品にcm級poseを要求しない。

### 10.3 unique location

同一identity-bearing物品は同時に2つのcontainer/positionでauthorityを持たない。

aggregate lotを分割/統合する場合もquantity continuityを維持する。

## 11. Vehicle / equipment physical state

vehicle/equipmentについてPhysicalBuiltは少なくとも次をownerできる。

```text
PhysicalAssetState {
  asset_ref,
  presence_ref,
  operational_physical_state,
  wear_state,
  damage_state,
  installed_component_summary,
  payload_containment_ref?,
  detail_level
}
```

運行scheduleやnetwork serviceはInfrastructure、ownership/valueはSocietyEconomy。

## 12. Construction plan / worksite

### 12.1 planとexecutionを分離

```text
PhysicalConstructionPlan {
  plan_id,
  target_scope,
  target_geometry_or_structure_spec,
  physical_stage_graph_ref,
  material_requirement_classes,
  equipment_requirement_classes,
  site_constraint_refs,
  revision,
  plan_state
}
```

計画の契約、資金、permitは他domainがownerする。

### 12.2 `ConstructionWorksiteState`

```text
ConstructionWorksiteState {
  worksite_id,
  plan_ref,
  spatial_scope_ref,
  current_stages,
  completed_physical_work,
  installed_material_state,
  delivered_material_refs,
  available_equipment_refs,
  active_worker_presence_refs,
  obstruction_or_pause_reasons,
  partial_structure_refs,
  detail_level
}
```

建設途中stateはworldに実在し、通行、占有、事故、火災、景観、weather exposure等へ影響し得る。

## 13. Construction progress

建設progressは単純timerだけで進めない。

最低限次を入力とする。

- prerequisite stage completion
- worker physical presence
- skill/knowledge fact
- material availability
- tool/equipment availability
- physical accessibility
- weather/environment condition
- equipment condition
- interruption/safety state

SocietyEconomyの契約やResidentの技能をreadしても、PhysicalBuiltがそれらを変更しない。

## 14. Terrain modification during construction/mining

excavation/fillが必要な場合:

```text
physical work成立
 -> physical_built material handling candidate
 -> spatial.geometry.carve/fill intent
 -> environment geology/resource transition intent
 -> shared invariant validation
 -> geometry/material/worksite progress commit
```

地形だけ変更して掘削materialを消す、resourceだけ採取してgeometryを無変更にすることを禁止する。

## 15. Demolition / removal

```text
DemolitionState {
  demolition_id,
  target_structure_ref,
  stage_state,
  worker/equipment/material handling refs,
  removed_component_state,
  salvage_refs,
  waste_refs,
  residual_structure_state,
  detail_level
}
```

解体は瞬時削除しない。

途中のunstable structure、debris、obstruction等をworld consequenceとして持てる。

## 16. Damage / degradation / repair

### 16.1 `PhysicalConditionState`

```text
PhysicalConditionState {
  subject_ref,
  wear_state,
  damage_components,
  serviceability_state,
  structural_integrity_state?,
  failure_modes,
  maintenance_state,
  repairability_state,
  revision
}
```

対象:

- buildings
- infrastructure physical assets
- vehicles
- tools/equipment
- items

### 16.2 damage source

- collision
- weather
- fire
- flood
- earthquake
- aging
- misuse
- overload
- combat
- construction accident

### 16.3 repair

repairは必要なworker、tool、part/material、time、accessを要求可能とする。

repair Operationだけでreasonless full restoreしない。

## 17. Building damage / collapse

building damageを次の段階へ表現可能にする。

```text
INTACT
 -> DEGRADED
 -> PARTIALLY_DAMAGED
 -> PARTIALLY_UNUSABLE
 -> COLLAPSE_RISK
 -> PARTIAL_COLLAPSE / COLLAPSED
 -> STABILIZED / REPAIRED / DEMOLISHED
```

固定enumそのものをPhase 4 schemaへ強制しないが、部分損傷・使用不能・修理・崩壊を区別できるsemantic stateを持つ。

collapse時:

- Spatial geometryへの必要なmutation intent
- Environmentへのdust/smoke/contaminant source
- Residentへのcollision/injury exposure event
- Infrastructureへのroute/service invalidation

を発行できる。

## 18. Fire / combustion

### 18.1 ownership

PhysicalBuiltはbuilt structure、furniture、equipment、item等のcombustion stateをownerする。

Environmentはambient weather/airと、emitted smoke/gasのtransportをownerする。

vegetation/wildland aggregate fireはEnvironment hazardとして保持でき、built/materialized assetへ延焼した部分をPhysicalBuiltへevent/intentで接続する。

### 18.2 `CombustionState`

```text
CombustionState {
  combustible_ref,
  ignition_state,
  burning_state,
  thermal_state,
  fuel_remaining,
  spread_interface_state,
  suppression_state,
  emission_rate_state,
  detail_level
}
```

燃焼化学・CFD最高詳細は標準必須ではない。

### 18.3 fire causality

```text
ignition cause
 -> combustible state
 -> heat/spread
 -> structure/item damage
 -> smoke/gas emission intent to Environment
 -> resident exposure/perception condition
 -> suppression action
```

## 19. Physical material flow

PhysicalBuiltは「materialがworld上のどこにphysicalにあるか」をownerする。

SocietyEconomyは「誰が所有するか、いくらか、inventory/accounting上どう扱うか」をownerする。

重要material flow:

- delivery
- construction consumption
- demolition salvage
- scrap/waste
- mining extraction handoff
- cargo transport

quantity continuityをcross-domain invariantで維持する。

## 20. Update phases

### 20.1 PREPARE

- Spatial geometry revision freeze
- Environment condition freeze
- scheduled work/motion/control input freeze
- physical detail transition決定

### 20.2 PHYSICAL

logical subphases:

```text
P0_GEOMETRY_AND_SUPPORT_READ
P1_OPENING_AND_ACCESS_MECHANISM
P2_MOTION_AND_OCCUPANCY
P3_CONTACT_COLLISION
P4_STRUCTURE_WORK_DAMAGE
P5_COMBUSTION
```

必要なsame-Step dependencyだけをedge化する。

### 20.3 AGENT_ACTION

Resident等からのphysical action intentを実行候補へ変換する。

例:

- pick up
- place
- open/close
- enter/exit
- work
- operate equipment
- extinguish

### 20.4 CONSEQUENCE

- damage event
- accessibility change
- structure completion
- material handoff
- fire emission
- Spatial geometry intent

を生成する。

### 20.5 VALIDATE

shared invariantを検証してcandidate stateを確定する。

## 21. Same-Step dependency

基本DAG:

```text
Spatial geometry fact
Environment forcing fact
    -> Physical movement/work/damage
    -> Physical consequences
    -> Resident/Infrastructure/Society consequences
```

Resident actionとPhysical movementのcycleは:

```text
Resident decision from State(S)
 -> Motion/ActionIntent
 -> PhysicalBuilt apply candidate
 -> MovementResult event
 -> Resident State(S+1) consequence
```

とし、同一StepでResidentがPhysicalBuilt結果を読んで無限再判断するcycleを作らない。

## 22. Intent catalog

PhysicalBuiltが受理する主要intent:

- `physical.motion.request`
- `physical.forced_motion.request`
- `physical.item.pickup`
- `physical.item.place`
- `physical.item.transfer`
- `physical.opening.change_state`
- `physical.asset.operate`
- `physical.construction.perform_work`
- `physical.demolition.perform_work`
- `physical.repair.perform_work`
- `physical.damage.apply`
- `physical.fire.ignite`
- `physical.fire.suppress`
- `physical.install`
- `physical.remove`

## 23. Event catalog

- `PhysicalMovementCompleted`
- `PhysicalMovementBlocked`
- `PhysicalContactOccurred`
- `PhysicalOccupancyChanged`
- `OpeningStateChanged`
- `PhysicalItemLocationChanged`
- `PhysicalAssetConditionChanged`
- `ConstructionStageChanged`
- `StructureBecameUsable`
- `StructureBecameUnusable`
- `DemolitionStageChanged`
- `StructureCollapsed`
- `PhysicalFireStarted`
- `PhysicalFireSpread`
- `PhysicalFireEnded`
- `PhysicalMaterialTransferred`

## 24. Conflict scope / deterministic merge

主要scope:

```text
physical/presence/{presence_id}
physical/occupancy/{scope}
physical/opening/{opening_id}
physical/item/{item_or_lot}
physical/worksite/{worksite_id}/{stage}
physical/condition/{asset}
physical/fire/{combustible_ref}
```

例:

同一itemを複数actorが同時pickup:

- `exclusive_first_valid`をstable same-Step orderで適用
- arrival/thread orderを使わない

同一openingへのopen/close:

- semantic priority + canonical intent order

複数damage:

- deterministic reduce可能なdamage contributionとしてmerge

## 25. Shared invariant

### 25.1 `INV-PHYSICAL-PRESENCE-UNIQUENESS`

同一physical subjectが同時に矛盾する複数authoritative presenceを持たない。

### 25.2 `INV-PHYSICAL-CONTAINMENT-UNIQUENESS`

identity-bearing item/assetは同時に複数containerでauthorityを持たない。

### 25.3 `INV-PHYSICAL-SPATIAL-VALIDITY`

presence/built geometryがSpatial solid/void constraintと矛盾しない。

### 25.4 `INV-PHYSICAL-MATERIAL-CONTINUITY`

construction/demolition/mining/repairでmaterialを理由なく生成・消滅させない。

### 25.5 `INV-PHYSICAL-WORK-INPUT`

physical progressに必要なmaterial/equipment/work conditionが満たされていないのにprogressを確定しない。

### 25.6 `INV-PHYSICAL-STRUCTURE-LIFECYCLE`

active structureをdemolition/damage/historyなしで瞬時消去しない。

### 25.7 `INV-PHYSICAL-FIRE-ENVIRONMENT-COUPLING`

smoke/gas等のenvironment emissionを同一causal combustionと結び付ける。

## 26. Detail level

### 26.1 `D0_ENTITY`

保持:

- physical pose/motion
- local collision occupancy
- room/opening/interior connectivity
- item/container physical location
- active construction workers/materials/stages
- component-level damage where required
- room/item-level fire where required

### 26.2 `D1_LOCAL_AGGREGATE`

保持:

- building/space connectivity
- room/zone occupancy summary
- item lot/container summary
- vehicle/asset presence by local segment/scope
- construction stage + material/work stock
- structure condition
- fire zone summary

persistent identity-bearing assetを失わない。

### 26.3 `D2_REGIONAL_AGGREGATE`

保持:

- building/road/major asset existence/condition
- vehicle/resident/item movement journey state
- inventory physical location by facility/region
- construction/demolition progress
- major blockage/damage/fire
- detailed archive anchor

### 26.4 `D3_BOUNDARY_SUMMARY`

保持:

- entering/leaving identity-bearing movement handoff
- cargo/material flow
- major structure/capacity facts required by otherdomains
- active hazard crossing boundary
- archived detailed built state lineage

## 27. Promotion trigger

- Diver/resident接近・操作
- entering building/interior
- item/asset interaction
- construction/repair/demolition開始
- collision/accident/fire/disaster
- military/security physical activity
- boundary crossing
- predictive detail rule

## 28. Demotion guard

- movement/contact resolution中
- resident/Diver physical interaction中
- pickup/transfer handoff中
- active construction/demolition/repair stageでdetailが必要
- fire/collapse中
- unresolved material transfer
- active boundary crossing
- archiveに必要なidentity/locationを失う場合

## 29. Deterministic promotion / demotion

過去詳細stateがarchiveされている場合は同一lineageを復元する。

aggregateからinterior/item placement等を初materializeする場合:

```text
source_scope_lineage
+ structure/item identity
+ promotion_step
+ role/slot semantic key
+ WorldSeed
```

からdeterministicに配置する。

既にworld historyで確定したroom、door、item、damage、ruin等は再promotionで別stateへ作り直さない。

## 30. Boundary exchange

identity-bearing subjectの移動:

```text
SOURCE_PHYSICAL_AUTHORITY
 -> TRANSFER_PREPARED
 -> boundary crossing committed
 -> TARGET_PHYSICAL_AUTHORITY
```

同一subjectを二重存在させない。

aggregate cargo/materialはquantity flowとしてsource減算/target加算を同一exchangeへ結ぶ。

## 31. Cross-domain causal table

| Source | PhysicalBuilt input | PhysicalBuilt result | Target consequence |
|---|---|---|---|
| resident | motion/work/action intent | movement/work/item interaction | resident experience/health/action result |
| spatial | terrain geometry | collision/support/accessibility basis | physical presence/structure state |
| environment | weather/water/ground motion | damage/work slowdown/fire effect | environment emission/hydrology feedback |
| society_economy | material/contract/work allocation fact | actual physical use/progress | inventory/payment/contract consequence |
| governance_security | legal/access/permit fact | physical mechanism remains separate | violation/access outcome fact |
| infrastructure_information | route/service operation | vehicle/asset physical state | network capacity/service change |
| participation | Diver-bound detail requirement | physical detail floor | General View interaction basis |

## 32. Persistence / Replay

replayで次を再現する。

- physical presence transition
- opening state
- item containment
- construction/demolition progress
- damage/repair
- fire transition
- material handoff
- detail materialization/archive lineage

physical scheduler/engine implementationが変わってもsemantic ordering contractを維持する。

## 33. Publication boundary

Viewへpublication可能:

- confirmed physical pose
- built geometry/interior
- opening state
- item/asset physical state
- worksite progress
- damage/fire

General Viewのprediction poseはnon-authoritativeであり、Core confirmed PhysicalPresenceへreconcileする。

## 34. Traceability

| Requirement | Coverage |
|---|---|
| Q016 | settlementをactual built environmentから形成 |
| Q033/Q104 | item lot/identityとphysical location |
| Q034 | room/door/furniture/equipmentをactual 3D interiorとして扱う |
| Q039 | built/interior fire、smoke emission coupling |
| Q043 | full 3D movement/basic collision/occupancy |
| Q054 | building/machine/infrastructure physical degradation |
| Q067 | manufacturing equipment/material physical basis |
| Q075 | clothing/equipment physical possession/use basis |
| Q085 | excavation/fillのSpatial/Environment transaction |
| Q087/Q088 | building/item damage/repair |
| Q089 | ruins as persistent built state |
| Q092〜Q097 | underground built spaces、air/water coupling |
| Q100 | worksite physical access/tools/material requirement |
| Q101 | carry/load/transport physical presence |
| Q102/Q103 | opening/lock physical stateとinstitutional access分離 |
| Q105〜Q109 | staged construction/in-progress/plan/public work physical execution/demolition |
| Q115〜Q119 | material/item flowとphysical conservation basis |
| Q171〜Q176 | carrying/storage/accident/traffic physical basis |
| Q187/Q188 | public space/facility physical accessibility basis |
| Q190〜Q194 | detail and boundary movement continuity |
| Q232 | confirmed physical state as Diver prediction reconcile target |
| Q260 | Diver residentも通常physical ruleに従う |
| Q265 | identity/existenceとupdate detail分離 |

## 35. Phase 4 handoff

Phase 4で確定する事項:

- pose/velocity numeric schema
- collision/occupancy geometry representation
- movement integrator
- path/local navigation interface
- structure/interior schema
- opening mechanism schema
- item/container location encoding
- construction stage graph schema
- damage model
- combustion/spread algorithm
- material quantity representation
- aggregate movement travel algorithm
- conflict resolver details
- publication delta schema

Phase 4は本書のowner境界、物理実体性、material continuity、detail semanticsを変更してはならない。
