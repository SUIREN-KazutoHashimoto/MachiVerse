# Gateway Phase 0 設計確定状況

Status: In Progress  
Tracking: Issue #36

## 1. 目的

本書は Gateway 開発ロードマップ Phase 0 の設計確定状況を管理し、`docs/architecture/` と `docs/protocols/` に残る未確定事項を実装着手前に収束させるための作業正本である。

Phase 0 では実装コードを変更しない。後続の詳細設計文書に存在する決定を参照する場合も、その内容が `docs/architecture/` または `docs/protocols/` の正本へ反映されるまでは、Phase 0 完了済みとは扱わない。

## 2. 適用原則

- Gateway は Simulation Core / General View / Admin View の実装コード、DLL、内部型、共有 DTO libraryへ直接依存しない。
- component 間契約は `docs/protocols/` を正本とする。
- world state の正本は Simulation Core であり、Gateway cache は非権威な派生状態とする。
- Gateway の調整可能な運用値は Gateway 外部 Config で所有する。
- standard protocol に addon 固有 functional payload を持ち込まない。
- retry、failover、reconnect、Gateway 数、Master identity、network arrival timing を world outcome の暗黙入力にしない。
- 後続詳細設計で既に具体化された事項を採用する場合、古い「未確定」記述を残したまま二重の正本を作らない。

## 3. Phase 0 完了項目監査

| # | 項目 | 現在の主な正本 | 現状 | Phase 0 で必要な収束 |
|---:|---|---|---|---|
| 1 | Core ↔ Gateway Protocol 詳細化 | `docs/protocols/core-gateway.md` | 主要意味論は確定、旧「component実装へ残す事項」に protocol-level 項目が残る | transport、serialization、Gateway logical identity、Master heartbeat message、FULL/DELTA publication、status query route を正本へ反映 |
| 2 | Gateway ↔ Gateway Protocol 詳細化 | `docs/protocols/gateway-gateway.md` | custody / retry / failover / deterministic merge は確定 | transport、serialization、Gateway logical identity、heartbeat/Master transition message、login/session handoff を正本へ反映 |
| 3 | Gateway ↔ General View Protocol 詳細化 | `docs/protocols/gateway-view.md` | role、publication continuity、Operation 基本契約は確定 | auth/session wire、role permission matrix、FULL/DELTA、resync status、binding/publication payload を正本へ反映 |
| 4 | Gateway ↔ Admin View Protocol 詳細化 | `docs/protocols/gateway-admin-view.md` | management category と責務分離は確定 | auth/session、permission、health/log/config/audit payload、component management routing を正本へ反映 |
| 5 | 認証・認可設計 | `docs/architecture/authentication-authorization-session.md` | 高位原則は確定するが具体技術が旧文書上未確定 | OIDC Authorization Code + PKCE、Gateway BFF、opaque session、General/Admin permission 分離、Master login finalization を正本化 |
| 6 | 要求集約・競合調停 | `docs/architecture/gateway-operation-delivery.md`, `docs/protocols/gateway-gateway.md` | stable Operation identity、Batch、custody、deterministic merge 原則は確定 | protocol-defined merge key / owner-domain field の参照境界を明確化し、arrival order 非依存を維持 |
| 7 | Core の Master 選出・変更通知を受けた安全な切替 | `docs/architecture/gateway-master-failover.md`, `docs/protocols/core-gateway.md` | MasterGeneration、stale generation、custody 継続は確定 | heartbeat / election / transition message、GatewayLogicalId、Master transition state machine の protocol 反映 |
| 8 | 参照 cache / publication buffer 同期 | `docs/architecture/gateway-cache-resynchronization.md`, `docs/protocols/core-gateway.md`, `docs/protocols/gateway-view.md` | continuity token、resync、非権威 cache 原則は確定 | FULL/DELTA/chunk、resync status、completion 条件、publication gate を正本へ反映 |
| 9 | 外部 Config 項目 | `docs/architecture/gateway.md`, `docs/architecture/configuration.md` | category は存在するが exact key/default/range は正本上不足 | Gateway schema 1.0 の stable key、型、既定値、範囲、mutability を正本へ反映 |
| 10 | addon を考慮した責務境界 | `docs/architecture/addon-boundary-safety.md`, `docs/architecture/protocol-compatibility-capability.md` | standard protocol と addon functional data の分離は確定 | Gateway Phase 0 では既存境界を維持し、addon framework 自体の未確定詳細を Gateway 実装 blocker にしないことを明記 |

