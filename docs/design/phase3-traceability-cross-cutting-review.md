# 詳細設計 Phase 3: Traceability / Cross-Cutting Review

Status: Complete / P3-09 / Phase 3 Completion Review  
Tracking: Issue #15  
Parent: `phase3-world-domain-design.md`

## 1. 目的

本書はPhase 3で定義したworld simulation domain群について、Q001〜Q279とのtraceability、state ownership、same-Step dependency、cross-domain causality、detail level、promotion/demotion、Phase 1/2との責務境界を横断監査し、Phase 4へ移行可能かを判定する。

本書をPhase 3 completion判定の正本とする。

## 2. 参照優先順位

設計解釈は次の順とする。

1. `docs/requirements` の後続要件を含む確定回答
2. Phase 1 final review / common contracts
3. Phase 2 cross-component review / component internal design
4. 本Phase 3 completion review
5. Phase 3 common/domain/cross-domain個別設計
6. 旧architecture文書の未決定記述

Phase 3個別文書の作業時点Statusや「後続で確定」記述が本書のcompletion判定と競合する場合、本書を優先する。

## 3. Phase 3成果物

| P3 | 成果物 | 判定 |
|---|---|---|
| P3-01 | `phase3-domain-common-contract.md` | Complete |
| P3-02 | `phase3-spatial-domain-design.md` | Complete |
| P3-02 | `phase3-environment-domain-design.md` | Complete |
| P3-03 | `phase3-physical-built-domain-design.md` | Complete |
| P3-04 | `phase3-resident-domain-design.md` | Complete |
| P3-04 | `phase3-participation-domain-design.md` | Complete |
| P3-05 | `phase3-society-economy-domain-design.md` | Complete |
| P3-06 | `phase3-governance-security-domain-design.md` | Complete |
| P3-07 | `phase3-infrastructure-information-domain-design.md` | Complete |
| P3-08 | `phase3-cross-domain-causality.md` | Complete |
| P3-09 | 本書 | Complete |

## 4. Domain ownership final matrix

| Semantic state | Authoritative owner | Explicitly not owner |
|---|---|---|
| world 3D scope / natural terrain solid-void geometry | `spatial` | Environment, PhysicalBuilt, View |
| geology / soil / natural resource / water / atmosphere / weather / ocean / ecosystem / natural hazard | `environment` | Spatial geometry, Resident health |
| actual physical pose / collision / built structure / interior / item location / construction / damage / built combustion | `physical_built` | Resident intention, legal/economic ownership |
| Diver↔Resident binding / absence policy / world-effective control mode | `participation` | Gateway session/auth, Resident cognition |
| Resident identity / lifecycle / health / perception / belief / memory / psychology / goals / skills / family relation | `resident` | physical pose, institutional/economic relation |
| organization / employment / contract / property claim / market / money / finance / economic logistics / culture-reputation social state | `society_economy` | physical location, law enforcement, delivery network |
| polity / law / jurisdiction / territorial claim / effective control / tax authority / diplomacy / justice / security / military authority | `governance_security` | actual physical execution, generic organization assets |
| transport/utility/communication service / queue/capacity / information delivery / record carrier-store / dependency outage | `infrastructure_information` | physical asset geometry, information truth/belief |

Authoritative ownerの重複は認めない。cross-domain relationはread/event/intent/shared invariant/aggregate exchangeで接続する。

## 5. Responsibility overlap audit

### 5.1 Terrain geometry vs geology/material

- terrain solid/void geometry: Spatial
- geology/material/resource stock: Environment
- excavation work/material handling: PhysicalBuilt

`Spatial geometry change + Environment stock transition + Physical material handoff`をshared invariantで結ぶため、掘削でgeometryだけ消える、resourceだけ減る、materialだけ増える状態を禁止した。

判定: overlap解消済み。

### 5.2 Resident action vs physical result

- Resident: intention/decision/action intent
- PhysicalBuilt: actual pose/contact/item manipulation/work progress

意思決定だけでteleport、pickup、construction completionを発生させない。

判定: overlap解消済み。

### 5.3 Physical possession vs economic/legal ownership

- physical possession/location: PhysicalBuilt
- economic/social ownership/claim: SocietyEconomy
- law上の認定/執行: GovernanceSecurity

窃取、係争、担保、没収等で3概念が不一致な状態を表現可能。

判定: overlap解消済み。

### 5.4 Action truth vs legality

- world action fact: Resident/PhysicalBuilt等
- applicable law / legal classification: GovernanceSecurity

