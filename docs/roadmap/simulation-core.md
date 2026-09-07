# Simulation Core Implementation Roadmap

Status: Implementation Ready  
Work IDs: `SIM-01..SIM-15`  
Base branch: `simulation`  
Canonical breakdown: `docs/design/phase4-implementation-work-breakdown.md`

## 1. 目的

Simulation Core の実装順序を、Phase 4 で確定した dependency DAG に従って追跡する。

旧 Phase 0 の設計項目は Phase 1〜4 で詳細化済みであり、新しい実装段階の前提未確定事項として扱わない。Architecture/Protocol に残る stale TBD の同期は design-baseline normalization として並行処理する。

## 2. Work Package

| ID | Stage | Scope | Main dependencies |
|---|---|---|---|
| `SIM-01` | A | Core scaffold / deterministic primitives | QA-01 fixtureでfinal acceptance |
| `SIM-02` | B | Core Config coordinator | SIM-01 |
| `SIM-03` | B | Persistence engine | SIM-01, QA-01 |
| `SIM-04` | B | WorldState / 97 partition registry | SIM-01 |
| `SIM-05` | C | Operation lifecycle / scheduling / dedup | SIM-02, SIM-03 |
| `SIM-06` | C | StepCoordinator / deterministic merge / transaction base | SIM-03, SIM-04, SIM-05 |
| `SIM-07` | D | Spatial / Environment domains | SIM-04, SIM-06 |
| `SIM-08` | D | Physical / Built domain | SIM-04, SIM-06; SIM-07 spatial query contract |
| `SIM-09` | D | Resident / Participation domains | SIM-04, SIM-06; SIM-08 physical fixture |
| `SIM-10` | D | Society / Economy domain | SIM-04, SIM-06 |
| `SIM-11` | D | Governance / Security domain | SIM-04, SIM-06 |
| `SIM-12` | D | Infrastructure / Information domain | SIM-04, SIM-06 |
| `SIM-13` | E | Cross-domain transactions / detail transitions | SIM-07..SIM-12 |
| `SIM-14` | E | Core protocol boundary / publication projection | SIM-03, SIM-05, SIM-06; final after SIM-13 |
| `SIM-15` | D | Core observability / telemetry | SIM-06 |

## 3. Critical path

```text
SIM-01
 ├─ SIM-02 ─┐
 ├─ SIM-03 ─┼─> SIM-05 ─┐
 └─ SIM-04 ──────────────┼─> SIM-06
                         ├─> SIM-07..SIM-12 ─> SIM-13
                         ├─> SIM-14
                         └─> SIM-15
```

`SIM-07..SIM-12` は stable DomainRuntime contract 成立後に最大並列で進める。

## 4. Implementation gates

### Foundation gate

`SIM-01` 完了時:

- executable/test projectが成立
- deterministic primitive、ID/hash/random/order keyをfixtureで検証可能
- worker abstractionがsemantic orderingを持たない

### Runtime gate

`SIM-06` 完了時:

- `State(S) -> State(S+1)` pipelineが成立
- operation/config/input freeze、canonical merge、invariant、durable finalizeが統合可能
- domain packageをproduction implementationへ進められる

### Domain gate

`SIM-13` 完了時:

- 8 domain familyのcross-domain transactionとdetail conservationが成立
- 17 TransactionKindをpartial commitなしで扱える

### External boundary gate

`SIM-14` 完了時:

- Gateway mockとCoreをProtocol契約だけで接続可能
- full/delta publication、Operation/Batch/status、Master generation、resyncが成立

## 5. Non-negotiable acceptance

- worker 1/4/8/16で同一logical inputから同一authoritative result
- wall clock / thread completion / network arrival raceをworld outcomeへ使用しない
- durable commit前にconfirmed state/resultをpublishしない
- accepted Operationをqueue pressureやMaster failoverでloss/duplicateしない
- authoritative full 3D / fixed-point deterministic profileを維持
- foreign domain private mutable stateへdirect writeしない

## 6. Issue tracking

Component roadmap Issue は #35 を利用する。

#35 は旧「Phase 0が完了するまでPhase 1以降未定」という意味ではなく、次を追跡する親Issueへ更新する。

- Architecture/Protocol stale baseline normalization
- `SIM-01..SIM-15` implementation package progress
- design amendmentが発生した場合の依存再評価

個々の実装作業は原則 `SIM-xx` ごとに独立Issueを起票し、#35へ紐付ける。