## 4. 後続詳細設計から確認済みの決定候補

以下は既存の後続詳細設計で既に具体化または completion 判定されているため、Phase 0 では新規発明せず、整合性を確認して `docs/architecture/` / `docs/protocols/` へ反映する候補とする。

### 4.1 Protocol transport / serialization

`docs/design/phase4-protocol-completion-review.md` では次が P4-02 Complete と判定されている。

- Protocol Buffers proto3 を standard binary serialization とする。
- Core ↔ Gateway / Gateway ↔ Gateway は gRPC bidirectional streaming とする。
- Gateway ↔ General View / Gateway ↔ Admin View は TLS WebSocket binary message とする。
- standard envelope hard limit は 8 MiB。
- state publication は 1 MiB 以下の chunk に分割可能とする。
- `GatewayLogicalId` を Gateway logical identity とする。
- FULL / DELTA publication と resync message を formal schema 化する。

Phase 0 ではこれらを既存 Phase 1/2 契約と再照合し、矛盾がなければ protocol 正本へ反映する。

### 4.2 Auth / session

`docs/design/phase4-auth-session-protocol.md` では次が Complete とされている。

- OpenID Connect Core 1.0。
- OAuth 2.0 Authorization Code Grant + PKCE `S256`。
- Gateway を browser Backend-for-Frontend とし、OAuth access token / refresh token を browser JavaScript へ渡さない。
- browser session は Gateway 発行 opaque session cookie を使用する。
- login は connected Gateway が受け、Master Gateway が finalization authority を持つ。
- General View と Admin View は別 auth / permission domain とする。
- role / permission change は `session_generation` で stale admission を防止する。

Phase 0 では architecture/protocol の旧「具体方式未確定」記述をこの確定内容と整合させる。

### 4.3 Gateway Config

`docs/design/phase4-config-specification.md` では `config.gateway / 1.0` として、少なくとも次の群が具体化されている。

- network reconnect
- peer heartbeat
- aggregation window / count / size
- queue / admission capacity
- publication buffer / client backlog
- cache capacity / confirmed publication retention
- result / custody retention
- OIDC / BFF deployment
- auth/session lifetime
- observability

同文書は現時点で `In Progress` 表記であるため、Phase 0 では completion 状態と cross-document consistency を確認せずに一括採用しない。確定済み subset のみを正本へ移す。

## 5. Phase 0 の非目標

次は protocol / architecture の意味論を変更しない限り、Phase 0 で特定製品や物理実装まで固定する必要はない。

- Gateway durable queue / custody store の具体製品・data structure
- metrics / log backend 製品
- secret store / encryption key management 製品
- Core dedup index の物理 data structure
- addon framework の package / distribution / signing 実装
- View の interpolation / prediction rendering algorithm

ただし、これらが wire contract、durability guarantee、security boundary、Config ownership に影響する場合は、影響する契約部分だけ Phase 0 で先に確定する。

## 6. 完了条件

Phase 0 は次をすべて満たしたとき完了とする。

1. 本書3章の10項目がすべて `Resolved` となる。
2. 関連する `docs/architecture/` / `docs/protocols/` に、Phase 0 を妨げる「未確定」「今後決定が必要」「詳細設計へ残す事項」が残っていない。
3. 残す未決定事項は implementation choice または後続 domain-specific design と明示され、Gateway 基盤実装の追加仕様判断を要求しない。
4. 4 protocol の transport、serialization、identity、version/Capability、auth/session、Operation/custody、Master transition、state synchronization の境界が相互矛盾しない。
5. Gateway external Config の stable key と ownership が確定し、調整可能値を source code 固定する必要がない。
6. addon 対応余地を残しつつ、standard protocol / component independence / security / determinism を弱めない。

## 7. 次の作業順

1. P4-02 completion 内容を4 protocol正本へ反映する。
2. auth/session の具体契約を architecture と View/Admin/Peer protocolへ反映する。
3. Master heartbeat / transition と GatewayLogicalId を Core/Peer protocol間で整合させる。
4. FULL/DELTA/chunk/resync status を Core/View protocol間で整合させる。
5. Gateway Config schema の completion 状態を確認し、確定済み項目を正本へ反映する。
6. addon boundary と Phase 0 completion の横断レビューを行う。
