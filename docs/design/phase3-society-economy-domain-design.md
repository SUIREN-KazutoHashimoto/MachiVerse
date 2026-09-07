# 詳細設計 Phase 3: Society / Economy Domain設計

Status: Complete / P3-05  
Tracking: Issue #15  
Parent: `phase3-world-domain-design.md`  
Common contract: `phase3-domain-common-contract.md`

## 1. 目的

`society_economy` domainは、組織、所属、雇用、職務、契約、所有・債務、貨幣・決済、家計・企業会計、市場・価格、生産・取引、物流上の経済関係、教育・文化・評判・社会的情報のauthoritative social/economic stateを所有する。

本domainは物品の実3D位置や車両運行を所有しない。財が「誰のものか・何として取引/会計されるか」と「world上のどこに物理的にあるか」を分離し、economic transactionがphysical stockを理由なく生成・消滅させない境界を定義する。

## 2. Responsibility / Non-responsibility

### 2.1 SocietyEconomyが所有する責務

- organization identity/lifecycle、membership、role、economic authority relation
- employment/job relation、compensation obligation
- household/economic unit relation
- social/economic ownership、claim、debt、contract
- currency/money issuance and monetary institution state within standard scope
- account/financial asset、payment/settlement obligation
- market/offer/demand/transaction/price history
- accounting inventory/asset valuation reference
- production/business plan and economic result
- shipment/logistics contract/consignment state
- education/learning institution relation and social opportunity
- culture/language/religion affiliation/trait state at social level
- reputation/social trust projection distinct from private Resident belief
- society-level information object/claim provenance where not network-delivery state

### 2.2 SocietyEconomyが所有しない責務

- physical item pose/container/vehicle/cargo presence: `physical_built`
- natural resource deposit: `environment`
- Resident private knowledge/belief/memory: `resident`
- network delivery/media transmission capacity: `infrastructure_information`
- law/tax/public authority/judgment/territory: `governance_security`
- building/service capacity: `physical_built` / `infrastructure_information`

## 3. State partitions

```text
society.organization
society.membership_role
society.employment
society.household
society.contract_claim
society.property_right
society.currency_money
society.finance_account
society.market_transaction
society.business_production
society.logistics_obligation
society.education
society.culture
society.reputation
society.information_claim
society.history_lineage
```

## 4. Organization

```text
OrganizationState {
  organization_id,
  organization_class,
  lifecycle_state,
  purpose_state,
  membership_refs,
  role_structure,
  asset_claim_refs,
  obligation_refs,
  governance_method_ref?,
  location_or_facility_refs,
  parent_or_affiliation_refs,
  detail_level,
  lineage_id
}
```

組織は歴史的に成立・成長・縮小・分裂・統合・消滅できる。固定カテゴリだけで全組織を説明しない。

Residentは複数組織へ所属可能。

国家のpublic authority部分はGovernanceSecurityがownerし、国家/政府組織のsocial organization identityとのcross-domain relationを持てる。

## 5. Membership / Role

```text
MembershipRoleState {
  membership_id,
  organization_id,
  resident_or_org_ref,
  role_refs,
  authority_refs,
  responsibility_refs,
  compensation_refs?,
  joined_step,
  ended_step?,
  status
}
```

role上の権限と法的public authorityを同一概念にしない。

## 6. Employment

```text
EmploymentState {
  employment_id,
  worker_resident_id,
  employer_ref,
  job_kind,
  work_location_refs,
  schedule_or_time_obligation,
  compensation_terms,
  duty_refs,
  status,
  start_step,
  end_step?
}
```

雇用はResidentの「職業ラベル」だけではなく、実worker/employer relationとして保持する。

Residentが仕事を選ぶ意思はResident、actual physical workはPhysicalBuilt、雇用契約/賃金義務はSocietyEconomy。

## 7. Household / shared economy

```text
HouseholdEconomicState {
  household_id,
  member_refs,
  shared_resource_claims,
  shared_expense_rules,
  housing_relation_refs,
  dependent_or_care_refs,
  budget_state,
  obligation_refs,
  lifecycle_state
}
```

