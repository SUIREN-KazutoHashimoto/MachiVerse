# 詳細設計 Phase 3: Resident Domain設計

Status: Complete / P3-04  
Tracking: Issue #15  
Parent: `phase3-world-domain-design.md`  
Common contract: `phase3-domain-common-contract.md`

## 1. 目的

`resident` domainは、世界内に存在する通常住人の永続identity、life cycle、身体・健康、生理的欲求、知覚、知識・信念、記憶、感情・stress、目標・計画、技能・適性、日常行動、対人関係、家族・世代をauthoritative stateとして所有する。

Residentは単純なutility maximizerでも、Diver操作時だけ別ruleで動くplayer avatarでもない。Diverにbindingされた場合も同一Resident stateを維持し、通常の物理・社会・経済・法・健康・関係・生活ruleへ従う。

## 2. Responsibility / Non-responsibility

### 2.1 Residentが所有する責務

- Resident persistent identityとlifecycle
- 身体・成長・加齢・健康・負傷・疾病・障害・回復・死亡
- hunger/thirst/sleep等のphysiological needs
- resident側のthermal/hygiene/fatigue condition
- sensory capability、attention、perception result
- resident-level knowledge、understanding、belief、confidence
- memory
- emotion、stress、values、personality、preference
- goal、plan、routine、absence時を含むbehavior priorityの適用
- skill、aptitude、practice/decay
- resident-to-resident relationship state
- parent/child、family lineage、generation relation
- action decisionとdomain外へのAction/Mutation intent生成

### 2.2 Residentが所有しない責務

- actual 3D pose、collision、item physical location: `physical_built`
- terrain/weather/water/air truth: `spatial` / `environment`
- item ownership、price、contract、employment、organization membership: `society_economy`
- legal status、law、license authority、criminal judgment: `governance_security`
- transport/network/service state: `infrastructure_information`
- Diver-resident bindingとabsence policyのeffective history: `participation`
- Gateway session/auth/exclusive-control admission
- View prediction/presentation

## 3. DomainDefinition

```text
DomainDefinitionV1 resident {
  domain_token = "resident"
  domain_family = "resident"
  state_partitions = [
    resident.identity_lifecycle,
    resident.body_health,
    resident.physiology,
    resident.perception,
    resident.knowledge_belief,
    resident.memory,
    resident.psychology,
    resident.goal_plan,
    resident.skill_aptitude,
    resident.relationship,
    resident.family_lineage,
    resident.behavior_state,
    resident.lineage
  ]
  update_phases = [PREPARE, AGENT_INTERNAL, AGENT_ACTION, CONSEQUENCE, VALIDATE]
}
```

## 4. Resident identity / lifecycle

### 4.1 `ResidentIdentityState`

```text
ResidentIdentityState {
  resident_id,
  lifecycle_state,
  birth_or_creation_ref,
  birth_step?,
  death_step?,
  parent_relation_refs,
  lineage_generation,
  persistent_profile_ref,
  detail_level
}
```

ResidentIdは一度world history上で確定したらdetail降格、save/replay、Diver binding、死亡後の記録を跨いでstableとする。

未来Resident IDをworld生成時に全予約しない。出生/生成事象を一意に識別するdeterministic creation contextからPhase 1規則で導出する。

exactlyどの生物学的eventでResident entity identityを作成するかはPhase 4 schemaで固定するが、thread順やruntime timingをcreation contextに使わない。

### 4.2 lifecycle semantic states

少なくとも次を区別可能にする。

```text
DEVELOPING
ALIVE
DECEASED
```

必要に応じて成長stage等をBody state側に保持する。lifecycle enumのexact schemaはPhase 4。

死亡Residentをdetail降格やDiver都合で削除/復活させない。

## 5. Body / Health

### 5.1 `ResidentBodyHealthState`

```text
ResidentBodyHealthState {
  resident_id,
  age_and_development_state,
  body_capability_state,
  body_region_states,
  injury_states,
  disease_states,
  chronic_or_disability_states,
  immune_state_summary,
  recovery_state,
  pregnancy_or_reproductive_state?,
  vital_state,
  detail_level
}
```

