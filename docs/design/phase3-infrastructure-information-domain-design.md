# 詳細設計 Phase 3: Infrastructure / Information Domain設計

Status: Complete / P3-07  
Tracking: Issue #15  
Parent: `phase3-world-domain-design.md`  
Common contract: `phase3-domain-common-contract.md`

## 1. 目的

`infrastructure_information` domainは、交通、水、電力、通信等のnetwork/service operational state、施設service capacity/queue、情報delivery、media delivery、record/document storage/retrieval state、およびinfrastructure間dependencyをauthoritative stateとして所有する。

本domainはphysical assetそのものや自然資源、情報内容の真偽、Residentの信念を所有しない。物理設備が存在することとserviceが利用可能であることを分離し、上流供給、network connectivity、容量、障害、運用状態からservice consequenceを導出する。

## 2. Responsibility / Non-responsibility

### 2.1 InfrastructureInformationが所有する責務

- transport network logical topology/service state
- route/service availability、capacity、schedule、congestion/service load
- water intake/distribution/sewer service network state
- electric generation service/feed/distribution logical state
- communication network reachability/capacity/delay/availability
- infrastructure dependency graph and outage propagation
- facility service capacity、queue、reservation/assignment operational state
- message/information delivery lifecycle
- media publication/distribution delivery state
- record/document storage、version、availability、retrieval lifecycle
- service outage/degradation/recovery state
- operational maintenance/service routing relation

### 2.2 InfrastructureInformationが所有しない責務

- road/rail/pipe/power plant/antenna/building physical geometry/condition: `physical_built`
- natural water/energy resource/weather: `environment`
- actual vehicle/person/item pose/movement: `physical_built`
- shipment economic contract: `society_economy`
- information claim/content social provenance: `society_economy`
- Resident knowledge/belief/perception: `resident`
- public-record legal effect/authority: `governance_security`
- land/permit/ownership: Society/Governance

## 3. State partitions

```text
infrastructure.network_topology
infrastructure.transport_service
infrastructure.water_service
infrastructure.power_service
infrastructure.communication_service
infrastructure.dependency
infrastructure.facility_service
infrastructure.service_queue
information.delivery
information.media_distribution
information.record_store
information.address_place_index
infrastructure.failure_recovery
infrastructure.lineage
```

## 4. Common network model

### 4.1 `InfrastructureNetworkState`

```text
InfrastructureNetworkState {
  network_id,
  network_kind,
  node_refs,
  edge_refs,
  service_scope_refs,
  operator_refs,
  dependency_refs,
  capacity_summary,
  availability_state,
  detail_level,
  revision
}
```

physical node/edge assetはPhysicalBuilt参照。

logical edgeが存在しても、physical asset damageやupstream service喪失によりunavailableになり得る。

### 4.2 `InfrastructureNodeState`

```text
InfrastructureNodeState {
  node_id,
  network_ref,
  physical_asset_refs,
  function_class,
  nominal_capacity,
  effective_capacity,
  input_dependency_refs,
  output_interface_refs,
  operational_state
}
```

## 5. Dependency graph

```text
InfrastructureDependency {
  dependency_id,
  consumer_node_or_service,
  provider_service_ref,
  dependency_kind,
  minimum_service_condition,
  degradation_function_ref,
  fallback_refs?,
  status
}
```

例:

- water pump -> electric power
- communication site -> electric power
- hospital service -> power + water + communication + staff
- railway signal -> power + communication
- fuel distribution -> transport

局所設備が正常でもrequired upstream serviceが失われればservice degrade/stopできる。

## 6. Transport network/service

### 6.1 ownership split

- PhysicalBuilt: road/rail/vehicle physical state and actual movement
- InfrastructureInformation: route topology、service schedule、capacity、network availability、logical congestion state
- SocietyEconomy: fare/contract/shipment economic obligation
- Resident: route choice/knowledge/action decision

### 6.2 `TransportNetworkState`

```text
TransportNetworkState {
  transport_network_id,
  mode_class,
  route_edges,
  interchange_refs,
  capacity_state,
  congestion_state,
  disruption_refs,
  service_refs,
  detail_level
}
```

### 6.3 `TransportServiceState`