householdとbiological/social familyを同一視しない。family truthはResident、共同支出/資源関係はSocietyEconomy。

## 8. Contract / Claim / Obligation

### 8.1 Contract

```text
ContractState {
  contract_id,
  parties,
  contract_kind,
  terms,
  promised_performance,
  consideration_refs?,
  effective_period,
  status,
  breach_state?,
  dispute_refs?,
  governing_institution_refs?,
  history_anchor
}
```

### 8.2 lifecycle

```text
PROPOSED -> AGREED -> ACTIVE -> FULFILLED
                    -> BREACHED / TERMINATED / DISPUTED
```

exact enumはPhase 4だが、契約をdetail降格で消さない。

### 8.3 physical fulfillment

物品delivery契約は、契約成立だけで物品移動済みにしない。

```text
contract obligation
 -> shipment/work/payment action
 -> physical/service result
 -> fulfillment evidence
 -> contract state transition
```

## 9. Property / ownership

```text
PropertyClaimState {
  claim_id,
  subject_ref,
  holder_ref,
  claim_kind,
  quantity_or_share?,
  effective_period,
  transfer_restrictions?,
  institutional_basis_ref?,
  status
}
```

physical possession/useとinstitutional ownership/rightを分離する。

- PhysicalBuilt: 誰が持っている/どこにある
- SocietyEconomy: social/economic ownership/claim
- GovernanceSecurity: law上の認定/強制/争いの制度結果

## 10. Goods / accounting inventory boundary

SocietyEconomyはaccounting/economic lotを持てる。

```text
EconomicInventoryLot {
  lot_ref,
  owner_or_account_ref,
  item_kind,
  claimed_quantity,
  physical_stock_refs,
  reserved_quantity,
  valuation_state,
  status
}
```

`claimed_quantity`は対応physical stockとshared invariantで整合させる。

同じ物品を会計上二重売却/二重消費しない。

## 11. Production / Business

```text
ProductionActivityState {
  production_id,
  organization_or_actor_ref,
  recipe_or_process_ref,
  input_requirement_refs,
  equipment/facility_refs,
  labor_requirement_refs,
  output_expectation,
  current_batch_state,
  cost_state,
  status
}
```

actual transformationはPhysicalBuilt上のmaterial/equipment/workと接続する。

経済側planが存在するだけでoutput inventoryを生成しない。

## 12. Market / Price / Transaction

### 12.1 MarketState

```text
MarketState {
  market_id,
  market_scope,
  tradable_class,
  participant_access_ref,
  demand_summary,
  supply_summary,
  recent_transaction_refs,
  price_state,
  liquidity_or_activity_state,
  detail_level
}
```

標準は実取引からpriceが形成される。詳細order bookを必須にしない。

### 12.2 Transaction

```text
EconomicTransaction {
  transaction_id,
  buyer_ref,
  seller_ref,
  subject_ref,
  quantity,
  price_or_terms,
  payment_ref,
  delivery_obligation_ref?,
  status,
  effective_step
}
```

transaction agreement、payment、physical deliveryを分ける。

### 12.3 price information

market truthとResident/Organizationが知っているmarket informationを分離する。

価格情報がnetwork/communicationを介して届くまでremote actorが自動で最新価格を知ることにはしない。

## 13. Currency / Money

```text
CurrencyState {
  currency_id,
  issuer_ref,
  medium_or_unit_class,
  supply_state,
  issuance_rules_ref,
  acceptance_context,
  exchange_relation_refs,
  lifecycle_state,
  institutional_refs
}
```

複数通貨・交換手段を許容し、現代中央銀行を普遍前提にしない。

Q279に従い、通貨発行、供給量、発行主体、基本的金融政策を標準scopeに含める。

## 14. Financial account / debt / credit

```text
FinancialPositionState {
  account_or_position_id,
  holder_ref,
  asset_balances,
  liability_refs,
  receivable_refs,
  payable_refs,
  liquidity_state,
  credit_state,
  status
}
```

債務不履行時に債務を理由なく消去しない。

default、延滞、asset処分、credit deterioration等へ遷移可能にする。

## 15. Payment / Settlement

