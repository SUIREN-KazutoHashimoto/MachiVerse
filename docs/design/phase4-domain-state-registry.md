# 詳細設計 Phase 4: Domain State / Partition Registry

Status: Complete / P4-01 registry  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`  
Common structures: `phase4-core-data-structures.md`  
Predecessor: Phase 3 domain designs / completion review

## 1. 目的

Phase 3で確定した8 domain family・97 authoritative state partitionを、実装時にそのままregistryへ登録できる安定したpartition identity、owner、schema identity、record container、primary key、canonical orderingへ固定する。

本書はdomain algorithmそのものを固定しない。weather solver、geometry representation、Resident cognition、market algorithm等のpayload内部数値表現はP4-05で具体化するが、それらがどのauthoritative partitionに属し、どのkeyでrecordを識別し、どのschema/versionで永続化されるかは本書で固定する。

## 2. Registry invariant

Phase 4標準構成では、Phase 3のauthoritative partitionをすべて次のregistryへ登録する。

```text
DomainPartitionRegistryV1 {
  schema: SchemaRefV1 = { schema_id = "core.domain-partition-registry", version = 1.0 },
  registry_generation: uint32,
  entries: ordered map<PartitionId, DomainPartitionRegistrationV1>
}
```

```text
DomainPartitionRegistrationV1 {
  partition_id: PartitionId,
  owner_domain: DomainToken,
  partition_schema: SchemaRefV1,
  record_schema: SchemaRefV1,
  primary_key_kind: PrimaryKeyKindV1,
  persistence_class: PersistenceClassV1,
  canonical_order: CanonicalOrderKindV1,
  detail_capabilities: bitset,
  required_indexes: ordered list<IndexId>,
  invariant_ids: ordered list<InvariantId>
}
```

標準entry数は **97**。

- duplicate `partition_id`: component start reject。
- owner mismatch: component start reject。
- unknown required partition: component start reject。
- persisted worldに存在するstandard partitionをregistryから無言で削除しない。
- partition split/merge/owner変更はpersistence major migrationを要求する。

## 3. Schema identity rule

各standard partitionについて、schema identityを次で固定する。

```text
partition_schema_id = "domain." + partition_id
record_schema_id    = "domain." + partition_id + ".record"
```

例:

```text
partition_id        = "resident.body_health"
partition_schema_id = "domain.resident.body_health"
record_schema_id    = "domain.resident.body_health.record"
```

Phase 4 initial versionはすべて:

```text
SchemaVersion { major = 1, minor = 0 }
```

とする。

formulaは命名補助ではなくstandard registryのnormative ruleである。一度永続化したpartition tokenをrenameしない。

## 4. Primary key / record identity

### 4.1 `PartitionRecordId`

```text
PartitionRecordId := 128-bit opaque value
```

- binary canonical form: 16 octets。
- ZERO invalid。
- canonical comparison: 16 octets bytewise ascending。
- database row id、memory address、container indexを使用しない。

既にdomain identityを持つrecordでは、そのidentityを`PartitionRecordId`として使用する。

例:

- Resident: `ResidentId`
- Organization: `OrganizationId`
- Binding: `BindingId`
- SpatialScope: `scope_id`
- infrastructure network: `network_id`

relationやaggregate等、単独の既存identityがないrecordはdeterministic creation contextから導出する。

```text
PartitionRecordId = Trunc128(DomainHash(
  "mv.partition-record.v1",
  {
    world_id,
    partition_id,
    creation_kind,
    stable_subject_refs,
    creation_event_or_operation_ref,
    stable_local_ordinal
  }
))
```

`stable_subject_refs`はschema指定順でcanonical sortし、thread completion orderを使用しない。

### 4.2 Primary key kind

Phase 4 standard domain partitionsは全て次を使用する。

```text
PrimaryKeyKindV1 := RECORD_ID_128
```

複合検索はsecondary indexで行い、physical composite DB primary keyをauthoritative identityにしない。

## 5. Domain partition physical-logical container

各standard authoritative partitionは論理的に次へ写像する。

```text
DomainPartitionStateV1 {
  header: PartitionStateHeaderV1,
  records: ordered map<PartitionRecordId, DomainRecordEnvelopeV1>,
  index_catalog: ordered map<IndexId, PartitionIndexDescriptorV1>
}
```

```text
DomainRecordEnvelopeV1 {
  record_id: PartitionRecordId,
  record_schema: SchemaRefV1,
  revision: uint64,
  created_step: SimulationStep,
  retired_step: SimulationStep | NONE,
  detail_level: DetailLevelV1,
  lineage_ref: PartitionRecordId | NONE,
  payload: DomainOwnedPayload
}
```

### 5.1 Record revision

- initial revision: `1`。
- authoritative field変更ごとに+1。
- no-op updateでは増加させない。
- wrap禁止。
- persistence storage generationやrow MVCC versionとは別物。

### 5.2 Retirement

world history上のidentityを維持すべきrecordは、削除の代わりに`retired_step`やdomain lifecycle fieldでretireできる。

persistent identity-bearing entity、契約、法的記録、binding等をdetail demotionだけで物理削除しない。

### 5.3 Canonical ordering

全standard partition:

```text
CanonicalOrderKindV1 := RECORD_ID_BYTEWISE_ASC
```

state digest、snapshot serialization、replay diagnostic、full publication projectionをrecord table iterationへ依存させる場合、必ずこの順序へnormalizeする。

## 6. Index contract

```text
IndexId := StableToken
```

```text
PartitionIndexDescriptorV1 {
  index_id: IndexId,
  partition_id: PartitionId,
  authority: IndexAuthorityV1,
  key_schema: SchemaRefV1,
  value_schema: SchemaRefV1,
  uniqueness: IndexUniquenessV1,
  canonical_key_order: CanonicalOrderKindV1,
  rebuild_recipe: SchemaRefV1 | NONE
}
```

```text
IndexAuthorityV1 :=
  AUTHORITATIVE
  | DERIVED_REBUILDABLE
