# 詳細設計 Phase 4: Performance Completion Review

Status: Complete / P4-06 Completion Review  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`

## 1. 目的

P4-06のStep、CPU、memory、detail、queue、publication、persistence、benchmark budgetをP4-03〜P4-05と横断監査し、test acceptanceへ移行可能か判定する。

本書をP4-06 completion判定の正本とする。

## 2. 成果物

- `phase4-performance-budget.md`
- `phase4-performance-benchmark-profile.md`
- 本書

## 3. 30Hz target audit

Standard StepRate 30/1に対しp95 Step <=33.333msをreference targetに固定した。

OverrunでStep skip/merge/solver縮退しない。

判定: PASS。

## 4. CPU budget audit

8 domain + merge/cross-domainへaggregate CPU/wall targetを割り当てた。

budget超過はworld semantic inputではない。

判定: PASS。

## 5. Algorithm semantic audit

GJK/EPA/Jacobi/GOAP等のiteration boundをperformance pressureで変更しない。

optimizationはdata layout/index/parallelismを優先する。

判定: PASS。

## 6. Memory audit

32 GiB reference node、Core 22 GiB steady target/28 GiB guardを固定した。

authoritative stateをmemory pressureでevictしない。derived rebuildable cacheを先に縮退する。

判定: PASS。

## 7. Detail budget audit

Promotion/demotion per-Step semantic budgetをConfigへ追加し、canonical queue/deferを定義した。

wall-clock loadをdetail transition priorityへ使用しない。

判定: PASS。

## 8. Cadence audit

P4-03 D0〜D3 defaultをP4-06 initial referenceとして承認した。

event/Operation required workをcadence理由で遅延しない。

判定: PASS。

## 9. Persistence audit

- SQLite commit p95 target 4ms。
- Snapshot COW barrier p95 5ms。
- background Snapshot 120s p95。
- full history retentionをstorage planningへ反映。

P4-04 semantic変更なし。

判定: PASS。

## 10. Publication audit

Core->Gateway、Gateway->Viewにbandwidth/latency targetを定義し、coalesce可能範囲をconfirmed continuityを壊さないpresentation pathへ限定した。

判定: PASS。

## 11. Backpressure audit

優先順位:

1. authority
2. durability
3. accepted Operation
4. deterministic Step
5. terminal result
6. continuity
7. freshness/diagnostic/presentation

accepted Operation/historyをdropしない。

判定: PASS。

## 12. Benchmark reproducibility audit

`perf.reference.v1`で:

- fixed WorldSeed
- fixed counts/distribution
- fixed Operation mix
- fixed detail transition pressure
- fixed warm-up/measurement
- 3 repetitions
- worker 1/4/8/16 digest comparison

を定義した。

判定: PASS。

## 13. Config audit

P4-06で要求した4 detail budget fieldは`phase4-config-addendum.md`へ反映済み。

P4-03 completion reviewと矛盾なし。

判定: PASS。

## 14. Observability handoff

P4-06 required performance metricはP4-07 canonical metricsへmapping済み。

high-cardinality IDsをmetric labelsへ持ち込まない。

判定: PASS。

## 15. Completion criteria

| criterion | result |
|---|---|
| Step wall target | PASS |
| CPU/domain budget | PASS |
| memory budget | PASS |
| detail/cadence budget | PASS |
| queue/backpressure | PASS |
| persistence throughput | PASS |
| publication bandwidth | PASS |
| deterministic benchmark profile | PASS |
| Config cross-review | PASS |
| P4-07 metric handoff | PASS |
| unresolved P4-06 blocker | 0 |

## 16. Completion decision

P4-06を`Complete`と判定する。

実測benchmark結果はproduction code完成後のP4-08/implementation acceptanceで評価する。P4-06詳細設計としては、測定入力と合否条件が確定しておりblockerはない。