```text
TransportServiceState {
  service_id,
  operator_ref,
  mode_class,
  route_ref,
  schedule_state,
  vehicle_assignment_refs,
  passenger_or_cargo_capacity,
  service_status,
  delay_state,
  queue_refs
}
```

actual車両がphysicalに存在しなければ運行済みとしない。

### 6.4 congestion

congestionはPhysicalBuilt occupancy/movement factとTransport capacityを入力に、network service performanceを更新する。

D2/D3ではindividual vehicle軌跡をaggregate flowへ簡略化できるが、identity-bearing trip/cargo boundary handoffは失わない。

## 7. Water / sanitation service

### 7.1 `WaterServiceNetworkState`

```text
WaterServiceNetworkState {
  water_network_id,
  source_interface_refs,
  treatment_node_refs,
  storage_node_refs,
  distribution_refs,
  sewer_refs,
  treatment_capacity,
  supply_capacity,
  demand_state,
  quality_service_state,
  operational_state
}
```

### 7.2 natural/physical/service split

- Environment: river/lake/groundwater, natural water stock/quality
- PhysicalBuilt: pipe/pump/tank/treatment facility physical condition
- InfrastructureInformation: intake/distribution/sewer/treatment service operation/capacity

intake intentでEnvironment water stockを減らし、service network stock/flowへhandoffする。

排水は処理後/未処理stateに応じEnvironmentへdischarge intentを返す。

### 7.3 no universal utility assumption

water networkが存在しない社会ではwell/carrying等のphysical/resident action経路を使用できる。近代水道を全時代の前提にしない。

## 8. Power service

```text
PowerServiceState {
  power_network_id,
  generation_service_refs,
  transmission_distribution_refs,
  supply_state,
  demand_state,
  reserve_or_margin_state,
  outage_scope_refs,
  operational_state,
  detail_level
}
```

standardは詳細AC power flow/周波数modelを必須としないが、generation、capacity、network reachability、demand、failureから停電/供給不足を生じさせる。

fuel/resource availabilityはEnvironment/Society、generator physical conditionはPhysicalBuilt。

## 9. Communication network

### 9.1 `CommunicationNetworkState`

```text
CommunicationNetworkState {
  communication_network_id,
  technology_class,
  node_refs,
  link_refs,
  reachability_state,
  capacity_state,
  delay_state,
  failure_state,
  service_scope_refs,
  detail_level
}
```

郵便、電信、電話、無線、digital等を時代/technologyに応じて表現可能にする。

packet-level routingやradio spectrum detailは標準必須でない。

### 9.2 message delivery

```text
InformationDeliveryState {
  delivery_id,
  content_or_claim_ref,
  sender_ref,
  recipient_or_audience_ref,
  channel_ref,
  origin_step,
  route_or_hop_summary,
  scheduled_arrival_context,
  delivery_status,
  integrity_or_loss_state,
  capacity_consumption,
  causality_refs
}
```

information contentはSocietyEconomy等のclaim refを参照する。

受信後にResidentが理解/信じるかはResident domain。

### 9.3 delivery lifecycle

```text
CREATED -> ACCEPTED_BY_CHANNEL -> IN_TRANSIT -> DELIVERED
                                      -> DELAYED / FAILED / LOST
```

exact statesはPhase 4。

通信設備/経路/容量/故障を無視してremote deliveryを即時完了しない。

## 10. Physical mail / courier boundary

郵便等ではlogical deliveryだけでなくphysical item/worker/vehicle movementが必要になり得る。

```text
Delivery obligation
 -> physical carrier/cargo movement
 -> arrival fact
 -> information delivery completed
```

手段ごとにphysical coupling levelを定義可能にする。

## 11. Media distribution

### 11.1 ownership split

- SocietyEconomy: media organization、editorial/social information claim
- InfrastructureInformation: publication channel、distribution、audience reach、capacity/delay
- Resident: actual receipt/belief

### 11.2 `MediaDistributionState`

```text
MediaDistributionState {
  publication_id,
  publisher_ref,
  content_claim_refs,
  channel_refs,
  intended_scope,
  distributed_scope,
  distribution_step_range,
  reach_state,
  failure_or_censorship_route_refs?,
  status
}
```

