# 詳細設計 Phase 3: 世界シミュレーションDomain設計

Status: Complete  
Tracking: Issue #15  
Predecessors: `phase1-cross-cutting-review.md`, `phase2-cross-component-review.md`  
Completion review: `phase3-traceability-cross-cutting-review.md`

## 1. 目的

Q001〜Q279で確定した世界シミュレーション要件を、Simulation Core内のauthoritative domain state、event、更新責務、domain間因果、detail level制御へ落とし込む。

Phase 3ではPhase 1の共通契約とPhase 2のcomponent ownershipを再定義しない。個別domainはPhase 2で定義した`DomainRuntime` contractへ登録され、`WorldStateStore`への直接mutationではなくintent/eventと明示的なinvariantを通じて連携する。

本Phaseの成果はPhase 4でdata structure、schema、algorithmを確定できる粒度までsemantic contractを具体化することであり、実装class、thread scheduler、DB、serialization、物理storage layoutは固定しない。

## 2. 正本と設計優先順位

Phase 3では次を優先する。

1. `docs/requirements` の確定要件
2. Phase 1 final reviewとその参照先
3. Phase 2 final reviewとその参照先
4. `phase3-traceability-cross-cutting-review.md`
5. `phase3-cross-domain-causality.md`
6. `phase3-domain-common-contract.md`
7. 個別domain文書

個別domain文書の作業時点Statusや古い未決定記述がcompletion reviewと競合する場合、completion reviewを優先する。

## 3. Phase 3で維持する前提

- 世界状態のauthorityは単一Simulation Coreにある。
- 権威あるWorld Timeは固定`SimulationStep`である。
- 同一Seed・同一Config・同一有効Operation集合では論理結果を決定論化する。
- full 3Dは表示だけでなくauthoritative world modelへ適用する。
- 可変詳細度を許容するが、aggregation/promotion/demotionで世界の因果履歴や重要なstock/identity/obligationを捏造・消失させない。
- simulation-affecting数値閾値は外部Configを原則とする。
- Viewのcamera位置やrender loadを、そのままauthoritative simulation outcomeの暗黙入力にしない。
- Diverは既存の通常住人へbindingされ、Diver不在時も当該住人は世界内で継続して存在・行動する。

## 4. Domain分割

Phase 3の設計単位を次のlogical domain familyへ分ける。名称はsemantic ownershipを表し、実装module名を固定しない。

| Domain family | 主なauthoritative state | 主な責務 |
|---|---|---|
| `spatial` | 3D座標系、地形形状、空間partition、containment、領域形状 | 全domainが参照する空間基盤 |
| `environment` | 地下、気候、天候、水、海洋、生態系、自然資源、災害要因 | 自然環境の状態遷移と自然起因event |
| `physical_built` | 移動、占有、衝突、建物、室内、建設、損傷、物品位置 | 物理・建造環境と3D interaction |
| `resident` | 身体、健康、知覚、感情、記憶、目標、技能、行動、家族、世代 | 住人の内部状態と行動決定 |
| `society_economy` | 組織、雇用、市場、物流契約、金融、契約、教育、文化、評判 | 社会・経済関係と交換 |
| `governance_security` | 統治、法律、税、公共権限、外交、軍事、治安、制度上の領域 | 制度・政治・治安の権威状態 |
| `infrastructure_information` | 交通網、水、電力、通信、メディア、記録、施設capacity | network/service供給と情報伝播基盤 |
| `participation` | Diver-resident binding、absence behavior policyのeffective state/history | 世界内参加状態のCore側authority |

`participation`はsession/auth/exclusive control admissionを所有しない。それらはPhase 2どおりGateway責務である。

## 5. Domain間依存の原則

Domain間関係は、すべて次のいずれかとして明示する。

- `state_read(S)`: finalized `State(S)`のread-only参照
- `same_step_dependency`: 同一Step内で前phaseの確定intermediate resultを必要とする依存
- `event`: source domainで成立したimmutable factの通知
- `intent`: target domain ownerへmutation候補を要求
- `shared_invariant`: 複数domainに跨るcommit前整合性条件
- `aggregate_exchange`: detail level境界・外部簡略領域とのstock/flow交換

単なる`state_read(S)`は同一Stepの実行DAG edgeを意味しない。循環依存は原則として`State(S)`参照と`State(S+1)`へのintent/eventへ分解し、暗黙の相互即時mutationを作らない。

## 6. Phase 3作業分解と成果物

### P3-01 Domain共通契約・detail framework — Complete

成果物: `phase3-domain-common-contract.md`

- DomainDefinitionのPhase 3拡張
- state ownership、event/intent、dependency種別
- detail level共通語彙
- promotion/demotionの共通invariant
- aggregate/external exchange contract

### P3-02 空間・自然環境 — Complete

成果物:

- `phase3-spatial-domain-design.md`
- `phase3-environment-domain-design.md`

### P3-03 物理・建造環境 — Complete

成果物: `phase3-physical-built-domain-design.md`

### P3-04 住人・参加 — Complete

成果物:

- `phase3-resident-domain-design.md`
- `phase3-participation-domain-design.md`

### P3-05 社会・経済 — Complete

成果物: `phase3-society-economy-domain-design.md`

### P3-06 政治・制度・治安 — Complete

成果物: `phase3-governance-security-domain-design.md`

### P3-07 インフラ・情報 — Complete

成果物: `phase3-infrastructure-information-domain-design.md`

### P3-08 Cross-domain因果・aggregation統合 — Complete

成果物: `phase3-cross-domain-causality.md`

- domain間因果連携表
- shared invariant
- cross-domain event/intent flow
- semantic transaction
- detail promotion/demotionの連鎖
- 外部簡略領域との交換

