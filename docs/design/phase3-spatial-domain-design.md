# 詳細設計 Phase 3: Spatial Domain設計

Status: Complete / P3-02  
Tracking: Issue #15  
Parent: `phase3-world-domain-design.md`  
Common contract: `phase3-domain-common-contract.md`

## 1. 目的

`spatial` domainは、MachiVerseのauthoritative full 3D worldを他domainが一貫して参照できるよう、world-globalな空間基準、terrain solid/void geometry、空間scope、containment/topology、detail境界を所有する。

本書でいうSpatialはrender scene graphではない。General View向け描画表現、camera、mesh LOD、GPU resourceはauthorityではなく、Simulation Coreのworld geometryから派生するprojectionである。

また、Spatialはすべての動的Entityの行動・衝突・所有権を所有しない。住人・車両・物品・建物等のdomain固有stateは各owner domainが保持し、位置・形状を表現する際にSpatial contractへ従う。

## 2. 前提と上位契約

Spatialは少なくとも次を満たす。

- 標準world modelはfull 3Dである。
- authoritative terrainを単一heightmapだけで表現してはならない。
- 同一水平位置に複数高さのsurface/voidが存在できる。
- tunnel、cave、地下室、坑道、overhang、切通し等をworld geometryとして表現可能にする。
- terrain excavationやvoid生成は決定論的である。
- render LODとsimulation detailを分離する。
- exact mesh/voxel/SDF/CSG/octree等のdata structureはPhase 4へ持ち越す。

## 3. Responsibility / Non-responsibility

### 3.1 Spatialが所有する責務

- world-global spatial reference semantics
- spatial scope / region geometry
- authoritative natural terrain solid/void boundary geometry
- terrain geometry revision historyに必要なsemantic state
- natural cave/voidのgeometry identityとconnectivity
- domain-neutral containment relation
- domain-neutral adjacency / boundary interface
- detail region geometryとdetail boundary topology
- world生成時のcoarse-to-detailed geometry lineage
- geometry mutationのowner validation
- cross-domain spatial consistency invariantへのauthoritative input

### 3.2 Spatialが所有しない責務

- resident/vehicle/itemの移動意思決定
- dynamic collision response
- building structural stateやdamage
- room/door/furniture等のbuilt-space semantics
- geology/soil/resource composition
- water amount/flow、weather、air、ocean、ecosystem
- land ownership、administrative territory、legal boundary
- road/rail/utility network service capacity
- View描画用mesh、texture、camera、render culling

上記の位置・領域はSpatial coordinate/scopeを参照できるが、その意味stateのownerは各domainに残る。

## 4. DomainDefinition

```text
DomainDefinitionV1 spatial {
  domain_token = "spatial"
  domain_family = "spatial"
  state_partitions = [
    spatial.world_frame,
    spatial.scope_registry,
    spatial.terrain_geometry,
    spatial.void_geometry,
    spatial.containment_topology,
    spatial.boundary_topology,
    spatial.detail_regions,
    spatial.geometry_lineage
  ]
  update_phases = [PREPARE, ENVIRONMENT, PHYSICAL, CONSEQUENCE, VALIDATE]
}
```

Spatialは複数phaseへ参加できるが、同一Stepで他domainのprivate stateを直接変更しない。

## 5. Authoritative state model

### 5.1 WorldFrame

```text
WorldFrame {
  frame_id,
  frame_kind,
  parent_frame_id?,
  transform_semantics,
  validity_scope,
  revision
}
```

`world-global` frameを全worldで一意のroot spatial referenceとする。

Phase 3では次だけを固定する。

- 3軸を持つmetric 3D空間として位置・方向・距離を意味付けできる。
- local frameを階層的に定義できる。
- frame変換はworld outcomeへ影響する場合に決定論的である。
- frameの意味をStep途中で暗黙変更しない。

軸方向、原点の具体位置、座標数値型、固定小数/浮動小数、単位encodingはPhase 4で確定する。

### 5.2 SpatialScope

```text
SpatialScope {
  scope_id,
  geometry_ref,
  parent_scope_id?,
  scope_class,
  lineage_id,
  active_from_step,
  retired_at_step?
}
```

SpatialScopeはdomain-neutralな3D範囲を表す。

例:

- world
- regional volume
- local cell volume
- terrain solid chunk semantic scope
- natural void/cave volume
- detail boundary region