行為へ普遍的`crime=true`を埋め込まない。

判定: overlap解消済み。

### 5.5 Core truth vs claim vs delivery vs belief

- Core world truth: each owner domain
- social/media claim: SocietyEconomy
- delivery/channel/record availability: InfrastructureInformation
- Resident receipt/knowledge/belief: Resident

全知的information propagationを禁止。

判定: overlap解消済み。

### 5.6 Resident vs Participation

- Resident identity/lifecycle/behavior: Resident
- Diver binding/control availability/absence policy: Participation
- account/session/auth/exclusive admission: Gateway

Diver用Residentを生成せず、disconnectでbindingを自動解除・reassignしない。

判定: overlap解消済み。

### 5.7 Infrastructure physical asset vs service

- physical road/pipe/plant/antenna/facility condition: PhysicalBuilt
- logical network/service/capacity/outage: InfrastructureInformation

設備単体が正常でもupstream dependency不成立ならservice停止可能。

判定: overlap解消済み。

### 5.8 Hazard vs damage

- natural hazard driver: Environment
- terrain geometry consequence: Spatial
- building/item damage: PhysicalBuilt
- Resident injury: Resident
- network outage: InfrastructureInformation
- emergency authority: GovernanceSecurity

hazard domainが他domain private stateへ直接damageを書き込まない。

判定: overlap解消済み。

### 5.9 Family vs household

- parent-child/family relation: Resident
- shared household budget/resource relation: SocietyEconomy

普遍的な単一家族形へ統合しない。

判定: overlap解消済み。

### 5.10 Military authority vs combat

- mission/order/unit institutional authority: GovernanceSecurity
- soldier body/skill/decision: Resident
- equipment/presence/contact/damage: PhysicalBuilt
- supply/ownership/contract: SocietyEconomy
- transport/command communication: InfrastructureInformation
- terrain/weather: Spatial/Environment

単一軍事力値だけでworld resultを確定しない。

判定: overlap解消済み。

## 6. Dependency cycle audit

自然・社会worldにはfeedback loopが存在するが、domain private mutable stateのsame-Step相互writeは禁止した。

### 6.1 Standard rule

```text
A reads State(S).B
B reads State(S).A
 -> A_candidate(S+1)
 -> B_candidate(S+1)
```

同一Stepの結果が本当に必要な場合のみ、producerのmerge済みimmutable factへ`same_step_dependency`を張る。

### 6.2 Audited feedback loops

| Feedback | Resolution |
|---|---|
| Atmosphere ↔ Ocean | State(S) mutual read / next-state candidate、必要なforcingのみexplicit edge |
| Weather -> Hydrology | merged precipitation factをsame-step edgeで許可 |
| Environment -> Spatial -> Environment | geometry mutation intent + candidate validation、private mutual write禁止 |
| Resident decision -> Physical result -> Resident | action resultは原則State(S+1) consequence |
| Health -> Work -> Income -> Food -> Health | Step間feedback、同一Step無限再plan禁止 |
| Market -> Production -> Inventory -> Market | transaction/production resultを次stateへ、same-step stock conflictはdeterministic merge |
| Law -> Action -> Enforcement -> Resident | orderとphysical executionを分離、結果をconsequenceへ |
| Power -> Communication -> Repair -> Power | dependency propagation + next-Step feedback / bounded coupled resolution |
| Military order -> combat -> territorial control -> order | physical result後のgovernance consequenceを次stateへ |

unresolved mutable dependency cycle: 0件。

## 7. Domain rank / same-Step ordering review

P3-08でstable domain rankを次へ固定した。

```text
10 spatial
20 environment
30 physical_built
40 participation
50 resident
60 society_economy
70 governance_security
80 infrastructure_information
```

これはPhase 1 same-Step canonical total orderの`domain_rank`へ使用可能なstable semantic rankであり、physical executionをsingle-thread直列化するものではない。

world resultへwall clock、thread completion、Gateway/Master identity、View FPS/camera、storage iteration orderを持ち込まない。

判定: Phase 1 determinism contractと整合。

## 8. Detail level review

全主要domainは共通detail contractに従う。

```text
D0_ENTITY
D1_LOCAL_AGGREGATE
D2_REGIONAL_AGGREGATE
D3_BOUNDARY_SUMMARY
```

### 8.1 Identity / update detail separation

Resident、organization、building、vehicle、contract、Diver binding等のpersistent identityは、detail低下だけを理由に消去・別identity再生成しない。

