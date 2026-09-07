# 詳細設計 Phase 3: Governance / Security Domain設計

Status: Complete / P3-06  
Tracking: Issue #15  
Parent: `phase3-world-domain-design.md`  
Common contract: `phase3-domain-common-contract.md`

## 1. 目的

`governance_security` domainは、統治主体、制度、法、管轄、行政権限、税・財政上のpublic claim、公共サービス上の権限、領域主張・行政境界・実効支配、外交、治安、捜査、司法、処罰、軍事組織上のauthority/missionをauthoritative institutional stateとして所有する。

本domainは「行為が起きたこと」と「その行為がある制度下で合法/違法か」を分離し、法・制度・実効支配が歴史的に変化しても、物理world stateやResident action truthを上書きしない。

## 2. Responsibility / Non-responsibility

### 2.1 GovernanceSecurityが所有する責務

- polity/governing authority identityとinstitutional lifecycle
- law/rule/normative instrumentと適用範囲
- jurisdiction / administrative territory / sovereign claim
- effective territorial control institutional state
- public authority role/capability/mandate
- taxation/public fiscal claim and public spending authority
- legal/administrative permission, license, sanction
- public service entitlement/authority policy
- diplomacy/treaty/alliance/recognition/sanction state
- incident/case/investigation/judicial proceeding institutional state
- arrest/detention/sentence/order authority state
- military command authority, unit institutional relation, mission/order
- border/checkpoint/legal movement restriction state
- institutional records of enforcement outcome

### 2.2 GovernanceSecurityが所有しない責務

- Resident action decision、knowledge/belief: `resident`
- organization generic identity/employment/economic assets: `society_economy`
- actual person/vehicle/item movement, detention physical location, combat damage: `physical_built`
- natural terrain: `spatial`
- road/communication/service operation: `infrastructure_information`
- actual information delivery: `infrastructure_information`
- physical possession vs legal ownership: Physical/Society boundary

## 3. State partitions

```text
governance.polity
governance.institution
governance.law_rule
governance.jurisdiction
governance.territorial_claim
governance.effective_control
governance.public_authority
governance.tax_fiscal
governance.permission_license
governance.diplomacy
governance.security_incident
governance.investigation
governance.judicial_case
governance.enforcement
governance.military_authority
governance.border_control
governance.lineage
```

## 4. Polity / governing authority

```text
PolityState {
  polity_id,
  related_organization_refs,
  lifecycle_state,
  governing_institution_refs,
  jurisdiction_refs,
  territorial_claim_refs,
  effective_control_refs,
  public_authority_refs,
  succession_rule_refs,
  recognition_refs,
  fiscal_refs,
  detail_level,
  lineage_id
}
```

Polityは国家・自治体等の制度的統治主体を表すが、特定の現代国家modelを前提にしない。

一般組織identityはSocietyEconomyがownerでき、GovernanceSecurityはその組織へpublic authority relationを付与する。

## 5. Institution / power transition

```text
InstitutionState {
  institution_id,
  polity_ref,
  institution_kind,
  authority_structure,
  office_refs,
  decision_method_ref,
  appointment_or_selection_rules,
  lifecycle_state,
  history_anchor
}
```

選挙、任命、世襲、内部競争等の権力移行を表現可能にするが、単一方式を普遍化しない。

office vacancy/successionはSocietyEconomyのorganization roleと連携しつつ、public authorityのeffective holderはGovernanceSecurityが確定する。

## 6. Law / rule

### 6.1 `LegalRuleState`

```text
LegalRuleState {
  rule_id,
  issuing_authority_ref,
  rule_kind,
  normative_content_ref,
  jurisdiction_ref,
  subject_scope,
  effective_from_step,
  effective_until_step?,
  priority_or_conflict_rule_ref,
  enforcement_refs,
  status,
  revision
}
```

### 6.2 act vs legality

```text
World action fact
 -> applicable jurisdiction/law determination
 -> legal classification/effect
 -> possible investigation/enforcement/judicial process
```

Physical/Resident actionへ絶対的`crime=true`を埋め込まない。