標準は臓器/細胞/分子を常時完全simulateしないが、粗い身体部位、負傷、疾病、障害、妊娠・出産、成長・加齢が移動・仕事・生活へ差を生む粒度を持つ。

### 5.2 health causal input

Healthは少なくとも次から影響を受けられる。

- nutrition / hydration
- sleep / fatigue
- hygiene
- environmental heat/cold
- air/water contamination
- infectious exposure
- physical accident/collision
- work load
- combat/violence
- medical treatment
- stress

入力factをResidentがownerするhealth transitionへ変換し、source domain stateを直接mutationしない。

### 5.3 health output

- movement capability
- work capability
- sensory capability
- fatigue/stress input
- treatment need
- mortality risk/state

等を他partition/domainへ提供できる。

## 6. Physiological needs

```text
ResidentPhysiologyState {
  hunger_state,
  hydration_state,
  sleep_need_state,
  fatigue_state,
  thermal_load_state,
  cleanliness_hygiene_state,
  activity_load_state,
  recovery_need_state
}
```

needsは単なるUI barではなく、時間、行動、環境、実摂取、睡眠、衛生行動等から変化し、健康・感情・goal selection・work performanceへ影響する。

食事を選んだだけで栄養を得たことにはせず、PhysicalBuilt/SocietyEconomy上のfood accessとactual consumption eventを必要とする。

## 7. Perception

### 7.1 truthとperceptionを分離

ResidentはCore truthを直接知ることを標準にしない。

```text
World Fact
 -> perceptibility calculation
 -> PerceptionCandidate
 -> attention/sensory processing
 -> ResidentObservation
 -> memory/knowledge/belief update
```

### 7.2 `ResidentPerceptionState`

```text
ResidentPerceptionState {
  sensory_capabilities,
  attention_state,
  current_observations,
  uncertainty_state,
  observation_sources,
  basis_step
}
```

視覚はdistanceだけでなく遮蔽、lighting、weather/smoke等を参照可能にする。聴覚はdistance/reachability等を扱う。

### 7.3 source responsibility

- Spatial/PhysicalBuilt: geometry/occlusion/physical presence
- Environment: light/weather/smoke/visibility/ambient condition
- InfrastructureInformation: delivered communication
- Resident: 実際に知覚したobservationとその後の認知state

## 8. Knowledge / Belief

### 8.1 `ResidentKnowledgeState`

```text
ResidentKnowledgeState {
  subject_ref,
  awareness_state,
  understanding_state,
  usability_state,
  source_refs,
  confidence_state,
  last_reinforced_step?,
  detail_level
}
```

技術・知識について少なくとも次を区別する。

- world上で事実/技術が存在する
- Residentが存在を知る
- Residentが内容を理解する
- Residentが実際に利用できる

### 8.2 `ResidentBeliefState`

```text
ResidentBeliefState {
  proposition_ref,
  belief_value_or_hypothesis,
  confidence,
  evidence_refs,
  source_trust_refs,
  contradiction_refs,
  revision_history_anchor
}
```

情報を受信したことと信じたことを分離する。

競合情報を単純latest-winsにしない。source、evidence、trust、memory等を用いたdeterministic update policyをPhase 4で定義する。

## 9. Memory

```text
ResidentMemoryEntry {
  memory_id,
  resident_id,
  event_or_experience_ref,
  semantic_summary,
  emotional_weight,
  confidence_or_fidelity,
  strength_state,
  formed_step,
  last_recalled_step?,
  related_subject_refs,
  detail_class
}
```

全30Hz stateをmemoryとして保持しない。

意味のあるexperienceを選択して形成し、時間、想起、感情、重要度等によりstrength/fidelityが変化できる。

忘却はCore truth/historyの削除ではない。

## 10. Psychology

### 10.1 `ResidentPsychologyState`

```text
ResidentPsychologyState {
  needs_and_drives,
  values,
  personality_traits,
  preferences,
  emotional_state,
  stress_state,
  risk_tendency,
  social_tendency,
  detail_level
}
```