Residentについては特にQ265/Q266に従い、個体として存在することと高頻度詳細updateを分離した。

### 8.2 Universal conservation classes

P3-08で以下を固定した。

- `IDENTITY`
- `STOCK`
- `OBLIGATION`
- `FLOW`
- `PROVENANCE`

promotion/demotionやboundary exchangeでこれらを無理由に失わない。

### 8.3 Promotion

promotion inputsはauthoritative world state、effective Config、scheduled operations/domain triggers、Participation requirement等に限定する。

View camera/FPS、worker availability、wall-clock timingをworld-affecting promotion triggerにしない。

### 8.4 Demotion

active transaction/event/handoff中はdemotionを延期可能にし、archive + aggregate installが完了する前にdetail authorityを破棄しない。

判定: Q190〜Q194、Q265〜Q266と整合。

## 9. Cross-domain semantic transaction review

P3-08で少なくとも次をsemantic transactionとして設計した。

- mining/excavation
- construction
- demolition/ruin
- birth
- death
- disease transmission
- food consumption
- market sale + physical delivery
- information transmission
- public record
- crime/justice
- border crossing/smuggling
- natural disaster cascade
- infrastructure cascading outage
- medical service
- employment/physical work
- military operation

CrossDomainTransactionはdatabase実装を固定しない論理契約であり、required invariant失敗時にworld-facing participant effectを部分commitしない。

判定: Phase 4 data structure/transaction implementationへ落とせる粒度。

## 10. Q001〜Q279 traceability

本節は全要件番号を欠番なくPhase 3 domainまたはPhase 1/2の前提契約へ対応付ける。

### 10.1 Q001〜Q099

| Q range | Primary Phase 3 coverage | Notes |
|---|---|---|
| Q001〜Q007 | Spatial / Environment / Resident / SocietyEconomy | world initialization、自然因果、prehistory、technology/knowledge formationへ接続 |
| Q008〜Q012 | Resident / SocietyEconomy | knowledge、occupation、psychology、relationship、organization |
| Q013〜Q019 | SocietyEconomy / GovernanceSecurity / Resident / InfrastructureInformation | economy、politics、military、ownership、crime、information |
| Q020〜Q026 | Resident / Environment / SocietyEconomy | lifecycle/health/family/ecology/culture/education |
| Q027〜Q034 | InfrastructureInformation / PhysicalBuilt / SocietyEconomy / Environment / Resident | transport/logistics/resources/weather/business/market/daily life/items/interior |
| Q035〜Q041 | Resident / Environment / PhysicalBuilt / InfrastructureInformation | medicine/infection/pollution/fire/water/power/communication |
| Q042〜Q049 | GovernanceSecurity / SocietyEconomy / PhysicalBuilt / Resident / InfrastructureInformation | land use、movement、perception、migration、class/public opinion/political change/invention |
| Q050〜Q058 | SocietyEconomy / GovernanceSecurity / Environment / PhysicalBuilt / Resident | finance/tax/resource/ecology/maintenance/relations/reputation/organizations/culture activities |
| Q059〜Q069 | Resident / SocietyEconomy / GovernanceSecurity / PhysicalBuilt / InfrastructureInformation | memory/housing/consumption/advertising/insurance/welfare/research/media/manufacturing/quality/standards |
| Q070〜Q084 | SocietyEconomy / GovernanceSecurity / Resident / InfrastructureInformation / PhysicalBuilt / Environment | contracts/territory/calendar-light/maps/equipment/physiology/lifecycle/population/postmortem |
| Q085〜Q099 | Spatial / Environment / PhysicalBuilt | terrain/water/building damage/items/ruins/subsurface/mining/underground/air/liquid/ocean/caves |

### 10.2 Q100〜Q199