同じactionでも地域・時代・制度でclassificationが変わり得る。

### 6.3 Resident knowledge

法が施行されたこととResidentがその法を知っていることを分離する。

law truthはGovernance、Resident awareness/beliefはResident domain。

## 7. Jurisdiction / territory

### 7.1 spatial reference

```text
JurisdictionState {
  jurisdiction_id,
  authority_ref,
  jurisdiction_kind,
  spatial_scope_refs,
  subject_matter_scope,
  effective_period,
  status,
  dispute_refs?
}
```

SpatialScopeを参照するが、geometry ownerはSpatial。

### 7.2 separate territory concepts

少なくとも次を分離する。

- land/use right: SocietyEconomy
- private ownership claim: SocietyEconomy
- administrative jurisdiction: GovernanceSecurity
- sovereign/territorial claim: GovernanceSecurity
- effective control: GovernanceSecurity
- actual physical occupation/presence: PhysicalBuilt

境界は一致しなくてよい。

### 7.3 `TerritorialClaimState`

```text
TerritorialClaimState {
  claim_id,
  claimant_ref,
  spatial_scope_ref,
  claim_kind,
  recognition_state,
  dispute_refs,
  effective_period,
  status
}
```

### 7.4 `EffectiveControlState`

```text
EffectiveControlState {
  control_scope_id,
  spatial_scope_ref,
  controlling_authority_ref,
  enforcement_capacity_ref,
  control_strength_state,
  contested_by_refs,
  basis_step,
  status
}
```

claim/recognitionとactual effective controlを同一視しない。

## 8. Public authority / administrative capacity

```text
PublicAuthorityState {
  authority_id,
  institution_ref,
  authority_kind,
  jurisdiction_ref,
  permitted_actions,
  resource_capacity_refs,
  office_holder_refs,
  execution_capacity_state,
  status
}
```

制度上「実行可能な権限」と、実際の人員・車両・施設・予算・通信によるexecution capacityを分離する。

public orderを出しただけでworld consequenceを即時発生させない。

## 9. Tax / public fiscal state

```text
TaxRuleState {
  tax_rule_id,
  authority_ref,
  tax_base_semantics,
  rate_or_amount_rule,
  jurisdiction_ref,
  effective_period,
  exemptions_or_conditions,
  collection_process_ref,
  status
}
```

```text
PublicFiscalClaimState {
  fiscal_claim_id,
  authority_ref,
  liable_party_ref,
  amount_or_assessment,
  currency_ref,
  due_context,
  payment_ref?,
  delinquency_state,
  status
}
```

money/account transferはSocietyEconomy。Governanceはtax obligation/assessment/public spending authorityをownerする。

税を定義しただけで自動徴収せず、assessment/payment/enforcement processへ接続する。

## 10. Permission / license / certification boundary

```text
InstitutionalPermissionState {
  permission_id,
  authority_ref,
  holder_ref,
  permission_kind,
  scope,
  validity_period,
  conditions,
  status
}
```

actual skillはResident、physical key/lockはPhysicalBuilt、institutional permission/licenseはGovernance。

「資格がある」ことと「実能力がある」ことを分離する。

## 11. Diplomacy

```text
DiplomaticRelationState {
  relation_id,
  party_refs,
  recognition_state,
  relation_facets,
  treaty_refs,
  dispute_refs,
  negotiation_refs,
  sanction_refs,
  mission_or_envoy_refs,
  history_anchor
}
```

単一友好度だけにしない。

### 11.1 Treaty

```text
TreatyState {
  treaty_id,
  parties,
  terms_ref,
  effective_period,
  obligation_refs,
  compliance_state,
  termination_condition_refs,
  status
}
```

economic contractと似てもpublic/diplomatic institutional authorityを伴うtreatyはGovernance owner。

## 12. Security incident

行為/事件truthと制度上のcaseを分ける。

```text
SecurityIncidentState {
  incident_id,
  world_event_refs,
  jurisdiction_refs,
  reported_or_detected_state,
  suspected_legal_classification_refs,
  victim_or_subject_refs,
  evidence_refs,
  case_refs,
  status
}
```