```

原則としてdomain recordがauthorityで、検索用secondary indexは`DERIVED_REBUILDABLE`とする。

authoritative indexが必要な場合は、そのindex自体をstate partitionとして登録する。

標準共通index:

| index_id | key | value | authority |
|---|---|---|---|
| `core.partition.record-by-id` | `(partition_id, record_id)` | record ref | authoritative root lookup |
| `core.reference.reverse` | canonical `CausalityRefV1` | ordered record refs | derived rebuildable |
| `core.partition.by-owner` | domain token | ordered partition ids | derived registry index |
| `core.partition.by-schema` | schema id/version | ordered partition ids | derived registry index |

Domain-specific secondary indexはP4-05で追加できるが、record authorityを置換しない。

## 7. Persistence class

Phase 3でauthoritative ownerと確定した下記97 partitionは全て初期標準で:

```text
PersistenceClassV1 = AUTHORITATIVE_ALWAYS
```

とする。

理由:

- P4-05でlossless reconstruction recipeが正式に定義・versioned・acceptance testされる前にauthoritative partitionをsnapshotから省略しない。
- 将来一部を`AUTHORITATIVE_RECONSTRUCTABLE_WITH_RECIPE`へ変更する場合はpersistence schema minor/major compatibility reviewを行う。

## 8. Spatial registry — 8 partitions

Owner: `spatial`

| partition_id | record semantic | key |
|---|---|---|
| `spatial.world_frame` | world/local frame definition | `PartitionRecordId` |
| `spatial.scope_registry` | domain-neutral 3D scope | `PartitionRecordId` |
| `spatial.terrain_geometry` | terrain solid/void boundary revision | `PartitionRecordId` |
| `spatial.void_geometry` | natural void/cave identity and connectivity | `PartitionRecordId` |
| `spatial.containment_topology` | containment relation | `PartitionRecordId` |
| `spatial.boundary_topology` | boundary interface | `PartitionRecordId` |
| `spatial.detail_regions` | simulation detail region geometry/state | `PartitionRecordId` |
| `spatial.geometry_lineage` | geometry/source lineage | `PartitionRecordId` |

Required owner invariant:

- natural terrain solid/void geometry authorityは`spatial`のみ。
- building/built geometryを本partitionへ移さない。
- geology/material compositionを本partitionへ移さない。

## 9. Environment registry — 13 partitions

Owner: `environment`

| partition_id | record semantic | key |
|---|---|---|
| `environment.geology` | geology volume/material natural state | `PartitionRecordId` |
| `environment.soil` | soil patch state | `PartitionRecordId` |
| `environment.resource_deposit` | natural resource deposit/stock | `PartitionRecordId` |
| `environment.groundwater` | groundwater volume/flow state | `PartitionRecordId` |
| `environment.atmosphere` | atmosphere field/cell state | `PartitionRecordId` |
| `environment.climate` | climate aggregate/regime state | `PartitionRecordId` |
| `environment.weather` | weather state/event driver | `PartitionRecordId` |
| `environment.surface_water` | river/lake/runoff surface water state | `PartitionRecordId` |
| `environment.ocean` | ocean body/cell state | `PartitionRecordId` |
| `environment.ecosystem` | ecosystem population/cohort state | `PartitionRecordId` |
| `environment.contaminant` | contaminant concentration/stock | `PartitionRecordId` |
| `environment.hazard` | natural hazard driver/state | `PartitionRecordId` |
| `environment.environment_lineage` | environment aggregate/materialization lineage | `PartitionRecordId` |

Required owner invariant:

- terrain geometryはSpatialへintentする。
- Resident healthを直接mutateしない。
- natural resource stockとcommercial ownershipを分離する。

## 10. Physical / Built registry — 11 partitions

Owner: `physical_built`

| partition_id | record semantic | key |
|---|---|---|
| `physical.presence` | actual physical pose/location/motion | `PartitionRecordId` |
| `physical.occupancy` | collision/occupancy state | `PartitionRecordId` |
| `built.structure` | built structure geometry/physical state | `PartitionRecordId` |
| `built.space` | room/passages/interior physical space | `PartitionRecordId` |
| `built.opening` | door/window/gate mechanism | `PartitionRecordId` |
| `physical.container_location` | carried/contained/stored physical location | `PartitionRecordId` |
| `built.worksite` | construction/demolition physical progress | `PartitionRecordId` |
| `physical.condition` | item/vehicle/equipment physical condition | `PartitionRecordId` |
| `physical.combustion` | built/item combustion state | `PartitionRecordId` |
| `physical.material_handoff` | cross-domain physical material transfer | `PartitionRecordId` |
| `physical.lineage` | physical material/build lineage | `PartitionRecordId` |

Required owner invariant:

- intention/action decisionはResident。
- legal/economic ownershipはSociety/Governance。
- road/power/water/communication service availabilityはInfrastructureInformation。

## 11. Participation registry — 5 partitions

Owner: `participation`

| partition_id | record semantic | key |
|---|---|---|
| `participation.binding` | Diver↔Resident binding lifecycle | `PartitionRecordId` |
| `participation.absence_policy` | effective absence behavior policy | `PartitionRecordId` |
| `participation.control_mode` | world-effective resident control mode | `PartitionRecordId` |
| `participation.history` | binding/policy semantic history anchor | `PartitionRecordId` |
| `participation.detail_requirement` | bound-resident detail floor requirement | `PartitionRecordId` |

Required owner invariant:

- account/session/auth credentialを保存しない。
- network disconnectだけでactive bindingをretireしない。
- Resident lifecycle authorityを奪わない。

## 12. Resident registry — 13 partitions

Owner: `resident`

| partition_id | record semantic | key |
|---|---|---|
| `resident.identity_lifecycle` | Resident identity/lifecycle | `PartitionRecordId` |
| `resident.body_health` | body/health/injury/disease | `PartitionRecordId` |
| `resident.physiology` | hunger/thirst/sleep/thermal/fatigue | `PartitionRecordId` |
| `resident.perception` | perception/attention result | `PartitionRecordId` |
| `resident.knowledge_belief` | knowledge/belief/confidence | `PartitionRecordId` |
| `resident.memory` | memory state | `PartitionRecordId` |
| `resident.psychology` | emotion/stress/personality/preference | `PartitionRecordId` |
| `resident.goal_plan` | goal/plan/routine | `PartitionRecordId` |
| `resident.skill_aptitude` | skill/aptitude/practice state | `PartitionRecordId` |
| `resident.relationship` | interpersonal relation | `PartitionRecordId` |
| `resident.family_lineage` | parent/child/family lineage | `PartitionRecordId` |
| `resident.behavior_state` | behavior/action decision state | `PartitionRecordId` |
| `resident.lineage` | materialization/identity lineage | `PartitionRecordId` |

Required owner invariant:

- physical pose/locationはPhysicalBuilt。
- organization/employment/property/economic contractはSocietyEconomy。
- law/legal judgmentはGovernanceSecurity。
- delivery/network truthはInfrastructureInformation。

## 13. Society / Economy registry — 16 partitions

Owner: `society_economy`

| partition_id | record semantic | key |
|---|---|---|
| `society.organization` | organization identity/lifecycle | `PartitionRecordId` |
| `society.membership_role` | membership/role relation | `PartitionRecordId` |
| `society.employment` | employment/job/compensation obligation | `PartitionRecordId` |
| `society.household` | household/economic unit | `PartitionRecordId` |
| `society.contract_claim` | contract/debt/claim | `PartitionRecordId` |
| `society.property_right` | social/economic ownership/right | `PartitionRecordId` |
| `society.currency_money` | currency/money issuance/state | `PartitionRecordId` |
| `society.finance_account` | financial account/asset/settlement | `PartitionRecordId` |
| `society.market_transaction` | offer/demand/trade/price history | `PartitionRecordId` |
| `society.business_production` | business/production economic state | `PartitionRecordId` |
| `society.logistics_obligation` | shipment/consignment/logistics contract | `PartitionRecordId` |
| `society.education` | education institution/relation | `PartitionRecordId` |
| `society.culture` | social culture/language/religion trait | `PartitionRecordId` |
| `society.reputation` | social reputation/trust | `PartitionRecordId` |
| `society.information_claim` | social/media claim/provenance | `PartitionRecordId` |
| `society.history_lineage` | social/economic lineage/history anchor | `PartitionRecordId` |

Required owner invariant:

- physical possession/locationとeconomic ownershipを分離する。
- public authority/legal effectをGovernanceSecurityから奪わない。
- information deliveryをInfrastructureInformationから奪わない。

## 14. Governance / Security registry — 17 partitions

Owner: `governance_security`

| partition_id | record semantic | key |
|---|---|---|
| `governance.polity` | polity/governing authority identity | `PartitionRecordId` |
| `governance.institution` | institution/office/selection structure | `PartitionRecordId` |
| `governance.law_rule` | law/rule/normative instrument | `PartitionRecordId` |
| `governance.jurisdiction` | jurisdiction/applicability scope | `PartitionRecordId` |
| `governance.territorial_claim` | institutional territorial claim | `PartitionRecordId` |
| `governance.effective_control` | effective territorial control | `PartitionRecordId` |
| `governance.public_authority` | public authority/capability/mandate | `PartitionRecordId` |
| `governance.tax_fiscal` | tax/public fiscal claim/authority | `PartitionRecordId` |
| `governance.permission_license` | permission/license/sanction | `PartitionRecordId` |
| `governance.diplomacy` | diplomacy/treaty/recognition/sanction | `PartitionRecordId` |
| `governance.security_incident` | institutional incident record | `PartitionRecordId` |
| `governance.investigation` | investigation state | `PartitionRecordId` |
| `governance.judicial_case` | judicial proceeding | `PartitionRecordId` |
| `governance.enforcement` | enforcement/order outcome institutional state | `PartitionRecordId` |
| `governance.military_authority` | military authority/unit/mission/order | `PartitionRecordId` |
| `governance.border_control` | border/checkpoint/legal movement control | `PartitionRecordId` |
| `governance.lineage` | institutional succession/history lineage | `PartitionRecordId` |

Required owner invariant:

- world action factとlegal classificationを分離する。
- physical detention/combat damageを直接ownerしない。
- generic organization economic assetsをSocietyEconomyから奪わない。

## 15. Infrastructure / Information registry — 14 partitions

Owner: `infrastructure_information`

| partition_id | record semantic | key |
|---|---|---|
| `infrastructure.network_topology` | logical network node/edge topology | `PartitionRecordId` |
| `infrastructure.transport_service` | transport service/capacity/schedule | `PartitionRecordId` |
| `infrastructure.water_service` | water/sewer service state | `PartitionRecordId` |
| `infrastructure.power_service` | power service/feed/distribution | `PartitionRecordId` |
| `infrastructure.communication_service` | communication reachability/capacity | `PartitionRecordId` |
| `infrastructure.dependency` | infrastructure dependency graph relation | `PartitionRecordId` |
| `infrastructure.facility_service` | facility operational service/capacity | `PartitionRecordId` |
| `infrastructure.service_queue` | queue/reservation/assignment | `PartitionRecordId` |
| `information.delivery` | message/information delivery lifecycle | `PartitionRecordId` |
| `information.media_distribution` | media publication/distribution delivery | `PartitionRecordId` |
| `information.record_store` | record/document storage/version/availability | `PartitionRecordId` |
| `information.address_place_index` | address/place lookup semantic state | `PartitionRecordId` |
| `infrastructure.failure_recovery` | outage/degradation/recovery state | `PartitionRecordId` |
| `infrastructure.lineage` | network/service history lineage | `PartitionRecordId` |

Required owner invariant:

- physical asset geometry/conditionはPhysicalBuilt。
- natural resource/weatherはEnvironment。
- claim truth/social provenanceはSocietyEconomy。
- Resident beliefはResident。

## 16. Domain count audit

| owner domain | partition count |
|---|---:|
| `spatial` | 8 |
| `environment` | 13 |
| `physical_built` | 11 |
| `participation` | 5 |
| `resident` | 13 |
| `society_economy` | 16 |
| `governance_security` | 17 |
| `infrastructure_information` | 14 |
| **total** | **97** |

Registry loaderはstandard profileでexactlyこの97 entryを期待する。

Addon domainはstandard 97 entryを変更せず、別namespaceのadditional registrationとして追加する。

## 17. Addon partition namespace

Addon partition tokenはstandard tokenとのcollisionを禁止する。

推奨形式:

```text
addon/<addon-token>/<partition-token>
```

ただしstandard Phase 4 protocol/persistenceはaddon functional payloadを自動理解しない。

Addon partitionをauthoritative world stateへ導入する場合は、少なくとも:

- addon schema identity/version
- owner domain registration
- persistence capability
- deterministic ordering
- snapshot/replay compatibility
- required addon availability on restore

を明示する。

required addon欠落worldをsilent partial restoreしない。

## 18. Record mutation boundary

Domain runtimeがrecordを変更する場合:

```text
WorldReadViewV1
 -> owner-domain calculation
 -> DomainOwnedChangeV1
 -> owner PartitionBuilder
 -> canonical record-id merge
 -> partition candidate