| Q range | Primary Phase 3 coverage | Notes |
|---|---|---|
| Q100〜Q109 | PhysicalBuilt / Resident / SocietyEconomy / GovernanceSecurity | actual work/item transport/access/construction/public works/demolition |
| Q110〜Q119 | Environment / PhysicalBuilt / SocietyEconomy | wildlife/vegetation/ecology/material/process/reuse/conservation/waste |
| Q120〜Q129 | SocietyEconomy / InfrastructureInformation / Resident / GovernanceSecurity | currency/payment/household/accounting/default/media/network/secrets/belief |
| Q130〜Q139 | Resident / SocietyEconomy / GovernanceSecurity | emotion/communication/goals/routines/stress/skills/aptitude/license/tacit knowledge |
| Q140〜Q144 | Resident / Environment / PhysicalBuilt / SocietyEconomy | hunting/fishing/animal disease/domestication/conflict/biodiversity |
| Q145〜Q154 | InfrastructureInformation / Resident / PhysicalBuilt / GovernanceSecurity / SocietyEconomy | service queue/crowd/event/emergency/records/inheritance/succession/inequality/naming/burial |
| Q155〜Q165 | InfrastructureInformation / Environment / PhysicalBuilt / SocietyEconomy / Resident / GovernanceSecurity | waste/pests/reservation/hours/address/employment/labor/safety/organizations/care/economic info search |
| Q166〜Q179 | Resident / InfrastructureInformation / PhysicalBuilt / SocietyEconomy / GovernanceSecurity / Environment | expectations/schedules/route/time/item search/carrying/tools/storage/spoilage/accident/liability/risk/warnings |
| Q180〜Q189 | GovernanceSecurity / InfrastructureInformation / SocietyEconomy / Environment / PhysicalBuilt | census/admin capacity/border/smuggling/effective control/shared resources/public access/network dependency |
| Q190〜Q194 | Phase3 common contract + all domains + cross-domain causality | variable detail/promotion/demotion/materialization/boundary causality |
| Q195〜Q199 | Phase 1 persistence contracts + all Phase 3 domains | domain state/event must be snapshot/replay/config-compatible; storage algorithm remains Phase 1/4 boundary |

### 10.3 Q200〜Q279

| Q range | Primary owner/coverage | Phase 3 impact |
|---|---|---|
| Q200〜Q214 | Phase 1 common/determinism/Config contracts | all domains use SimulationStep, deterministic merge/random, effective Config; no redefinition |
| Q215〜Q229 | Phase 1 operation lifecycle/protocol + Phase 2 Gateway/Core | Phase3 intents/events receive only Core-effective scheduled input; no network timing authority |
| Q230〜Q231 | Phase 2 General View | Phase3 publication is authoritative-derived; View prediction/render does not mutate domain state |
| Q232〜Q234 | Participation / Resident / PhysicalBuilt + Phase 2 View | Diver prediction reconcile、disconnect absence policy、Resident continuity |
| Q235〜Q239 | Phase 1/2 Admin/Gateway/Core contracts | Phase3 state transitions still pass generic world invariants; no Admin special bypass |
| Q240〜Q244 | Phase 2 Gateway security domain | Participation does not own login/session/auth; receives admitted opaque world facts only |
| Q245〜Q249 | Phase 1 protocol capability contracts | Phase3 domain semantics are internal Core contracts; protocol capabilities remain component boundary |
| Q250〜Q254 | Phase 1/2 observability | Phase3 contributes diagnostic partitions/projections; operational backend not domain concern |
| Q255〜Q259 | Phase 1/2 addon boundary | addon cannot silently violate Phase3 ownership/determinism/invariant contracts |
| Q260〜Q264 | Participation / Resident / PhysicalBuilt | existing normal Resident binding, one-to-one, same Diver reconnect, normal death/rebind semantics |
| Q265〜Q266 | Phase3 common contract / Resident / all persistent domains | identity and update detail separated; future IDs from deterministic generation context |
| Q267〜Q274 | Phase 1 persistence/addon/config contracts | Phase3 domain state/history/references must migrate/replay without silent loss |
| Q275 | GovernanceSecurity + all domains under Core invariants / Phase2 Gateway admission | Gateway validates Admin semantics; Core/domain invariants remain authoritative |
| Q276〜Q278 | Phase 1 Operation/SimulationStep/Pause contract | Phase3 consumes final effective Step; no arrival-race or paused-time ambiguity |
| Q279 | SocietyEconomy | currency issuance/supply/issuer/basic monetary policy included in standard domain scope |

Traceability result: Q001〜Q279 coverage gap 0件。

## 11. Requirements requiring multi-domain coverage

一部要件は単独domainへ割り当てず、意図的に複数ownerを接続する。