「公開した」ことと「全Residentが受け取った」ことを分離する。

## 12. Records / documents

### 12.1 record truth separation

公的/民間recordはCore truthそのものではない。

```text
RecordArtifactState {
  record_id,
  record_kind,
  issuer_or_creator_ref,
  semantic_content_ref,
  created_step,
  record_version,
  storage_refs,
  access_state,
  availability_state,
  integrity_state,
  supersedes_refs?,
  status
}
```

InfrastructureInformationはrecord carrier/store/version/retrieval availabilityをownerする。

法的効力/registration authorityはGovernance、economic meaningはSociety、Residentが知っている内容はResident。

recordは誤り、古さ、虚偽、紛失、破損を許容する。

### 12.2 `RecordStoreState`

```text
RecordStoreState {
  store_id,
  physical_facility_refs,
  operator_ref,
  record_refs,
  capacity_state,
  indexing_state,
  access_service_state,
  damage_or_loss_state,
  detail_level
}
```

## 13. Address / place identification

社会的な住所・場所識別をrouting/indexに接続できる。

```text
PlaceIdentifierState {
  place_identifier_id,
  naming_authority_or_social_source_ref,
  identifier_components,
  spatial_or_structure_ref,
  effective_period,
  status
}
```

Spatial geometryそのものではなく、social/information layerのidentifierとして扱う。

## 14. Facility service capacity

### 14.1 `FacilityServiceState`

```text
FacilityServiceState {
  facility_service_id,
  facility_ref,
  provider_ref,
  service_kind,
  staffing_refs,
  equipment_refs,
  operating_schedule,
  nominal_capacity,
  effective_capacity,
  queue_ref,
  reservation_state,
  availability_state
}
```

対象例:

- medical
- administrative
- retail
- education
- transport terminal
- entertainment

### 14.2 capacity derivation

実効capacityは少なくとも:

- staff presence/skill/health
- equipment availability/condition
- room/bed/seat/desk/machine physical availability
- power/water/communication dependencies
- business hours/schedule
- active failure

から変化できる。

## 15. Queue / Reservation

```text
ServiceQueueState {
  queue_id,
  service_ref,
  waiting_request_refs,
  in_service_refs,
  reservation_refs,
  priority_classes,
  allocation_state,
  cancellation_refs,
  revision
}
```

C相当として必要領域では個別requestのqueue/予約/priority/cancel/allocationを保持する。

arrival orderをsemantic priority以外のnondeterministic inputにしない。到着Step/Operation canonical keyを使う。

## 16. Service request lifecycle

```text
REQUESTED
 -> ACCEPTED_OR_QUEUED
 -> ASSIGNED
 -> SERVICE_IN_PROGRESS
 -> COMPLETED
```

分岐:

```text
CANCELLED / LEFT_QUEUE / REJECTED / FAILED
```

service requestを受付しただけでservice resultを即時生成しない。

Residentは待ち時間を認識して離脱/予定変更できる。

## 17. Failure / degradation / recovery

```text
InfrastructureFailureState {
  failure_id,
  affected_node_or_service_refs,
  source_cause_refs,
  severity,
  started_step,
  degradation_state,
  dependency_propagation_refs,
  workaround_refs,
  repair_refs,
  recovered_step?,
  status
}
```

source:

- physical damage
- weather/disaster
- resource/fuel shortage
- capacity overload
- upstream outage
- maintenance failure
- war/security event

repair actual workはPhysicalBuilt。service restoreはrepair result + upstream dependenciesが満たされてから確定する。

## 18. Cascading failure

dependency graphでdeterministic propagationを行う。

例:

```text
power outage
 -> communication site down
 -> dispatch communication degraded
 -> emergency response capacity reduced
 -> water pump down
 -> water service degraded
```

same-Stepに無限cycleを作らないよう、dependency SCCが存在する場合は明示的なcoupled resolution policyかnext-step feedbackを使用する。

## 19. Infrastructure maintenance

- maintenance schedule/operation plan: Society/Infrastructure relation
- actual worker/material/repair: PhysicalBuilt
- operational service state: InfrastructureInformation

定期点検/repairを省略した場合、condition/failure riskへ影響できる。

## 20. Update phases

### 20.1 PREPARE

