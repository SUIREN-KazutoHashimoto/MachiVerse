# 詳細設計 Phase 4: Implementation Work Breakdown

Status: Complete / P4-09 work breakdown  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`  
Acceptance: `phase4-test-acceptance.md`

## 1. 目的

Phase 4成果物をproduction implementation Issueへ直接起票可能なwork packageへ分解し、target component branch、依存関係、parallelization、主要acceptance TestCaseIdを固定する。

本書はIssue番号を固定しない。実際のGitHub Issue作成時に`ImplementationWorkId`をtitle/bodyへ保持する。

## 2. ImplementationWorkId

```text
SIM-01..SIM-15
GW-01..GW-07
VIEW-01..VIEW-05
ADMIN-01..ADMIN-04
QA-01..QA-04
INT-01..INT-03
```

Total standard work package: **38**。

## 3. Branch ownership

Repository常設branchに従う。

| component | base branch |
|---|---|
| Simulation Core | `simulation` |
| Gateway | `gateway` |
| General View | `view` |
| Administration View | `administration-view` |
| cross-component integration/release | `develop`へ各component PR統合後 |

各実装Issueは原則target component branchからfeature branchを切る。

Cross-component code/DTO libraryを共有するための独立shared runtime projectは作らない。

## 4. Contract source rule

Protocol/Config/Persistence schemaの契約正本はPhase4 document/schema sourceであり、component generated/runtime typeではない。

各componentは同じprotocol schema sourceから自身のlocal generated codeを生成可能だが、別component build artifact/DLLへ依存しない。

## 5. Stage 0 — test/schema foundation

### QA-01 Contract fixtures / schema golden vectors

**Suggested Issue title:** `実装: Contract schema fixtures / golden vectors`  
Target: repository/tooling + component-local test projects  
Dependencies: none

Scope:

- stable token/ID/hash/DCBOR vectors
- protobuf fixtures
- Config examples validation
- persistence fixture generator
- TestCaseId registry

Acceptance:

```text
schema.*
determinism.dcbor.vector
determinism.domain-hash.vector
```

### SIM-01 Core project scaffold / deterministic primitives

Suggested title: `実装: Simulation Core基盤と決定論primitive`

Scope:

- Core executable/test project
- checked integer/fixed-point types
- Id/Hash/StableToken
- MV-DCBOR/hash/random/order key
- worker abstraction without semantic order

Dependencies: QA-01 fixtures may proceed parallel; final acceptance depends QA-01。

Acceptance:

```text
schema.*
determinism.entity-id.vector
determinism.intent-id.vector
determinism.random.*
determinism.order.*
```

### GW-01 Gateway project scaffold / protocol-config foundation

Suggested title: `実装: Gateway基盤・protocol/config scaffold`

Scope:

- Gateway executable/test project
- local protobuf generated types
- common envelope validator
- Gateway Config 1.0 loader

Dependencies: QA-01 contract fixture source。

### VIEW-01 General View scaffold / Gateway protocol client

Suggested title: `実装: General View基盤・Gateway protocol client`

Scope:

- Web application scaffold
- binary WebSocket/protobuf client
- Config loader/presentation defaults
- lifecycle shell

Dependencies: QA-01 protocol fixture。

### ADMIN-01 Admin View scaffold / Gateway protocol client

Suggested title: `実装: Admin View基盤・Gateway protocol client`

Dependencies: QA-01 protocol fixture。

## 6. Stage 1 — Core common runtime

### SIM-02 Core Config coordinator

Scope:

- `config.simulation-core/1.0` 68 fields
- default completion/write-back
- ConfigGeneration/digest
- runtime change scheduling

Dependencies: SIM-01

Acceptance: `config.*` core subset。

### SIM-03 Persistence engine

Scope:

- generation directory
- SQLite WAL/FULL
- history/dedup/scheduler/meta
- Snapshot chunk/manifest
- migration/export/import

Dependencies: SIM-01, QA-01

Acceptance:

```text
persistence.history.*
persistence.snapshot.*
persistence.migration.*
```

### SIM-04 WorldState / 97 partition registry

Scope:

- WorldStateV1
- DomainRecordEnvelope
- 97 partition registry/payload codecs
- secondary index rebuild framework
- StateDiagnostic

Dependencies: SIM-01

Acceptance:

```text
schema.partition-count
domain.partition.*
```

### SIM-05 Operation lifecycle / scheduling / dedup

Scope:

- UNSEEN -> ACCEPTED_DURABLE -> SCHEDULED_DURABLE -> TERMINAL_DURABLE
- immutable digest
- deadline/grace/pause
- scheduler state

Dependencies: SIM-02, SIM-03

Acceptance: `protocol.operation.*`, `persistence.dedup.*`。

### SIM-06 StepCoordinator / deterministic merge / transaction engine base

Scope:

- freeze input
- execution plan
- DomainRuntime context
- canonical intent merge/conflict
- StepCandidate
- invariant barrier
- durable finalize integration

Dependencies: SIM-03, SIM-04, SIM-05

Acceptance:

```text
determinism.conflict.*
transaction.*.crash-before-commit
```

## 7. Stage 2 — Domain implementation packages

Domain work begins after SIM-04 and stable DomainRuntime API from SIM-06 is available. Packages may run largely in parallel。

### SIM-07 Spatial / Environment domains

Scope:

- SBO-SDF terrain
- frame/scope/containment
- environment field/cohort payload
- atmosphere/hydrology/ocean/geology/ecology
- domain event/intent registry subset

Dependencies: SIM-04, SIM-06

Acceptance: `domain.spatial.*`, `domain.environment.*`。

### SIM-08 Physical / Built domain

Scope:

- presence/occupancy/built partitions
- hierarchical AABB grid
- GJK/EPA/contact/SDF collision
- pathfinding integration
- item/worksite/material handoff

Dependencies: SIM-04, SIM-06; uses spatial query contract from SIM-07, can develop against fixture before merge。

Acceptance: `domain.physical.*`, `domain.path.*`。

### SIM-09 Resident / Participation domains

Scope:

- Resident lifecycle/health/perception/belief/memory/psychology/goal/skill
- bounded GOAP
- Participation binding/control/absence/detail floor

Dependencies: SIM-04, SIM-06; physical interface fixture from SIM-08。

Acceptance: `domain.resident.*`, `domain.participation.*`。

### SIM-10 Society / Economy domain

Scope:

- organization/employment/contracts/property
- accounts/double-entry
- call auction
- production/logistics/culture/reputation/claim

Dependencies: SIM-04, SIM-06

Acceptance: `domain.market.*`, `domain.ledger.*`, society transaction tests。

### SIM-11 Governance / Security domain

Scope:

- polity/institution/jurisdiction
- rule AST/evaluation
- permission/tax/diplomacy
- incident/investigation/case/enforcement/military/border

Dependencies: SIM-04, SIM-06

Acceptance: `domain.law.*`, governance tests。

### SIM-12 Infrastructure / Information domain

Scope:

- network/service/queue/dependency
- power/water/transport/communication
- information delivery/media/record/address
- outage/recovery

Dependencies: SIM-04, SIM-06

Acceptance: `domain.infrastructure.*`。

## 8. Stage 3 — Core cross-domain/detail/protocol

### SIM-13 Cross-domain transactions / detail transitions

Scope:

- 17 TransactionKind
- participant/invariant assembly
- promotion/demotion queue
- identity/stock/obligation/flow/provenance conservation
- per-Step materialization budgets

Dependencies: SIM-07..SIM-12

Acceptance:

```text
transaction.*
detail.*
```

### SIM-14 Core protocol boundary / publication projection

Scope:

- gRPC bidirectional Core-Gateway
- registration/heartbeat/Master generation
- operation/batch/status
- full/delta publication
- continuity/resync

Dependencies: SIM-03, SIM-05, SIM-06; domain projection can be incremental, final acceptance after SIM-13。

Acceptance: protocol common/operation/publication/master tests。

### SIM-15 Core observability / telemetry

Scope:

- structured logs
- canonical core metrics/spans
- StateDiagnostic export
- performance instrumentation

Dependencies: SIM-06, P4-07 schema; can develop parallel with domains。

Acceptance: `observability.*` core subset。

## 9. Gateway packages

### GW-02 Core protocol / confirmed cache / resync

Scope:

- Core gRPC client
- confirmed cache
- continuity validation
- resync coordinator
- scheduling policy view

Dependencies: GW-01; fixture protocol from QA-01, integration with SIM-14 later。

Acceptance: `protocol.publication.*`, `protocol.gateway.resync-gate`。

### GW-03 Peer/Master/custody/retry

Scope:

- Gateway-Gateway gRPC
- peer heartbeat
- Master role
- local/cross gateway batch
- custody store/retry/status convergence

Dependencies: GW-01, GW-02

Acceptance: `protocol.master.*`, Gateway arrival/retry tests。

### GW-04 OIDC/BFF session / authentication

Scope:

- Authorization Code + PKCE
- BFF cookie/session
- Master login proxy integration
- session revoke/lifetime

Dependencies: GW-01, GW-03 for Master login routing; IdP mocked independently。

Acceptance: `security.oidc.*`, session tests。

### GW-05 Authorization / View+Admin boundaries

Scope:

- General role permission matrix enforcement
- Admin permission matrix
- WebSocket envelope/session validation
- operation category authorization

Dependencies: GW-04

Acceptance: `security.role-domain-separation`, unauthorized forwarding tests。

### GW-06 Publication / result routing / backpressure

Scope:

- publication buffer/coalesce
- subscriber filter
- result router
- slow consumer handling
- View/Admin protocol payload catalog

Dependencies: GW-02, GW-05

Acceptance: publication/slow-client tests。

### GW-07 Gateway observability / management audit

Scope:

- Gateway metrics/log/trace
- audit.sqlite/hash chain/retention anchor
- Admin audit query/export
- fail-closed protected Admin audit path

Dependencies: GW-05

Acceptance: observability/audit tests。

## 10. General View packages

### VIEW-02 Confirmed state store / publication consumer

Scope:

- FULL/DELTA
- continuity
- atomic confirmed swap
- resync lifecycle

Dependencies: VIEW-01

Acceptance: `protocol.publication.*` client subset。

### VIEW-03 Three.js scene projection / renderer

Scope:

- SceneProjection model
- Three.js scene lifecycle
- full 3D terrain/built/presence presentation
- render LOD/culling

Dependencies: VIEW-02; asset fixture can enable parallel work。

Acceptance: presentation tests; no authoritative mutation from render/camera。

### VIEW-04 Prediction / reconciliation / Operation controller

Scope:

- local prediction/interpolation
- stable Operation request
- pending/retry/result
- reconcile

Dependencies: VIEW-02

Acceptance: prediction-not-authority, operation retry identity tests。

### VIEW-05 Participation UX

Scope:

- Resident selection/preferences
- binding projection
- absence policy UI
- reconnect/death state

Dependencies: VIEW-04, GW-05 protocol contract

Acceptance: Participation protocol/UI contract tests。

## 11. Admin View packages

### ADMIN-02 Health / metrics / log / audit UI

Scope:

- target catalog
- metrics dashboard
- structured log query
- audit query/export presentation

Dependencies: ADMIN-01, GW-07 fixture。

### ADMIN-03 Config / operational command management

Scope:

- Config projection/editor
- expected generation
- command catalog
- stable request/result tracking

Dependencies: ADMIN-01, GW-05/GW-06 fixture。

Acceptance: config stale/no generic undo tests。

### ADMIN-04 High-impact / simulation Admin Operation

Scope:

- high-impact confirmation
- simulation operation flow
- audit correlation
- revoke/failure state

Dependencies: ADMIN-03

Acceptance: audit/confirmation/authorization tests。

## 12. QA / verification packages

### QA-02 Determinism / replay harness

Scope:

- worker 1/4/8/16
- process restart checkpoints
- Gateway/View/telemetry permutation
- Step-by-Step digest comparator

Dependencies: SIM-06; grows with domain merges。

### QA-03 Crash/fuzz/security harness

Scope:

- persistence crash matrix
- protobuf/WebSocket/TOML/AST/snapshot fuzz
- auth/session negative corpus
- audit redaction/tamper

Dependencies: SIM-03, GW-04, protocol parsers。

### QA-04 Performance / soak harness

Scope:

- `perf.reference.v1`
- persistence/publication subprofiles
- benchmark report
- 24h soak orchestration

Dependencies: SIM-13, SIM-14, GW-06, QA-02

## 13. Integration packages

### INT-01 Single Gateway end-to-end

Scenario:

```text
Core + 1 Gateway + General View + Admin View
```

Scope:

- login
- state publication
- Diver binding/action
- Config read/change
- save/restart/recovery

Dependencies: component minimum viable packages through VIEW-05/ADMIN-03/SIM-14/GW-06。

### INT-02 Multi-Gateway failover / resync

Scenario:

```text
Core + 4 Gateway + View churn
```

Scope:

- Master failover
- custody unknown convergence
- stale generation
- resync/slow clients

Dependencies: GW-03/GW-06/SIM-14/QA-02。

### INT-03 Release acceptance

Scope:

- all P4-08 suites
- perf.reference.v1
- persistence stress
- publication stress
- 24h soak
- ReleaseAcceptanceRecordV1

Dependencies: all standard work packages。

## 14. Dependency DAG summary

```text
QA-01
 ├─ SIM-01 -> SIM-02
 │          -> SIM-03 -> SIM-05
 │          -> SIM-04
 │ SIM-03 + SIM-04 + SIM-05 -> SIM-06
 │ SIM-06 -> SIM-07..SIM-12 (parallel)
 │ SIM-07..12 -> SIM-13
 │ SIM-05/06/03 -> SIM-14 -> INT
 │ SIM-06 -> SIM-15
 │
 ├─ GW-01 -> GW-02 -> GW-03 -> GW-04 -> GW-05 -> GW-06
 │                                      └────────────-> GW-07
 │
 ├─ VIEW-01 -> VIEW-02 -> VIEW-03
 │                    └-> VIEW-04 -> VIEW-05
 │
 └─ ADMIN-01 -> ADMIN-02
              -> ADMIN-03 -> ADMIN-04