standard modelは世界の行動差へ寄与する必要十分な粒度とし、無制限な心理尺度を要求しない。

### 10.2 emotion / stress

emotion/stressは少なくとも次から変化できる。

- event/experience
- memory
- relationship
- health/physiology
- environment
- goal success/failure
- uncertainty/threat

行動へ影響するが、単一emotion値だけで行動を一意決定しない。

## 11. Goal / Plan / Routine

### 11.1 `ResidentGoalState`

```text
ResidentGoalState {
  goal_id,
  goal_kind,
  priority_state,
  motivation_refs,
  target_refs,
  status,
  deadline_or_time_context?,
  conflict_refs,
  created_step,
  causality_refs
}
```

### 11.2 `ResidentPlanState`

```text
ResidentPlanState {
  plan_id,
  goal_ref,
  action_sequence_or_graph,
  assumptions,
  required_resource_refs,
  progress_state,
  replanning_state,
  revision
}
```

### 11.3 routine

通勤、食事、睡眠、家事等のroutineをshortcutとして保持できるが、障害、goal、weather、health、Diver absence policy等で破棄/再計画できる。

routineを「必ずその行動をするscript」にしない。

## 12. Decision model

Resident action decisionは、少なくとも次の入力を統合できる。

```text
perceived_world
+ current knowledge/belief
+ health/physiology
+ emotion/stress
+ goals/plans/routines
+ values/personality/preferences
+ relationships
+ skills/aptitude
+ social/economic/institutional opportunities
+ participation control mode
```

同一条件での確率的選択はaddressable deterministic randomを用いる。

physical thread completion順、wall clock、View FPSを行動差の原因にしない。

## 13. Action output

Residentはdomain外stateを直接変更せず、action intentを発行する。

主要例:

- `physical.motion.request`
- `physical.item.pickup`
- `physical.item.place`
- `physical.opening.change_state`
- `physical.asset.operate`
- `resident.communication.attempt`
- `society.transaction.request`
- `society.employment.action`
- `society.education.participate`
- `governance.institutional.action`
- `infrastructure.service.request`
- `resident.medical.action`

Actionの成立/失敗結果をeventとして受け、State(S+1)以降のResident stateへ反映する。

## 14. Skill / Aptitude

### 14.1 `ResidentSkillState`

```text
ResidentSkillState {
  skill_kind,
  proficiency,
  practice_state,
  recent_use_state,
  instruction_refs,
  degradation_state,
  detail_level
}
```

skillとknowledgeを分離する。

practice、education、experienceで向上し、不使用等で低下可能にする。

### 14.2 aptitude

```text
ResidentAptitudeState {
  aptitude_kind,
  learning_modifier,
  performance_tendency,
  confidence_or_uncertainty
}
```

aptitudeだけで成功を保証しない。実際のperformanceはskill/knowledge/tool/material/fatigue/health/environment等との因果で決まる。

## 15. Interpersonal relationship

### 15.1 relation pair

```text
ResidentRelationshipState {
  relationship_id,
  resident_a,
  resident_b,
  relation_facets,
  trust_state,
  affinity_or_hostility_state,
  familiarity_state,
  obligation_or_expectation_refs,
  interaction_history_anchor,
  lifecycle_state,
  detail_level
}
```

全Resident pairを事前生成しない。実際に関係が成立した、または保持する因果価値があるpairだけ作成する。

### 15.2 ownership boundary

resident-to-resident interpersonal relationはResident owner。

organization membership、employment、contract等のinstitutional relationはSocietyEconomy ownerであり、そこからResident interpersonal stateへ影響eventを送れる。

## 16. Family / Generation

### 16.1 parent-child relation

```text
FamilyRelationState {
  family_relation_id,
  resident_refs,
  relation_kind,
  biological_relation_ref?,
  social_relation_state,
  formed_step,
  ended_step?,
  lineage_refs
}
```

parent/child relationを実stateとして保持し、複数世代へ継続する。

家族制度を普遍的な単一家族形に固定しない。

### 16.2 inheritance boundary

