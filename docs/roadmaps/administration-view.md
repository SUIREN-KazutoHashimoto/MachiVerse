# Administration View Roadmap

Status: Reconstructed from current design contracts  
Implementation target branch: `administration-view`

## 1. Purpose

Administration View is the system-operator UI for MachiVerse. It is a separate security and responsibility domain from General View, and it communicates through Gateway rather than directly manipulating Simulation Core or other component internals.

This roadmap is derived from the current canonical requirements, architecture, protocol and detailed-design documents. It does not define new product semantics by itself.

Primary sources:

- `docs/architecture/admin-view.md`
- `docs/architecture/admin-operation-safety.md`
- `docs/architecture/addon-boundary-safety.md`
- `docs/architecture/authentication-authorization-session.md`
- `docs/architecture/configuration.md`
- `docs/architecture/config-semantics.md`
- `docs/protocols/gateway-admin-view.md`
- `docs/protocols/schema/`
- relevant `docs/design/phase4-*` implementation-ready contracts

## 2. Phase index

| Phase | Goal | Tracking Issue | Depends on |
|---|---|---:|---|
| 0 | Architecture / Protocol Contract Complete | #38 | current design baseline |
| 1 | Secure Administration Foundation | #62 | Phase 0 |
| 2 | Observability | #63 | Phase 1 |
| 3 | Config Management | #64 | Phase 2 |
| 4 | Operational Control | #65 | Phase 3 |
| 5 | Safety / Audit | #66 | Phase 4 |
| 6 | Addon Management | #67 | Phase 5 |
| 7 | Production Hardening | #68 | Phases 1–6 |

The implementation order is intentionally:

```text
contract
  -> secure foundation
  -> observe
  -> configure
  -> operate
  -> protect/audit
  -> manage addons
  -> production hardening
```

Read-only observability is completed before broad state-changing management. High-impact operation safety is not treated as a cosmetic UI enhancement; it is a distinct protocol and authorization boundary.

## Phase 0 — Architecture / Protocol Contract Complete

Tracking: Issue #38

### Scope

- Gateway ↔ Administration View protocol contract
- permission token / operation authorization matrix
- component health/status and metric requirements
- structured log query/display requirements
- Config read/change semantics and safety boundaries
- operational-command registry requirements
- simulation Admin Operation boundary
- high-impact operation confirmation semantics
- audit requirements
- addon inventory/catalog/install/update/disable/remove model
- official addon store relationship
- official addon integrity/publisher verification
- third-party addon trust distinction and explicit-risk UX
- cross-document and protobuf/message-registry consistency

### Exit criteria

- `admin-view.md`, `admin-operation-safety.md`, `addon-boundary-safety.md` and related architecture documents contain no contradictory unresolved Administration View semantics.
- `gateway-admin-view.md` is implementation-ready for all Phase 1 prerequisites.
- every standard Phase 0 management message resolves to a canonical protobuf payload/message registry entry or is explicitly deferred to a later capability/version.
- permission decisions are enforceable by Gateway and are not UI-only.
- high-impact actions have an explicit non-replayable confirmation/commit boundary distinct from OperationId.
- addon trust classification and installation state transitions are defined without introducing addon functional payload into the standard protocol.
- cross-document consistency review reports no Phase 1 blocker.

## Phase 1 — Secure Administration Foundation

Tracking: Issue #62  
Depends on: Phase 0

### Scope

- Administration View application shell and routing
- Gateway connection lifecycle
- TLS WebSocket transport and protocol handshake
- ProtocolVersion / Capability negotiation
- Admin-specific login/session attachment
- session-generation and privilege-revocation handling
- permission-aware navigation/action availability
- common ResultCode / RetryAdvice / diagnostic presentation
- reconnect and session reattachment baseline

### Exit criteria

- an operator can authenticate through Gateway and establish an Administration View session without General View role reuse.
- required Capability mismatch fails explicitly.
- privilege revoke/change is reflected without requiring unsafe stale-session operation execution.
- no management action is authorized solely because a control is visible or enabled in the UI.
- connection, auth, protocol and permission errors have stable machine-code-driven UI handling.

## Phase 2 — Observability

Tracking: Issue #63  
Depends on: Phase 1

### Scope

- component inventory/reachability display
- health/status dashboard
- CPU/memory/connection and architecture-specific metrics
- Simulation Step / lag display
- Master Gateway identity/generation and resync state
- protocol/Capability mismatch diagnostics
- Config validation state
- save/recovery status
- structured-log search, paging and correlation
- OperationId / BatchId / CorrelationId / SimulationStep context navigation

### Exit criteria

- operators can diagnose all four standard components without direct component-internal access.
- health conditions and metrics use stable names/schema and bounded label cardinality.
- log queries support bounded pagination and do not expose credentials/secrets.
- observability remains usable during simulation pause and for degraded/resyncing Gateway states where protocol semantics permit.

## Phase 3 — Config Management

Tracking: Issue #64  
Depends on: Phase 2

### Scope

- Config current-effective-value display
- impact/mutability/sensitivity classification
- ConfigGeneration and digest presentation
- validation and preflight UI
- atomic change-set submission
- expected-base-generation concurrency guard
- runtime-mutable / restart-required / world-regeneration-required distinction
- simulation-affecting safe-step application status
- Config change history/audit linking
- explicit revert-as-new-change workflow