犯罪は「一定確率で発生する統計event」ではなく、Resident/organization action factがlawに照らされ、発覚/通報/捜査される過程を持つ。

## 13. Detection / report / investigation

```text
InvestigationState {
  investigation_id,
  incident_refs,
  investigating_authority_ref,
  investigator_refs,
  evidence_refs,
  suspect_refs,
  witness_refs,
  resource_refs,
  progress_state,
  status
}
```

事件発生 = 全員/警察が知る、にはしない。

Resident perception/report、Infrastructure communication、organization/security capacityを通じて発覚する。

Evidenceのphysical existenceはPhysicalBuilt、意味/evidentiary statusはGovernance、Resident witness memoryはResident。

## 14. Arrest / detention / enforcement

```text
EnforcementOrderState {
  order_id,
  issuing_authority_ref,
  target_refs,
  action_kind,
  legal_basis_refs,
  jurisdiction_ref,
  effective_period,
  execution_status,
  executor_refs,
  result_refs
}
```

Order発行とphysical executionを分ける。

逮捕/拘束を確定するにはactual officer/target/location/access等のphysical consequenceを必要とする。

## 15. Judicial case

```text
JudicialCaseState {
  case_id,
  forum_or_authority_ref,
  party_refs,
  charge_or_claim_refs,
  applicable_rule_refs,
  evidence_refs,
  proceeding_state,
  judgment_refs,
  remedy_or_sentence_refs,
  status
}
```

違法行為発生時に固定penaltyを即時付与しない。

investigation、case、judgment、sentence/enforcementを分離する。

## 16. Military authority / unit relation

### 16.1 ownership split

- Resident: soldier individual health/skills/morale-related psychology
- SocietyEconomy: organization/employment/material ownership
- GovernanceSecurity: military legal/institutional authority、unit hierarchy/mission/order
- PhysicalBuilt: actual movement/combat contact/damage/equipment presence
- InfrastructureInformation: command communication/transport service

### 16.2 `MilitaryUnitState`

```text
MilitaryUnitState {
  military_unit_id,
  authority_ref,
  member_refs,
  commander_role_refs,
  subordinate_refs,
  mission_refs,
  readiness_institutional_state,
  supply_requirement_refs,
  location_ref,
  status
}
```

### 16.3 Mission / Order

```text
MilitaryOrderState {
  order_id,
  issuing_authority_ref,
  target_unit_refs,
  mission_kind,
  objective_refs,
  geographic_scope_ref,
  timing_context,
  constraints,
  communication_status_ref,
  execution_status
}
```

Orderを出しただけで部隊がteleport/勝利しない。

## 17. Combat / war boundary

GovernanceSecurityはwar/diplomatic status、mission、military authorityをownerする。

actual combat outcomeは:

```text
resident decisions/morale
+ physical presence/equipment
+ terrain/weather
+ supply/logistics
+ command information
+ mission/order
 -> physical combat interactions
 -> injury/damage/material consumption
 -> governance military consequence
```

単一軍事力数値比較だけで解決しない。

exact weapon/ballistic algorithmはPhase 4/Addon。

## 18. Border / checkpoint

```text
BorderControlState {
  border_control_id,
  jurisdiction_or_claim_ref,
  spatial_interface_ref,
  controlling_authority_ref,
  entry_exit_rules,
  checkpoint_refs,
  enforcement_capacity_ref,
  status
}
```

border ruleとactual checkpoint physical presence/capacityを分離する。

smugglingはruleを物理的/社会的に回避するworld actionとして成立し得る。

## 19. Public service authority

Governanceは公共サービスの「誰に何を提供する制度/priority/entitlementか」をownerできる。

InfrastructureInformationはservice network/capacity/queue/availabilityをownerし、SocietyEconomyは費用/contract、PhysicalBuiltはfacility presenceをownerする。

## 20. Update phases

### 20.1 PREPARE

- effective law/institution Config/Operation
- world event/report
- organization/resident action
- territory/control change candidate
- service/military/security facts

をfreeze。

