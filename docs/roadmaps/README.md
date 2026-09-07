# MachiVerse Component Roadmaps

このディレクトリは、確定済みの要件・Architecture・Protocol契約を、実装順序と完了条件へ落とし込むためのロードマップを管理します。

## 正本との関係

ロードマップは仕様の正本ではありません。

優先順位は次の通りです。

1. `docs/requirements/` — 確定要件・決定記録
2. `docs/architecture/` — component責務・横断意味論
3. `docs/protocols/` — component間通信契約
4. `docs/design/` — 実装可能な詳細設計
5. `docs/roadmaps/` — 上記から導出した実装順序・Phase完了条件
6. GitHub Issue — 実作業の追跡

ロードマップと上位文書が矛盾する場合はロードマップ側を更新します。Issueのみを根拠にArchitecture/Protocol仕様を新規確定しません。

## Phase設計原則

- Phaseは「機能テーマ」だけでなく、依存関係と検証可能なexit criteriaで分割する。
- read-only観測基盤をstate-changing managementより先に成立させる。
- security / permission / auditをUI表示だけへ依存させない。
- cross-component contractが必要な機能は、実装より先にProtocol/schemaを確定する。
- 各Phaseの完了は、対象Issueのchecklistだけでなく関連設計・test acceptanceとの整合で判定する。
- 後続Phaseで前Phaseの契約を破壊する場合は、先に設計文書とProtocol version/Capability compatibilityを更新する。

## Component Roadmaps

- [Administration View](administration-view.md)

Simulation Core / Gateway / General Viewについても同じ原則で現行設計から再構築する。
