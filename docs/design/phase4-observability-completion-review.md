# 詳細設計 Phase 4: Observability / Audit Completion Review

Status: Complete / P4-07 Completion Review  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`

## 1. 目的

P4-07のlogs/metrics/traces/auditについて、authority separation、cardinality、privacy/security、retention、performance handoffを横断監査し、P4-08 test acceptanceへ移行可能か判定する。

本書をP4-07 completion判定の正本とする。

## 2. 成果物

- `phase4-observability-audit.md`
- 本書

## 3. Standard telemetry profile audit

- OpenTelemetry-compatible model/export。
- OTLP standard export profile。
- W3C Trace Context propagation。
- MachiVerse固有stable registryは本設計を正本。

External telemetry standard updateでworld semanticを変更しない。

判定: PASS。

## 4. Authority separation audit

Metric/log/trace sampling/exporter statusを:

- Step order
- random
- Entity/Operation identity
- detail trigger
- world scheduling

へ使用しない。

Audit exporterをworld persistence commitの代替にしない。

判定: PASS。

## 5. Registry count audit

| registry | count |
|---|---:|
| LogEventKind | 51 |
| canonical metric instruments | 51 |
| recommended span kinds | 13 |
| AuditEventKind | 26 |

Unknown kindはfree-form stable semanticとしてsilent生成しない。

判定: PASS。

## 6. Metric cardinality audit

Metric labelへEntity/Resident/Operation/Message/Correlation/Batch/user identityを使用しない。

bounded labelsだけを許可し、standard active series target <=5,000/component、warning 10,000を設定した。

判定: PASS。

## 7. Structured log audit

`StructuredLogEventV1`にoperational timeとSimulationStepを分離した。

wall-clock log順をworld orderへ使用しない。

secret/raw credential/private contentをdefault logへ出さない。

判定: PASS。

## 8. Trace audit

W3C trace identityとMachiVerse MessageId/CorrelationId/OperationIdを別identityとして保持する。

TraceIdをworld dedup/orderへ使用しない。

判定: PASS。

## 9. Audit authority audit

二層構造:

- world execution audit: Core P4-04 authoritative history。
- security/management audit: append-only `AuditRecordV1` chain。

同じfactを別authorityとして競合させない。

判定: PASS。

## 10. Audit durability audit

Gateway/management auditはSQLite WAL/FULL、hash chain、single logical writer。

Protected Admin mutationでrequired request auditをdurable化できない場合fail closed可能。

Core world transitionは別audit DB failureだけでrollbackしない。

判定: PASS。

## 11. Retention audit

- general diagnostic log: 14 days default。
- security/management audit: 400 days default。
- world execution history: world lifecycle full retention v1.0。

Audit retention deletionはanchor recordを残す。

判定: PASS。

## 12. Config audit

P4-07追加Config:

- Core `observability.log-retention-days`
- Gateway `audit.retention-days`
- Gateway `audit.query-max-page-size`

`phase4-config-addendum.md`へ反映しP4-03 completion済み。

判定: PASS。

## 13. Performance metric audit

P4-06 required metricsをcanonical `machiverse.*` registryへmappingした。

unitをmetric metadataで保持し、high-cardinality identifierをlabelsへ持ち込まない。

判定: PASS。

## 14. Privacy/security audit

Standard audit required fieldへraw credential/token/cookie/private content/network forensic identityを入れない。

actorはopaque reference、requestはdigest + approved summary field。

判定: PASS。

## 15. Backpressure audit

TRACE/DEBUG diagnosticはsample/drop可能。

ERROR/FATAL/Auditは同じlossy queueへ依存しない。

logging pressureがworld Stepを変更しない。

判定: PASS。

## 16. P4-08 handoff

Acceptance testへ以下を引き渡す。

- W3C context propagation
- secret redaction
- metric cardinality
- audit hash chain
- retention anchor
- audit writer failure fail-closed path
- telemetry exporter failureでworld digest不変
- StateDiagnostic export correlation

## 17. Completion criteria

| criterion | result |
|---|---|
| log schema/registry | PASS |
| metric registry/cardinality | PASS |
| trace propagation/identity separation | PASS |
| audit schema/hash chain | PASS |
| retention policy | PASS |
| Config cross-review | PASS |
| performance metric mapping | PASS |
| security/privacy boundary | PASS |
| unresolved P4-07 blocker | 0 |

## 18. Completion decision

P4-07を`Complete`と判定する。