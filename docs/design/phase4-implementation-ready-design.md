# 詳細設計 Phase 4: 実装直前設計

Status: In Progress  
Tracking: Issue #16  
Predecessors: `phase1-cross-cutting-review.md`, `phase2-cross-component-review.md`, `phase3-traceability-cross-cutting-review.md`

## 1. 目的

Phase 1〜3で確定した横断契約、component ownership、world simulation domain semanticsを、実装Issueへ直接分解できる具体度まで落とし込む。

Phase 4では「何を意味するか」だけでなく、少なくとも次を実装契約として固定する。

- concrete data structure / state layout / index / ownership
- protocol schema / field / scalar type / constraint / error code
- Config key / type / default / range / mutability / apply boundary
- save / snapshot / history / migration format
- algorithm choice / deterministic ordering / reduction
- performance / memory / cadence / detail budget
- observability / log / metrics / audit field
- test strategy / determinism / replay / recovery / compatibility acceptance criteria
- implementation Issueへ分解するwork packageと依存順

本Phaseではproduction実装コードは書かない。

## 2. 設計優先順位

Phase 4の設計解釈は次の順で行う。

1. `docs/requirements` の確定回答
2. Phase 1 final reviewとその正本
3. Phase 2 cross-component reviewとcomponent設計
4. Phase 3 completion reviewとdomain設計
5. Phase 4 completion review（作成後）
6. Phase 4個別specification
7. 旧architecture/protocol文書の未決定記述

Phase 4は上位Phaseのsemantic ownershipを変更しない。具体化の過程で上位契約と矛盾を発見した場合は、暗黙に意味を書き換えずblockerとして記録する。

## 3. Phase 4で維持する不変条件

- authoritative World Timeは `SimulationStep`。
- `State(S) -> State(S+1)` のfinalizeは単一logical commit boundaryを持つ。
- world outcomeへwall clock、thread completion order、network arrival race、Gateway/Master identity、View camera/FPSを持ち込まない。
- domain private stateへのcross-domain direct mutable writeを作らない。
- persistent identity、stock、obligation、flow、provenanceをdetail transitionやboundary exchangeで無理由に失わない。
- accepted Operationのretry/failover/reconnectでlogical identityとimmutable payload digestを変更しない。
- durability完了前のcandidate state / terminal resultをconfirmed publishしない。
- saved worldのsimulation Config/historyをrestore continuationの正本とする。
- render LODとsimulation detail levelを分離する。

## 4. Phase 3からの正式handoff

Phase 3 completion reviewで、次をPhase 4のconcrete design対象として引き継いだ。

- coordinate numeric representation / exact geometry data structure
- weather / hydrology / ocean / geology / ecology numerical algorithm
- collision / pathfinding / motion solver
- Resident cognition / health / skill numerical schema
- market / price / allocation / monetary policy algorithm
- law/rule representation / legal resolution algorithm
- transport / power / water / communication graph algorithm
- queue allocation algorithm
- concrete CrossDomainTransaction / candidate-state data structure
- persistence physical layout / database / compression
- publication full/delta encoding
- exact Config defaults / thresholds / cadences

これらはPhase 3 semantic ownership、cross-domain causality、detail semanticsを変更せず具体化する。

## 5. Phase 4作業分解

### P4-01 共通data structure / state layout / index — Complete

成果物:

- `phase4-core-data-structures.md`
- `phase4-domain-state-registry.md`

確定事項:

- primitive/value typeのexact representation
- stable token / schema identity / version表現
- authoritative `WorldStateV1` directory
- partition header / revision / owner metadata
- Step candidate / partition candidate / event / intent / invariant result layout
- deterministic collection ordering
- operation dedup / scheduler / transaction / state lookup index contract
- partition builder ownership・mutation境界
- Phase 3全8 domain / 97 authoritative partition registry
- `PartitionRecordId` / common record envelope / canonical record order
- standard partition schema identity rule
- snapshot/replayで保持すべきinitial persistence class

Domain payload内部の数値model・solver・domain-specific secondary indexはP4-05へ引き渡す。

### P4-02 Protocol正式schema / error catalog — Complete

成果物:

- `phase4-protocol-schema.md`
- `phase4-auth-session-protocol.md`
- `phase4-protocol-payload-catalog.md`
- `phase4-protocol-completion-review.md`

確定事項:

- standard wire serialization: Protocol Buffers proto3
- internal Core↔Gateway / Gateway↔Gateway: gRPC bidirectional stream
- Web General View / Admin View: TLS binary WebSocket
- common `WireEnvelopeV1` field number/type/validation
- 8 MiB envelope limitとcommon structural limit
- uint64のbrowser lossless mapping rule
- handshake / version / Capability common wire schema
- common Result / RetryAdvice / ErrorCode registry
- state publication FULL/DELTA chunking schema
- Standard Operation / Batch / status query base schema
- Gateway registration/heartbeat/Master role message
- Batch ACK / custody state
- View subscription / projection / resync schema
- Admin health/log/Config/audit wire schema
- OIDC Authorization Code + PKCE / Gateway BFF session profile
- General View role / Admin permission domain分離
- message-by-message required Capability mapping

P4-02 completion reviewでprotocol-level unresolved blocker 0件を確認した。

World-domain固有Operation payloadはP4-05、Config値はP4-03、Metric/Log/Audit registryはP4-07へownershipどおり引き渡す。

### P4-03 Config specification — In Progress

成果物: `phase4-config-specification.md`

確定済み:

- 4 component Config schema `1.0`
- component別TOML key一覧
- exact type / default / min / max / enum
- SIMULATION / OPERATIONAL / PRESENTATION classification
- RUNTIME_SAFE / RESTART_REQUIRED / WORLD_REGENERATION_REQUIRED boundary
- effective Step / atomic apply boundary
- Core scheduling/detail/snapshot/publication/Master/queue values
- 8 domain D0〜D3 cadence baseline
- Gateway network/queue/publication/cache/auth/session values
- General/Admin View presentation/operational values
- ConfigGeneration / default completion / restore / migration/error rules

残作業:

- P4-04 persistence parametersとのcross-review
- P4-06 performance measurementによるcadence/default再検証
- P4-07 observability/audit retentionとのcross-review
- canonical TOML sample追加

### P4-04 Persistence / snapshot / history / migration — In Progress

成果物: `phase4-persistence-specification.md`

確定済み:

- SQLite 3 WAL + synchronous FULL standard profile
- world/persistence generation directory layout
- unsigned uint64のSQLite `U64BE` representation
- history / operation dedup / scheduler / Config / operational state tables
- Operation accepted/scheduled/transition commitのdurable transaction boundary
- 97 domain + 6 core = 103 required logical Snapshot section
- immutable snapshot manifest/chunk framing
- Zstandard default compression
- snapshot staging/fsync/atomic discovery sequence
- recovery selection/replay algorithm
- copy-on-write persistence generation migrationとCURRENT pointer

残作業:

- remaining history payload schema
- SameStepOrderKey binary DB encoding
- snapshot chunk splitting
- historical replay retention default
- compaction anchor/transaction
- backup/export format
- P4-06 performance cross-review

### P4-05 Domain algorithm / numeric / deterministic reduction — Planned

予定成果物: `phase4-algorithm-determinism.md`

- coordinate/frame/geometry representation
- spatial index
- 97 partition payload field/numeric schema
- weather/hydrology/ocean/geology/ecology solver family
- collision / motion / pathfinding
- Resident numerical state update
- economy / market / finance
- law / security resolution
- infrastructure graph / queue allocation
- deterministic reduction / bounded iterative solver / convergence fallback

### P4-06 Performance / memory / cadence / detail budget — Planned

予定成果物: `phase4-performance-budget.md`

- Step wall-clock targetとoverrun policy
- domain compute budget
- memory budget / entity / region budget
- snapshot / history throughput target
- publication bandwidth / delta budget
- D0〜D3 update cadence / promotion budget
- queue and backpressure limits
- graceful degradationで変更してよいもの/ならないもの

### P4-07 Observability / audit — Planned

予定成果物: `phase4-observability-audit.md`

- structured log event registry
- metrics names / labels / cardinality constraint
- trace correlation
- deterministic diagnostic digest
- operation/config/admin audit trail
- snapshot/recovery/migration audit
- security/privacy boundary

### P4-08 Test / acceptance criteria — Planned

予定成果物: `phase4-test-acceptance.md`

- unit / property / integration / compatibility test
- determinism verification across thread count/process restart
- replay / snapshot / crash recovery
- protocol compatibility / malformed input
- Config migration / atomic apply
- detail promotion/demotion conservation
- cross-domain semantic transaction atomicity
- performance / soak / memory acceptance

