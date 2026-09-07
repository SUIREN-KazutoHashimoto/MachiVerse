# Administration View Implementation Roadmap

Status: Implementation Ready  
Work IDs: `ADMIN-01..ADMIN-04`  
Base branch: `administration-view`  
Canonical breakdown: `docs/design/phase4-implementation-work-breakdown.md`

## 1. 目的

Administration View の実装を、確定済みGateway management protocol、permission、Config、health/log/audit、high-impact operation契約に従って進める。

旧 Phase 0 checklist の管理Protocol、ログ/ステータス、Config、運用コマンド、監査・安全性は詳細設計で実装可能レベルまで確定済みである。Addon store/trust/install等で標準実装範囲外または将来frameworkへ残る事項は、未確定だから標準Admin実装全体を停止するblockerとは扱わない。

## 2. Work Package

| ID | Stage | Scope | Main dependencies |
|---|---|---|---|
| `ADMIN-01` | A | Admin View scaffold / Gateway protocol client | QA-01 protocol fixture |
| `ADMIN-02` | B | Health / metrics / log / audit UI | ADMIN-01, GW-07 fixture |
| `ADMIN-03` | B | Config / operational command management | ADMIN-01, GW-05/GW-06 fixture |
| `ADMIN-04` | C | High-impact / simulation Admin Operation | ADMIN-03 |

## 3. Critical path

```text
ADMIN-01 -> ADMIN-02
         └-> ADMIN-03 -> ADMIN-04
```

`ADMIN-02` と `ADMIN-03` は Gateway production implementationを待たず、確定済みprotocol fixtureを使って並列化できる。

## 4. Implementation gates

### Client foundation gate

`ADMIN-01` 完了時:

- standalone Admin Web application shell
- binary WebSocket/protobuf management client
- session/protocol lifecycle
- Gateway mock contract test

### Observability gate

`ADMIN-02` 完了時:

- component target catalog
- metrics/status dashboard
- structured log query
- audit query/export presentation

### Management gate

`ADMIN-03` 完了時:

- Config projection/editor
- expected ConfigGenerationによるstale update防止
- operational command catalog
- stable request/result tracking
- generic undoではなくexplicit new operationで変更

### High-impact gate

`ADMIN-04` 完了時:

- high-impact prepare/confirm/commit flow
- simulation-affecting Admin Operationのauthoritative scheduling
- permission/audit correlation
- revoke/failure/retry stateを安全に表示

## 5. Permission boundary

Administration View は General View の Administrator role と別の system-operation permission domain とする。

- General View roleからAdmin permissionへautomatic promotionしない
- GatewayがAdmin authentication/authorizationを所有する
- CoreはUI Admin roleを解釈せずworld-state invariantを維持する
- high-impact operationはconfirmation/audit contractを迂回しない

## 6. Addon関連の扱い

Addon管理のうち、標準Protocolで扱うのはcompatibility/trust/inventory/management metadataと、標準管理機能として確定した範囲に限定する。

addon固有のcross-component functional payloadをstandard protocolのgeneric extension slotへ流さない。将来のaddon framework詳細が未確定でも、`ADMIN-01..ADMIN-04` の標準実装を一律blockしない。

## 7. Non-negotiable acceptance

- Simulation Coreへ直接接続しない
- permission不足操作をGatewayがforwardしない
- stale ConfigGenerationをsilent overwriteしない
- audit対象操作をauditなしでsuccess扱いしない
- credential/private contentをlog/audit presentationで不必要に露出しない
- high-impact confirmationを単なるUI-only decorationにしない

## 8. Issue tracking

Component roadmap Issue は #38 を利用する。

#38 は旧Phase 0の設計待ちIssueではなく、次を追跡する親Issueへ更新する。

- Architecture/Protocol/management baseline normalization
- `ADMIN-01..ADMIN-04` implementation package progress
- Admin-owned design amendmentの依存再評価

各 `ADMIN-xx` 実装は原則独立Issueとして起票し、#38へ紐付ける。
