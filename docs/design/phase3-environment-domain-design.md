# 詳細設計 Phase 3: Environment Domain設計

Status: Complete / P3-02  
Tracking: Issue #15  
Parent: `phase3-world-domain-design.md`  
Common contract: `phase3-domain-common-contract.md`  
Spatial dependency: `phase3-spatial-domain-design.md`

## 1. 目的

`environment` domainは、MachiVerseの自然環境をworld simulationのauthoritative stateとして所有し、地質、土壌、地下水、大気、気候、天候、表流水、海洋、生態系、自然資源、環境汚染、自然災害要因を因果的に更新する。

自然環境を単なる背景parameterやvisual effectとして扱わず、住人、建築、農業、物流、資源利用、災害、健康等が参照するworld stateとして扱う。

最高精度の数値気象予報、CFD、海洋大循環、地盤工学、種レベルの完全生態系等は標準必須ではないが、標準B/C相当要件に必要な因果、stock/flow、3D空間関係を保持する。

## 2. Responsibility / Non-responsibility

### 2.1 Environmentが所有する責務

- subsurface geology / soil composition
- natural resource deposit stockとrenewal/depletion state
- groundwater
- atmosphere state
- weather / climate state
- surface hydrology
- ocean state
- vegetation/wildlife等のaggregate-native ecosystem state
- environmental contaminant concentration/accumulation
- natural hazard driverとhazard event fact
- natural erosion/deposition/cave formation等のenvironmental geometry cause
- natural environmentのdetail promotion/demotion
- environmental boundary flux

### 2.2 Environmentが所有しない責務

- terrain solid/void geometryそのもの: `spatial`
- building/road/tunnel/room geometryとdamage: `physical_built`
- waterworks/power/communication service network: `infrastructure_information`
- resident physiology/health: `resident`
- ownership/mining right/land law: `governance_security`
- mining work、transport、inventory、commercial resource ownership: `physical_built` / `society_economy`
- structural fire combustion semantics: P3-03 `physical_built`を主ownerとし、Environmentはweather/air/smoke/wildland fuel等を供給する
- render particle/cloud/wave effect: View projection

## 3. Environment内部state partition

Environmentを1つの巨大mutable stateとして扱わず、semantic partitionを次のように分ける。

```text
environment.geology
environment.soil
environment.resource_deposit
environment.groundwater
environment.atmosphere
environment.climate
environment.weather
environment.surface_water
environment.ocean
environment.ecosystem
environment.contaminant
environment.hazard
environment.environment_lineage
```

これらは同じdomain family内のpartitionであり、Phase 4で別module/classにすることを要求しない。

## 4. DomainDefinition

```text
DomainDefinitionV1 environment {
  domain_token = "environment"
  domain_family = "environment"
  dependencies = [
    state_read(spatial),
    state_read(physical_built),
    state_read(infrastructure_information),
    state_read(resident),
    state_read(society_economy),
    state_read(governance_security)
  ]
  update_phases = [PREPARE, ENVIRONMENT, CONSEQUENCE, VALIDATE]
}
```

他domainへのstate_readは必要な公開read modelだけに限定し、private stateへ依存しない。

## 5. Geology state

### 5.1 `GeologyVolumeState`

```text
GeologyVolumeState {
  geology_scope_id,
  spatial_scope_ref,
  strata_summary,
  material_classes,
  porosity_class,
  stability_class,
  permeability_class,
  fault_or_plate_refs,
  resource_deposit_refs,
  water_interaction_refs,
  detail_level,
  lineage_id
}
```

標準要件では地層、岩石、土壌、資源を3D配置し、掘削・地下水と接続する。

fault/plateの最高詳細解析は要求しないが、地震、山地形成、地下資源等の因果を作れる状態を持つ。

### 5.2 地質とSpatial geometryの分離

- `spatial`: solid/void geometryをowner
- `environment.geology`: solid materialの自然組成・地質特性・stockをowner