| Concern | Required domains |
|---|---|
| agriculture | Environment + Resident + PhysicalBuilt + SocietyEconomy |
| medical care | Resident + PhysicalBuilt + InfrastructureInformation + SocietyEconomy + GovernanceSecurity |
| fire | PhysicalBuilt + Environment + Resident + InfrastructureInformation + GovernanceSecurity |
| mining | Spatial + Environment + PhysicalBuilt + SocietyEconomy + Resident/Governance as needed |
| transport/logistics | PhysicalBuilt + InfrastructureInformation + SocietyEconomy + Resident |
| public works | GovernanceSecurity + SocietyEconomy + PhysicalBuilt + InfrastructureInformation + Environment/Spatial |
| crime/justice | Resident + PhysicalBuilt + GovernanceSecurity + InfrastructureInformation + SocietyEconomy |
| military | GovernanceSecurity + Resident + PhysicalBuilt + SocietyEconomy + InfrastructureInformation + Spatial/Environment |
| emergency response | GovernanceSecurity + InfrastructureInformation + Resident + PhysicalBuilt + SocietyEconomy |
| information/media | SocietyEconomy + InfrastructureInformation + Resident + GovernanceSecurity where institutional |

このmulti-domain性は責務重複ではなく、authoritative fieldごとのowner分離とexplicit causalityによるものと判定する。

## 12. Phase 1 compatibility review

Phase 3は以下を再定義しない。

- `SimulationStep`
- World/Entity/Operation/Batch identity common rules
- deterministic semantic encoding/hash suite
- same-Step canonical order framework
- addressable deterministic random
- Config schema/version/effective apply/history contract
- protocol envelope
- persistence/replay/recovery consistency boundary
- Operation retry/dedup/lifecycle

Domain token/rank、domain event/intent、detail contractはこれらへ従属する。

判定: incompatible redefinition 0件。

## 13. Phase 2 compatibility review

Phase 3 domainはSimulation Coreの`DomainRuntime` / `DomainRegistry` / `DeterministicMerge` / `WorldStateStore`境界へ配置する。

再確認事項:

- DomainRuntimeはdirect shared mutable WorldState writeを行わない。
- domainはGateway/View protocol typeへ依存しない。
- publicationはconfirmed authoritative-derived projection。
- Participationのworld binding/effective absence policyはCore authority。
- Gatewayはsession/auth/exclusive admissionをownerし続ける。
- Admin View/Gateway component boundaryを変更しない。

判定: Phase 2 ownership conflict 0件。

## 14. Phase 4 readiness review

各domain文書は少なくとも次を持つ。

- authoritative state partitions/model
- responsibility/non-responsibility
- input intent
- emitted event
- update phase/cadence
- conflict scope/deterministic merge semantics
- shared invariant
- D0〜D3 state/detail behavior
- promotion/demotion trigger/guard
- boundary exchange
- persistence/replay requirement
- traceability
- Phase 4 handoff items

Phase 4で決定すべきdata structure/schema/algorithmは明示的に残し、Phase 3 semantic ownershipを変更せず具体化できる粒度になった。

判定: Phase 4 entry condition satisfied。

## 15. Non-blocking Phase 4 / implementation items

以下は未決定だがPhase 3 blockerではない。

- coordinate numeric representation / exact geometry data structure
- weather/hydrology/ocean/geology/ecology numerical algorithms
- collision/pathfinding/motion solver
- Resident cognition/health/skill numerical schema
- market/price/allocation/monetary policy algorithms
- law/rule representation and legal resolution algorithm
- transport/power/water/communication graph algorithms
- queue allocation algorithm
- concrete CrossDomainTransaction/candidate-state data structure
- persistence physical layout / database / compression
- publication full/delta encoding
- exact Config defaults/thresholds/cadences for domain detail

これらはPhase 4でschema/algorithmへ落とし込む。

## 16. Completion criteria audit

Issue #15完了条件に対する判定:

| Completion criterion | Result |
|---|---|
| 主要world subsystemがdomain designへ対応付けられている | PASS |
| domain間循環依存が整理されている | PASS |
| domain責務重複が整理されている | PASS |
| state/event/update/input/output/dependencyが定義されている | PASS |
| detail level別state/cadence/promotion/demotionが定義されている | PASS |
| cross-domain因果連携が定義されている | PASS |
| Q001〜Q279 traceabilityがある | PASS |
| Phase 4でschema/data structure/algorithmへ落とせる粒度 | PASS |
| unresolved domain-level blocker = 0 | PASS |

## 17. Phase 3 completion decision

Phase 3を`Complete`と判定する。

unresolved domain-level blocker: 0件。

Phase 4は、本Phaseで確定したauthoritative ownership、event/intent causality、detail semantics、cross-domain invariant、determinismを前提に、concrete data structures、schemas、algorithms、numeric representationsへ進む。