### P3-09 Traceability・横断整合性review — Complete

成果物: `phase3-traceability-cross-cutting-review.md`

- Q001〜Q279の欠番なしtraceability
- ownership重複監査
- dependency cycle監査
- Phase 1/2 compatibility review
- Phase 4 handoff
- unresolved domain-level blocker 0件確認

## 7. Domain設計書の必須項目

各個別domain文書は最低限、次を持つ。

1. responsibility / non-responsibility
2. authoritative state model
3. identityとlifecycle
4. input Operation / Config / read dependency
5. emitted event / intent / output projection
6. Step内update phaseと更新責務
7. same-Step dependency
8. conflict scopeとdeterministic merge要件
9. detail level別保持state
10. update frequency/cadence policy
11. promotion/demotion条件
12. conserved stock / identity / obligation invariant
13. cross-domain causal links
14. persistence/replay上の意味
15. Q001〜Q279 traceability
16. Phase 4へ引き渡すdata/schema/algorithm未確定事項

## 8. Detail level共通方針

Phase 3では全domain共通のlogical detail levelを次の4段階として扱う。各domainが同じ内部表現を使うことは要求しない。

| level | 意味 | 保持の原則 |
|---|---|---|
| `D0_ENTITY` | entity/局所状態を標準domain modelの粒度で更新 | causal interactionに必要な個体・3D状態を保持 |
| `D1_LOCAL_AGGREGATE` | 局所cell/cohort/network segment単位へ集約 | topology、capacity、主要identity、stock/flowを保持 |
| `D2_REGIONAL_AGGREGATE` | 地域単位のstock/flow/rate中心 | 長期傾向と境界交換に必要な状態を保持 |
| `D3_BOUNDARY_SUMMARY` | 外部簡略領域または低関与領域の境界summary | 3D領域geometryと境界stock/flow、履歴anchorを保持 |

`D0_ENTITY`は最高精度物理を意味しない。標準要件で要求するentity-resolved domain modelの詳細度を意味する。

Detail transitionはdeterministicであり、原則として`State(S)`、scheduled input、effective Configから判定する。promotion/demotion閾値、hysteresis、最低滞在Step等の調整数値はConfigへ置く。

## 9. Promotion / Demotionの共通invariant

- persistent identity-bearing entityをdetail低下だけで消去・再採番しない。
- 人口、重要資源、在庫、資金、エネルギー等のdomain-defined conserved stockを不自然に生成・消滅させない。
- 所有権、契約、債務、家族関係、法的拘束、Diver binding等のobligation/referenceをdetail低下で失わない。
- promotionは同一aggregate stateから同一詳細stateを再構成できるdeterministic recipeを持つ。
- 過去に個体として確定したidentity-bearing entityは再promotion時も同一identityを維持する。
- aggregate-only population等から新規個体をmaterializeする場合は、WorldSeedとstable lineage/contextからidentity/stateを決定論的に導出する。
- demotion直前に未解決の局所interaction、進行中Operation、shared invariant violationがある場合はdemotionを拒否または延期する。
- Diver-bound residentとその直近interaction範囲にはdomain-specific detail floorを設ける。標準floorの具体値は個別domainで定義し、調整値はConfig化する。

## 10. 外部簡略領域との交換

`D3_BOUNDARY_SUMMARY`との交換は、直接remote mutable stateを参照せず、Step basisを持つ境界contractとして扱う。

最低限、次を表現可能にする。

- 人・物品・車両等の移動flow
- 水・エネルギー・資源等のphysical flow
- 商品・資金・契約上のeconomic flow
- 情報・通信のdelivery flow
- 気象・水系・海洋等のenvironmental boundary condition
- 行政・領域・治安等のinstitutional boundary fact

境界を跨ぐidentity-bearing entityはhandoff中に複製・消失させず、source/target ownership transferを明示する。

## 11. Traceability運用

Phase 3 completion reviewではQ001〜Q279を欠番なくcoverage分類し、各要件をowning/supporting domainまたはPhase 1/2 cross-cutting contractへ対応付ける。

同一semantic ownership/coverageを持つ連続要件はrangeとして記載できるが、range間に未分類Qを残してはならない。

必要に応じPhase 4でschema/algorithm単位のtraceabilityを1件単位へ細分化する。

coverageは次を区別する。

- `domain_covered`
- `cross_cutting`
- `foundation_or_component_contract`
- `phase4_algorithm_detail`

`phase4_algorithm_detail`はPhase 3 semantic contractが定義済みである場合に限りblockerとしない。

## 12. Phase 4へ持ち越してよい事項

次はsemantic contractを変えない限りPhase 4以降でよい。

- concrete struct/class layout
- exact serialization schema
- DB/file partition
- spatial index data structure
- scheduler/threading primitive
- exact numerical solver
- exact compression/LOD storage encoding
- concrete queue capacity値

ただし、これらを後回しにするためにDomain ownership、event意味論、conservation、detail transitionの意味を曖昧にしてはならない。

## 13. Completion state

P3-01〜P3-09を完了した。

確定事項:

- 8 domain familyのauthoritative ownership
- domain state/event/intent/update phase/conflict/invariant
- D0〜D3 detail framework、cadence、promotion/demotion
- domain間因果DAGとstable domain rank
- mining、construction、birth/death、market delivery、information、justice、disaster、medical、military等のcross-domain semantic transaction
- identity/stock/obligation/flow/provenance conservation class
- Q001〜Q279 traceability
- Phase 1/2とのcompatibility
- Phase 4 handoff boundary

`phase3-traceability-cross-cutting-review.md`でunresolved domain-level blocker 0件を確認し、Phase 3をCompleteと判定した。