- physical asset condition facts
- environment supply/forcing
- service requests
- network/config/schedule changes
- queue inputs
- cross-network dependency facts

をfreeze。

### 20.2 INFRASTRUCTURE_SERVICE logical subphases

```text
I0_TOPOLOGY_AVAILABILITY
I1_RESOURCE_SUPPLY
I2_TRANSPORT_SERVICE
I3_WATER_POWER_SERVICE
I4_COMMUNICATION_DELIVERY
I5_FACILITY_CAPACITY_QUEUE
I6_DEPENDENCY_PROPAGATION
I7_RECORD_MEDIA_SERVICE
```

### 20.3 CONSEQUENCE

- outage/degradation
- delivery/arrival
- queue/service result
- physical movement/service intent
- Environment intake/discharge
- Resident information receipt
- Society logistics/business fulfillment

を生成。

### 20.4 VALIDATE

- topology refs
- capacity non-double-allocation
- resource/service continuity
- queue uniqueness
- delivery lifecycle
- dependency cycle policy
- boundary exchange

を検証。

## 21. Same-Step dependency

基本:

```text
State(S) physical/environment/network
 -> service availability/capacity
 -> request allocation/delivery
 -> cross-domain service result
 -> consequence State(S+1)
```

water intake等でexplicit same-step resource allocationが必要ならEnvironmentのmerge済みallocation factを読む。

outage cascadeはdomain内DAGまたはbounded coupled-resolutionとして定義し、thread recursionに依存させない。

## 22. Intent catalog

- `infrastructure.transport.service_request`
- `infrastructure.route.reserve_capacity`
- `infrastructure.water.intake_request`
- `infrastructure.water.service_request`
- `infrastructure.power.service_request`
- `information.delivery.request`
- `information.media.publish`
- `information.record.create/update/retrieve`
- `infrastructure.facility.service_request`
- `infrastructure.queue.cancel`
- `infrastructure.failure.report`
- `infrastructure.recovery.apply`

## 23. Event catalog

- `InfrastructureAvailabilityChanged`
- `TransportServiceChanged`
- `CongestionChanged`
- `WaterServiceChanged`
- `PowerServiceChanged`
- `CommunicationServiceChanged`
- `InformationDelivered`
- `InformationDeliveryFailed`
- `MediaDistributionChanged`
- `RecordAvailabilityChanged`
- `FacilityCapacityChanged`
- `ServiceQueueChanged`
- `ServiceCompleted`
- `InfrastructureDependencyFailurePropagated`

## 24. Conflict scope

```text
infrastructure/network/{network_id}
infrastructure/node/{node_id}
infrastructure/capacity/{service_id}/{time_bucket}
infrastructure/queue/{queue_id}
information/delivery/{delivery_id}
information/record/{record_id}
infrastructure/dependency/{consumer_ref}
```

有限capacityへの複数requestはstable priority + canonical keyでallocationする。

thread/arrival timingだけでslotを決めない。

## 25. Shared invariant

### 25.1 `INV-INFRA-PHYSICAL-ASSET-BASIS`

存在しない/使用不能physical assetをservice networkのactive capacityとして無条件利用しない。

### 25.2 `INV-INFRA-RESOURCE-SUPPLY-CONTINUITY`

water/fuel/power等のsource supplyとservice outputをcause-linkedにし、service layerで資源を生成しない。

### 25.3 `INV-INFRA-CAPACITY-UNIQUENESS`

同一finite capacityを同時に二重割当しない。

### 25.4 `INV-INFRA-DELIVERY-NON-OMNISCIENCE`

delivery完了なしにremote recipientへ情報receiptを自動付与しない。

### 25.5 `INV-INFRA-RECORD-TRUTH-SEPARATION`

record contentをCore truthの自動mirrorにしない。

### 25.6 `INV-INFRA-DEPENDENCY-CONSISTENCY`

required upstream service不成立時にdependent serviceを理由なくfully availableとしない。

## 26. Detail level

### 26.1 `D0_ENTITY`

- individual network nodes/links where active
- vehicle/service assignments
- individual communication delivery
- facility queue/request
- active outage/dependency
- detailed record access

### 26.2 `D1_LOCAL_AGGREGATE`