掘削、崩落、侵食等では両者をshared invariantでatomicに整合させる。

## 6. Soil state

```text
SoilPatchState {
  patch_id,
  spatial_scope_ref,
  soil_class,
  depth_or_volume_summary,
  moisture,
  fertility,
  organic_state,
  erosion_state,
  salinity_or_degradation,
  contaminant_refs,
  vegetation_refs,
  detail_level
}
```

soilは農業、生態系、浸透、侵食、土地利用のnatural basisを提供する。

耕作や造成等のhuman modificationは他domainからintentを受け、Environment ownerがsoil transitionを確定する。

## 7. Natural resource state

### 7.1 `NaturalResourceDeposit`

```text
NaturalResourceDeposit {
  deposit_id,
  resource_kind,
  spatial_scope_ref,
  recoverable_stock,
  inaccessible_or_uncertain_stock,
  quality_distribution,
  renewal_model_ref?,
  depletion_state,
  discovery_truth_state,
  detail_level,
  lineage_id
}
```

EnvironmentはCore真実としてのresource depositを所有する。

住人・組織がその存在を知っているかはresident/information側の認識stateであり、Environmentのtruthとは分離する。

### 7.2 extraction

mining等で資源を取得する場合:

1. `physical_built`が実作業・掘削を成立させる
2. Environmentへresource extraction intentを送る
3. Environmentがdeposit stockを減らす
4. extracted materialは`physical_built`/`society_economy`側のphysical/inventory stateへhandoffする
5. Spatial geometry changeとmaterial stock changeをshared invariantで結ぶ

同一資源を二重取得しない。

## 8. Groundwater state

```text
GroundwaterState {
  aquifer_id,
  spatial_scope_ref,
  stored_water,
  hydraulic_head_summary,
  recharge_rate_state,
  discharge_interfaces,
  quality_state,
  contaminant_refs,
  detail_level
}
```

地下水はsoil/geology、surface water、well/intake、地下構造と因果接続する。

詳細水理solverはPhase 4以降のalgorithm選択とし、標準では水収支と到達可能性・浸水等の因果を維持する。

## 9. Atmosphere state

### 9.1 `AtmosphereCellState`

```text
AtmosphereCellState {
  atmospheric_scope_id,
  spatial_scope_ref,
  pressure_state,
  temperature_state,
  humidity_state,
  wind_state,
  precipitation_state,
  cloud_or_radiative_summary,
  gas_and_aerosol_state,
  visibility_state,
  detail_level
}
```

大気は3D spatial distributionを持てる。

標準で無制限CFDを要求しないが、風、建物、地形に応じた煙・ガスの移動/濃度と、屋内・地下との交換を扱える必要がある。

### 9.2 atmospheric dispersion

contaminant/smoke/gasについて最低限次を保持する。

- source cause
- spatial concentration/aggregate distribution
- transport/advection tendency
- deposition/removal
- indoor/subsurface exchange boundary
- health/perceptionへ渡すexposure condition

residentの実際のdose/health effectは`resident`がowner。

## 10. Climate / Weather state

ClimateとWeatherを分離する。

### 10.1 `ClimateState`

長期baseline/seasonal regime等を保持する。

```text
ClimateState {
  region_id,
  temperature_regime,
  precipitation_regime,
  wind_regime,
  seasonality,
  ocean_coupling,
  long_term_anomaly,
  lineage_id
}
```

### 10.2 `WeatherState`

```text
WeatherState {
  weather_scope_id,
  basis_step,
  temperature,
  pressure,
  humidity,
  wind,
  precipitation,
  storm_state,
  visibility,
  derived_hazard_refs,
  detail_level
}
```

ClimateはWeatherを固定script化するものではなく、Weather生成・遷移の長期条件となる。

### 10.3 causal input

Weather/Climateは少なくとも次を参照可能にする。

- solar/day-night forcing
- terrain/elevation
- coast/ocean state
- large-scale wind
- soil/vegetation moisture
- human-caused environmental changes when material

