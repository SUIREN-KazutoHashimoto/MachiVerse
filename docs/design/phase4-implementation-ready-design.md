# 詳細設計 Phase 4: 実装直前設計

Status: Complete  
Tracking: Issue #16  
Predecessors: `phase1-cross-cutting-review.md`, `phase2-cross-component-review.md`, `phase3-traceability-cross-cutting-review.md`  
Completion review: `phase4-completion-review.md`

## 1. 目的

Phase 1〜3で確定した横断契約、component ownership、world simulation domain semanticsを、production implementation Issueへ直接分解できる具体度まで落とし込む。

Phase 4ではproduction実装コードは書かず、次を実装契約として固定した。

- concrete data structure / state layout / index / ownership
- protocol schema / transport / field / scalar type / constraint / error code
- Config key / type / default / range / mutability / apply boundary
- save / snapshot / history / migration physical/logical format
- algorithm / numeric representation / deterministic ordering / reduction
- performance / memory / cadence / detail budget
- observability / structured log / metrics / trace / audit
- test / determinism / replay / recovery / compatibility acceptance
- platform/runtime profile
- implementation work packageとdependency DAG

## 2. 設計優先順位

Phase 4完了後は次を優先する。

1. `docs/requirements` の確定要件
2. Phase 1 final reviewとその正本
3. Phase 2 cross-component reviewとcomponent設計
4. Phase 3 completion reviewとdomain設計
5. `phase4-completion-review.md`
6. Phase 4個別specification/completion review
7. 旧architecture/protocol文書の未決定記述

個別Phase4文書に作業途中のStatus/「残作業」表現が残る場合でも、completion判定は`phase4-completion-review.md`を正本とする。

## 3. Phase 4で維持する不変条件

- authoritative World Timeは`SimulationStep`。
- `State(S) -> State(S+1)`は単一logical finalization boundaryを持つ。
- world outcomeへwall clock、thread completion order、network arrival race、Gateway/Master identity、View camera/FPS、telemetryを持ち込まない。
- domain private stateへのcross-domain direct mutable writeを作らない。
- persistent identity / stock / obligation / flow / provenanceをdetail transitionやboundary exchangeで無理由に失わない。
- accepted Operationのretry/failover/reconnectでOperationId/immutable digestを変更しない。
- durability前のcandidate State/terminal successをconfirmed publishしない。
- saved worldのsimulation Config/historyをrestore/replayのauthorityとする。
- render LODとsimulation detailを分離する。
- component間compiled DTO/internal project dependencyを契約にしない。

## 4. P4-01 共通data structure / state layout / index — Complete

成果物:

- `phase4-core-data-structures.md`
- `phase4-domain-state-registry.md`

確定:

- exact primitive/value/identity width
- `WorldStateV1`
- partition descriptor/header/read boundary
- MutationIntent / DomainEvent / StepCandidate / PartitionCandidate
- CrossDomainTransactionCandidate / invariant result
- scheduler / Operation dedup / detail directory
- deterministic collection/index/builder model
- 8 domain / 97 authoritative partition registry
- stable partition/record/schema identity

## 5. P4-02 Protocol正式schema / error catalog — Complete

成果物:

- `phase4-protocol-schema.md`
- `phase4-auth-session-protocol.md`
- `phase4-protocol-payload-catalog.md`
- `phase4-protocol-completion-review.md`

確定:

- Protocol Buffers proto3
- Core↔Gateway / Gateway↔Gateway gRPC bidirectional streaming
- General/Admin View binary WebSocket over TLS
- common envelope/version/capability/result/error
- 8 MiB envelope hard limit
- Operation/Batch/status/custody/retry/Master failover
- FULL/DELTA state publication/resync
- OIDC Authorization Code + PKCE / Gateway BFF
- General View/Admin View authz domain separation

Protocol-level blocker: 0。

## 6. P4-03 Config specification — Complete

成果物:

- `phase4-config-specification.md`
- `phase4-config-addendum.md`
- `phase4-config-standard-examples.md`
- `phase4-config-completion-review.md`

Schema:

```text
config.simulation-core / 1.0
config.gateway / 1.0
config.general-view / 1.0
config.admin-view / 1.0
```

`meta.*`を除くstandard fieldは136件。

- exact type/default/range/impact/mutability
- default completion + atomic write-back
- ConfigGeneration/digest
- simulation effective Step
- D0〜D3 domain cadence/detail budget
- persistence/publication/queue/auth/session/observability values
- complete standard TOML examples

Config blocker: 0。

## 7. P4-04 Persistence / snapshot / history / migration — Complete

成果物:

- `phase4-persistence-specification.md`
- `phase4-persistence-record-catalog.md`
- `phase4-persistence-completion-review.md`

確定:

- SQLite WAL + synchronous FULL
- logical uint64 U64BE DB representation
- history hash chain
- scheduler/Operation dedup/Config physical tables
- 55-byte SameStepOrderKey DB encoding
- 97 domain + 6 core = 103 required Snapshot logical sections
- immutable Snapshot chunk/manifest/digest
- 32 MiB target / 64 MiB max chunk
- Zstandard default compression
- crash-safe Snapshot discovery
- deterministic recovery/replay
- non-destructive migration generation + CURRENT pointer
- portable export/import
- semantic history full retention v1.0

Persistence blocker: 0。

## 8. P4-05 Domain algorithm / numeric / deterministic reduction — Complete

成果物:

- `phase4-algorithm-determinism.md`
- `phase4-domain-payload-schema.md`
- `phase4-domain-operation-event-intent-catalog.md`
- `phase4-algorithm-completion-review.md`

確定:

- integer/fixed-point authoritative numerical profile
- Int128 intermediate / checked arithmetic / round ties to even
- world coordinate / `Vec3Mm` / `QuaternionQ30`
- Sparse Brick Octree SDF terrain
- spatial index / motion / GJK-EPA / contact / pathfinding
- natural environment numerical model
- Resident cognition/health/skill
- deterministic market/ledger/production
- declarative law AST
- infrastructure graph/queue/power/water
- deterministic parallel/reduction/random
- 97/97 partition payload schema

Stable semantic registry:

```text
OperationKind 69
EventKind 129
IntentKind 63
CrossDomainTransactionKind 17
```

Algorithm/domain blocker: 0。

## 9. P4-06 Performance / memory / cadence / detail budget — Complete

成果物:

- `phase4-performance-budget.md`
- `phase4-performance-benchmark-profile.md`
- `phase4-performance-completion-review.md`

Standard reference:

- 30Hz target
- Step p95 <=33.333 ms reference target
- Core memory <=22 GiB steady target / 28 GiB guard
- domain CPU/memory/work budgets
- deterministic detail materialization budget
- persistence/Snapshot throughput targets
- publication bandwidth/latency targets
- queue/backpressure priority
- reproducible `perf.reference.v1`
- worker 1/4/8/16 determinism comparison

Performance design blocker: 0。

## 10. P4-07 Observability / audit — Complete

成果物:

- `phase4-observability-audit.md`
- `phase4-observability-completion-review.md`

確定:

- OpenTelemetry-compatible telemetry / OTLP profile
- W3C Trace Context
- stable structured log/metric/span registries
- bounded metric cardinality
- telemetry/world authority separation
- append-only management/security AuditRecord hash chain
- diagnostic log 14日 / management audit 400日 default
- world execution historyとのauthority分離
- credential/private-content redaction

Observability blocker: 0。

## 11. P4-08 Test / acceptance — Complete

成果物:

- `phase4-test-acceptance.md`

確定:

- stable `TestCaseId`
- golden fixtures
- all 97 partition/domain property tests
- all 17 cross-domain transaction atomicity tests
- protocol/auth/config test
- crash injection matrix
- Snapshot/history/recovery/migration tests
- determinism matrix
- `perf.reference.v1` performance acceptance
- 24h soak
- observability/security/fuzz
- independent component contract test
- release non-waivable failures

P4-08 blocker: 0。

## 12. P4-09 Platform / implementation work breakdown / final review — Complete

成果物:

- `phase4-platform-runtime-profile.md`
- `phase4-implementation-work-breakdown.md`
- `phase4-completion-review.md`

Standard implementation profile:

```text
Core/Gateway: .NET 10 LTS / C# 14
Gateway: ASP.NET Core 10
General View: standalone Blazor WebAssembly net10.0
Admin View: standalone Blazor WebAssembly net10.0
General View rendering: Three.js WebGLRenderer / WebGL 2
```

WebGPUはinitial standardのrequired dependencyにせずoptional experimental profileとする。

Implementation work breakdown:

```text
Simulation Core 15
Gateway 7
General View 5
Admin View 4
QA 4
Integration 3
total 38
```

各work packageにtarget branch、dependency DAG、parallel stage、acceptance TestCaseIdを定義した。

## 13. Versioning / registry policy

Phase 4で永続化/wire/world resultへ現れるstable identityはversioned registryへ置く。

対象:

- SchemaId / PartitionId / DomainToken
- OperationKind / EventKind / IntentKind / TransactionKind
- InvariantId / ErrorCode
- MetricName / AuditEventKind / TestCaseId / AlgorithmId

一度history/wire/persistenceへ公開したtokenの意味をin-place変更しない。

## 14. Data ownership

97 authoritative partitionはP4-01 registryを正本とする。

- partition ownerは一意。
- cross-domain refはstable ID/reference。
- foreign mutable pointer禁止。
- foreign mutationはIntent。
- required multi-domain effectはCrossDomainTransaction + shared invariant。

## 15. Determinism acceptance baseline

同一:

- WorldSeed
- genesis State
- simulation Config/history
- accepted/scheduled Operation history
- enabled domain/schema/algorithm set

に対し、次を変えてもauthoritative logical resultを一致させる。

- worker/thread count
- task completion order
- process restart
- Gateway count/route/Master identity
- View connection/camera/render state
- wall-clock speed
- logging/telemetry state

比較authorityはcommitted StateDiagnostic digest。

## 16. Failure policy

Phase4 failure classification:

```text
validation_reject
step_abort
component_start_reject
connection_reject
degraded_operational
fatal_invariant
```

silent coercion、last-arrival-wins、best-effort partial commitでworld semanticsを変更しない。

## 17. Implementation-local freedom

次はPhase4 contractに影響しない範囲でimplementation/release choiceとして残せる。

- internal class/project/file naming
- physical lock/container/arena/pool implementation
- compatible .NET 10 servicing patch
- exact package/Three.js release lock
- CSS/layout details
- telemetry backend/vendor
- container/orchestrator/CI product
- host deployment topology

これらは詳細設計blockerではない。

## 18. Completion state

P4-01〜P4-09をすべてCompleteと判定する。

Phase 4全体進捗: **100%**。

Unresolved detailed-design blocker: **0件**。

Issue #16完了条件の最終判定は`phase4-completion-review.md`を正本とする。

次工程:

1. 本ブランチをreview/PRで`documentation`へ統合する。
2. 統合後、`phase4-implementation-work-breakdown.md`のdependency DAGに従って38 standard implementation Issueを起票する。
3. `simulation` / `gateway` / `view` / `administration-view`各常設branchからimplementation work branchを切って実装へ進む。