Residentはfamily/kin truthをownerするが、財産・債務・権利の相続結果はSocietyEconomy/GovernanceSecurity ownerへintent/eventを送って確定する。

### 16.3 genetic tendency

必要十分な身体的傾向を世代間へ引き継げるが、詳細なgene sequence simulationや「race mechanics」を標準化しない。

具体trait schemaはPhase 4。

## 17. Pregnancy / Birth / Development

pregnancy/reproductionはBodyHealth/Family stateへ跨る。

概念的因果:

```text
reproductive event
 -> pregnancy state
 -> time/health/nutrition/environment/medical influences
 -> birth event
 -> deterministic Resident creation context
 -> new persistent Resident identity
 -> parent/family lineage creation
 -> physical presence creation
```

Birthで生成されるResident identityとPhysicalPresenceをcross-domain invariantで結ぶ。

## 18. Disease / Infection

Residentは個体側のinfection/disease/immunity/health stateをownerする。

Environmentはenvironmental exposure condition、PhysicalBuiltはactual proximity/contact fact、resident/social actionsはcontact causeを提供できる。

感染成立はResident ownerのdeterministic transitionとして扱い、contactしただけで必ず感染するとはしない。

## 19. Medical care boundary

- resident: 症状/健康/treatment response
- society_economy: 医療契約/費用/雇用/organization
- physical_built: 人・薬・設備のactual presence/use
- infrastructure_information: facility capacity/queue/service availability where applicable
- governance_security: license/制度/規制

診療を選択しただけで治療済みにしない。実際のaccess、staff、medicine/equipment、time等を因果として接続する。

## 20. Information / communication boundary

Resident communicationは「相手へtruthを直接コピー」しない。

```text
speaker belief/intention
 -> communication act
 -> physical/network delivery
 -> listener perception/receipt
 -> interpretation
 -> knowledge/belief update
```

対面会話も通信network上のmessageも、受信・理解・信用を分ける。

## 21. Update phases

### 21.1 PREPARE

- effective Config
- participation control mode
- scheduled personal event
- cross-domain exposure/event
- resident detail/cadence

をfreezeする。

### 21.2 AGENT_INTERNAL

logical subphases:

```text
R0_LIFECYCLE_BODY_HEALTH
R1_PHYSIOLOGY
R2_PERCEPTION
R3_MEMORY_KNOWLEDGE_BELIEF
R4_EMOTION_STRESS
R5_SKILL_RELATIONSHIP
R6_GOAL_PLAN
```

同一Stepのexplicit dependencyだけをDAG edge化する。

### 21.3 AGENT_ACTION

- controlled Diver actionのworld-valid intent化
- autonomous action selection
- absence policy適用
- physical/social/institutional/service intent生成

### 21.4 CONSEQUENCE

前phase/他domainで確定したaction result、injury、transaction、communication等をResident state候補へ反映する。

### 21.5 VALIDATE

- lifecycle consistency
- family identity validity
- relationship endpoint validity
- health range/state consistency
- persistent resident identity continuity
- participation binding reference validity

を検証する。

## 22. Same-Step dependency

### 22.1 perception/action cycle

```text
State(S) world truth
 -> perception candidate
 -> Resident internal update
 -> action intent
 -> external domain result
 -> Resident consequence in State(S+1)
```

同一Stepでaction結果を読んで無限に再planしない。

### 22.2 health/action cycle

State(S) healthがaction capabilityを制約し、State(S)で実行したphysical actionのinjury/fatigue consequenceは原則State(S+1)へ反映する。

即時生命維持等にexplicit same-step consequenceが必要な場合は限定されたmerge済みfactとして定義する。

## 23. Conflict scope / deterministic merge

主要scope:

```text
resident/lifecycle/{resident_id}
resident/health/{resident_id}
resident/knowledge/{resident_id}/{subject}
resident/memory/{resident_id}/{memory_id}
resident/goal/{resident_id}/{goal_id}
resident/relationship/{canonical_pair}
resident/family/{relation_id}
resident/action/{resident_id}/{step}
```