render cloud effectやView時刻は入力にしない。

## 11. Surface water / Hydrology

### 11.1 `SurfaceWaterBodyState`

```text
SurfaceWaterBodyState {
  water_body_id,
  water_body_kind,
  spatial_scope_ref,
  stored_water,
  water_level_or_extent,
  flow_interfaces,
  inflow_outflow_state,
  sediment_state,
  quality_state,
  freeze_or_dry_state?,
  detail_level,
  lineage_id
}
```

kind例:

- river reach
- lake
- wetland
- temporary stream
- flood water

### 11.2 dynamic shoreline / river

河川、湖、海岸線は固定背景にしない。

降雨、渇水、洪水、堤防、dam、取水、erosion/deposition等からextent/flow/shore geometryが変化する。

Geometry変更が必要ならEnvironmentはSpatialへgeometry mutation intentを送る。

### 11.3 water conservation

重要water stockについて少なくとも次をcause-linked flowとして扱う。

```text
precipitation
 -> infiltration / surface runoff
 -> river/lake/groundwater/ocean
 -> intake/use/storage
 -> discharge/evaporation
```

全微視的water molecule保存は要求しないが、aggregate/detail transitionやdomain handoffでreasonless creation/lossを起こさない。

## 12. Ocean state

```text
OceanRegionState {
  ocean_scope_id,
  spatial_scope_ref,
  water_stock_summary,
  temperature_state,
  current_state,
  wave_and_sea_state,
  storm_coupling,
  salinity_or_quality_summary,
  ecosystem_refs,
  coastline_interfaces,
  detail_level
}
```

標準B相当として海流、波、荒天、温度を動的に扱い、航海、漁業、気候、海岸へ影響させる。

深海全域を常時最高詳細で解く必要はなく、detail levelに応じてregional current/sea-stateへ集約可能にする。

## 13. Ecosystem state

### 13.1 基本原則

Environmentは生態系の自然population/vegetation stateを所有する。

すべての野生生物・植物を常時個体Entity化する必要はない。

一方、必要な個体が実体化された場合はpersistent identityを維持し、aggregateへ戻す際も履歴とstockを失わない。

### 13.2 `EcologicalPopulationState`

```text
EcologicalPopulationState {
  population_id,
  species_or_ecological_class,
  habitat_scope_ref,
  population_stock,
  age_or_stage_summary,
  health_or_condition_summary,
  reproduction_state,
  mortality_state,
  migration_interfaces,
  food_web_refs,
  detail_level,
  lineage_id
}
```

### 13.3 `VegetationPatchState`

```text
VegetationPatchState {
  patch_id,
  spatial_scope_ref,
  vegetation_classes,
  biomass_or_cover,
  succession_state,
  moisture_state,
  fire_fuel_state,
  human_use_pressure,
  detail_level
}
```

### 13.4 food web

捕食、採食、food availability等はspecies/population間の因果として表現する。

最大詳細の栄養循環modelは標準必須でないが、人口stock、餌不足、捕食圧、人間活動、habitat変化による影響を表現する。

### 13.5 individual materialization

wildlife等をD0へmaterializeする場合:

```text
MaterializationContext {
  source_population_id,
  lineage_generation,
  materialization_step,
  ecological_role,
  stable_ordinal
}
```

から決定論的にidentity/stateを導出する。

## 14. Environmental contaminant state

```text
EnvironmentalContaminantState {
  contaminant_kind,
  medium,
  spatial_scope_ref,
  quantity_or_concentration,
  source_refs,
  transport_state,
  decay_or_removal_state,
  accumulation_state,
  detail_level
}
```

medium例:

- atmosphere
- surface water
- groundwater
- soil
- ocean

pollution sourceはindustrial process、transport、waste、energy use、fire等の他domain event/intentから受け取る。

Environmentはphysical environmental concentrationをownerとし、legal liabilityやwaste ownershipはownerしない。

## 15. Hazard / Disaster state

