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

## Phase 4: 実装直前設計

Status: Complete

### 全体 / P4-01 Data structure

- `phase4-implementation-ready-design.md`
- `phase4-core-data-structures.md`
- `phase4-domain-state-registry.md`

### P4-02 Protocol

- `phase4-protocol-schema.md`
- `phase4-auth-session-protocol.md`
- `phase4-protocol-payload-catalog.md`
- `phase4-protocol-completion-review.md`

### P4-03 Config

- `phase4-config-specification.md`
- `phase4-config-addendum.md`
- `phase4-config-standard-examples.md`
- `phase4-config-completion-review.md`

### P4-04 Persistence

- `phase4-persistence-specification.md`
- `phase4-persistence-record-catalog.md`
- `phase4-persistence-completion-review.md`

### P4-05 Algorithm / domain schema

- `phase4-algorithm-determinism.md`
- `phase4-domain-payload-schema.md`
- `phase4-domain-operation-event-intent-catalog.md`
- `phase4-algorithm-completion-review.md`

### P4-06 Performance

- `phase4-performance-budget.md`
- `phase4-performance-benchmark-profile.md`
- `phase4-performance-completion-review.md`

### P4-07 Observability / audit

- `phase4-observability-audit.md`
- `phase4-observability-completion-review.md`

### P4-08 Test / acceptance

- `phase4-test-acceptance.md`

### P4-09 Platform / implementation breakdown / completion

- `phase4-platform-runtime-profile.md`
- `phase4-implementation-work-breakdown.md`
- `phase4-completion-review.md`

Phase 4はIssue #16で管理し、Phase 1〜3の意味契約を、実装者が追加のarchitecture判断をほぼ必要としないdata structure / protocol / Config / persistence / algorithm / performance / observability / test / platform / implementation work packageへ具体化した。

Phase 4全体方針とP4-01〜P4-09状態は `phase4-implementation-ready-design.md`、Phase 4 completion判定と実装移行条件は `phase4-completion-review.md` を正本とする。

主要な確定値:

- 8 domain / 97 authoritative partition
- Config schema 4 component / 136 standard fields
- OperationKind 69 / EventKind 129 / IntentKind 63 / CrossDomainTransactionKind 17
- Protocol Buffers + gRPC / binary WebSocket
- SQLite WAL/FULL + 103 required Snapshot sections
- fixed-point/integer deterministic algorithm profile
- 30Hz reference performance profile
- OpenTelemetry-compatible observability + append-only audit
- P4-08 release acceptance suite
- .NET 10 LTS / C# 14 / Blazor WebAssembly / Three.js WebGLRenderer profile
- 38 implementation work packages / dependency DAG

Unresolved detailed-design blocker: 0件。

## 読み方

1. `docs/requirements` の確定要件を最上位入力とする。
2. `docs/architecture` でcomponent/world領域の責務を確認する。
3. `docs/protocols` でcomponent間通信契約を確認する。
4. 本directoryのPhase文書でcross-cutting/internal detailを確認する。
5. 同一Phase内で古い未決定記述とcompletion/final reviewが競合する場合、completion/final reviewを優先する。

実装時にPhase 4契約の変更が必要になった場合、implementation内でsilent変更せずdesign amendment、schema/version、compatibility/migration、P4-08 testを合わせて更新する。