同一ResidentにDiver actionとautonomous actionが同一Stepで競合する場合、Participation control modeとcanonical semantic priorityに従う。

network arrival順やlocal prediction orderをpriorityに使わない。

## 24. Shared invariant

### 24.1 `INV-RESIDENT-IDENTITY-LIFECYCLE`

同一ResidentIdが複数異なるlifecycleとして重複存在しない。

### 24.2 `INV-RESIDENT-PHYSICAL-PRESENCE`

ALIVEでworldへphysicalに存在するResidentは、domain policy上physical presenceが必要なdetailでは有効なPhysicalPresence referenceを持つ。

### 24.3 `INV-RESIDENT-FAMILY-ENDPOINT`

family relationが存在しないResidentIdへdanglingしない。

### 24.4 `INV-RESIDENT-BIRTH-CREATION-UNIQUENESS`

同一birth/generation eventから同一Residentを重複生成しない。

### 24.5 `INV-RESIDENT-DEATH-CONTINUITY`

死亡によってResident identity/history/family referenceを消去しない。

### 24.6 `INV-RESIDENT-EPISTEMIC-SEPARATION`

Core truth更新をreasonなくResident knowledge/beliefへ自動コピーしない。

### 24.7 `INV-RESIDENT-PARTICIPATION-BINDING`

Participation bindingのresident_refが有効なpersistent Resident identityを参照する。

## 25. Detail level

ResidentはQ265に従い、identity/existenceとupdate frequency/detailを分離する。

### 25.1 `D0_ENTITY`

保持/更新:

- full persistent Resident state
- local perception
- detailed active health/physiology
- goal/plan/action
- active memories/relationships
- skill/work state
- detailed physical/social interaction

### 25.2 `D1_LOCAL_AGGREGATE`

Resident identityは個体のまま保持しつつ:

- perception detailを局所summaryへ簡略化
- routine/actionをcoarser scheduling可能
- inactive memory/relationship更新頻度を低下
- health/physiologyを必要精度でintegrate

### 25.3 `D2_REGIONAL_AGGREGATE`

各persistent Residentについて最低限保持:

- ResidentId / lifecycle
- family/major relationship refs
- major health/chronic state
- knowledge/skill summary
- occupation/social obligation refs
- important goal/state
- journey/location aggregate reference
- causal history/archive anchor

日常actionはregional opportunity/constraintの下でlow cadenceまたはaggregate transitionを許容する。

### 25.4 `D3_BOUNDARY_SUMMARY`

デフォルトCではResident identityを捨てない。

保持:

- persistent resident directory/state core
- life/death/family identity
- major obligation/relationship
- aggregate location/journey
- major health state
- boundary crossing handoff
- detailed archive lineage

world外summary mode等をConfigで選ぶ場合もpromotion時に既存Residentを別identityで再生成しない。

## 26. Update cadence

Resident cadence class:

- `ACTIVE_STEP`: Diver-bound/危険/高interaction Resident
- `ACTIVE_FAST`: movement/work/social event中
- `NORMAL`: 通常生活
- `LOW`: 遠隔/低interaction individual
- `SCHEDULED`: sleep/travel/routine等のnext-event中心
- `EVENT_DRIVEN`: major external event発生時

exact Step intervalとpromotion thresholdはConfig。

低cadenceでもage、hunger等のtime-dependent stateはelapsed SimulationStepを用いて決定論的にintegrateする。

## 27. Promotion trigger

- Diver binding / active control
- boundary crossing
- accident/disaster/medical emergency
- dense interpersonal/social interaction
- active work/construction/combat
- important communication/decision
- pregnancy/birth/death transition
- predictive detail policy

## 28. Demotion guard

- Diver active control
- unresolved physical action
- acute injury/medical procedure
- birth/death transition
- active legal/contract event requiring individual tracking
- active interpersonal communication
- boundary handoff
- archive/persistent stateを失う場合

Diver binding自体はresident identity floorを要求する。absence時にupdate cadenceを下げる場合もbindingとResident identityは維持する。

## 29. Deterministic promotion / demotion

既存Residentはarchive/core persistent stateから復元し、aggregate statisticsから「似た別人」を作らない。