### 15.1 基本原則

自然災害を独立したランダムイベントgeneratorとして扱わず、可能な限りunderlying environment stateから因果的に発生させる。

対象例:

- flood
- drought
- storm
- heat/cold extreme
- earthquake
- landslide
- coastal storm/wave hazard
- wildfire environmental condition
- volcanic/tectonic hazard when supported by configured model

### 15.2 `NaturalHazardState`

```text
NaturalHazardState {
  hazard_id,
  hazard_kind,
  source_process_ref,
  spatial_scope_ref,
  severity_state,
  onset_step,
  progression_state,
  expected_or_actual_duration_state,
  physical_effect_intents,
  detail_level
}
```

hazardは「被害」そのものをownerしない。

例:

- earthquake ground motion/hazard: Environment
- building damage: PhysicalBuilt
- resident injury: Resident
- emergency response: governance/infrastructure/resident等

## 16. Environment update phases

### 16.1 PREPARE

- effective Configをfreeze
- environment cadence scheduleを決定
- external source/sink intentsを整理
- boundary exchangeをfreeze
- detail transition候補を決定

### 16.2 ENVIRONMENT logical subphases

内部logical orderを次のように扱える。

```text
E0_GEOLOGY_SLOW
E1_ATMOSPHERE_WEATHER
E2_HYDROLOGY_GROUNDWATER
E3_OCEAN
E4_SOIL_ECOSYSTEM_RESOURCE
E5_CONTAMINANT
E6_HAZARD_DERIVATION
```

これはphysical thread orderを固定するものではない。

同一Step dependencyが必要な箇所だけDAG edgeを持つ。

### 16.3 CONSEQUENCE

- cross-domain environmental event生成
- Spatial geometry mutation intent生成
- resident/physical/infrastructure向けexposure/forcing event生成
- resource/habitat change fact生成

### 16.4 VALIDATE

- water/resource/ecological stock continuity
- geometry/geology consistency
- boundary flux continuity
- invalid concentration/state範囲
- detail transition invariant

を検証する。

## 17. Same-Step dependency design

自然系には循環因果が多いため、same-Step mutual writeを避ける。

基本:

```text
Atmosphere(S) reads Ocean(S)
Ocean(S) reads Atmosphere(S)
 -> both calculate candidates independently
 -> deterministic coupled merge / next-step state
```

この形ならexecution cycleを作らない。

一方、同一Stepで降雨forcingをsurface runoffへ反映する等、明示的に必要な場合は:

```text
E1 weather precipitation fact
 -> E2 hydrology same_step_dependency
```

とする。

同様にhazard derivationはE1〜E5のmerge済みfactを読む。

## 18. Input intent catalog

Environmentが主に受理するintent:

- `environment.resource.extract`
- `environment.resource.return_or_redeposit`
- `environment.water.intake`
- `environment.water.discharge`
- `environment.water.impound_or_release`
- `environment.soil.modify`
- `environment.contaminant.emit`
- `environment.contaminant.remove`
- `environment.habitat.modify`
- `environment.ecosystem.harvest`
- `environment.ecosystem.introduce_population`
- `environment.environmental_structure_effect`

各intentはsource cause、target spatial scope、quantity/rate、basis Stepを持つ。

## 19. Event catalog

主要DomainEvent:

### 19.1 Weather / climate

- `WeatherConditionChanged`
- `PrecipitationOccurred`
- `SevereWeatherThresholdCrossed`
- `ClimateRegimeChanged`

### 19.2 Hydrology

- `WaterLevelChanged`
- `FloodExtentChanged`
- `DroughtStateChanged`
- `RiverConnectivityChanged`
- `GroundwaterAvailabilityChanged`

### 19.3 Geology / terrain cause

- `GeologicalStabilityChanged`
- `GroundMotionHazardOccurred`
- `ErosionOrDepositionOccurred`
- `NaturalVoidFormationChanged`

### 19.4 Resource

