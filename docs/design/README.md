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

## 読み方

1. `docs/requirements` の確定要件を最上位入力とする。
2. `docs/architecture` でcomponent/world領域の責務を確認する。
3. `docs/protocols` でcomponent間通信契約を確認する。
4. 本directoryのPhase文書でcross-cutting/internal detailを確認する。
5. 同一Phase内で古い未決定記述とfinal reviewが競合する場合、後続のfinal reviewを優先する。

未承認の実装技術は詳細設計文書から暗黙に固定せず、責務・意味論・安全境界とimplementation choiceを分離する。