```

foreign domainは`DomainRecordEnvelopeV1.payload`へのmutable referenceを取得しない。

Cross-domain変更:

```text
foreign source
 -> MutationIntentHeaderV1
 -> target owner validation
 -> target owner DomainOwnedChangeV1
```

required semantic transactionは`CrossDomainTransactionCandidateV1`へ参加させる。

## 19. DomainOwnedChangeV1

```text
DomainOwnedChangeV1 {
  partition_id: PartitionId,
  record_id: PartitionRecordId,
  basis_record_revision: uint64 | NONE,
  change_kind: StableToken,
  source_intent_id: IntentId | NONE,
  source_event_id: EventId | NONE,
  canonical_order_key: SameStepOrderKey,
  payload: PartitionOwnedChangePayload
}
```

`basis_record_revision = NONE`はnew record creationのみ。

update/delete/retireでNONEは禁止。

same recordへ複数changeがある場合、partition schemaが定義するconflict modeと`SameStepOrderKey`で解決する。

## 20. Reference integrity

Cross-partition referenceは次へnormalizeする。

```text
PartitionRecordRefV1 {
  partition_id: PartitionId,
  record_id: PartitionRecordId
}
```

外部domainがforeign recordを参照する場合もraw pointerではなくこのstable refを使う。

commit前に少なくとも:

- target partition exists
- target record exists or same semantic transactionでcreateされる
- retired referenceを許すfieldか
- expected owner/schemaか

を検証する。

## 21. Detail transition compatibility

全partitionは`DomainRecordEnvelopeV1.detail_level`を持つが、recordごとのdetail levelとregion-level policyは別概念である。

- D0→D1/D2/D3でpersistent identity-bearing recordを無理由削除しない。
- aggregate record creation時はlineage_refを設定する。
- promotionで過去persistent recordを復元する場合、同一record idを使用する。
- aggregate-native populationのmaterializationは`mv.partition-record.v1` derivation contextを使用できる。

## 22. Snapshot / replay requirement

Snapshotは各`AUTHORITATIVE_ALWAYS` partitionについて:

- PartitionStateHeaderV1
- schema id/version
- ordered record sequence
- record envelope
- domain payload

を復元可能でなければならない。

snapshot physical chunkingはP4-04で決めるが、logical record orderは本書を変更しない。

Replay時、同一historyから再構築したpartition digestはsnapshot由来stateと一致しなければならない。

## 23. Acceptance criteria

P4-01 domain registryは次を満たす。

- Phase 3の8 domain familyを全て登録した。
- Phase 3の97 authoritative partitionを欠番なく登録した。
- partitionごとにownerが1つに定まる。
- standard schema identity/version ruleが一意である。
- primary record identityとcanonical orderが一意である。
- foreign direct mutable writeを必要としない。
- snapshot/replay/digestへ同じlogical orderを使用できる。
- detail transitionでidentity/lineageを保持できる。
- later P4-05 payload schemaがpartition ownershipを変更せず追加できる。

## 24. P4-01 handoff

P4-01から後続へ引き渡す事項:

- P4-02: `PartitionRecordRefV1`、SchemaRef、ID型をprotocol payloadで使用する。
- P4-04: 97 authoritative partitionをsnapshot manifest/chunkへ配置する。
- P4-05: 各`record_schema_id`のpayload field/numeric representationとrequired secondary indexを確定する。
- P4-06: partition/record count、memory、update cadence budgetを確定する。
- P4-08: registry completeness、digest、snapshot/replay、cross-owner mutation rejectをtestする。

本書のregistry/ownership/record identityを変更する必要が生じた場合はP4-01を再openし、Phase 3 ownershipとのcompatibility reviewを行う。