行政区域、所有地、部屋、道路等の意味そのものはSpatialScopeではなく、各owner domainがSpatialScopeを参照して保持する。

### 5.3 TerrainGeometryState

```text
TerrainGeometryState {
  terrain_scope_id,
  solid_void_boundary,
  geometry_revision,
  source_lineage,
  exposed_surface_classes,
  connectivity_refs,
  detail_level,
  archive_anchor?
}
```

`solid_void_boundary`は「どこがterrain solidで、どこが通行可能なvoidか」というauthoritative geometry semanticsを表す。

具体表現はPhase 4で決めるが、単一XY heightだけでは表現不能なgeometryを許容しなければならない。

### 5.4 VoidGeometryState

自然洞窟、侵食空洞等、natural originのvoid geometry identity/connectivityをSpatialが保持する。

```text
VoidGeometryState {
  void_id,
  geometry_ref,
  connectivity,
  entrances,
  origin_class,
  lifecycle_state,
  geometry_revision
}
```

人工tunnelや地下室等のbuilt structure semanticsは`physical_built`が所有する。ただし、terrainを掘削して生成されたvoid boundary自体はSpatial geometryへ反映される。

### 5.5 ContainmentTopology

```text
ContainmentRelation {
  subject_spatial_ref,
  container_scope_id,
  relation_class,
  basis_geometry_revision
}
```

`relation_class`例:

- `inside`
- `intersects`
- `touches_boundary`
- `above_or_exposed_to`
- `subsurface_of`

Phase 4で高速検索data structureを選べるよう、semantic relationとindex implementationを分離する。

### 5.6 BoundaryInterface

```text
BoundaryInterface {
  boundary_id,
  scope_a,
  scope_b,
  interface_geometry,
  permeability_classes,
  detail_policy_ref,
  revision
}
```

`permeability_classes`はdomainが水・空気・Entity移動等の境界条件を問い合わせるためのdomain-neutral metadataであり、実際のflow量はSpatialが所有しない。

## 6. Geometry identity / lifecycle

### 6.1 stable identity

次はstable identityを持てる。

- SpatialScope
- natural void/cave
- detail region lineage
- long-lived terrain geometry partition
- boundary interface

geometry revisionが変化しても、同一logical objectを表す限りidentityを再採番しない。

### 6.2 terrain geometry lifecycle

```text
PLANNED_MUTATION
 -> VALIDATED
 -> APPLIED
 -> ACTIVE_REVISION
```

`PLANNED_MUTATION`自体をauthoritative terrainへ即反映しない。material/geology、水、built structure等のcross-domain invariantを満たしたmutationだけをcommit candidateへ反映する。

## 7. Input / accepted intent

Spatialが受理可能な主要intentを次とする。

### 7.1 `spatial.geometry.carve`

terrain solidを除去してvoidを作る要求。

主なsource:

- `physical_built`: excavation、tunnel、mine、cutting
- `environment`: natural erosion/cave formation/collapse結果

必須validation:

- target geometry revision一致
- scope validity
- geometry operation validity
- geology/material stock連携
- active built/entity occupancyとのshared invariant

### 7.2 `spatial.geometry.fill`

voidへmaterialを追加してsolid boundaryを変更する要求。

例: 盛土、堆積、自然堆積、collapse。

### 7.3 `spatial.geometry.deform`

地震、侵食、地滑り等でterrain boundaryを変更する要求。

### 7.4 `spatial.scope.create_or_revise`

detail region、自然void、domain-neutral region等のscopeを作成・改訂する。

### 7.5 `spatial.boundary.revise`

scope間boundary topologyを変更する。

## 8. Emitted event

### 8.1 `SpatialGeometryChanged`

```text
SpatialGeometryChanged {
  scope_id,
  old_revision,
  new_revision,
  affected_volume,
  change_class,
  causality_refs
}
```

consumer例:

- environment: drainage、air/water domain、exposed geology再評価
- physical_built: collision/navigation/structure support再評価
- resident: accessibility/perceptionの後続再評価
- infrastructure_information: route/network physical validity再評価

### 8.2 `SpatialConnectivityChanged`

void、passage、boundaryの接続性が変化したfact。

### 8.3 `SpatialDetailBoundaryChanged`

detail region geometryがpromotion/demotionにより変化したfact。

### 8.4 `SpatialContainmentInvalidated`

geometry changeにより既存containment cache/projectionの再評価が必要となったfact。cacheをauthorityにはしない。

## 9. Update phase

### 9.1 PREPARE

