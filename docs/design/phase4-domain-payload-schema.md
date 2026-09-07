# 詳細設計 Phase 4: Domain Partition Payload Schema

Status: Complete / P4-05 payload registry  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`  
Predecessors: `phase4-domain-state-registry.md`, `phase4-algorithm-determinism.md`

## 1. 目的

Phase 3で確定しP4-01でregistry化した97 authoritative partitionについて、P4-05で実装recordへ直接写像できる最小authoritative payload field、exact scalar family、canonical collection rule、required secondary indexを固定する。

本書のfieldは「最低限保持すべきauthoritative semantic field」であり、derived cacheやpresentation fieldを混在させない。実装は同一schema version内で非永続cacheを追加できるが、record digest、snapshot、replay、protocol projectionの意味を変更してはならない。

## 2. 共通field型

本書では次の略記を使用する。

```text
Id128        := 16-octet non-zero opaque id
Ref          := PartitionRecordRefV1
RefList      := ordered list<Ref>
Token        := StableToken
TokenList    := ordered list<Token>
Step         := SimulationStep
Vec3         := Vec3Mm
Quat         := QuaternionQ30
Length       := LengthMm
Velocity     := VelocityUmPerSecond
Mass         := MassGram
Volume       := VolumeMillilitre
Energy       := EnergyMillijoule
Power        := PowerMilliwatt
Temperature  := TemperatureMilliKelvin
Pressure     := PressurePascal
Money        := CurrencyMicrounit
Ratio         := ProbabilityPpm | ProgressPpm according to field semantics
Digest       := Hash256
```

全list/setは明示したsemantic orderがない限り、ID/token/refのcanonical bytewise orderへnormalizeする。

Optional fieldは`?`で示す。存在しないこととzero値を混同しない。

## 3. Record payload共通規則

各payload schema idはP4-01 ruleにより:

```text
domain.<partition_id>.record / 1.0
```

とする。

全recordは`DomainRecordEnvelopeV1`で次を共通保持するため、本書payloadで重複しない。

- record_id
- record schema/version
- record revision
- created_step / retired_step
- detail_level
- lineage_ref

Payload内の外部参照は原則`Ref`を使う。domain内identityを単独16 octetsで扱う必要がある場合のみ`Id128`を許可する。

## 4. Spatial payload — 8

| partition | required payload field | numeric/collection rule | required secondary index |
|---|---|---|---|
| `spatial.world_frame` | `frame_kind:Token`, `parent_frame:Ref?`, `translation:Vec3`, `rotation:Quat`, `valid_scope:Ref`, `transform_revision:uint64` | parent chain cycle禁止 | `spatial.frame-by-parent` |
| `spatial.scope_registry` | `scope_class:Token`, `geometry_ref:Ref`, `parent_scope:Ref?`, `active_from:Step`, `retired_at:Step?`, `scope_flags:uint32` | parent relation DAG | `spatial.scope-by-parent`, `spatial.scope-by-class` |
| `spatial.terrain_geometry` | `scope_ref:Ref`, `root_brick_ref:Ref`, `geometry_revision:uint64`, `surface_classes:TokenList`, `connectivity_refs:RefList`, `archive_anchor:Digest?` | SBO-SDF v1; brick traversal canonical | `spatial.terrain-by-scope` |
| `spatial.void_geometry` | `geometry_ref:Ref`, `connectivity:RefList`, `entrances:RefList`, `origin_class:Token`, `lifecycle:Token`, `geometry_revision:uint64` | entrance/ref order canonical | `spatial.void-by-entrance`, `spatial.void-by-lifecycle` |
| `spatial.containment_topology` | `subject_ref:Ref`, `container_scope:Ref`, `relation_class:Token`, `basis_geometry_revision:uint64` | relation tuple unique | `spatial.containment-by-subject`, `spatial.containment-by-container` |
| `spatial.boundary_topology` | `scope_a:Ref`, `scope_b:Ref`, `interface_geometry_ref:Ref`, `permeability_classes:TokenList`, `detail_policy_ref:Ref?`, `revision:uint64` | `(min(scope),max(scope))` canonical | `spatial.boundary-by-scope` |
| `spatial.detail_regions` | `scope_ref:Ref`, `level_by_domain:ordered map<Token,uint8>`, `lineage_generation:uint32`, `last_transition_step:Step`, `active_guards:TokenList` | domain token ASCII order | `spatial.detail-region-by-scope` |
| `spatial.geometry_lineage` | `subject_ref:Ref`, `parent_refs:RefList`, `creation_kind:Token`, `creation_ref:Ref?`, `generation:uint32`, `source_digest:Digest` | parents canonical | `spatial.lineage-by-subject` |

## 5. Environment payload — 13

| partition | required payload field | numeric/collection rule | required secondary index |
|---|---|---|---|
| `environment.geology` | `spatial_scope:Ref`, `material_classes:TokenList`, `strata_refs:RefList`, `porosity_ppm:Ratio`, `stability_ppm:Ratio`, `permeability_q32:int64`, `fault_refs:RefList`, `resource_refs:RefList` | physical stock never negative | `environment.geology-by-scope` |
| `environment.soil` | `spatial_scope:Ref`, `soil_class:Token`, `depth_mm:Length`, `moisture_ppm:Ratio`, `fertility_ppm:Ratio`, `organic_mass_g:Mass`, `contaminant_refs:RefList` | bounded ppm | `environment.soil-by-scope` |
| `environment.resource_deposit` | `spatial_scope:Ref`, `resource_kind:Token`, `remaining_mass_g:Mass`, `grade_ppm:Ratio`, `renewal_rate_g_per_step:int64`, `accessibility_ppm:Ratio` | mass >= 0 | `environment.resource-by-kind`, `environment.resource-by-scope` |
| `environment.groundwater` | `spatial_scope:Ref`, `water_volume_ml:Volume`, `hydraulic_head_mm:Length`, `quality_ppm:Ratio`, `temperature_mk:Temperature`, `neighbor_refs:RefList` | transfer conservative | `environment.groundwater-by-scope` |
| `environment.atmosphere` | `spatial_scope:Ref`, `pressure_pa:Pressure`, `temperature_mk:Temperature`, `humidity_ppm:Ratio`, `wind_um_s:Vec3`, `vapor_mass_g:Mass`, `liquid_mass_g:Mass`, `gas_ppb:ordered map<Token,uint32>` | gas token order; flux conservative | `environment.atmosphere-by-scope` |
| `environment.climate` | `spatial_scope:Ref`, `regime:Token`, `temperature_mean_mk:Temperature`, `precipitation_mean_ml:int64`, `wind_mean_um_s:Vec3`, `sample_count:uint64`, `aggregate_generation:uint32` | integer recurrence | `environment.climate-by-scope` |
| `environment.weather` | `spatial_scope:Ref`, `weather_class:Token`, `precipitation_ml_per_step:int64`, `cloud_ppm:Ratio`, `visibility_mm:Length`, `storm_intensity_ppm:Ratio`, `basis_atmosphere_revision:uint64` | bounded fixed-point | `environment.weather-by-scope`, `environment.weather-by-class` |
| `environment.surface_water` | `spatial_scope:Ref`, `water_body_class:Token`, `volume_ml:Volume`, `surface_level_mm:Length`, `flow_um_s:Vec3`, `temperature_mk:Temperature`, `quality_ppm:Ratio`, `downstream_refs:RefList` | volume conservative | `environment.surface-water-by-scope` |
| `environment.ocean` | `spatial_scope:Ref`, `water_volume_ml:Volume`, `surface_level_mm:Length`, `velocity_um_s:Vec3`, `temperature_mk:Temperature`, `salinity_ppm:Ratio`, `neighbor_refs:RefList` | face flux single calculation | `environment.ocean-by-scope` |
| `environment.ecosystem` | `spatial_scope:Ref`, `species_or_cohort:Token`, `population:uint64`, `biomass_g:Mass`, `birth_rate_ppm:Ratio`, `death_rate_ppm:Ratio`, `migration_rate_ppm:Ratio`, `resource_refs:RefList` | population checked; stochastic addressable | `environment.ecosystem-by-scope`, `environment.ecosystem-by-species` |
| `environment.contaminant` | `spatial_scope:Ref`, `contaminant_kind:Token`, `stock_mass_g:Mass`, `concentration_ppb:uint32`, `source_refs:RefList`, `sink_refs:RefList` | stock conservative | `environment.contaminant-by-scope`, `environment.contaminant-by-kind` |
| `environment.hazard` | `spatial_scope:Ref`, `hazard_kind:Token`, `intensity_ppm:Ratio`, `started_step:Step`, `expected_end_step:Step?`, `driver_refs:RefList`, `affected_scope_refs:RefList` | intensity bounded | `environment.hazard-by-scope`, `environment.hazard-by-kind` |
| `environment.environment_lineage` | `subject_ref:Ref`, `parent_refs:RefList`, `generation:uint32`, `materialization_kind:Token`, `source_digest:Digest` | parent refs canonical | `environment.lineage-by-subject` |

## 6. Physical / Built payload — 11

| partition | required payload field | numeric/collection rule | required secondary index |
|---|---|---|---|
| `physical.presence` | `subject_ref:Ref`, `frame_ref:Ref`, `position:Vec3`, `orientation:Quat`, `linear_velocity:Vec3`, `angular_rate_urad_s:Vec3`, `shape_ref:Ref`, `containment_ref:Ref?`, `presence_mode:Token` | semi-implicit Euler | `physical.presence-by-subject`, `physical.presence-by-scope` |
| `physical.occupancy` | `presence_ref:Ref`, `aabb_min:Vec3`, `aabb_max:Vec3`, `contact_refs:RefList`, `occupancy_flags:uint32`, `collision_layer:uint32` | min<=max; contacts canonical | `physical.occupancy-grid` |
| `built.structure` | `spatial_scope:Ref`, `structure_class:Token`, `geometry_parts:RefList`, `material_refs:RefList`, `integrity_ppm:Ratio`, `support_refs:RefList`, `lifecycle:Token` | geometry canonical | `built.structure-by-scope`, `built.structure-by-class` |
| `built.space` | `structure_ref:Ref`, `spatial_scope:Ref`, `space_class:Token`, `opening_refs:RefList`, `adjacent_space_refs:RefList`, `capacity_count:uint32` | adjacency canonical | `built.space-by-structure` |
| `built.opening` | `structure_ref:Ref`, `space_refs:RefList`, `opening_class:Token`, `mechanism_state:Token`, `locked:bool`, `aperture_ppm:Ratio`, `geometry_ref:Ref` | space refs max 2 unless schema extension | `built.opening-by-space` |
| `physical.container_location` | `subject_ref:Ref`, `container_ref:Ref`, `slot_token:Token?`, `containment_mode:Token`, `quantity:int64`, `mass_g:Mass` | quantity/mass >=0 | `physical.container-by-subject`, `physical.contents-by-container` |
| `built.worksite` | `spatial_scope:Ref`, `work_kind:Token`, `target_refs:RefList`, `progress_ppm:Ratio`, `required_material_refs:RefList`, `consumed_material_refs:RefList`, `worker_refs:RefList`, `status:Token` | material handoff transaction | `built.worksite-by-target`, `built.worksite-by-status` |
| `physical.condition` | `subject_ref:Ref`, `condition_class:Token`, `integrity_ppm:Ratio`, `wear_ppm:Ratio`, `temperature_mk:Temperature?`, `damage_refs:RefList`, `maintenance_due_step:Step?` | bounded ppm | `physical.condition-by-subject` |
| `physical.combustion` | `subject_ref:Ref`, `combustion_state:Token`, `fuel_mass_g:Mass`, `temperature_mk:Temperature`, `heat_output_mw:Power`, `smoke_mass_g:Mass`, `ignition_ref:Ref?` | stock/energy checked | `physical.combustion-by-subject`, `physical.combustion-active` |
| `physical.material_handoff` | `transaction_ref:Id128`, `material_kind:Token`, `source_ref:Ref`, `target_ref:Ref`, `mass_g:Mass`, `handoff_state:Token`, `prepared_step:Step`, `committed_step:Step?` | exactly-one authority invariant | `physical.handoff-by-transaction` |
| `physical.lineage` | `subject_ref:Ref`, `parent_refs:RefList`, `material_source_refs:RefList`, `creation_kind:Token`, `generation:uint32`, `source_digest:Digest` | refs canonical | `physical.lineage-by-subject` |

## 7. Participation payload — 5

| partition | required payload field | numeric/collection rule | required secondary index |
|---|---|---|---|
| `participation.binding` | `binding_id:Id128`, `diver_ref:Id128`, `resident_ref:Ref`, `status:Token`, `effective_from:Step`, `ended_step:Step?`, `binding_generation:uint32`, `absence_policy_ref:Ref?`, `causality_refs:RefList` | active resident/diver unique | `participation.binding-by-diver`, `participation.binding-by-resident` |
| `participation.absence_policy` | `diver_ref:Id128`, `policy_generation:uint32`, `priority_rules:ordered list<PolicyRuleV1>`, `effective_from:Step`, `effective_until:Step?` | priority then rule id | `participation.policy-by-diver` |
| `participation.control_mode` | `resident_ref:Ref`, `binding_ref:Ref?`, `mode:Token`, `effective_from:Step`, `input_authority_generation:uint32` | one effective mode/resident | `participation.control-by-resident` |
| `participation.history` | `binding_ref:Ref`, `history_kind:Token`, `basis_step:Step`, `previous_history_ref:Ref?`, `causality_digest:Digest` | history chain | `participation.history-by-binding` |
| `participation.detail_requirement` | `resident_ref:Ref`, `minimum_detail:uint8`, `scope_ref:Ref`, `reason:Token`, `effective_from:Step`, `effective_until:Step?` | min detail 0..3 | `participation.detail-by-resident` |

## 8. Resident payload — 13

| partition | required payload field | numeric/collection rule | required secondary index |
|---|---|---|---|
| `resident.identity_lifecycle` | `resident_id:Id128`, `lifecycle:Token`, `birth_step:Step?`, `death_step:Step?`, `parent_refs:RefList`, `lineage_generation:uint32`, `profile_token:Token` | lifecycle monotonic unless explicit revival extension | `resident.lifecycle-by-status` |
| `resident.body_health` | `resident_ref:Ref`, `development_ppm:Ratio`, `health_capacity_ppm:Ratio`, `body_region_states:ordered list<BodyRegionStateV1>`, `injury_refs:RefList`, `disease_refs:RefList`, `recovery_ppm:Ratio` | region token canonical | `resident.health-by-resident` |
| `resident.physiology` | `resident_ref:Ref`, `hunger_ppm:Ratio`, `thirst_ppm:Ratio`, `fatigue_ppm:Ratio`, `sleep_pressure_ppm:Ratio`, `thermal_stress_ppm:Ratio`, `hygiene_ppm:Ratio` | bounded ppm | `resident.physiology-by-resident` |
| `resident.perception` | `resident_ref:Ref`, `attention_target_refs:RefList`, `perceived_facts:ordered list<PerceivedFactV1>`, `sensory_capacity_ppm:Ratio`, `basis_step:Step` | fact id canonical | `resident.perception-by-resident`, `resident.perception-by-subject` |
| `resident.knowledge_belief` | `resident_ref:Ref`, `subject_ref:Ref`, `proposition_token:Token`, `confidence_ppm:Ratio`, `evidence_refs:RefList`, `last_updated_step:Step` | key `(resident,subject,proposition)` | `resident.belief-by-resident`, `resident.belief-by-subject` |
| `resident.memory` | `resident_ref:Ref`, `memory_kind:Token`, `subject_refs:RefList`, `encoded_step:Step`, `salience_ppm:Ratio`, `confidence_ppm:Ratio`, `decay_state_ppm:Ratio` | memory id canonical | `resident.memory-by-resident`, `resident.memory-by-subject` |
| `resident.psychology` | `resident_ref:Ref`, `emotion_vector:ordered map<Token,uint32>`, `stress_ppm:Ratio`, `traits:ordered map<Token,uint32>`, `preferences:ordered map<Token,int32>`, `values:ordered map<Token,int32>` | token ASCII order | `resident.psychology-by-resident` |
| `resident.goal_plan` | `resident_ref:Ref`, `goal_token:Token`, `utility:int64`, `status:Token`, `plan_actions:ordered list<Token>`, `current_action_index:uint16`, `planning_generation:uint32`, `target_refs:RefList` | GOAP canonical key | `resident.goal-by-resident`, `resident.goal-by-status` |
| `resident.skill_aptitude` | `resident_ref:Ref`, `skill_token:Token`, `skill_ppm:Ratio`, `aptitude_ppm:Ratio`, `practice_accumulator:uint64`, `last_practice_step:Step?` | key `(resident,skill)` | `resident.skill-by-resident`, `resident.skill-by-token` |
| `resident.relationship` | `subject_resident:Ref`, `object_resident:Ref`, `relationship_kind:Token`, `affinity:int32`, `trust_ppm:Ratio`, `familiarity_ppm:Ratio`, `status:Token` | directed relation; pair canonical for symmetric kinds | `resident.relation-by-subject`, `resident.relation-by-object` |
| `resident.family_lineage` | `resident_ref:Ref`, `parent_refs:RefList`, `child_refs:RefList`, `family_relation_refs:RefList`, `generation_index:int32` | no self-parent; ancestry cycle reject | `resident.family-by-resident`, `resident.children-by-parent` |
| `resident.behavior_state` | `resident_ref:Ref`, `mode:Token`, `active_goal_ref:Ref?`, `active_action_token:Token?`, `action_target_refs:RefList`, `action_started_step:Step?`, `control_source:Token` | one active behavior/resident | `resident.behavior-by-resident`, `resident.behavior-by-mode` |
| `resident.lineage` | `resident_ref:Ref`, `source_aggregate_ref:Ref?`, `generation:uint32`, `materialization_role:Token`, `creation_ref:Ref`, `source_digest:Digest` | deterministic materialization | `resident.lineage-by-resident` |

## 9. Society / Economy payload — 16

| partition | required payload field | numeric/collection rule | required secondary index |
|---|---|---|---|
| `society.organization` | `organization_id:Id128`, `organization_class:Token`, `lifecycle:Token`, `purpose_tokens:TokenList`, `parent_refs:RefList`, `facility_refs:RefList`, `founded_step:Step` | identity stable | `society.org-by-class`, `society.org-by-parent` |
| `society.membership_role` | `organization_ref:Ref`, `member_ref:Ref`, `role_tokens:TokenList`, `authority_tokens:TokenList`, `joined_step:Step`, `ended_step:Step?`, `status:Token` | membership identity unique | `society.membership-by-org`, `society.membership-by-member` |
| `society.employment` | `employer_ref:Ref`, `worker_ref:Ref`, `job_token:Token`, `status:Token`, `started_step:Step`, `ended_step:Step?`, `wage_microunit_per_period:Money`, `pay_period_steps:uint64`, `obligation_refs:RefList` | money checked | `society.employment-by-employer`, `society.employment-by-worker` |
| `society.household` | `member_refs:RefList`, `shared_account_refs:RefList`, `residence_refs:RefList`, `resource_budget_refs:RefList`, `status:Token` | members canonical | `society.household-by-member` |
| `society.contract_claim` | `contract_kind:Token`, `party_refs:RefList`, `claimant_ref:Ref?`, `obligor_ref:Ref?`, `amount:Money?`, `quantity:int64?`, `due_step:Step?`, `status:Token`, `terms_digest:Digest` | parties canonical | `society.contract-by-party`, `society.contract-by-status` |
| `society.property_right` | `asset_ref:Ref`, `holder_ref:Ref`, `right_kind:Token`, `share_ppm:Ratio`, `effective_from:Step`, `effective_until:Step?`, `claim_ref:Ref?` | shares <= 1,000,000 unless domain permits layered rights | `society.property-by-asset`, `society.property-by-holder` |
| `society.currency_money` | `currency_token:Token`, `issuer_ref:Ref`, `supply_microunit:Money`, `status:Token`, `policy_refs:RefList`, `unit_scale:uint32` | supply checked | `society.currency-by-token` |
| `society.finance_account` | `owner_ref:Ref`, `institution_ref:Ref?`, `currency_token:Token`, `balance_microunit:Money`, `credit_limit_microunit:Money`, `status:Token`, `ledger_head_digest:Digest` | double-entry invariant | `society.account-by-owner`, `society.account-by-currency` |
| `society.market_transaction` | `market_ref:Ref`, `instrument_token:Token`, `order_side:Token?`, `limit_price:Money?`, `quantity:int64`, `clearing_price:Money?`, `buyer_ref:Ref?`, `seller_ref:Ref?`, `eligible_step:Step`, `status:Token` | call-auction canonical order | `society.market-by-market`, `society.market-by-party`, `society.market-by-status` |
| `society.business_production` | `organization_ref:Ref`, `recipe_token:Token`, `planned_quantity:int64`, `completed_quantity:int64`, `input_refs:RefList`, `output_refs:RefList`, `work_required:uint64`, `energy_required_mj:Energy`, `status:Token` | integer recipe conservation | `society.production-by-org`, `society.production-by-status` |
| `society.logistics_obligation` | `shipper_ref:Ref`, `consignee_ref:Ref`, `cargo_refs:RefList`, `quantity:int64`, `origin_ref:Ref`, `destination_ref:Ref`, `due_step:Step?`, `status:Token`, `carrier_ref:Ref?` | cargo identity conserved | `society.logistics-by-party`, `society.logistics-by-status` |
| `society.education` | `provider_ref:Ref`, `learner_ref:Ref`, `program_token:Token`, `status:Token`, `progress_ppm:Ratio`, `skill_refs:RefList`, `started_step:Step`, `ended_step:Step?` | progress bounded | `society.education-by-learner`, `society.education-by-provider` |
| `society.culture` | `subject_ref:Ref`, `trait_token:Token`, `affiliation_ppm:Ratio`, `adoption_step:Step`, `source_refs:RefList`, `status:Token` | key `(subject,trait)` | `society.culture-by-subject`, `society.culture-by-trait` |
| `society.reputation` | `subject_ref:Ref`, `audience_scope_ref:Ref?`, `dimension_token:Token`, `score:int32`, `confidence_ppm:Ratio`, `evidence_refs:RefList`, `updated_step:Step` | dimension canonical | `society.reputation-by-subject`, `society.reputation-by-dimension` |
| `society.information_claim` | `claimant_ref:Ref`, `subject_refs:RefList`, `claim_token:Token`, `content_digest:Digest`, `provenance_refs:RefList`, `created_step:Step`, `status:Token` | content immutable per claim revision | `society.claim-by-claimant`, `society.claim-by-subject` |
| `society.history_lineage` | `subject_ref:Ref`, `history_kind:Token`, `parent_refs:RefList`, `basis_step:Step`, `causality_digest:Digest` | history refs canonical | `society.history-by-subject` |

## 10. Governance / Security payload — 17

| partition | required payload field | numeric/collection rule | required secondary index |
|---|---|---|---|
| `governance.polity` | `related_org_refs:RefList`, `lifecycle:Token`, `institution_refs:RefList`, `jurisdiction_refs:RefList`, `claim_refs:RefList`, `control_refs:RefList`, `recognition_refs:RefList`, `fiscal_refs:RefList` | refs canonical | `governance.polity-by-org`, `governance.polity-by-status` |
| `governance.institution` | `polity_ref:Ref`, `institution_kind:Token`, `office_refs:RefList`, `decision_method:Token`, `selection_rule_ref:Ref?`, `lifecycle:Token` | office refs canonical | `governance.institution-by-polity`, `governance.institution-by-kind` |
| `governance.law_rule` | `jurisdiction_ref:Ref`, `priority:int32`, `specificity:uint32`, `effective_from:Step`, `effective_until:Step?`, `predicate_ast:RulePredicateAstV1`, `effect_ast:RuleEffectAstV1`, `status:Token` | AST canonical field order; executable code禁止 | `governance.rule-by-jurisdiction`, `governance.rule-by-effective-step` |
| `governance.jurisdiction` | `polity_ref:Ref`, `scope_ref:Ref`, `jurisdiction_kind:Token`, `subject_classes:TokenList`, `effective_from:Step`, `effective_until:Step?` | spatial scope reference | `governance.jurisdiction-by-scope`, `governance.jurisdiction-by-polity` |
| `governance.territorial_claim` | `claimant_polity_ref:Ref`, `scope_ref:Ref`, `claim_kind:Token`, `strength_ppm:Ratio`, `effective_from:Step`, `effective_until:Step?`, `basis_refs:RefList` | overlapping claims permitted | `governance.claim-by-scope`, `governance.claim-by-polity` |
| `governance.effective_control` | `controller_ref:Ref`, `scope_ref:Ref`, `control_ppm:Ratio`, `security_capacity_ppm:Ratio`, `effective_from:Step`, `basis_refs:RefList` | scope aggregate bounded | `governance.control-by-scope`, `governance.control-by-controller` |
| `governance.public_authority` | `institution_ref:Ref`, `holder_ref:Ref`, `authority_tokens:TokenList`, `scope_refs:RefList`, `effective_from:Step`, `effective_until:Step?`, `status:Token` | authority tokens canonical | `governance.authority-by-holder`, `governance.authority-by-institution` |
| `governance.tax_fiscal` | `polity_ref:Ref`, `tax_kind:Token`, `tax_base_token:Token`, `rate_ppm:Ratio`, `claim_amount:Money?`, `debtor_ref:Ref?`, `due_step:Step?`, `status:Token` | money/rate checked | `governance.tax-by-polity`, `governance.tax-by-debtor` |
| `governance.permission_license` | `subject_ref:Ref`, `authority_ref:Ref`, `permission_kind:Token`, `scope_refs:RefList`, `effective_from:Step`, `effective_until:Step?`, `status:Token`, `conditions_digest:Digest` | one record per issued permission identity | `governance.permission-by-subject`, `governance.permission-by-kind` |
| `governance.diplomacy` | `party_refs:RefList`, `relation_kind:Token`, `status:Token`, `effective_from:Step`, `effective_until:Step?`, `instrument_refs:RefList`, `terms_digest:Digest` | parties canonical | `governance.diplomacy-by-party`, `governance.diplomacy-by-kind` |
| `governance.security_incident` | `incident_kind:Token`, `subject_refs:RefList`, `scope_ref:Ref`, `occurred_step:Step`, `fact_event_refs:RefList`, `status:Token`, `severity_ppm:Ratio` | event facts immutable refs | `governance.incident-by-scope`, `governance.incident-by-subject` |
| `governance.investigation` | `incident_ref:Ref`, `authority_ref:Ref`, `investigator_refs:RefList`, `evidence_refs:RefList`, `suspect_refs:RefList`, `status:Token`, `opened_step:Step`, `closed_step:Step?` | evidence id order | `governance.investigation-by-incident`, `governance.investigation-by-status` |
| `governance.judicial_case` | `case_kind:Token`, `jurisdiction_ref:Ref`, `party_refs:RefList`, `evidence_refs:RefList`, `charge_or_claim_refs:RefList`, `status:Token`, `opened_step:Step`, `decision_ref:Ref?` | parties/evidence canonical | `governance.case-by-party`, `governance.case-by-status` |
| `governance.enforcement` | `authority_ref:Ref`, `order_kind:Token`, `subject_refs:RefList`, `target_refs:RefList`, `status:Token`, `issued_step:Step`, `effective_step:Step?`, `outcome_event_refs:RefList` | physical effect is external intent/event | `governance.enforcement-by-subject`, `governance.enforcement-by-status` |
| `governance.military_authority` | `polity_ref:Ref`, `unit_or_org_ref:Ref`, `command_ref:Ref?`, `mission_token:Token`, `objective_refs:RefList`, `authority_scope_refs:RefList`, `status:Token`, `issued_step:Step` | no direct combat damage | `governance.military-by-polity`, `governance.military-by-status` |
| `governance.border_control` | `jurisdiction_ref:Ref`, `boundary_ref:Ref`, `checkpoint_refs:RefList`, `movement_rule_refs:RefList`, `status:Token`, `capacity_per_step:uint32` | boundary refs canonical | `governance.border-by-boundary`, `governance.border-by-jurisdiction` |
| `governance.lineage` | `subject_ref:Ref`, `predecessor_refs:RefList`, `succession_kind:Token`, `effective_step:Step`, `causality_digest:Digest` | predecessor refs canonical | `governance.lineage-by-subject` |

## 11. Infrastructure / Information payload — 14

| partition | required payload field | numeric/collection rule | required secondary index |
|---|---|---|---|
| `infrastructure.network_topology` | `network_kind:Token`, `node_refs:RefList`, `edge_refs:RefList`, `operator_refs:RefList`, `scope_refs:RefList`, `status:Token`, `topology_revision:uint64` | nodes/edges canonical | `infrastructure.network-by-kind`, `infrastructure.network-by-node` |
| `infrastructure.transport_service` | `network_ref:Ref`, `service_kind:Token`, `route_refs:RefList`, `capacity_per_step:uint64`, `load:uint64`, `schedule_ref:Ref?`, `availability_ppm:Ratio`, `status:Token` | capacity checked | `infrastructure.transport-by-network`, `infrastructure.transport-by-route` |
| `infrastructure.water_service` | `network_ref:Ref`, `service_scope_ref:Ref`, `supply_ml_per_step:Volume`, `demand_ml_per_step:Volume`, `pressure_head_mm:Length`, `quality_ppm:Ratio`, `availability_ppm:Ratio`, `status:Token` | flow conservative | `infrastructure.water-by-scope`, `infrastructure.water-by-network` |
| `infrastructure.power_service` | `network_ref:Ref`, `service_scope_ref:Ref`, `generation_mw:Power`, `demand_mw:Power`, `delivered_mw:Power`, `availability_ppm:Ratio`, `status:Token` | fixed-point graph solve | `infrastructure.power-by-scope`, `infrastructure.power-by-network` |
| `infrastructure.communication_service` | `network_ref:Ref`, `service_scope_ref:Ref`, `capacity_units_per_step:uint64`, `queued_units:uint64`, `latency_steps:uint32`, `availability_ppm:Ratio`, `status:Token` | queue key canonical | `infrastructure.communication-by-scope` |
| `infrastructure.dependency` | `consumer_ref:Ref`, `provider_ref:Ref`, `dependency_kind:Token`, `minimum_service_ppm:Ratio`, `degradation_curve_ref:Ref?`, `fallback_refs:RefList`, `status:Token` | dependency cycle validated | `infrastructure.dependency-by-consumer`, `infrastructure.dependency-by-provider` |
| `infrastructure.facility_service` | `facility_ref:Ref`, `service_kind:Token`, `capacity_per_step:uint32`, `active_load:uint32`, `required_resource_refs:RefList`, `availability_ppm:Ratio`, `status:Token` | capacity >= load unless queued overflow | `infrastructure.facility-by-facility`, `infrastructure.facility-by-kind` |
| `infrastructure.service_queue` | `service_ref:Ref`, `requester_ref:Ref`, `eligible_step:Step`, `semantic_priority:int32`, `requested_units:uint64`, `allocated_units:uint64`, `status:Token` | key `(eligible_step,priority,record_id)` | `infrastructure.queue-by-service`, `infrastructure.queue-by-requester` |
| `information.delivery` | `content_ref:Ref`, `sender_ref:Ref`, `recipient_refs:RefList`, `channel_ref:Ref`, `eligible_step:Step`, `delivered_step:Step?`, `priority:int32`, `status:Token`, `content_digest:Digest` | recipient refs canonical | `information.delivery-by-recipient`, `information.delivery-by-status` |
| `information.media_distribution` | `claim_ref:Ref`, `publisher_ref:Ref`, `channel_refs:RefList`, `audience_scope_refs:RefList`, `published_step:Step`, `reach_count:uint64`, `status:Token` | reach non-authoritative only if derived; authoritative delivered count exact | `information.media-by-claim`, `information.media-by-publisher` |
| `information.record_store` | `record_kind:Token`, `authority_ref:Ref?`, `subject_refs:RefList`, `content_digest:Digest`, `version:uint32`, `created_step:Step`, `available:bool`, `supersedes_ref:Ref?` | content digest immutable/version | `information.record-by-subject`, `information.record-by-kind` |
| `information.address_place_index` | `place_ref:Ref`, `address_token:Token`, `scope_ref:Ref`, `valid_from:Step`, `valid_until:Step?`, `aliases:TokenList` | normalized token exact schema | `information.address-by-token`, `information.place-by-scope` |
| `infrastructure.failure_recovery` | `subject_ref:Ref`, `failure_kind:Token`, `severity_ppm:Ratio`, `started_step:Step`, `recovery_progress_ppm:Ratio`, `expected_restore_step:Step?`, `dependency_refs:RefList`, `status:Token` | progress bounded | `infrastructure.failure-by-subject`, `infrastructure.failure-by-status` |
| `infrastructure.lineage` | `subject_ref:Ref`, `predecessor_refs:RefList`, `change_kind:Token`, `effective_step:Step`, `source_digest:Digest` | predecessor refs canonical | `infrastructure.lineage-by-subject` |

## 12. Nested schema registry

本書で使用したnested typeを次のschema idへ固定する。

```text
domain.common.policy-rule / 1.0
domain.resident.body-region-state / 1.0
domain.resident.perceived-fact / 1.0
domain.governance.rule-predicate-ast / 1.0
domain.governance.rule-effect-ast / 1.0
```

Nested recordがpersistent identityを必要とする段階へ昇格した場合、親payload内indexではなく独立PartitionRecordId/partitionへmigrationする。

## 13. Secondary index authority

本書で列挙したsecondary indexはすべて初期標準で:

```text
IndexAuthorityV1 = DERIVED_REBUILDABLE
```

とする。

index rebuildはauthoritative record sequenceを`PartitionRecordId` ascendingで読み、index keyをschema comparatorでsortして構築する。

index file/databaseが破損してもauthoritative stateを失ったと判定せず、安全に破棄・再構築できる。

## 14. Schema evolution

同一record schema `1.x`で許される変更:

- optional field追加
- existing fieldのconstraintを互換性を壊さない範囲で明確化
- derived index追加

major changeが必要:

- required field削除/rename
- scalar unit変更
- identity/reference semantics変更
- authoritative owner変更
- collection semantic order変更
- fieldの意味を別概念へ再利用

## 15. Validation baseline

全payload decoder/builderは最低限:

1. record envelope/schema一致
2. scalar range
3. required field存在
4. reference target partition/schema
5. local uniqueness
6. collection canonicalization
7. owner-specific invariant
8. cross-domain shared invariant

を検証する。

validation failureではpartially-mutated recordをauthoritative partitionへinstallしない。

## 16. Count audit

| domain | payload schema count |
|---|---:|
| spatial | 8 |
| environment | 13 |
| physical_built | 11 |
| participation | 5 |
| resident | 13 |
| society_economy | 16 |
| governance_security | 17 |
| infrastructure_information | 14 |
| **total** | **97** |

P4-01 registryとexact countが一致する。

## 17. Acceptance criteria

- 97 partitionすべてにrecord payload minimum fieldがある。
- authoritative numerical stateはP4-05 fixed-point/integer typeで表現可能。
- cross-domain fieldはraw mutable pointerを要求しない。
- all collectionはcanonical orderへnormalize可能。
- secondary indexはrecord authorityから再構築可能。
- detail promotion/demotionでpersistent identity/referenceを失わない。
- snapshot/replay後に同一payload canonical digestを再生成可能。
- schema evolution ruleがowner/identity/unit semanticを保護する。

blocker: なし。