### 20.2 SOCIAL_INSTITUTIONAL logical subphases

```text
G0_INSTITUTION_AUTHORITY
G1_LAW_JURISDICTION_TERRITORY
G2_TAX_PERMISSION_ADMIN
G3_DIPLOMACY
G4_SECURITY_INVESTIGATION
G5_JUDICIAL_ENFORCEMENT
G6_MILITARY_AUTHORITY
```

### 20.3 CONSEQUENCE

- tax/payment obligation
- permit/access fact
- enforcement action intent
- military action intent
- treaty/economic restriction
- Resident/Organization notification claim

をowner domainへ出す。

### 20.4 VALIDATE

- authority/jurisdiction validity
- effective law period
- case/order endpoints
- territory scope references
- tax claim consistency
- one institutional decision lineage
- detail boundary institutional continuity

を検証。

## 21. Same-Step dependency

基本:

```text
World action State(S)
 -> legal/institutional classification
 -> case/order/claim candidate
 -> physical/economic execution intent
 -> result fact
 -> governance consequence State(S+1)
```

Orderとexecution resultをsame-step loopで無限更新しない。

Emergency stop等のexplicit same-step constraintが必要なら限定されたphase edgeを定義する。

## 22. Intent catalog

- `governance.rule.enact/amend/repeal`
- `governance.jurisdiction.change`
- `governance.territorial_claim.change`
- `governance.effective_control.change`
- `governance.tax.assess`
- `governance.permission.issue/revoke`
- `governance.diplomacy.negotiate/treaty/sanction`
- `governance.security.report/investigate`
- `governance.enforcement.order`
- `governance.judicial.open/decide`
- `governance.military.order`
- `governance.border.apply_rule`

## 23. Event catalog

- `InstitutionalAuthorityChanged`
- `LegalRuleChanged`
- `JurisdictionChanged`
- `TerritorialClaimChanged`
- `EffectiveControlChanged`
- `TaxLiabilityChanged`
- `PermissionChanged`
- `DiplomaticRelationChanged`
- `TreatyStateChanged`
- `SecurityIncidentRecognized`
- `InvestigationStateChanged`
- `JudicialDecisionIssued`
- `EnforcementOrderIssued`
- `EnforcementResultRecorded`
- `MilitaryMissionChanged`
- `BorderControlChanged`

## 24. Conflict scope

```text
governance/authority/{authority_id}
governance/law/{jurisdiction}/{subject_scope}
governance/territory/{spatial_scope}/{claim_kind}
governance/tax/{claim_id}
governance/permission/{permission_id}
governance/case/{case_id}
governance/order/{order_id}
governance/military/{unit_id}
```

複数lawが競合する場合は制度stateとしてpriority/conflict ruleを持ち、runtime arrival順で適用法を決めない。

領域主張は複数並存可能だが、single-valued effective controlが必要なscopeはexplicit contested state/resolve policyを持つ。

## 25. Shared invariant

### 25.1 `INV-GOV-AUTHORITY-BASIS`

public authority actionは有効なinstitution/authority/jurisdiction basisを持つ。

### 25.2 `INV-GOV-LAW-EFFECTIVE-TIME`

future/repealed ruleをbasis Step外へ暗黙適用しない。

### 25.3 `INV-GOV-ACT-LEGALITY-SEPARATION`

world action factを制度classificationと混同しない。

### 25.4 `INV-GOV-TERRITORY-CONCEPT-SEPARATION`

ownership、administrative jurisdiction、claim、effective controlを単一fieldに潰さない。

### 25.5 `INV-GOV-ORDER-EXECUTION-SEPARATION`

institutional orderだけでphysical/economic resultを捏造しない。

### 25.6 `INV-GOV-CASE-CONTINUITY`

active judicial/investigation/enforcement obligationをdetail低下で消さない。

### 25.7 `INV-GOV-MILITARY-PHYSICAL-CAUSALITY`

military mission/resultはsoldier/equipment/supply/terrain等とのcross-domain因果を維持する。

## 26. Detail level

### 26.1 `D0_ENTITY`