- `ResourceDepositChanged`
- `ResourceDepleted`
- `RenewableResourceRecovered`

### 19.5 Ecosystem

- `PopulationStockChanged`
- `HabitatConditionChanged`
- `EcologicalCollapseOrRecoveryDetected`
- `WildlifeMaterializationRequired`

### 19.6 Contamination

- `EnvironmentalContaminationChanged`
- `ExposureConditionChanged`

### 19.7 Disaster

- `NaturalHazardStarted`
- `NaturalHazardEscalated`
- `NaturalHazardEnded`

## 20. Conflict scope / deterministic merge

主要conflict scope:

```text
environment/geology/{scope}
environment/resource/{deposit}
environment/water/{water_body_or_aquifer}
environment/atmosphere/{cell_or_scope}
environment/ocean/{scope}
environment/ecology/{population_or_patch}
environment/contaminant/{medium}/{scope}/{kind}
```

stock減算intentは`deterministic_reduce`またはowner-defined deterministic allocationを使用する。

資源・水の要求合計がavailable stockを超える場合、arrival順first-comeにはしない。Phase 4で具体allocation policyをschema/algorithm化するが、semantic priorityとstable keyにより結果を決定論化する。

## 21. Shared invariant

### 21.1 `INV-ENV-WATER-STOCK-CONTINUITY`

重要water stockのsource/sink/transferを因果的に追跡し、detail boundaryやdomain transferで理由なく増減させない。

### 21.2 `INV-ENV-RESOURCE-STOCK-CONTINUITY`

resource extraction、renewal、depletionを同一deposit lineageで追跡し、同一stockを二重消費しない。

### 21.3 `INV-ENV-GEOLOGY-SPATIAL-CONSISTENCY`

Spatial solid/void geometryとgeology/material stateを矛盾させない。

### 21.4 `INV-ENV-ECOLOGICAL-STOCK-CONTINUITY`

aggregate populationをdetail changeだけで増殖/消失させない。

### 21.5 `INV-ENV-CONTAMINANT-CONTINUITY`

emission/transport/deposition/removalのcauseを持たずに汚染mass/quantityを飛ばさない。

### 21.6 `INV-ENV-BOUNDARY-FLUX-UNIQUENESS`

地域detail boundaryを跨ぐwater、air、resource、ecological migration等をsource/target双方で二重計上しない。

## 22. Detail level別state

Environmentでは同一地域でもpartitionごとにdetailを変えられる。

ただし因果boundaryが崩れる組合せは禁止する。

### 22.1 `D0_ENTITY`

保持例:

- local 3D weather/air cell
- local river/flood extent
- groundwater local state
- soil patch
- detailed vegetation patch
- materialized wildlife individual + population backing relation
- local resource deposit distribution
- active hazard progression

### 22.2 `D1_LOCAL_AGGREGATE`

保持:

- local field aggregate
- watershed/river reach stock
- habitat/population cohort
- local resource deposit stock/quality
- local contamination field summary
- hazard boundary conditions

### 22.3 `D2_REGIONAL_AGGREGATE`

保持:

- regional climate/weather statistics + active extreme state
- water basin stock/flow
- regional ocean current/sea-state
- ecosystem population stock
- resource stock
- contamination load
- major hazard state

### 22.4 `D3_BOUNDARY_SUMMARY`

保持:

- atmospheric boundary condition
- river/water inflow-outflow
- ocean current/sea-state boundary
- ecological migration flow
- resource/exported physical flow only when ownership transfer occurs
- contaminant flux
- hazard approaching boundary fact
- archived lineage anchors

## 23. Update cadence

全Environment partitionを30Hz更新しない。

logical cadence classを次に分ける。

- `STEP`: active fast local process。必要Stepごと
- `FAST`: weather/water/active hazard等の比較的高頻度
- `NORMAL`: ecology/soil/local resource等
- `SLOW`: geology/climate/resource renewal等
- `EVENT_DRIVEN`: external source/sinkやthreshold crossingで更新

