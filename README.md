# MachiVerse

MachiVerse は、C# で開発するエージェントベースの世界シミュレーターです。

目標は、単に多数の機能を並べるのではなく、世界を構成する状態・因果・相互作用・歴史的変化を組み合わせることで、世界そのものが動的に成立するシミュレーションを構築することです。

## 現在の状態

現在は設計・要件整理を中心に進めています。標準実装のソースコードはまだ配置していません。

標準構成では、1つのシミュレーションコアを最大16スレッドで並列実行し、同一World Seed・同一設定・同一操作から必ず同一結果を得られる決定論的再現性を維持します。

## 最上位コンポーネント

MachiVerse は以下の4コンポーネントを独立した実行・ビルド・配布単位として扱います。

- **Simulation Core**: 世界シミュレーションの実行と正本状態の保持
- **Gateway**: 外部接続、認証・認可、状態キャッシュ、操作集約、負荷分散
- **View**: 一般利用者向けの参照・参加・操作UI
- **Administration View**: システム運用者向けの監視・設定・運用UI

コンポーネント間ではコードや内部型を共有せず、設計されたプロトコルだけを通信契約として使用します。

## 設計原則

MachiVerse の世界シミュレーションでは、特に次の考え方を重視します。

- 「どうすれば狂気的なまでに世界をシミュレーションできるか」を判断軸とする
- ダイバーが「世界を構成する一人の住人」と感じられる体験を重視する
- 現在状態だけでなく、過去から現在、現在から未来へ続く歴史的因果を持たせる
- 世界を単一都市や都市だけで完結させない
- 人の営みが自然環境から強い影響を受ける世界として考える
- 詳細シミュレーション領域の外部環境からの影響も扱う
- 同一World Seed・同一設定・同一操作では必ず同一結果にする
- 調整可能な数値は外部Configから変更可能にする

詳細は [`docs/README.md`](docs/README.md) および [`docs/architecture/`](docs/architecture/) を参照してください。

## ドキュメント

- [設計ドキュメント一覧](docs/README.md)
- [全体アーキテクチャ](docs/architecture/overview.md)
- [世界シミュレーション設計](docs/architecture/world-simulation.md)
- [シミュレーションコア設計](docs/architecture/simulation-core.md)
- [ゲートウェイ設計](docs/architecture/gateway.md)
- [プロトコル設計方針](docs/protocols/README.md)

## 開発ブランチ

常設ブランチは以下です。

```text
main
  ↑
develop
  ↑
├─ simulation
├─ gateway
├─ view
└─ administration-view
```

通常の機能追加・修正は対象コンポーネントの常設ブランチから作業ブランチを切り、Pull Requestを通して統合します。

詳しい開発ルールは [`AGENTS.md`](AGENTS.md) と [`CONTRIBUTING.md`](CONTRIBUTING.md) を参照してください。

## コントリビューション

IssueやPull Requestを歓迎します。現在は設計段階のため、既存の確定事項と未確定事項を区別し、未承認の仕様を確定事項として実装しないようお願いします。

参加前に [`CONTRIBUTING.md`](CONTRIBUTING.md) を確認してください。

## ライセンス

このリポジトリは [Apache License 2.0](LICENSE) の下で公開されています。