```text
PaymentState {
  payment_id,
  payer_ref,
  payee_ref,
  amount,
  currency_or_medium_ref,
  source_position_ref,
  destination_ref,
  status,
  settlement_step?,
  causality_refs
}
```

物理貨幣、account-like資産、credit paymentを区別可能にする。

詳細interbank clearingはPhase 4/Addon境界。

## 16. Logistics economic state

Physical transportとeconomic logisticsを分離する。

```text
ShipmentObligationState {
  shipment_id,
  consignor_ref,
  consignee_ref,
  cargo_claim_refs,
  origin_ref,
  destination_ref,
  required_time_window,
  carrier_ref?,
  transport_service_ref?,
  physical_cargo_refs,
  status
}
```

- SocietyEconomy: shipment obligation/contract/cargo claim
- InfrastructureInformation: route/service availability/capacity
- PhysicalBuilt: actual cargo/vehicle movement

## 17. Education / learning relation

```text
EducationParticipationState {
  education_ref,
  learner_resident_id,
  provider_ref,
  subject_or_skill_refs,
  attendance_or_participation_state,
  resource/time/cost_obligations,
  progress_evidence_refs,
  status
}
```

Resident knowledge/skillの実変化はResident owner。

学校/徒弟/家族/自学等の経路を許容し、制度は歴史的に形成可能。

## 18. Culture / Language / Religion

社会levelのculture/language/religionを固定排他的tagにしない。

```text
CulturalTraitState {
  trait_id,
  trait_kind,
  carrier_refs,
  regional_or_group_distribution,
  practice_state,
  transmission_refs,
  lineage,
  lifecycle_state
}
```

Resident個人のbelief/knowledge/practiceはResidentに保持し、society側はgroup/organization/social traitとtransmission relationを保持する。

形成・分岐・融合・衰退を許容する。

## 19. Reputation / social trust

```text
ReputationState {
  subject_ref,
  audience_scope_or_group_ref,
  reputation_facets,
  evidence_or_report_refs,
  confidence_or_strength,
  last_change_step
}
```

ReputationはCore truthそのものではない。

Resident個人がその評判を知る/信じるかはResident epistemic state。

## 20. Social information claim

社会・組織・mediaで流通する意味情報を次のように扱える。

```text
InformationClaimState {
  claim_id,
  proposition_or_content_ref,
  source_ref,
  creation_step,
  truth_relation_ref?,
  provenance_refs,
  intended_audience_refs?,
  secrecy_class?,
  status
}
```

本domainは「何というclaimが社会的に存在するか」をownerできる。

実network配送はInfrastructureInformation、Residentへの受信/信念化はResident owner。

## 21. Confidential information

秘密/機密について:

- SocietyEconomy/Governance: secrecy classification/authorization relation
- InfrastructureInformation: delivery/access channel
- Resident: 実際に知っているか

access permissionとactual knowledgeを分離する。

## 22. Organization decision / business action

OrganizationはResidentとは別のcollective decision stateを持てる。

意思決定modelはorganization governance method、roles、information、resources、goal等を入力とし、world action intentを出す。

owner外stateへ直接mutationしない。

例:

- hire/fire
- set price
- purchase/sell
- start production
- invest
- borrow/lend
- form/terminate contract
- open/close facility operation

## 23. Update phases

### 23.1 PREPARE

- scheduled economic/social Operation
- physical/service result
- Resident/organization decision intent
- effective Config
- detail/cadence

をfreeze。

### 23.2 SOCIAL_INSTITUTIONAL logical subphases

```text
S0_RELATION_ORGANIZATION
S1_CONTRACT_PROPERTY_EMPLOYMENT
S2_PRODUCTION_INVENTORY
S3_MARKET_TRANSACTION_PRICE
S4_PAYMENT_FINANCE
S5_LOGISTICS
S6_EDUCATION_CULTURE_REPUTATION_INFORMATION
```

public law/tax/enforcementはGovernanceSecurity phaseへ分離する。

### 23.3 CONSEQUENCE

- Resident income/occupation/knowledge opportunity
- Physical delivery/work requirement
- Infrastructure service demand
- Governance tax/legal consequence