exact Step intervalはConfig。

cadenceの異なるpartitionもauthoritative World Timeは共通`SimulationStep`を使う。

更新しないStepはstateが無意味にfreezeするという意味ではなく、必要に応じrate/integrated transitionを次のscheduled updateで決定論的に積分する。

## 24. Promotion trigger

- Diver/resident local interaction
- construction/mining/agriculture/fishing等のscheduled activity
- severe weather/hazard予測
- flood/fire/pollution等の境界接近
- resource extraction開始
- boundary crossing flow増加
- predictive pre-detail policy

cameraだけをtriggerにしない。

## 25. Demotion guard

以下のときEnvironment detailを下げない。

- active severe hazard
- flood front/contaminant plume等がboundaryを通過中
- resource extraction transaction未完了
- wildlife individual/persistent identityのhandoff未完了
- active local water/air exchangeがaggregate表現で保存できない
- Spatial geometry mutationとのcross-domain transaction未完了

## 26. Deterministic promotion / demotion

### 26.1 archive優先

過去にD0/D1だった地域は、Configがarchive保持を要求する場合、過去詳細stateをlineage anchorから復元する。

### 26.2 aggregate materialization

archiveがないaggregate-native stateを詳細化する場合:

- aggregate stock
- spatial scope lineage
- environmental lineage generation
- promotion Step
- deterministic random context

から詳細stateをmaterializeする。

materialize後の総water/resource/population等がsource aggregateと整合しなければならない。

### 26.3 demotion

詳細stateをaggregateへ写像する際:

- conserved stock
- persistent identity references
- active hazard
- pollution load
- migration/flow
- detailed archive anchor

を保持する。

## 27. Boundary exchange

Environment boundary exchange例:

```text
AtmosphericBoundaryExchange {
  pressure,
  temperature,
  humidity,
  wind,
  contaminant_flux
}

HydrologyBoundaryExchange {
  water_flow,
  sediment_flow,
  quality_load
}

OceanBoundaryExchange {
  current,
  sea_state,
  thermal_flux,
  biological_flow
}
```

boundary exchangeにはbasis Stepとsource/target scopeを必須とする。

詳細領域境界だけでriver、atmosphere、ocean、ecosystemの因果が途切れてはならない。

## 28. Built / infrastructureとの境界

### 28.1 dam / levee / waterworks

- structure geometry/damage: `physical_built`
- network/service operation: `infrastructure_information`
- natural water stock/flow: `environment`

Environmentはdam等のphysical existence/capacity read modelを参照し、水flow responseを計算する。

### 28.2 underground structure

- tunnel/underground room: `physical_built`
- surrounding geology/groundwater/air natural state: `environment`
- terrain solid/void: `spatial`

浸水/崩落/換気等をcross-domain event/intentで接続する。

### 28.3 agriculture

- crop/land-use operation: society/resident/physical depending later P3 ownership
- soil/water/weather/ecosystem natural condition: Environment

収穫物inventoryはEnvironmentへ残さず、physical/economic stateへhandoffする。

## 29. Residentとの境界

Environmentがresidentへ提供するtruth condition:

- local temperature
- rain/wind
- air quality
- visibility
- water availability/quality
- environmental hazard
- habitat/food source condition

residentが実際に知覚・理解するかは`resident.perception`側。

Environmentが天候を変えたから住人が自動で「知った」ことにはしない。

## 30. Initial world generation

初期自然worldは因果順を持つ。

概念pipeline:

```text
WorldSeed + InitializationConfig
 -> global plates / large-scale geology constraints
 -> Spatial terrain candidate
 -> ocean/land partition
 -> large-scale current / wind forcing
 -> climate baseline
 -> river/lake/groundwater
 -> soil
 -> vegetation/ecosystem
 -> resource deposits
 -> natural hazard baseline
 -> detailed regional materialization
 -> prehistory simulation
```