- Step basisのgeometry revisionをfreeze
- detail transition候補を確定
- scheduled geometry mutation intentをvalidate候補へ入れる

### 9.2 ENVIRONMENT

- environment起因のerosion/deposition/collapse等のgeometry intentを受理
- environment計算に必要なstable geometry read viewを提供

### 9.3 PHYSICAL

- construction/mining/demolition等のgeometry intentを受理
- dynamic physical calculation用のterrain geometry revisionを提供

### 9.4 CONSEQUENCE

- merge済みmutationからcandidate geometry revisionを形成
- connectivity/containment invalidationを導出

### 9.5 VALIDATE

- cross-domain invariantを検証
- invalid geometry、dangling scope、duplicate boundaryを拒否

## 10. Same-Step dependency

SpatialとEnvironment/Physicalの関係はcycleを作らないよう次とする。

```text
State(S).spatial_geometry
    -> environment calculate
    -> environment geometry intents/events
    -> spatial candidate revision
    -> PHYSICAL / CONSEQUENCE consumers as explicit merged fact where required
```

同一Step内でSpatialがenvironmentの未merge private stateを直接読むことは禁止する。

通常のphysical movementは`State(S)`のgeometryをbasisとし、同一Stepで生じた大規模collapse等を即時反映する必要がある場合だけ、明示されたsame-step merged geometry factを使う。

## 11. Conflict scope / deterministic merge

主要conflict scope:

```text
spatial/geometry/{scope_id}/{revision}
spatial/scope/{scope_id}
spatial/boundary/{boundary_id}
```

同一geometry範囲に複数carve/fill/deformがある場合、arrival orderでは処理しない。

Phase 3では次を要求する。

- operation kindごとのsemantic precedenceを定義可能にする。
- overlapping geometry mutationはstable conflict scopeへ正規化する。
- mathematically commutativeでないoperationはcanonical orderで適用する。
- invalid intermediate geometryを生成するsequenceはowner validationで拒否する。
- thread completion orderをtie-breakerにしない。

exact boolean geometry algorithmはPhase 4で確定する。

## 12. Shared invariant

### 12.1 `INV-SPATIAL-SOLID-VOID-CONSISTENCY`

同一authoritative volumeを同時に矛盾するsolid/voidとして確定しない。

### 12.2 `INV-SPATIAL-CONTAINMENT-CONSISTENCY`

active reference対象が存在しないscopeへcontainment relationを持たない。

### 12.3 `INV-SPATIAL-GEOLOGY-MASS-LINK`

human/natural terrain mutationがenvironmentのgeology/material stockを伴う場合、geometry changeとstock transitionを同一causal transactionへ結び付ける。

掘削でsolid volumeだけ消え、対応materialが理由なく消滅することを禁止する。

### 12.4 `INV-SPATIAL-WATER-BOUNDARY`

water volume/boundary conditionとterrain geometryがcommit時点で矛盾しない。水量のownerはenvironmentだが、void/solid geometryとの整合をshared invariantで検証する。

### 12.5 `INV-SPATIAL-BUILT-ANCHOR`

building/road/tunnel等のbuilt geometry anchorがretired/不存在terrain scopeへdanglingしない。

### 12.6 `INV-SPATIAL-DETAIL-CONTINUITY`

detail boundaryの変更でscope overlap/gapにより同一volumeが二重authorityまたは無authorityにならない。

## 13. Detail level別state

### 13.1 `D0_ENTITY`

保持:

- standard simulation resolutionの3D solid/void boundary
- cave/tunnel-adjacent自然geometry
- local connectivity
- active excavation/collapse対象geometry
- fine containment/boundary scope

ただし最高精度地盤解析や無制限surface resolutionを意味しない。

### 13.2 `D1_LOCAL_AGGREGATE`

保持:

- local terrain envelope
-重要なvoid/corridor topology
- water/air/environment boundaryに必要なsurface
- persistent cave/terrain modification identity
- high-detail archive anchor

省略可能:

- inactive small-scale surface detail
- simulation因果へ影響しないmicro geometry

### 13.3 `D2_REGIONAL_AGGREGATE`

保持:

- regional relief/terrain volume summary
- coast/river basin等environment couplingに必要なgeometry
- major cave/underground corridor references
- region adjacency
- detail archive lineage

### 13.4 `D3_BOUNDARY_SUMMARY`

保持:

