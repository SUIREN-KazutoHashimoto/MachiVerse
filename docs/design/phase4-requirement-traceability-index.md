# 詳細設計 Phase 4: Requirement Traceability Index

Status: Complete / Issue #17 traceability expansion  
Tracking: Issue #17  
Source: `phase3-traceability-cross-cutting-review.md` §10  
Verification: `phase4-test-acceptance.md`, `phase4-test-acceptance-addendum.md`

## 1. 目的

Phase 3 completion reviewで確認済みのQ001〜Q279 coverageを、implementation change impactとacceptance追跡に利用できるよう **1 requirement = 1 row** に展開する。

Requirement本文の正本は `docs/requirements`。本書はPhase 3のrange-level traceabilityを個別RequirementIdへ展開し、共通coverage profileへ参照させるindexである。

## 2. Coverage profile

| Profile | Q range | Primary semantic coverage | Phase 4 detailed contract family | Verification family |
|---|---|---|---|---|
| T01 | Q001〜Q007 | Spatial / Environment / Resident / SocietyEconomy | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md`; `phase4-algorithm-determinism.md` | `domain.*` / `transaction.*` / `determinism.*` |
| T02 | Q008〜Q012 | Resident / SocietyEconomy | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T03 | Q013〜Q019 | SocietyEconomy / GovernanceSecurity / Resident / InfrastructureInformation | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T04 | Q020〜Q026 | Resident / Environment / SocietyEconomy | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T05 | Q027〜Q034 | InfrastructureInformation / PhysicalBuilt / SocietyEconomy / Environment / Resident | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md`; `phase4-algorithm-determinism.md` | `domain.*` / `transaction.*` / `determinism.*` |
| T06 | Q035〜Q041 | Resident / Environment / PhysicalBuilt / InfrastructureInformation | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T07 | Q042〜Q049 | GovernanceSecurity / SocietyEconomy / PhysicalBuilt / Resident / InfrastructureInformation | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T08 | Q050〜Q058 | SocietyEconomy / GovernanceSecurity / Environment / PhysicalBuilt / Resident | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T09 | Q059〜Q069 | Resident / SocietyEconomy / GovernanceSecurity / PhysicalBuilt / InfrastructureInformation | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T10 | Q070〜Q084 | SocietyEconomy / GovernanceSecurity / Resident / InfrastructureInformation / PhysicalBuilt / Environment | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T11 | Q085〜Q099 | Spatial / Environment / PhysicalBuilt | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-algorithm-determinism.md` | `domain.*` / `transaction.*` / `determinism.*` |
| T12 | Q100〜Q109 | PhysicalBuilt / Resident / SocietyEconomy / GovernanceSecurity | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T13 | Q110〜Q119 | Environment / PhysicalBuilt / SocietyEconomy | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-algorithm-determinism.md` | `domain.*` / `transaction.*` / `determinism.*` |
| T14 | Q120〜Q129 | SocietyEconomy / InfrastructureInformation / Resident / GovernanceSecurity | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T15 | Q130〜Q139 | Resident / SocietyEconomy / GovernanceSecurity | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-algorithm-determinism.md` | `domain.*` / `determinism.*` |
| T16 | Q140〜Q144 | Resident / Environment / PhysicalBuilt / SocietyEconomy | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T17 | Q145〜Q154 | InfrastructureInformation / Resident / PhysicalBuilt / GovernanceSecurity / SocietyEconomy | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T18 | Q155〜Q165 | InfrastructureInformation / Environment / PhysicalBuilt / SocietyEconomy / Resident / GovernanceSecurity | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T19 | Q166〜Q179 | Resident / InfrastructureInformation / PhysicalBuilt / SocietyEconomy / GovernanceSecurity / Environment | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T20 | Q180〜Q189 | GovernanceSecurity / InfrastructureInformation / SocietyEconomy / Environment / PhysicalBuilt | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |
| T21 | Q190〜Q194 | Phase3 common contract / all domains / cross-domain causality | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-algorithm-determinism.md` | `detail.*` / `domain.*` / `transaction.*` |
| T22 | Q195〜Q199 | Phase1 persistence contract / all Phase3 domains | `phase4-persistence-specification.md`; `phase4-persistence-record-catalog.md`; `phase4-domain-state-registry.md` | `persistence.*` / `determinism.*` |
| T23 | Q200〜Q214 | Phase1 common / determinism / Config contracts | `phase4-core-data-structures.md`; `phase4-algorithm-determinism.md`; `phase4-config-specification.md`; `phase4-config-addendum.md` | `schema.*` / `determinism.*` / `config.*` |
| T24 | Q215〜Q229 | Phase1 Operation lifecycle/protocol + Phase2 Gateway/Core | `phase4-protocol-schema.md`; `phase4-protocol-payload-catalog.md`; `phase4-core-data-structures.md`; `phase4-persistence-specification.md` | `protocol.*` / `persistence.*` / `component.*` |
| T25 | Q230〜Q231 | Phase2 General View | `phase4-platform-runtime-profile.md`; `phase4-protocol-schema.md`; `phase4-protocol-payload-catalog.md` | `protocol.*` / `component.*` |
| T26 | Q232〜Q234 | Participation / Resident / PhysicalBuilt + Phase2 General View | `phase4-domain-state-registry.md`; `phase4-domain-operation-event-intent-catalog.md`; `phase4-protocol-payload-catalog.md` | `domain.*` / `protocol.*` / `detail.*` |
| T27 | Q235〜Q239 | Phase1/2 Admin / Gateway / Core contracts | `phase4-protocol-payload-catalog.md`; `phase4-auth-session-protocol.md`; `phase4-config-specification.md`; `phase4-observability-audit.md` | `protocol.*` / `security.*` / `config.*` / `observability.*` |
| T28 | Q240〜Q244 | Phase2 Gateway security domain | `phase4-auth-session-protocol.md`; `phase4-internal-component-auth-profile.md`; `phase4-protocol-payload-catalog.md` | `security.*` / `protocol.*` |
| T29 | Q245〜Q249 | Phase1 protocol capability contracts | `phase4-protocol-schema.md`; `phase4-protocol-payload-catalog.md`; `docs/protocols/schema/message-registry-v1.md` | `protocol.*` / `schema.*` |
| T30 | Q250〜Q254 | Phase1/2 observability | `phase4-observability-audit.md`; `phase4-observability-completion-review.md`; `phase4-protocol-payload-catalog.md` | `observability.*` / `protocol.*` |
| T31 | Q255〜Q259 | Phase1/2 addon boundary | `phase4-protocol-schema.md`; `phase4-platform-runtime-profile.md`; `phase4-implementation-work-breakdown.md` | `protocol.*` / `component.*` / `release.*` |
| T32 | Q260〜Q264 | Participation / Resident / PhysicalBuilt | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md`; `phase4-protocol-payload-catalog.md` | `domain.*` / `protocol.*` / `detail.*` |
| T33 | Q265〜Q266 | Phase3 common / Resident / all persistent domains | `phase4-core-data-structures.md`; `phase4-domain-state-registry.md`; `phase4-algorithm-determinism.md` | `schema.*` / `detail.*` / `determinism.*` |
| T34 | Q267〜Q274 | Phase1 persistence / addon / Config contracts | `phase4-persistence-specification.md`; `phase4-persistence-record-catalog.md`; `phase4-config-specification.md`; `phase4-platform-runtime-profile.md` | `persistence.*` / `config.*` / `release.*` |
| T35 | Q275〜Q275 | GovernanceSecurity + Core invariants + Phase2 Gateway admission | `phase4-domain-operation-event-intent-catalog.md`; `phase4-auth-session-protocol.md`; `phase4-protocol-payload-catalog.md` | `domain.*` / `security.*` / `protocol.*` |
| T36 | Q276〜Q278 | Phase1 Operation / SimulationStep / Pause contract | `phase4-core-data-structures.md`; `phase4-protocol-schema.md`; `phase4-algorithm-determinism.md` | `protocol.*` / `determinism.*` / `component.*` |
| T37 | Q279〜Q279 | SocietyEconomy | `phase4-domain-state-registry.md`; `phase4-domain-payload-schema.md`; `phase4-domain-operation-event-intent-catalog.md` | `domain.*` / `transaction.*` |

## 3. Per-requirement index

| Requirement | Coverage profile |
|---|---|
| Q001 | T01 |
| Q002 | T01 |
| Q003 | T01 |
| Q004 | T01 |
| Q005 | T01 |
| Q006 | T01 |
| Q007 | T01 |
| Q008 | T02 |
| Q009 | T02 |
| Q010 | T02 |
| Q011 | T02 |
| Q012 | T02 |
| Q013 | T03 |
| Q014 | T03 |
| Q015 | T03 |
| Q016 | T03 |
| Q017 | T03 |
| Q018 | T03 |
| Q019 | T03 |
| Q020 | T04 |
| Q021 | T04 |
| Q022 | T04 |
| Q023 | T04 |
| Q024 | T04 |
| Q025 | T04 |
| Q026 | T04 |
| Q027 | T05 |
| Q028 | T05 |
| Q029 | T05 |
| Q030 | T05 |
| Q031 | T05 |
| Q032 | T05 |
| Q033 | T05 |
| Q034 | T05 |
| Q035 | T06 |
| Q036 | T06 |
| Q037 | T06 |
| Q038 | T06 |
| Q039 | T06 |
| Q040 | T06 |
| Q041 | T06 |
| Q042 | T07 |
| Q043 | T07 |
| Q044 | T07 |
| Q045 | T07 |
| Q046 | T07 |
| Q047 | T07 |
| Q048 | T07 |
| Q049 | T07 |
| Q050 | T08 |
| Q051 | T08 |
| Q052 | T08 |
| Q053 | T08 |
| Q054 | T08 |
| Q055 | T08 |
| Q056 | T08 |
| Q057 | T08 |
| Q058 | T08 |
| Q059 | T09 |
| Q060 | T09 |
| Q061 | T09 |
| Q062 | T09 |
| Q063 | T09 |
| Q064 | T09 |
| Q065 | T09 |
| Q066 | T09 |
| Q067 | T09 |
| Q068 | T09 |
| Q069 | T09 |
| Q070 | T10 |
| Q071 | T10 |
| Q072 | T10 |
| Q073 | T10 |
| Q074 | T10 |
| Q075 | T10 |
| Q076 | T10 |
| Q077 | T10 |
| Q078 | T10 |
| Q079 | T10 |
| Q080 | T10 |
| Q081 | T10 |
| Q082 | T10 |
| Q083 | T10 |
| Q084 | T10 |
| Q085 | T11 |
| Q086 | T11 |
| Q087 | T11 |
| Q088 | T11 |
| Q089 | T11 |
| Q090 | T11 |
| Q091 | T11 |
| Q092 | T11 |
| Q093 | T11 |
| Q094 | T11 |
| Q095 | T11 |
| Q096 | T11 |
| Q097 | T11 |
| Q098 | T11 |
| Q099 | T11 |
| Q100 | T12 |
| Q101 | T12 |
| Q102 | T12 |
| Q103 | T12 |
| Q104 | T12 |
| Q105 | T12 |
| Q106 | T12 |
| Q107 | T12 |
| Q108 | T12 |
| Q109 | T12 |
| Q110 | T13 |
| Q111 | T13 |
| Q112 | T13 |
| Q113 | T13 |
| Q114 | T13 |
| Q115 | T13 |
| Q116 | T13 |
| Q117 | T13 |
| Q118 | T13 |
| Q119 | T13 |
| Q120 | T14 |
| Q121 | T14 |
| Q122 | T14 |
| Q123 | T14 |
| Q124 | T14 |
| Q125 | T14 |
| Q126 | T14 |
| Q127 | T14 |
| Q128 | T14 |
| Q129 | T14 |
| Q130 | T15 |
| Q131 | T15 |
| Q132 | T15 |
| Q133 | T15 |
| Q134 | T15 |
| Q135 | T15 |
| Q136 | T15 |
| Q137 | T15 |
| Q138 | T15 |
| Q139 | T15 |
| Q140 | T16 |
| Q141 | T16 |
| Q142 | T16 |
| Q143 | T16 |
| Q144 | T16 |
| Q145 | T17 |
| Q146 | T17 |
| Q147 | T17 |
| Q148 | T17 |
| Q149 | T17 |
| Q150 | T17 |
| Q151 | T17 |
| Q152 | T17 |
| Q153 | T17 |
| Q154 | T17 |
| Q155 | T18 |
| Q156 | T18 |
| Q157 | T18 |
| Q158 | T18 |
| Q159 | T18 |
| Q160 | T18 |
| Q161 | T18 |
| Q162 | T18 |
| Q163 | T18 |
| Q164 | T18 |
| Q165 | T18 |
| Q166 | T19 |
| Q167 | T19 |
| Q168 | T19 |
| Q169 | T19 |
| Q170 | T19 |
| Q171 | T19 |
| Q172 | T19 |
| Q173 | T19 |
| Q174 | T19 |
| Q175 | T19 |
| Q176 | T19 |
| Q177 | T19 |
| Q178 | T19 |
| Q179 | T19 |
| Q180 | T20 |
| Q181 | T20 |
| Q182 | T20 |
| Q183 | T20 |
| Q184 | T20 |
| Q185 | T20 |
| Q186 | T20 |
| Q187 | T20 |
| Q188 | T20 |
| Q189 | T20 |
| Q190 | T21 |
| Q191 | T21 |
| Q192 | T21 |
| Q193 | T21 |
| Q194 | T21 |
| Q195 | T22 |
| Q196 | T22 |
| Q197 | T22 |
| Q198 | T22 |
| Q199 | T22 |
| Q200 | T23 |
| Q201 | T23 |
| Q202 | T23 |
| Q203 | T23 |
| Q204 | T23 |
| Q205 | T23 |
| Q206 | T23 |
| Q207 | T23 |
| Q208 | T23 |
| Q209 | T23 |
| Q210 | T23 |
| Q211 | T23 |
| Q212 | T23 |
| Q213 | T23 |
| Q214 | T23 |
| Q215 | T24 |
| Q216 | T24 |
| Q217 | T24 |
| Q218 | T24 |
| Q219 | T24 |
| Q220 | T24 |
| Q221 | T24 |
| Q222 | T24 |
| Q223 | T24 |
| Q224 | T24 |
| Q225 | T24 |
| Q226 | T24 |
| Q227 | T24 |
| Q228 | T24 |
| Q229 | T24 |
| Q230 | T25 |
| Q231 | T25 |
| Q232 | T26 |
| Q233 | T26 |
| Q234 | T26 |
| Q235 | T27 |
| Q236 | T27 |
| Q237 | T27 |
| Q238 | T27 |
| Q239 | T27 |
| Q240 | T28 |
| Q241 | T28 |
| Q242 | T28 |
| Q243 | T28 |
| Q244 | T28 |
| Q245 | T29 |
| Q246 | T29 |
| Q247 | T29 |
| Q248 | T29 |
| Q249 | T29 |
| Q250 | T30 |
| Q251 | T30 |
| Q252 | T30 |
| Q253 | T30 |
| Q254 | T30 |
| Q255 | T31 |
| Q256 | T31 |
| Q257 | T31 |
| Q258 | T31 |
| Q259 | T31 |
| Q260 | T32 |
| Q261 | T32 |
| Q262 | T32 |
| Q263 | T32 |
| Q264 | T32 |
| Q265 | T33 |
| Q266 | T33 |
| Q267 | T34 |
| Q268 | T34 |
| Q269 | T34 |
| Q270 | T34 |
| Q271 | T34 |
| Q272 | T34 |
| Q273 | T34 |
| Q274 | T34 |
| Q275 | T35 |
| Q276 | T36 |
| Q277 | T36 |
| Q278 | T36 |
| Q279 | T37 |

## 4. Coverage result

- Requirement rows: 279。
- Q001〜Q279欠番: 0。
- Phase 3 coverage判定との差異: 0。
- Detailed-design traceability blocker: 0件。

## 5. Implementation/Test linkage

Implementation issue作成時は対象RequirementIdからCoverage profileを引き、Phase 4 contract familyとP4-08 verification familyをacceptanceへ転記する。

QA-01で個別TestCaseId registryを確定した後、必要に応じexact TestCaseId列を追加できる。test implementation未作成を理由にrequirement/design coverageを未定義へ戻さない。