SIM-06 -> QA-02
SIM-03/GW-04 -> QA-03
SIM-13/SIM-14/GW-06/QA-02 -> QA-04

component flows -> INT-01 -> INT-02 -> INT-03
```

## 15. Parallelization stages

### Stage A

Parallel:

- QA-01
- SIM-01
- GW-01
- VIEW-01
- ADMIN-01

### Stage B

Parallel after foundations:

- SIM-02/03/04
- GW-02
- VIEW-02
- ADMIN-02/03 fixture-based

### Stage C

- SIM-05/06
- GW-03/04
- VIEW-03/04
- ADMIN-04 fixture-based

### Stage D

Maximum domain parallelism:

- SIM-07
- SIM-08
- SIM-09
- SIM-10
- SIM-11
- SIM-12
- SIM-15
- GW-05/07

### Stage E

- SIM-13/14
- GW-06
- VIEW-05
- QA-02/03

### Stage F

- INT-01/02
- QA-04
- INT-03

## 16. Issue body template

Each implementation Issue should include:

```text
ImplementationWorkId:
Target component/base branch:
Phase4 source-of-truth docs:
Dependencies:
Scope:
Non-scope:
Stable schema/token affected:
Required TestCaseId:
Performance budget if relevant:
Migration/compatibility impact:
Completion criteria:
```

## 17. Definition of Done per work package

- implementation code committed to feature branch。
- component-local tests PASS。
- relevant P4-08 TestCaseId PASS。
- no undocumented stable token/schema addition。
- Config changes reflected in schema/examples。
- protocol changes independently contract-tested。
- persistence changes crash/replay-tested。
- no new cross-component code dependency。
- observability fields avoid unbounded metric cardinality/secret leakage。

## 18. Dependency violation rule

Implementationが未完成dependencyを仮定する場合、fixture/mock contractを使用する。

別componentのinternal project参照でshortcutしない。

Temporary TODO schemaをwire/persistenceへ公開しない。

## 19. Scope-change rule

Implementation中にPhase4 contract変更が必要になった場合:

1. implementation issue内だけでsilent変更しない。
2. design issueを作る/Phase4 doc amendmentを行う。
3. affected schema/version/compatibility/testを更新する。
4. dependent work packageへ通知する。

## 20. Work package count audit

| group | count |
|---|---:|
| Simulation Core | 15 |
| Gateway | 7 |
| General View | 5 |
| Admin View | 4 |
| QA | 4 |
| Integration | 3 |
| **total** | **38** |

## 21. P4-09 acceptance

- all 4 components have independent implementation path。
- 38 work packages have target/dependency/acceptance mapping。
- domain packages are parallelizable after Core contract foundation。
- cross-component integration comes after independent contract tests。
- implementation Issue can be created in dependency order without additional architecture decision。

blocker: なし。