などのevent/intentを生成する。

### 23.4 VALIDATE

- ownership/contract endpoint validity
- money/account balance semantics
- inventory/physical stock link
- obligation continuity
- organization lifecycle refs
- detail boundary flow

を検証。

## 24. Same-Step dependency

例:

```text
Resident/Organization request from State(S)
 -> contract/transaction decision
 -> payment/delivery/work intent
 -> physical/service result
 -> fulfillment/accounting consequence State(S+1)
```

「売買成立した瞬間に遠隔物品がteleportする」cycleを作らない。

Market priceは同一Stepのtransaction集合をcanonical mergeして形成する。

## 25. Intent catalog

主要intent:

- `society.organization.create/change/terminate`
- `society.membership.join/leave/change_role`
- `society.employment.create/change/end`
- `society.contract.propose/accept/perform/breach`
- `society.property.transfer`
- `society.market.offer/request/transaction`
- `society.payment.request`
- `society.finance.borrow/repay/default`
- `society.production.request`
- `society.shipment.create/update`
- `society.education.participate`
- `society.information.publish/share`

## 26. Event catalog

- `OrganizationLifecycleChanged`
- `MembershipChanged`
- `EmploymentChanged`
- `ContractStateChanged`
- `PropertyClaimChanged`
- `MarketTransactionCompleted`
- `MarketPriceChanged`
- `PaymentSettled`
- `DebtDefaulted`
- `ProductionBatchEconomicStateChanged`
- `ShipmentObligationChanged`
- `EducationParticipationChanged`
- `CulturalTraitChanged`
- `ReputationChanged`
- `InformationClaimCreated`

## 27. Conflict scope

```text
society/org/{organization_id}
society/membership/{membership_id}
society/contract/{contract_id}
society/property/{subject_ref}
society/inventory/{lot_ref}
society/market/{market_id}/{tradable_class}
society/payment/{account_or_position}
society/currency/{currency_id}
society/shipment/{shipment_id}
```

同一stock/assetを複数transactionでconsume/transferする場合はavailable quantityを基準にdeterministic allocationする。

arrival順first-comeをworld outcomeへ使わない。

## 28. Shared invariant

### 28.1 `INV-SOCIETY-PHYSICAL-STOCK-LINK`

physical item/material quantityとeconomic inventory/claimのtransferをcause-linkedにし、detail/transactionだけでstockを増殖させない。

### 28.2 `INV-SOCIETY-OWNERSHIP-UNIQUENESS`

exclusive ownership semanticsを持つsubjectについて矛盾するexclusive claimsを同時確定しない。共同/分割所有はexplicit share relationで表す。

### 28.3 `INV-SOCIETY-CONTRACT-OBLIGATION-CONTINUITY`

active契約/債務をdetail低下やcounterparty lifecycleだけで黙って消さない。

### 28.4 `INV-SOCIETY-MONEY-SUPPLY-CONTINUITY`

currency issuance/destruction/transferをissuer/policy/transaction causeなしで増減させない。

### 28.5 `INV-SOCIETY-ACCOUNT-BALANCE`

payment/transferでsource/destinationを同一logical settlementへ結ぶ。

### 28.6 `INV-SOCIETY-TRUTH-EPISTEMIC-SEPARATION`

market/contract/world truthをResident beliefへ自動コピーしない。

## 29. Detail level

### 29.1 `D0_ENTITY`

- individual organization/member/contract/account/transaction
- actual offer/demand/negotiation where needed
- employment/work relation
- detailed shipment/inventory link
- individual education/social event

### 29.2 `D1_LOCAL_AGGREGATE`

- persistent identities/obligations retained
- market/local demand/supply aggregate
- organization/account/inventory summary
- employment cohort with individual obligation anchors
- shipment/service queue summary

### 29.3 `D2_REGIONAL_AGGREGATE`

- organization/major contract persistent core
- market price/volume/rate
- sector production/consumption stocks
- household/finance aggregate with persistent claims retained
- interregional logistics flow
- culture/reputation distribution

### 29.4 `D3_BOUNDARY_SUMMARY`