- individual case/investigation/order
- detailed checkpoint/security action
- office holder/authority
- military unit/mission
- local law/permission execution

### 26.2 `D1_LOCAL_AGGREGATE`

- persistent cases/orders retained
- local enforcement capacity summary
- local jurisdiction/control state
- military unit aggregate with member refs
- administrative queue/capacity

### 26.3 `D2_REGIONAL_AGGREGATE`

- polity/institution/law full persistent core
- territory/claim/control
- tax/fiscal aggregates + individual obligations where persistent
- crime/security rates only as derived projection, not replacement for active cases
- military force/unit summary with persistent identity
- diplomatic state

### 26.4 `D3_BOUNDARY_SUMMARY`

- border/jurisdiction interface
- treaty/sanction/recognition facts
- military/security boundary activity
- cross-boundary legal obligations/cases
- persistent law/polity identity

## 27. Cadence / promotion / demotion

cadence:

- `STEP/FAST`: combat/security/emergency/active proceeding
- `NORMAL`: tax/admin/case/territory
- `SLOW`: institution/political/diplomatic structural change
- `EVENT_DRIVEN`: law enactment、judgment、order、treaty

promotion trigger:

- active Diver/resident interaction with authority
- crime/security incident
- court/enforcement process
- border crossing
- political transition
- military conflict
- territorial dispute/control change

Demotion guard:

- active court/investigation
- arrest/enforcement execution
- unresolved territorial/control transfer
- active battle/mission needing individual detail
- treaty/claim transition
- persistent case/order archive incomplete

## 28. Boundary exchange

```text
InstitutionalBoundaryExchange {
  jurisdiction_refs,
  border_rules,
  recognition/treaty/sanction facts,
  enforcement_capacity_summary,
  military_presence_summary,
  active_case_or_obligation_refs,
  basis_step
}
```

Resident/vehicle crossingに伴い、identity-bearing case/order/permissionを複製・消失させない。

## 29. Persistence / Replay

persist/replay:

- polity/institution lifecycle
- law versions/effective Steps
- jurisdiction/territorial claim/effective control
- tax/permission history
- diplomacy/treaties
- incident/investigation/case/judgment/enforcement
- military unit/mission/order
- border rule/control
- detail transitions

## 30. Traceability

| Requirement | Coverage |
|---|---|
| Q014/Q018 | politics/governance、crime/justice/security |
| Q015 | military authority coupled to actual residents/equipment/supply |
| Q017 | ownership/use vs legal/institutional right separation |
| Q037 | diplomacy/treaty/alliance/trade/territorial issue |
| Q042 | planning rules only where historically existing institutions |
| Q046〜Q051 | contracts/property/economy with law/tax/governance boundary |
| Q060〜Q066 | governance, tax, public services, diplomacy, military/security detailed state |
| Q070/Q071 | territory/jurisdiction/claim/effective control and 3D spatial boundary |
| Q103 | institutional permission vs physical lock |
| Q108 | public works authority/rights/contract boundary |
| Q138 | qualification/license authority vs actual skill |
| Q148/Q149 | emergency/public record institutional process |
| Q150/Q151 | inheritance and succession institutional consequence |
| Q181/Q182 | administrative enforcement capacity; border/checkpoint |
| Q183/Q184 | smuggling; effective control distinct from claim |
| Q187/Q188 | public-space/facility institutional accessibility |
| Q190〜Q194 | institutional continuity across detail boundaries |
| Q235〜Q239/Q275 | Admin world-affecting operation still subject to Core invariants; Gateway admission separate |

## 31. Phase 4 handoff

Phase 4で確定する事項:

- law/rule semantic schema
- jurisdiction/territory geometry reference schema
- authority/institution/office schema
- tax/fiscal model
- permission/license schema
- diplomacy/treaty schema
- incident/evidence/investigation/case schema
- enforcement state machine
- military unit/mission/order schema
- legal conflict-resolution algorithm
- effective-control computation
- cadence/detail parameters

Phase 4はact/legal classification separation、territory concept separation、order/execution separationを変更してはならない。
