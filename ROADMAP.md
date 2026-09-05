# Gateway ROADMAP

## 目的

Gateway コンポーネントの開発ロードマップを管理します。

詳細仕様の正本は `docs/architecture` および `docs/protocols` の設計文書です。ロードマップだけを根拠に未確定仕様を確定しません。

## 運用方針

- Gateway に関する項目のみを管理する。
- 設計変更が必要な場合は実装より先に設計文書・Protocol契約を更新する。
- Simulation Core、View、Administration View のコードへ直接依存しない。
- 将来的なアドオン対応を見据えるが、標準実装を不要に複雑化しない。
- 調整可能な数値は外部Configで管理する。

## 現在のフェーズ

設計・要件定義段階。

## Roadmap

### Phase 0: 設計確定

- [ ] Core↔Gateway Protocolを詳細化する
- [ ] Gateway↔Gateway Protocolを詳細化する
- [ ] Gateway↔View Protocolを詳細化する
- [ ] Gateway↔Administration View Protocolを詳細化する
- [ ] 認証・認可方式を設計する
- [ ] 要求集約・競合調停方式を設計する
- [ ] Master Gatewayの選出・障害時再選出方式を設計する
- [ ] 参照キャッシュと公開遅延バッファの同期方式を設計する
- [ ] 外部Config項目を定義する
- [ ] アドオン拡張を考慮した責務境界を設計する

### Phase 1以降

具体的な実装段階と順序は、Phase 0 の設計が確定した後に定義します。

## 完了済み

現時点では実装フェーズ未開始です。