- goods/finance/contract flow across boundary
- persistent external counterparty/contract refs
- market boundary conditions
- information claim/delivery handoff refs
- migration/employment obligation refs

## 30. Update cadence

- `STEP`: active market/payment/contention where necessary
- `FAST`: active retail/logistics/employment events
- `NORMAL`: business/household/market cycles
- `SLOW`: demographic-cultural/organization structural change
- `EVENT_DRIVEN`: contract/payment/default/major transaction

exact intervals are Config.

## 31. Promotion / Demotion

promotion trigger:

- Diver/resident local economic interaction
- market stress/shortage
- organization conflict/change
- active transaction/contract dispute
- shipment boundary crossing
- major financial/default event
- predictive policy

Demotion guard:

- unsettled transaction/payment
- active contract negotiation/dispute requiring individual detail
- physical/economic stock handoff incomplete
- active insolvency/succession
- cross-boundary shipment in transfer
- identity/obligation archive incomplete

既存organization/contract/account/Resident relationをpromotion時に再生成しない。

## 32. Boundary exchange

```text
EconomicBoundaryExchange {
  source_scope,
  target_scope,
  basis_step,
  goods_flow,
  service_flow,
  money_flow,
  claim_or_contract_refs,
  price_information_refs,
  shipment_refs
}
```

source/target二重計上を禁止する。

## 33. Cross-domain causal links

| Source | Society/Economy effect | Follow-up |
|---|---|---|
| Resident | demand/work/organization/transaction decision | contract/payment/work intent |
| PhysicalBuilt | actual production/delivery/damage | inventory/fulfillment/accounting update |
| Environment | resources/weather/ecology | production/price/shortage change |
| InfrastructureInformation | route/service/network/info delivery | logistics/market/business feasibility |
| GovernanceSecurity | law/tax/permit/sanction | cost/contract/property/business consequence |
| Participation | controlled Resident action | normal Resident/economic rules remain |

## 34. Persistence / Replay

persist/replay:

- organization history
- membership/employment
- contracts/claims/property
- market transaction/price basis
- currency/money issuance
- account/debt/payment
- production/inventory link
- shipment obligation
- culture/reputation/information provenance
- detail transitions

## 35. Traceability

| Requirement | Coverage |
|---|---|
| Q009/Q012/Q013 | occupation/organization/economy |
| Q017 | possession/use vs ownership/right separation |
| Q025/Q026 | culture/education social state |
| Q027 | logistics economic obligation coupled to physical transport |
| Q030/Q031 | company/business and real transaction price formation |
| Q046〜Q051 | property/contract/employment/market/finance social-economic interactions |
| Q056 | reputation social state distinct from Resident belief |
| Q057/Q058 | research/knowledge-media social institution boundary |
| Q067 | production economic process coupled to physical process |
| Q100〜Q101 | work/logistics contracts require actual physical action |
| Q115〜Q124 | materials/production/reuse/conservation/currency/payment/household/accounting/default |
| Q125〜Q129 | media/info claim provenance and epistemic separation |
| Q145/Q147 | finite service/social event participation relation |
| Q149〜Q153 | public-record boundary/inheritance/organization succession/intergenerational effects/name social semantics |
| Q157〜Q161 | reservation/business hours/employment/labor market |
| Q163〜Q168 | labor organization/insurance/liability/standards/consumer/economic information |
| Q183 | informal/illegal economic relation can exist, legal status separate |
| Q185/Q186 | resource rights/economic pressure coupled to environment |
| Q190〜Q194 | economic flow across detail boundaries |
| Q279 | currency issuance/supply/basic monetary policy standard scope |

## 36. Phase 4 handoff

Phase 4で確定する事項:

- organization/member/role schema
- contract/claim/property schema
- item/economic lot mapping
- production/process schema
- market transaction/price algorithm
- currency/account/payment/debt schema
- monetary policy model
- household/accounting schema
- logistics obligation schema
- culture/reputation/information claim schema
- economic cadence/detail thresholds
- cross-domain settlement transaction schema

Phase 4はphysical/economic separation、obligation continuity、epistemic separationを変更してはならない。