これはexact algorithm orderを固定するものではないが、自然layerを互いに独立random mapとして生成しない。

coarse mapの各stateはlineageを持ち、後続detail materializationのdeterministic sourceになる。

## 31. Natural hazard causality examples

### 31.1 flood

```text
precipitation / snowmelt
 -> soil saturation / runoff
 -> river/lake water rise
 -> overflow / flood extent
 -> Spatial/Physical accessibility change
 -> Resident/Infrastructure consequence
```

### 31.2 landslide

```text
geology + slope + water + disturbance
 -> stability reduction
 -> hazard threshold
 -> Spatial deform intent
 -> building/road impact
```

### 31.3 drought

```text
climate/weather deficit
 -> soil/groundwater/surface water reduction
 -> agriculture/ecosystem/water service pressure
 -> economy/resident consequence
```

### 31.4 earthquake

```text
tectonic/geologic state
 -> ground motion hazard
 -> Spatial deformation candidate
 -> built damage / infrastructure outage
 -> resident injury / emergency response
```

被害側をEnvironmentが直接mutationしない。

## 32. Persistence / Replay

snapshot/replayでは少なくとも次を再現可能にする。

- environment partition state
- lineage/detail state
- scheduled cadence basis
- source/sink/handoff event
- hazard source process
- deterministic materialization context
- boundary flux

同じSeed/Config/inputでweather/hazard/resource outcomeがworker orderやCPU速度に依存して変化してはならない。

## 33. Publication boundary

View向けに次をprojection可能にする。

- weather
- water extent
- ocean state
- terrain-coupled natural layer
- ecosystem/vegetation summary
- hazard
- contamination/visibility where allowed

View visual effectはprojectionであり、particle数やrender qualityをEnvironment authorityへ戻さない。

## 34. Traceability

| Requirement | Coverage |
|---|---|
| Q003/Q004 | geology→terrain→climate/water/ecology/resourceの因果初期化 |
| Q022 | agriculture/fishing/ecosystem natural basis |
| Q023 | natural disasterをunderlying stateから因果生成 |
| Q028 | natural resource extraction/depletion basis |
| Q029 | dynamic weather/climate |
| Q038 | air/water/soil contaminant state |
| Q039 | weather/smoke/wildland fuelのfire coupling |
| Q040 | natural waterとwater infrastructureの責務分離 |
| Q052 | resource depletion/renewal |
| Q053 | ecosystem degradation/recovery |
| Q073 | natural light/day-night forcingのenvironment coupling |
| Q077 | resident thermal conditionへ環境truthを提供 |
| Q085/Q086 | erosion/waterによるdynamic terrain/river/coast coupling |
| Q090 | 3D geology/resource placement |
| Q091 | mining extraction stock transfer |
| Q093/Q094 | geology/groundwater/underground environment coupling |
| Q095/Q096 | atmospheric/indoor/subsurface air exchange boundary |
| Q097 | flood/leak water extent basis |
| Q098 | ocean dynamics |
| Q099 | natural cave formation cause |
| Q110〜Q114 | wildlife/vegetation/food web aggregate/detail model |
| Q118 | important material/resource flow continuity |
| Q175/Q176 | weather/environmentをaccident causeへ提供 |
| Q185/Q186 | common natural resource/overuse basis |
| Q190〜Q194 | variable detail and environmental boundary flux |
| Q265/Q266 | identity/materializationとupdate detailを分離 |

## 35. Phase 4 handoff

Phase 4で確定する事項:

- atmosphere/weather numerical state schema
- hydrology/ocean solver class
- geology/soil representation
- exact resource deposit distribution schema
- ecosystem cohort schema
- contaminant transport algorithm
- hazard threshold schema
- cadence interval defaults
- boundary exchange numerical encoding
- conservation tolerance/rounding
- initial world generation algorithms
- climate/weather deterministic random addressing

Phase 4のalgorithm choiceは、本書で固定したowner、causality、stock continuity、detail semanticsを変更してはならない。
