# 詳細設計 Phase 4: Observability / Logging / Metrics / Audit

Status: In Progress / P4-07  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`  
Predecessors: Phase 1 tracing/determinism, Phase 2 component observability, P4-02 Protocol, P4-04 Persistence, P4-06 Performance

## 1. 目的

MachiVerseの運用観測を、world-authoritative stateから明確に分離しつつ、Step/Operation/Config/protocol/recovery/Admin actionを相関可能にするstructured log、metrics、trace、auditのstable contractへ固定する。

Observability failureだけでworld resultを変更しない。

## 2. Standard telemetry profile

Standard export profile:

```text
Telemetry API/model: OpenTelemetry-compatible
Export protocol: OTLP
Distributed HTTP trace propagation: W3C Trace Context
```

Phase 4設計時の互換性参照:

```text
OpenTelemetry Specification: 1.60 series
OpenTelemetry Semantic Conventions: 1.44 series
W3C Trace Context: Recommendation-compatible traceparent/tracestate
```

MachiVerse固有metric/log/audit tokenは本書を契約正本とし、外部semantic convention version updateでin-place意味変更しない。

## 3. Authority boundary

Telemetryはauthorityではない。

禁止:

- metric値をworld scheduling/order/random入力にする
- log arrival/orderをOperation orderへ使う
- trace idをEntity/Operation identityにする
- audit exporter successをworld durability commitの代替にする
- telemetry samplingでauthoritative history factを失わせる

World-authoritative audit factはP4-04 history/target durable stateが正本であり、telemetry backendは検索/運用projectionである。

## 4. Trace identity mapping

Operational trace identity:

```text
TraceId  := 16 octets / W3C 32 hex
SpanId   := 8 octets / W3C 16 hex
```

Protocol identityとの関係:

```text
MessageId       != TraceId
CorrelationId   != TraceId
OperationId     != TraceId
```

関連付けはattributeで行う。

Standard span attributes:

```text
machiverse.message_id
machiverse.correlation_id
machiverse.operation_id
machiverse.batch_id
machiverse.protocol_id
machiverse.message_type
machiverse.simulation_step
machiverse.master_generation
machiverse.config_generation
```

ID attributeはlog/traceのみで許可し、metric labelへ使用しない。

## 5. Trace propagation

### HTTP/OIDC/WebSocket handshake

- W3C `traceparent` / optional `tracestate`をpropagateできる。
- external untrusted `tracestate`をcredentialとして扱わない。
- `tracestate`へ個人識別情報やsecretを格納しない。

### gRPC/internal protocol

W3C compatible trace context metadataをpropagateできる。

`WireEnvelopeV1`のworld semantic fieldへTraceId/SpanIdを埋め込まない。protocol transport metadataとして扱う。

## 6. Component resource attributes

Telemetry resource minimum:

```text
service.name = machiverse-simulation-core | machiverse-gateway | machiverse-general-view | machiverse-admin-view
service.instance.id = operational opaque instance id
deployment.environment.name = deployment-defined token
machiverse.component = simulation-core | gateway | general-view | admin-view
machiverse.build.version = build semantic version
```

WorldIdはCore/Gateway log contextへ必要時に含められるが、cross-world metricsのunbounded labelへしない。

## 7. Structured log schema

Standard structured log logical schema:

```text
StructuredLogEventV1 {
  event_kind: LogEventKind,
  severity: TRACE|DEBUG|INFO|WARN|ERROR|FATAL,
  observed_at_unix_ns: int64,
  component: Token,
  component_instance_id: Id128,
  message_template_id: Token,
  simulation_step: uint64?,
  world_id: Id128?,
  operation_id: Id128?,
  batch_id: Id128?,
  message_id: Id128?,
  correlation_id: Id128?,
  master_generation: uint64?,
  config_generation: uint64?,
  domain_token: Token?,
  result_code: Token?,
  attributes: ordered map<Token,Scalar>,
  exception: ExceptionSummaryV1?
}
```

`observed_at_unix_ns`はoperational wall clockでありworld orderingへ使用しない。

## 8. Log payload restrictions

Default structured logへ記録禁止:

- password
- access/refresh/id token
- OIDC authorization code
- cookie value
- private key
- secret material
- raw credential headers
- full user-provided private message content
- arbitrary binary world payload

Secret referenceは必要性がある場合もredacted digest/`is_configured`へ変換する。

## 9. LogEventKind registry

### Core

```text
core.lifecycle.starting
core.lifecycle.ready
core.lifecycle.failed-safe
core.step.started
core.step.overrun
core.step.aborted
core.step.committed
core.domain.failed
core.invariant.failed
core.numeric.failed
core.operation.accepted
core.operation.scheduled
core.operation.terminal
core.operation.duplicate
core.operation.payload-mismatch
core.detail.promotion-deferred
core.persistence.commit-failed
core.snapshot.started
core.snapshot.committed
core.snapshot.failed
core.recovery.started
core.recovery.completed
core.recovery.failed
core.migration.started
core.migration.completed
core.migration.failed
```

### Gateway

```text
gateway.lifecycle.ready
gateway.connection.opened
gateway.connection.closed
gateway.connection.rejected
gateway.master.changed
gateway.resync.started
gateway.resync.completed
gateway.operation.rejected
gateway.custody.changed
gateway.queue.backpressure
gateway.auth.login-succeeded
gateway.auth.login-failed
gateway.auth.session-revoked
gateway.authorization.denied
gateway.publication.coalesced
```

### View/Admin

```text
view.connection.state-changed
view.resync.requested
view.operation.delivery-unknown
view.reconcile.applied
view.render.degraded
admin.request.submitted
admin.request.result
admin.config.stale-generation
admin.confirmation.expired
admin.audit.query-failed
```

Unknown future event kind requires registry/version update; arbitrary free-form event kindを生成しない。

## 10. Metric naming

Canonical metric prefix:

```text
machiverse.<component-or-subsystem>.<metric>
```

Unitはmetric metadataで指定し、nameに`_ms`, `_bytes`等unit suffixを付けない。

P4-06記載のconceptual metric名は本書canonical nameへmappingする。

## 11. Core metric registry

| metric | instrument | unit | required attributes |
|---|---|---|---|
| `machiverse.core.step.duration` | histogram | ms | `phase?` |
| `machiverse.core.step.lag` | gauge | ms | none |
| `machiverse.core.step.overrun` | counter | `{overrun}` | none |
| `machiverse.core.domain.cpu` | histogram | ms | `domain` |
| `machiverse.core.domain.wall` | histogram | ms | `domain` |
| `machiverse.core.domain.failure` | counter | `{failure}` | `domain`,`code` |
| `machiverse.core.intent.count` | histogram | `{intent}` | `domain` |
| `machiverse.core.event.count` | histogram | `{event}` | `domain` |
| `machiverse.core.transaction.count` | histogram | `{transaction}` | `kind_class` |
| `machiverse.core.conflict.count` | counter | `{conflict}` | `domain`,`mode` |
| `machiverse.core.candidate.changed_record_count` | histogram | `{record}` | none |
| `machiverse.core.memory.authoritative` | gauge | By | none |
| `machiverse.core.memory.index` | gauge | By | none |
| `machiverse.core.memory.candidate` | gauge | By | none |
| `machiverse.core.operation.pending` | gauge | `{operation}` | `state` |
| `machiverse.core.queue.depth` | gauge | `{item}` | `queue` |
| `machiverse.core.persistence.commit.duration` | histogram | ms | `commit_kind` |
| `machiverse.core.persistence.history.written` | counter | By | `record_class` |
| `machiverse.core.snapshot.duration` | histogram | s | `result` |
| `machiverse.core.snapshot.size` | histogram | By | `compression` |
| `machiverse.core.recovery.duration` | histogram | s | `result` |
| `machiverse.core.publication.written` | counter | By | `kind` |
| `machiverse.core.detail.transition_record_count` | histogram | `{record}` | `direction`,`domain` |

## 12. Gateway metric registry

| metric | instrument | unit | attributes |
|---|---|---|---|
| `machiverse.gateway.connection.active` | gauge | `{connection}` | `protocol` |
| `machiverse.gateway.protocol.error` | counter | `{error}` | `protocol`,`code` |
| `machiverse.gateway.master.generation_change` | counter | `{change}` | none |
| `machiverse.gateway.resync.duration` | histogram | s | `result` |
| `machiverse.gateway.operation.admission` | counter | `{operation}` | `result_class` |
| `machiverse.gateway.custody.count` | gauge | `{operation}` | `state` |
| `machiverse.gateway.retry.count` | counter | `{retry}` | `reason_class` |
| `machiverse.gateway.queue.depth` | gauge | `{item}` | `queue` |
| `machiverse.gateway.publication.written` | counter | By | `client_class`,`kind` |
| `machiverse.gateway.publication.coalesced` | counter | `{publication}` | `client_class` |
| `machiverse.gateway.auth.login` | counter | `{attempt}` | `result_class` |
| `machiverse.gateway.authorization.denied` | counter | `{request}` | `permission_class` |

## 13. View metric registry

```text
machiverse.view.publication.apply.duration     histogram ms
machiverse.view.publication.resync            counter
machiverse.view.operation.pending             gauge
machiverse.view.operation.result              counter[result_class]
machiverse.view.prediction.active             gauge
machiverse.view.reconcile.count               counter[correction_class]
machiverse.view.render.frame.duration         histogram ms
machiverse.view.render.visible_object_count   gauge
machiverse.view.asset.queue_depth             gauge
```

General View metricをworld simulation inputへ戻さない。

## 14. Admin View metric registry

```text
machiverse.admin.target.reachable             gauge[target_class]
machiverse.admin.request.pending              gauge[request_class]
machiverse.admin.request.result               counter[request_class,result_class]
machiverse.admin.config.stale                 counter[target_class]
machiverse.admin.log.query.duration           histogram ms
machiverse.admin.audit.query.duration         histogram ms
machiverse.admin.confirmation.expired         counter
```

## 15. Metric attribute cardinality

Metric labelへ禁止:

- EntityId
- ResidentId
- OperationId
- MessageId
- CorrelationId
- BatchId
- account/user identity
- arbitrary URL/path/query text
- raw ResultCode if addon can create unbounded codes

Allowed bounded label examples:

```text
domain = one of 8 standard domains
protocol = one of 4 standard ProtocolId class tokens
state = fixed lifecycle enum
queue = fixed registry token
result_class = success/rejected/failed/duplicate/pending
```

Standard metric timeseries target:

```text
<= 5,000 active series/component instance
```

hard warning at 10,000 series。

## 16. Trace span registry

Recommended spans:

```text
core.operation.accept
core.operation.schedule
core.step.transition
core.domain.calculate
core.merge
core.persistence.commit
core.snapshot.write
core.recovery.replay
gateway.operation.admit
gateway.batch.forward
gateway.core.submit
gateway.publication.publish
gateway.auth.login
view.publication.apply
admin.config.change
```

High-frequency per-entity/span creationは禁止。domain calculateやbatch単位でaggregateする。

## 17. Trace sampling

Default:

```text
errors/fatal/security audit-related traces: 100%
normal Core Step trace: 1%
normal protocol request trace: 5%
Admin mutation trace: 100%
```

samplingはobservability detailだけを変え、Operation/history/audit persistenceへ影響しない。

## 18. Audit authority model

Auditを二層へ分ける。

### 18.1 World execution audit

Core authoritative history/P4-04が正本。

対象:

- Operation accepted/scheduled/terminal
- simulation Config changed
- transition committed
- snapshot committed
- persistence migration

世界lifecycle中のfull history retention v1.0へ含まれる。

### 18.2 Security / management audit

Gateway/target componentがappend-only audit recordを保持する。

対象:

- authentication result
- session revoke
- authorization deny
- Admin Config read/change
- operational command
- high-impact confirmation
- audit/log export request
- target routing/result

## 19. Audit record schema

```text
AuditRecordV1 {
  audit_sequence: uint64,
  previous_digest: Hash256,
  audit_kind: AuditEventKind,
  observed_at_unix_ns: int64,
  component: Token,
  component_instance_id: Id128,
  actor_ref: OpaqueActorRef?,
  session_ref_digest: Hash256?,
  operation_id: Id128?,
  correlation_id: Id128?,
  target_ref: Token?,
  world_id: Id128?,
  simulation_step: uint64?,
  config_generation: uint64?,
  request_digest: Hash256?,
  result_status: Token,
  result_code: Token,
  approval_evidence_digest: Hash256?,
  summary_fields: ordered map<Token,Scalar>,
  record_digest: Hash256
}
```

`actor_ref`はauthentication system由来のopaque stable referenceであり、credential materialではない。

## 20. Audit digest

```text
AuditRecordDigest = DomainHash(
  "mv.audit-record.v1",
  normalized_record_without_record_digest
)
```

sequence 1 previous digestはZERO256。

Audit chain破損をsilent skipしない。

Audit sequenceはworld orderingへ使用しない。

## 21. AuditEventKind registry

```text
audit.auth.login-success
audit.auth.login-failure
audit.auth.session-created
audit.auth.session-revoked
audit.authorization.denied
audit.admin.config-read
audit.admin.config-change-requested
audit.admin.config-change-applied
audit.admin.config-change-rejected
audit.admin.command-requested
audit.admin.command-completed
audit.admin.simulation-operation-requested
audit.admin.high-impact-confirmed
audit.admin.high-impact-expired
audit.admin.log-query
audit.admin.audit-query
audit.admin.audit-export
audit.gateway.master-role-changed
audit.persistence.recovery-started
audit.persistence.recovery-completed
audit.persistence.recovery-failed
audit.persistence.migration-started
audit.persistence.migration-completed
audit.persistence.migration-failed
audit.snapshot.exported
audit.snapshot.imported
```

## 22. Audit storage

Security/management audit standard storage:

```text
<component-data>/audit/audit.sqlite
```

SQLite profile:

```text
journal_mode = WAL
synchronous = FULL
single logical audit writer
```

Tables:

```text
audit_record(sequence U64BE primary key, digest blob32, previous_digest blob32, kind text, payload blob)
audit_meta(key text primary key, value blob)
```

Audit DBはworld persistence DBと別transaction authority。

world mutation successはaudit DB commit failureだけでrollbackしない。ただしAdmin/security requestのGateway-side request auditがpolicy上requiredな場合、mutation forwarding前のrequest audit commit failureではrequestを`component.unavailable`としてfail closedできる。

## 23. Audit retention

Default:

```text
security/management audit retention = 400 days
general diagnostic log retention = 14 days
world execution history = world lifecycle full retention (P4-04 v1.0)
```

Retention deletionはcomplete audit segment/sequence range単位で行い、retention boundaryにanchor recordを作成する。

```text
AuditRetentionAnchorV1 {
  first_retained_sequence,
  prior_final_digest,
  deleted_through_sequence,
  policy_generation
}
```

policy変更/retention action自体もaudit eventにする。

## 24. Audit Config additions

P4-03 schema 1.0へ追加:

### Simulation Core

```text
observability.log-retention-days uint16 default 14 range 1..365 OPERATIONAL RUNTIME_SAFE
```

### Gateway

```text
audit.retention-days uint16 default 400 range 30..3650 OPERATIONAL RUNTIME_SAFE
audit.query-max-page-size uint16 default 1000 range 100..10000 OPERATIONAL RUNTIME_SAFE
```

Admin View `audit.default-page-size=200`はserver max以下であること。

## 25. Audit privacy / security boundary

Standard auditへ記録しない:

- raw credentials/token/cookie
- authentication secret
- private key
- full private message content
- arbitrary uploaded binary content

Request contentは原則normalized request digest + schema-approved summary fieldで監査する。

IP address、user-agent等のnetwork forensic dataを標準AuditRecordV1 required fieldにしない。必要なdeploymentは別privacy-controlled security log profileとして追加する。

## 26. Exception logging

`ExceptionSummaryV1`:

```text
exception_type: Token/string <= 256 bytes
message_redacted: string <= 2048 bytes
stack_digest: Hash256
stack_text: optional <= 16384 bytes, diagnostic log only
```

Payload/credential内容がexception textへ混入する可能性があるためredaction filterを通す。

## 27. Deterministic diagnostic digest

CoreはP4-01 `StateDiagnosticV1`をauthority divergence診断に使う。

Telemetry exporterは:

```text
state_digest
partition digest summary
basis Step
ConfigDigest
schema registry digest
```

をlog/auditへ出せる。

StateDiagnostic生成自体はtelemetry samplingに依存しない。

## 28. Alert baseline

Recommended operational alerts:

| condition | severity |
|---|---|
| Core failed-safe | critical |
| persistence commit failure | critical |
| history/audit hash mismatch | critical |
| p95 Step >33.3 ms 10s | warning |
| lag >1s | warning/high |
| accepted queue >90% | high |
| Snapshot failure 2 consecutive | high |
| Gateway all Core links unavailable | high |
| repeated auth payload mismatch/invalid | warning/security |
| audit writer unavailable for protected Admin mutation | high |

Alert firing timeをworld resultへ使用しない。

## 29. Log backpressure

Diagnostic log queueはbounded/lossy categoryを許容する。

- TRACE/DEBUG periodic diagnosticはdrop/sample可能。
- ERROR/FATAL/Auditはsame lossy queueへ依存しない。
- audit writerは専用bounded durable path。
- logging failureでCore Stepを通常はblockしない。

## 30. P4-06 cross-review

P4-06 metric requirementsをcanonical metric registryへmapping済み。

- Step duration/lag
- domain CPU/wall
- candidate counts
- memory
- persistence commit/history
- snapshot
- publication
- detail transition
- Gateway queues/bandwidth

label cardinality制約を追加した。

判定: PASS。

## 31. P4-03 cross-review

Config追加:

- Core `observability.log-retention-days=14`
- Gateway `audit.retention-days=400`
- Gateway `audit.query-max-page-size=1000`

既存Gateway `observability.log-retention-days=14`と矛盾なし。

P4-03 completion artifactでstandard example/registryへ反映する。

## 32. P4-08 handoff

P4-08で少なくとも:

- metric cardinality test
- secret redaction test
- W3C context propagation test
- AuditRecord hash-chain test
- audit retention anchor test
- audit writer failure fail-closed Admin path
- telemetry failureでworld digest不変
- worker count変更でStateDiagnostic一致

をacceptanceへ登録する。

## 33. Remaining P4-07 work

- metric/log/audit registry count audit
- P4-03 Config addendum反映
- P4-08 acceptance mapping
- completion review

blocker: なし。