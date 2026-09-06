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

### P4-01 共通data structure / state layout / index — In Progress

成果物: `phase4-core-data-structures.md`

対象:

- primitive/value typeのexact representation
- stable token / schema identity / version表現
- authoritative `WorldState` directory
- state partition header / revision / owner metadata
- Step candidate / partition candidate / event / intent / invariant result layout
- deterministic collection ordering
- operation dedup / scheduler / transaction / state lookup index contract
- ownership・mutation境界

P4-01ではdomain固有数値solverまでは固定しない。

### P4-02 Protocol正式schema / error catalog — Planned

予定成果物: `phase4-protocol-schema.md`

- 4 protocolのmessage registry
- exact envelope field / scalar type / requiredness
- request/response/event/ACK/result semantics
- protocol version / capability negotiation schema
- common/domain error code registry
- full/delta publication wire contract
- size / count / recursion / string limit
- malformed / incompatible / stale generation handling

### P4-03 Config specification — Planned

予定成果物: `phase4-config-specification.md`

- component別TOML key一覧
- exact type / default / min / max / enum
- SIMULATION / OPERATIONAL / PRESENTATION classification
- RUNTIME_SAFE / RESTART_REQUIRED / WORLD_REGENERATION_REQUIRED
- effective Step / atomic apply boundary
- domain detail threshold / cadence / hysteresis
- migration / unknown key policy

### P4-04 Persistence / snapshot / history / migration — Planned

予定成果物: `phase4-persistence-specification.md`

- physical directory/file/database boundary
- snapshot manifest / chunk / partition format
- history record registryとpayload schema
- hash chain / checksum / compression
- candidate commit / fsync / atomic publish sequence
- migration transaction / rollback
- retention / compaction / dedup tombstone storage
- recovery selection algorithm

### P4-05 Domain algorithm / numeric / deterministic reduction — Planned

予定成果物: `phase4-algorithm-determinism.md`

- coordinate/frame/geometry representation
- spatial index
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

- `partition_id`はworld内で一意。
- owner変更はschema migrationなしに行わない。
- cross-domain参照はstable ID/referenceを使う。
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

Phase 4開始時点:

- P4-01: In Progress
- P4-02: Planned
- P4-03: Planned
- P4-04: Planned
- P4-05: Planned
- P4-06: Planned
- P4-07: Planned
- P4-08: Planned
- P4-09: Planned

blocker: なし。

Phase 1〜3は `documentation` へ統合済みで、Phase 4 entry conditionは満たされている。