### P4-09 Implementation work breakdown / completion review — Planned

予定成果物:

- `phase4-implementation-work-breakdown.md`
- `phase4-completion-review.md`

- implementation Issue単位
- dependency DAG
- parallelizable work package
- acceptance criteria link
- schema/config/persistence versioning dependency
- blocker audit
- Phase 4 completion判定

## 6. Phase 4 artifact共通要件

各Phase 4 specificationは、適用対象に応じ最低限次を含む。

1. normative scope
2. exact name / stable token / schema id
3. exact scalar/container type
4. required/optional/default
5. range / length / cardinality / uniqueness
6. ownership / mutability
7. serialization/persistence representation
8. deterministic ordering / comparison / reduction
9. validation rule / failure code
10. lifecycle / state transition
11. compatibility / migration
12. observability
13. test acceptance criteria
14. implementation dependency
15. traceability to Phase 1〜3

`implementation-defined`を残す場合は、world outcome・compatibility・replayに影響しない局所最適化だけに限定し、その自由度を明記する。

## 7. Versioning policy

Phase 4で永続化またはprotocolへ現れるschemaにはstable identityとversionを与える。

```text
SchemaVersion {
  major: uint16,
  minor: uint16
}
```

原則:

- `major`: backward-incompatible semantic/layout change
- `minor`: backward-compatible field/capability addition
- persisted/wire tokenの意味をin-place変更しない
- field削除/renameはmigration/aliasを伴う
- unknown majorはreject
- unknown optional minor fieldはschema policyに従いskip可能
- required capability不足はsilent downgradeしない

Config schema、protocol version、persistence schemaは別generation/versionを持ち、1つの数値へ統合しない。

## 8. Stable registry policy

Phase 4で次のregistryを構築する。

- `SchemaId`
- `PartitionId`
- `DomainToken`
- `OperationKind`
- `EventKind`
- `IntentKind`
- `InvariantId`
- `ErrorCode`
- `MetricName`
- `AuditEventKind`

stable tokenはlowercase ASCIIで、Phase 1の `StableToken` 制約へ従う。

registry entryは一度world history、snapshot、wire messageへ出た後に意味を変更しない。

## 9. Data ownershipの具体化原則

Phase 3のauthoritative ownerを、Phase 4ではpartition単位へ固定する。

```text
PartitionDescriptorV1 {
  partition_id,
  owner_domain,
  schema_id,
  schema_version,
  persistence_class,
  detail_capabilities,
  invariant_ids
}
```

Phase 4 standard profileは97 authoritative partitionを`phase4-domain-state-registry.md`で固定する。

- `partition_id`はworld内で一意。
- owner変更はschema migrationなしに行わない。
- cross-domain参照は`PartitionRecordRefV1`等stable ID/referenceを使う。
- foreign partitionのmutable object referenceをdomain runtimeへ渡さない。
- candidate mutationはowner partitionのbuilderへ集約する。

## 10. Determinism acceptance baseline

Phase 4の全algorithm/data structureは少なくとも次を満たす。

同一:

- WorldSeed
- genesis state
- simulation Config/history
- accepted/scheduled Operation history
- enabled domain/schema set

に対して、次を変えてもauthoritative logical resultが一致する。

- worker/thread数
- task completion order
- process restart
- Gateway route
- View接続状態
- wall-clock execution speed

比較は最低限 `transition.committed` ごとのcanonical state diagnostic digestで行う。

## 11. Failure policy

Phase 4で定義する各failureは次のいずれかへ分類する。

- `validation_reject`: candidate/input単位でrejectしState(S)維持
- `step_abort`: transition全体をabortしState(S)維持
- `component_start_reject`: incompatible/invalid persisted/config/schemaで起動拒否
- `connection_reject`: incompatible protocol/capabilityで接続拒否
- `degraded_operational`: world semanticsを変えない範囲の運用縮退
- `fatal_invariant`: authority/replay整合性を保証できず停止

silent coercionでworld semanticsを変更しない。

## 12. 現在の進捗

- P4-01: Complete
- P4-02: Complete
- P4-03: In Progress
- P4-04: In Progress
- P4-05: Planned
- P4-06: Planned
- P4-07: Planned
- P4-08: Planned
- P4-09: Planned

Phase 4全体進捗目安: 50%。

blocker: なし。

Phase 1〜3は `documentation` へ統合済みで、Phase 4 entry conditionは満たされている。