- local capacity/topology
- traffic flow + persistent trip refs
- water/power demand/supply by local node group
- communication route summary
- queue cohort + priority anchors

### 26.3 `D2_REGIONAL_AGGREGATE`

- regional network connectivity/capacity
- transport flow/service frequency
- water/power supply-demand
- communication delay/reachability
- facility service rates/queue distributions
- persistent delivery/record refs where required

### 26.4 `D3_BOUNDARY_SUMMARY`

- transport flow
- utility import/export
- communication boundary capacity/delay
- information deliveries crossing boundary
- cross-region service dependencies
- persistent outage/record anchors

## 27. Update cadence

- `STEP`: active movement/queue/critical control where needed
- `FAST`: traffic/communication/power/water active service
- `NORMAL`: facility queues/transport schedules
- `SLOW`: network planning/record archive/index
- `EVENT_DRIVEN`: outage/recovery/message/record update

exact intervalはConfig。

## 28. Promotion / Demotion

promotion trigger:

- Diver/resident local service use
- network failure
- congestion/overload
- emergency response
- large shipment/transport event
- communication burst/critical message
- utility shortage
- predictive detail policy

Demotion guard:

- active boundary transfer
- in-flight identity-bearing delivery
- unresolved queue assignment
- critical outage cascade
- resource handoff incomplete
- active emergency service
- record transaction incomplete

## 29. Boundary exchange

```text
InfrastructureBoundaryExchange {
  source_scope,
  target_scope,
  basis_step,
  transport_flow,
  water_flow,
  power_flow,
  communication_capacity,
  pending_delivery_refs,
  service_dependency_refs,
  outage_refs
}
```

cross-boundary flowをsource/target双方で二重countしない。

## 30. Emergency response integration

Emergency response自体は複数domain orchestrationになる。

- Governance: authority/dispatch mandate
- InfrastructureInformation: call delivery、dispatch service、route/service availability、facility capacity
- Resident: responder decisions/skills/health
- PhysicalBuilt: actual responder/vehicle movement/equipment use
- SocietyEconomy: organization/employment/resource relation

「通報した瞬間に救助完了」にはしない。

## 31. Persistence / Replay

persist/replay:

- network topology/service state
- capacity/allocation
- queue lifecycle
- utility intake/supply
- communication delivery lifecycle
- media distribution
- record versions/store availability
- dependency/outage/recovery
- detail transitions

## 32. Traceability

| Requirement | Coverage |
|---|---|
| Q027 | transport/logistics network/service with actual physical movement |
| Q040 | natural water vs water utility separation |
| Q041 | power/communication equipment/service/failure |
| Q047 | infrastructure service state contributes to settlement/economy |
| Q063 | public service capacity/operation boundary |
| Q069 | transport infrastructure/network operation |
| Q074 | time/calendar/schedule input for services |
| Q095/Q096 | ventilation/air exchange equipment dependency boundary |
| Q101 | cargo transport service coupled to physical cargo |
| Q108 | public infrastructure project results become network service after construction |
| Q125〜Q127 | media/communication delivery via real networks |
| Q131 | remote interpersonal communication delivery |
| Q145 | finite service queue/capacity C-level |
| Q148 | emergency dispatch/transport/communication capacity |
| Q149 | record artifact can be wrong/old/lost, not Core truth |
| Q157/Q158 | reservation/capacity/business schedule |
| Q159 | address/place identification |
| Q168/Q169 | economic info/time recognition depends on delivery/schedule semantics |
| Q179 | warnings must actually reach recipients |
| Q180 | census/statistical records as derived records, not omniscient resident knowledge |
| Q189 | infrastructure dependency cascades |
| Q190〜Q194 | network flows across detail boundaries |

## 33. Phase 4 handoff

Phase 4で確定する事項:

- common network graph schema
- transport routing/capacity/congestion algorithm
- water network balance model
- power supply allocation model
- communication delay/capacity model
- information delivery schema
- record/document storage schema
- facility queue/allocation algorithm
- infrastructure dependency propagation algorithm
- outage/recovery schema
- boundary exchange encoding
- detail/cadence defaults

Phase 4はphysical/service separation、non-omniscient information delivery、finite capacity、dependency propagation semanticsを変更してはならない。
