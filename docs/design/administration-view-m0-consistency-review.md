# Administration View M0 Baseline Consistency Review

Status: PASS  
Tracking: Issue #38  
Roadmap: `/ROADMAP.md`, `docs/roadmap/administration-view.md`

## 1. 目的

詳細設計Phase 4完了後のM0 Contract Baseline Consolidationとして、Administration Viewの旧Architecture/Protocol文書に残るstale TBDを、確定済みPhase 2〜4 designとimplementation roadmapへ同期したことを確認する。

本reviewは新しいProtocolやpermissionを設計しない。正本はPhase 4 design/schemaである。

## 2. Reviewed sources

- `docs/design/phase2-admin-view-internal-design.md`
- `docs/design/phase4-auth-session-protocol.md`
- `docs/design/phase4-protocol-payload-catalog.md`
- `docs/design/phase4-implementation-work-breakdown.md`
- `docs/design/phase4-test-acceptance.md`
- `docs/protocols/schema/payloads.proto`
- `docs/protocols/schema/message-registry-v1.md`
- `docs/roadmap/administration-view.md`

Normalized documents:

- `docs/architecture/admin-view.md`
- `docs/architecture/admin-operation-safety.md`
- `docs/architecture/addon-boundary-safety.md`
- `docs/protocols/gateway-admin-view.md`

## 3. Result

M0 Administration View baseline: **PASS**

`ADMIN-01`開始を妨げるstale Architecture/Protocol blocker: **0**

| Area | Result | Baseline |
|---|---|---|
| Runtime | PASS | standalone Blazor WebAssembly / `net10.0` |
| External boundary | PASS | Admin View connects only to Gateway |
| Auth profile | PASS | OIDC + Authorization Code + PKCE S256 + Gateway BFF |
| Auth domain separation | PASS | General View Administrator != Admin View permission |
| Permission registry | PASS | Phase 4 `admin.*` permission set only |
| WebSocket | PASS | TLS binary WebSocket `/ws/v1/admin` |
| Serialization | PASS | Protocol Buffers / canonical schema |
| Message registry | PASS | existing Phase 4 `mv.gateway-admin-view` rows only |
| Capability registry | PASS | Phase 4 capability names only |
| Health/log | PASS | ADMIN-02 scope, canonical health/log payloads |
| Config/command | PASS | ADMIN-03 scope, canonical Config/OperationalCommand payloads |
| Simulation Admin Operation | PASS | `operation.submit/result`, Gateway authz + Core invariant |
| High-impact confirmation | PASS | ADMIN-04 implementation boundary; token is not OperationId |
| Audit | PASS | Admin local cache is not audit authority |
| Addon | PASS | future extension boundary, not `ADMIN-01..04` standard implementation scope |

## 4. Schema / registry check

M0 normalization does **not** modify:

- `docs/protocols/schema/common.proto`
- `docs/protocols/schema/auth.proto`
- `docs/protocols/schema/payloads.proto`
- `docs/protocols/schema/message-registry-v1.md`

No new `admin.action.*` message family, Addon management message, permission token, or Capability token is introduced by M0.

If implementation requires a new wire contract, design amendment, schema, registry, Capability and acceptance fixture updates must precede implementation.

## 5. Canonical Admin permission set

```text
admin.health.read
admin.metrics.read
admin.log.read
admin.config.read
admin.config.write.operational
admin.config.write.presentation
admin.config.write.simulation
admin.command.execute.low-impact
admin.command.execute.high-impact
admin.operation.submit
admin.audit.read
admin.session.read
admin.security.revoke-session
```

The normalized architecture/protocol documents no longer define an alternate permission namespace.

## 6. Canonical current implementation scope

```text
ADMIN-01 Admin View scaffold / Gateway protocol client
ADMIN-02 Health / metrics / log / audit UI
ADMIN-03 Config / operational command management
ADMIN-04 High-impact / simulation Admin Operation
```

Addon install/update/disable/remove is not part of the current standard Administration View work package set.

## 7. High-impact boundary

Phase 4 fixes the following invariant without introducing a dedicated Standard Protocol message family:

- high-impact actions require additional confirmation and audit;
- high-impact command authorization uses the canonical high-impact permission;
- simulation Admin Operation remains Gateway-authorized and Core-validated;
- confirmation state/token is not OperationId or an authorization credential;
- confirmation expiry requires a new confirmation;
- ACK/accepted is not terminal effect success.

Exact confirmation UX/evidence transport is an `ADMIN-04` implementation decision unless it requires a wire-contract change; any wire change requires prior design amendment.

## 8. Addon boundary

Q255〜Q259 architecture remains valid, but Phase 4 work breakdown intentionally has no standard Addon management implementation package.

Therefore M0 retains only the future extension boundary and startup/compatibility safety principles. It does not invent official store/signature/hash/package/install contracts.

## 9. Conclusion

Administration View stale Architecture/Protocol wording is normalized to the completed Phase 4 design and the current `ADMIN-01..ADMIN-04` roadmap.

The next standard work item is `ADMIN-01`, subject to its `QA-01` protocol-fixture dependency defined by the implementation work breakdown.