- external boundary geometry
- major topographic envelope
- cross-boundary portal/interface
- environmental flux boundaryに必要なsurface metadata
- persistent geometry lineage/history anchor

## 14. Promotion / Demotion

### 14.1 promotion trigger

- scheduled excavation/construction/mining
- resident/Diver participationによりlocal physical detailが必要
- disaster/collapse/flood等でfine geometryが必要
- boundary crossing entityが接近
- predictive detail policy

閾値・buffer幅・hysteresisはConfig。

### 14.2 deterministic promotion

低detail geometryから高detail geometryを復元する場合、優先順位は次とする。

1. archived prior detailed geometryがある場合は同一lineageから復元
2. persistent modification historyをreapply
3. aggregate-native未materialized部分はWorldSeed + stable scope lineage + promotion contextから決定論的生成

既に確定したcave、excavation、coastline change等を再生成時に別物へ置換しない。

### 14.3 demotion guard

次の場合はdemotionを延期する。

- active excavation/construction
- collapse/landslide/flood等の局所event進行中
- boundary crossing中
- unresolved geometry conflict
- detail archive生成に必要なstateが揃っていない
- local geometryを参照するactive persistent obligationがあり、aggregate表現では意味を維持できない

## 15. World initialization

World initializationではcoarse world mapから詳細geometryへ次のlineageを持たせる。

```text
WorldSeed + InitializationConfig
 -> coarse global tectonic/topographic constraints
 -> regional terrain scopes
 -> 3D terrain solid/void materialization
 -> hydrology/ocean/environment coupling
 -> natural cave/subsurface refinement
 -> prehistory modifications
 -> normal simulation State(0)
```

Spatialはプレート/geologyそのものを所有せず、environmentが生成した因果条件からterrain geometry候補を受ける。

生成済みcoarse mapと詳細3D geometryの間にstable lineageを持ち、同一Seed/Configから同一world geometryへ到達できるようにする。

## 16. Cross-domain causal links

| Source | Cause | Spatial effect | Follow-up |
|---|---|---|---|
| environment | erosion/deposition | geometry deform/fill/carve | hydrology/ecology/physical再評価 |
| environment | earthquake/landslide | geometry deform/collapse | built damage、route obstruction |
| physical_built | excavation/mining | carve/fill | geology resource transfer、void生成 |
| physical_built | construction | surface/anchor update | drainage/accessibility変化 |
| governance_security | territory change | geometry変更なし | governanceがSpatialScope参照を変更 |
| infrastructure_information | network change | geometry変更なし | network ownerがSpatial geometry参照 |
| resident | movement/action | geometry変更なし（通常） | physical_builtがpose/collisionをowner |

## 17. Persistence / Replay

replayで次を再現可能にする。

- geometry mutation source/cause
- revision sequence
- scope lineage
- detail transition
- archived detailed geometry anchor
- natural/generated geometry materialization context

snapshot storage方式やgeometry compressionはPhase 4。

## 18. Publication boundary

General Viewへはauthoritative geometryからprojectionを生成できる。

publicationは次を満たす。

- basis Step / geometry revisionを識別可能
- tunnel/cave/overhang等を失う2D height-only projectionをauthoritative replacementとして扱わない
- View用mesh simplificationはworld geometryを変更しない
- render cullingによる非表示をworld上の不存在とみなさない

## 19. Traceability

| Requirement | Coverage |
|---|---|
| Q003/Q004 | coarse world mapから3D geometry lineageへ接続 |
| Q016/Q034 | built/interiorが参照可能なfull 3D基盤 |
| Q043 | full 3D movement/collisionのterrain basis |
| Q071 | territory/land boundaryが参照するdomain-neutral geometry |
| Q085 | terrain modification geometry owner |
| Q086 | river/lake/coastline dynamic geometry coupling |
| Q090〜Q099 | subsurface/void/water/ocean/caveを扱える3D基盤 |
| Q190〜Q194 | detail region、promotion/demotion、boundary continuity |
| Q265 | existence/geometry identityとupdate detailを分離 |

## 20. Phase 4 handoff

Phase 4で決める事項:

- global/local coordinate numeric representation
- exact axis/origin/unit encoding
- geometry primitive/data structure
- terrain boolean operation algorithm
- spatial index
- containment query algorithm
- geometry precision/tolerance semantics
- geometry archive encoding
- projection/delta schema
- exact conflict normalization algorithm

これらは本書のownership、full 3D、identity、detail continuityを変更してはならない。
