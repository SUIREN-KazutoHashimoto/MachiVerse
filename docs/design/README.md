# 詳細設計ドキュメント

`docs/design` は、MachiVerseの横断契約とcomponent/domain詳細設計をPhase単位で管理する。

## Phase 1: 共通基盤・契約

Status: Complete

- `phase1-common-foundation-contracts.md`
- `phase1-determinism-ordering-random.md`
- `phase1-config-contract.md`
- `phase1-protocol-envelope.md`
- `phase1-persistence-replay-recovery.md`
- `phase1-operation-lifecycle-retry-dedup.md`
- `phase1-cross-cutting-review.md`

Phase 1の最終状態と後続Phaseへの引き渡しは `phase1-cross-cutting-review.md` を正本とする。

## Phase 2: コンポーネント内部設計

Status: Complete

- `phase2-component-internal-design.md`
- `phase2-simulation-core-internal-design.md`
- `phase2-gateway-internal-design.md`
- `phase2-general-view-internal-design.md`
- `phase2-admin-view-internal-design.md`
- `phase2-cross-component-review.md`

Phase 2のcomponent間ownership、protocol mapping、Phase 3開始条件、completion判定は `phase2-cross-component-review.md` を正本とする。

## Phase 3: 世界シミュレーションDomain設計

Status: Complete

- `phase3-world-domain-design.md`
- `phase3-domain-common-contract.md`
- `phase3-spatial-domain-design.md`
- `phase3-environment-domain-design.md`
- `phase3-physical-built-domain-design.md`
- `phase3-resident-domain-design.md`
- `phase3-participation-domain-design.md`
- `phase3-society-economy-domain-design.md`
- `phase3-governance-security-domain-design.md`
- `phase3-infrastructure-information-domain-design.md`
- `phase3-cross-domain-causality.md`
- `phase3-traceability-cross-cutting-review.md`

Phase 3はIssue #15で管理し、Phase 1/2の契約を前提としてSimulation Core内のdomain state、event、更新依存、detail level、aggregation/promotion/demotion、cross-domain因果、Q001〜Q279 traceabilityを具体化した。

Phase 3全体の作業分解と共通方針は `phase3-world-domain-design.md`、全domainが従うstate ownership・event/intent・detail transitionの共通契約は `phase3-domain-common-contract.md`、Phase 3のcompletion判定とPhase 4への引き渡しは `phase3-traceability-cross-cutting-review.md` を正本とする。

## 読み方

1. `docs/requirements` の確定要件を最上位入力とする。
2. `docs/architecture` でcomponent/world領域の責務を確認する。
3. `docs/protocols` でcomponent間通信契約を確認する。
4. 本directoryのPhase文書でcross-cutting/internal detailを確認する。
5. 同一Phase内で古い未決定記述とfinal reviewが競合する場合、後続のfinal reviewを優先する。

未承認の実装技術は詳細設計文書から暗黙に固定せず、責務・意味論・安全境界とimplementation choiceを分離する。