必要なinactive memory/routine/detailをmaterializeする場合は:

```text
resident_id
+ persistent state lineage
+ promotion_step
+ semantic materialization role
+ effective Config
```

をdeterministic contextとする。

人口stockだけから新Residentを作る場合は必ずbirth/migration等のexplicit generation factを持たせる。

## 30. Participation control boundary

Residentは`participation`から次をreadする。

```text
ResidentControlContext {
  resident_id,
  binding_state,
  control_mode,
  effective_absence_policy_ref?,
  basis_step
}
```

control mode例:

- `AUTONOMOUS`
- `DIVER_CONTROL_AVAILABLE`
- `DIVER_ABSENT_POLICY`

Gateway connection/sessionそのものをResidentがreadしない。

Diver actionはParticipationでbinding validityを確認した後、Residentのgoal/action boundaryへ入力される。

Diver commandでもhealth/physical/legal constraintsを無視しない。

## 31. Absence behavior application

Diver不在時は通常Residentのautonomous decision pipelineを使用し、Participationから取得したeffective absence policyをpriority modifierとして適用する。

policyは「Residentを停止する」「worldから退避させる」ものではない。

normal disconnectとerror disconnectでResident behavior semanticsを分けない。

## 32. Persistence / Replay

replayで少なくとも次を再現する。

- Resident identity/lifecycle
- birth/death
- health/disease/injury
- knowledge/belief/memory evolution
- emotion/stress
- goal/plan/action decision context
- skill progression
- relationship/family state
- detail/cadence transition
- participation control basis

## 33. Publication boundary

General View/Gateway向けpublicationはauthorized projectionとして必要stateを派生できるが、Resident private epistemic stateを標準で無制限公開することを本書は要求しない。

公開可能性とaccess policyはPhase 4/protocol設計で確定する。

View predictionはResident action/physical poseのauthorityではない。

## 34. Traceability

| Requirement | Coverage |
|---|---|
| Q008 | resident-level knowledge: awareness/understanding/usability分離 |
| Q009/Q010 | dynamic occupation input、needs/values/personality |
| Q011 | actual relation pairのみ保持 |
| Q019 | truth/perception/receipt/belief分離 |
| Q020/Q021 | lifecycle/health/family/generation |
| Q025/Q026 | resident-level culture/learning input boundary |
| Q032 | detailed daily behavior |
| Q035/Q036 | health/medical/infection individual state |
| Q044 | perception with occlusion/distance/light/hearing |
| Q045 | migration as resident decision + physical movement |
| Q055/Q056/Q059 | partnership/reputation input/memory |
| Q076〜Q082 | physiology/thermal/hygiene/injury/pregnancy/development/disability |
| Q083/Q190〜Q194/Q265 | identity retention vs update detail |
| Q100 | physical work requires actual action/access |
| Q128/Q129 | who knows; evidence/source/trust based belief |
| Q130〜Q139 | emotion/conversation/goal/routine/stress/skill/aptitude/license distinction/tacit skill |
| Q145〜Q148 | queue/event/emergency behaviors as actual resident actions |
| Q150/Q152〜Q154 | family/inheritance boundary/generation/name-culture inputs/burial consequence |
| Q160〜Q162 | employment/labor/safety resident state inputs |
| Q169/Q170/Q178/Q179 | time awareness/item search/risk perception/warning reception |
| Q260〜Q264 | Diver uses normal existing Resident; death/binding separation |
| Q266 | deterministic future Resident ID generation |

## 35. Phase 4 handoff

Phase 4で確定する事項:

- Resident state schema
- age/development/body-region model
- disease/injury/immunity model
- physiology integration algorithm
- perception query algorithm
- knowledge/belief representation
- memory formation/decay algorithm
- psychology state dimensions
- goal/action selection algorithm
- skill/aptitude numeric model
- relationship/family schema
- birth/identity creation context schema
- resident detail/cadence defaults
- publication/privacy projection schema

Phase 4はResident identity continuity、epistemic separation、normal-world-rule principleを変更してはならない。