### Exit criteria

- Administration View never directly edits another component's Config file.
- stale ConfigGeneration is rejected explicitly rather than silently overwritten.
- invalid change sets are never partially applied.
- secrets remain non-readable by default even when a setting is administratively changeable.
- simulation-affecting change results expose the authoritative effective boundary/Step.

## Phase 4 — Operational Control

Tracking: Issue #65  
Depends on: Phase 3

### Scope

- standard operational-command registry UI
- command parameter schema/validation
- operation submission/status/result lifecycle
- idempotency and retry-safe UX
- timeout and temporarily-unavailable handling
- simulation Admin Operations through Gateway
- Pause-aware scheduling
- candidate/effective Simulation Step presentation
- deterministic conflict/late-result handling where simulation-affecting

### Exit criteria

- every state-changing command has a stable OperationId and immutable request identity where required.
- ACK/accepted states are visually and semantically distinct from terminal success.
- simulation-affecting Admin Operations never bypass Core world-state invariants.
- arrival timing and UI processing speed are not treated as authoritative world ordering.

## Phase 5 — Safety / Audit

Tracking: Issue #66  
Depends on: Phase 4

### Scope

- high-impact operation classification
- prepare → confirm → commit workflow
- expiry and single-use confirmation challenge
- optional approval-policy hooks where contract permits
- audit-query UI
- actor/target/request/result/effective-boundary trace
- permission-token granularity review
- dangerous-operation UX and explicit target confirmation
- no-generic-Undo behavior and compensating-operation navigation

### Exit criteria

- high-impact actions cannot be committed through the ordinary single-step action path.
- confirmation artifacts cannot substitute for OperationId and cannot be replayed after expiry/use.
- all accepted/rejected high-impact operations are auditable.
- audit records cannot be modified or hidden through a generic Undo UI.
- privilege revocation prevents new privileged actions immediately according to the session contract.

## Phase 6 — Addon Management

Tracking: Issue #67  
Depends on: Phase 5

### Scope

- addon inventory and dependency view
- official catalog/store metadata view
- package staging/download workflow
- integrity and publisher verification result display
- compatibility / required-provided Capability checks
- install/update/disable/remove operations
- restart/safe-boundary requirement display
- persistent-world/save impact checks
- official vs third-party trust presentation
- explicit third-party risk acknowledgement
- failure/recovery diagnostics

### Exit criteria

- official addons are cryptographically verified according to the canonical trust contract before activation.
- third-party addons are never visually or semantically represented as official/verified when that trust proof is absent.
- addon configuration/dependency incompatibility cannot silently degrade into normal component startup.
- install/update/remove are explicit audited operations with deterministic component-owned validation.
- standard protocol still carries only addon management/compatibility metadata, not addon-specific functional payload.

## Phase 7 — Production Hardening

Tracking: Issue #68  
Depends on: Phases 1–6

### Scope

- reconnect/resume and Gateway failover E2E tests
- Master switch during Administration View activity
- partial-failure and retry tests
- long-running log/metric load behavior
- management request rate/capacity limits
- security testing for authz, CSRF-equivalent flows, replay and stale confirmation artifacts
- audit retention/rotation operational validation
- browser compatibility/accessibility baseline
- observability for Administration View itself
- deployment/runbook/recovery documentation
- acceptance-test closure

### Exit criteria

- all Administration View acceptance tests pass against supported Gateway/Protocol versions.
- no known authorization bypass, unsafe replay, stale-generation overwrite or high-impact confirmation bypass remains.
- expected degraded/failover states have documented operator-visible behavior.
- performance remains within configured operational budgets under representative management/log/metric load.
- deployment, rollback and recovery procedures are documented and tested.

## 3. Dependency rules

- Phase 1 may start only after Phase 0 has no unresolved contract blocker required by its scope.
- Phase 2 can overlap late Phase 1 implementation only after auth/session and health/log wire contracts are stable.
- Phase 3 must not invent Config semantics beyond the canonical Config contracts.
- Phase 4 must not use operational commands as a generic escape hatch around explicit Protocol message/contracts.
- Phase 5 safety boundaries apply retroactively to any Phase 3/4 operation classified as high-impact.
- Phase 6 may reuse Phase 4/5 operation infrastructure, but addon management remains separately permissioned and audited.
- Phase 7 can begin incrementally earlier, but the phase is complete only after Phases 1–6 are functionally complete.

## 4. Issue management policy

- one tracking Issue per Phase.
- implementation subtasks may be split into child Issues when a Phase becomes active.
- Phase tracking Issue checklist is derived from this roadmap and canonical design documents.
- roadmap changes caused by new requirements must update the relevant design/Protocol documents first when semantics change.
- completed Phase Issues remain historical execution records; this roadmap is the current ordering/index.

## 5. Out of scope for Administration View roadmap

- General View Administrator role implementation
- Simulation Core world-rule implementation
- direct access to Core mutable state
- direct editing of another component's Config file
- addon-specific business/function protocol implementation
- arbitrary generic command/data extension channel
