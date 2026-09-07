# 詳細設計 Phase 4: Protocol Payload / Message Catalog

Status: Complete / P4-02 payload sub-spec  
Tracking: Issue #16  
Parent: `phase4-protocol-schema.md`  
Auth: `phase4-auth-session-protocol.md`

## 1. 目的

P4-02 common wire envelope上で使用するstandard message typeとpayloadを、4 protocol境界ごとに実装可能なfield/type/requirednessへ固定する。

Domain固有world Operationの内部payload fieldはP4-05で定義するが、wire container、registry、routing、ACK/result、publication、management payloadは本書で固定する。

## 2. Additional common identities

```text
GatewayLogicalId := 128-bit opaque value
SubscriptionId   := 128-bit opaque value
PublicationId    := 128-bit opaque value
AuditRecordId    := 128-bit opaque value
```

- binary: 16 octets。
- ZERO invalid。
- operational identityをworld orderingへ使用しない。

GatewayLogicalIdはComponentInstanceIdと異なり、Gateway deployment logical member identityを表す。process restartでComponentInstanceIdは変わってよいが、GatewayLogicalIdはdeployment memberとして維持できる。

## 3. Common component target

```proto
message ComponentTargetV1 {
  ComponentKindV1 component_kind = 1;
  optional bytes logical_instance_id = 2; // Id128 where applicable
}

enum ComponentKindV1 {
  COMPONENT_KIND_UNSPECIFIED = 0;
  SIMULATION_CORE = 1;
  GATEWAY = 2;
  GENERAL_VIEW = 3;
  ADMIN_VIEW = 4;
}
```

Coreはstandard worldでsingle logical authorityのためlogical_instance_id absentを標準とする。

## 4. Core↔Gateway registration / heartbeat

### 4.1 Gateway registration

Message type:

```text
gateway.register
```

```proto
message GatewayRegisterV1 {
  bytes gateway_logical_id = 1;         // Id128
  bytes component_instance_id = 2;      // Id128
  uint64 last_known_master_generation = 3;
  GatewayReadinessV1 readiness = 4;
}
```

### 4.2 Heartbeat

Message type:

```text
gateway.heartbeat
```

```proto
message GatewayHeartbeatV1 {
  bytes gateway_logical_id = 1;
  bytes component_instance_id = 2;
  GatewayReadinessV1 readiness = 3;
  optional uint64 confirmed_basis_step = 4;
  optional bytes confirmed_continuity_token = 5;
  uint32 peer_connection_count = 6;
  uint32 view_connection_count = 7;
  uint32 admin_connection_count = 8;
}

enum GatewayReadinessV1 {
  GATEWAY_READINESS_UNSPECIFIED = 0;
  STARTING = 1;
  RESYNCING = 2;
  READY = 3;
  DEGRADED = 4;
  DRAINING = 5;
}
```

heartbeat interval/timeoutはP4-03 OPERATIONAL Config。

heartbeat timingをworld outcomeへ使用しない。

### 4.3 Role assignment

Core→Gateway message type:

```text
gateway.role-state
```

```proto
message GatewayRoleStateV1 {
  bytes gateway_logical_id = 1;
  GatewayRoleV1 role = 2;
  uint64 master_generation = 3;
  optional bytes current_master_gateway_id = 4;
}

enum GatewayRoleV1 {
  GATEWAY_ROLE_UNSPECIFIED = 0;
  NON_MASTER = 1;
  MASTER = 2;
  TRANSITION = 3;
}
```

`MASTER`を受ける前にMaster-only final outputを送信しない。

## 5. Gateway peer heartbeat

Gateway↔Gateway message type:

```text
peer.heartbeat
```

```proto
message PeerHeartbeatV1 {
  bytes gateway_logical_id = 1;
  bytes component_instance_id = 2;
  uint64 observed_master_generation = 3;
  GatewayReadinessV1 readiness = 4;
}
```

Peer heartbeatはCore Master authorityの代替ではない。

## 6. Scheduling policy publication

Message type:

```text
world.scheduling-policy
```

```proto
message OperationSchedulingPolicyWireV1 {
  uint64 owner_config_generation = 1;
  uint32 min_lead_steps = 2;
  optional uint32 default_deadline_window_steps = 3;
  uint32 grace_steps = 4;
  LatePolicyWireV1 late_policy = 5;
}

enum LatePolicyWireV1 {
  LATE_POLICY_UNSPECIFIED = 0;
  REJECT = 1;
  DEFER_WITHIN_GRACE = 2;
}
```

## 7. Batch ACK

Gateway↔Gateway ACK message type:

```text
gateway.batch.ack
```

```proto
message GatewayBatchAckV1 {
  bytes batch_id = 1;
  bytes batch_digest = 2;
  BatchWireStatusV1 batch_status = 3;
  repeated BatchEntryAckV1 entries = 4;
  ResultV1 result = 5;
}

enum BatchWireStatusV1 {
  BATCH_STATUS_UNSPECIFIED = 0;
  RECEIVED = 1;
  PARTIAL = 2;
  COMPLETE = 3;
  REJECTED = 4;
}

message BatchEntryAckV1 {
  bytes operation_id = 1;
  GatewayCustodyWireStateV1 custody_state = 2;
  optional OperationLifecycleWireStateV1 core_state = 3;
  optional ResultV1 result = 4;
}

enum GatewayCustodyWireStateV1 {
  CUSTODY_STATE_UNSPECIFIED = 0;
  SOURCE_HELD = 1;
  MASTER_RECEIVED = 2;
  CORE_ACCEPTED = 3;
  TERMINAL = 4;
}
```

`MASTER_RECEIVED`はCore acceptanceを意味しない。

## 8. Core batch result

Message type:

```text
operation.batch.result
```

```proto
message OperationBatchResultV1 {
  bytes batch_id = 1;
  bytes batch_digest = 2;
  BatchWireStatusV1 status = 3;
  repeated OperationBatchEntryResultV1 entries = 4;
  ResultV1 result = 5;
}

message OperationBatchEntryResultV1 {
  bytes operation_id = 1;
  bytes operation_payload_digest = 2;
  OperationLifecycleWireStateV1 lifecycle = 3;
  optional uint64 effective_step = 4;
  ResultV1 result = 5;
}
```

entries canonical wire emissionはOperationId bytewise ascending。

## 9. View subscription

Message type:

```text
world.subscribe
```

```proto
message ViewSubscriptionRequestV1 {
  bytes subscription_id = 1;
  string projection_profile = 2;
  repeated PartitionRecordRefWireV1 spatial_scope_refs = 3;
  repeated string requested_record_kinds = 4;
  bool prefer_delta = 5;
}
```

Standard projection profile:

```text
view.public.v1
view.participant.v1
view.moderation.v1
view.administration.v1
```

Gatewayはsession permissionによりrequested profileをadmit/rejectする。

higher privilege profileをsilent downgradeして機密fieldを曖昧にしない。明示的`REJECTED / auth.unauthorized`を返す。

## 10. Partition record ref wire

```proto
message PartitionRecordRefWireV1 {
  string partition_id = 1;
  bytes record_id = 2;
}
```

- partition_id StableToken。
- record_id Id128 non-zero。

## 11. Projection record envelope

Viewへauthoritative domain recordをそのまま公開しない。

```proto
message ProjectionRecordV1 {
  string record_schema_id = 1;
  SchemaVersionWireV1 record_schema_version = 2;
  bytes record_id = 3;
  uint64 record_revision = 4;
  ProjectionMutationKindV1 mutation_kind = 5;
  bytes payload = 6;
}

enum ProjectionMutationKindV1 {
  PROJECTION_MUTATION_UNSPECIFIED = 0;
  UPSERT = 1;
  DELETE = 2;
}
```

`record_schema_id`はprojection registryで固定し、domain persistence schema idを直接wire public contractとして要求しない。

## 12. Publication projection payload

`StatePublicationChunkV1.payload`のuncompressed contentは次のprotobuf messageとする。

```proto
message ProjectionChunkPayloadV1 {
  bytes subscription_id = 1;
  bytes publication_id = 2;
  uint32 chunk_index = 3;
  repeated ProjectionRecordV1 records = 4;
}
```

records canonical emission order:

```text
(record_schema_id ASCII ascending, record_id bytes ascending)
```

同一record keyを1 publication内で重複させない。

FULL publicationでDELETEを使用しない。

DELTAのみDELETEを使用可能。

## 13. View resync state

Message type:

```text
component.resync-state
```

```proto
message ResyncStateV1 {
  ResyncWireStateV1 state = 1;
  optional uint64 last_confirmed_basis_step = 2;
  optional bytes continuity_token = 3;
  string reason_code = 4;
}

enum ResyncWireStateV1 {
  RESYNC_STATE_UNSPECIFIED = 0;
  SYNCED = 1;
  SUSPECT = 2;
  RESYNCING = 3;
}
```

## 14. Participation binding View projection

Message type:

```text
participation.binding.state
```

```proto
message ParticipationBindingViewV1 {
  optional bytes binding_id = 1;
  optional bytes resident_id = 2;
  ParticipationBindingWireStatusV1 status = 3;
  optional uint64 effective_from_step = 4;
  optional string absence_policy_profile = 5;
}

enum ParticipationBindingWireStatusV1 {
  PARTICIPATION_BINDING_UNSPECIFIED = 0;
  NONE = 1;
  ACTIVE = 2;
  RESIDENT_DECEASED = 3;
  RELEASED = 4;
  SUPERSEDED = 5;
}
```

DiverRef/account identityを他user向けprojectionへ含めない。

## 15. Health query

Admin message type:

```text
component.health.query
component.health.result
```

```proto
message HealthQueryV1 {
  repeated ComponentTargetV1 targets = 1;
  repeated string metric_names = 2;
}

message ComponentHealthV1 {
  ComponentTargetV1 target = 1;
  HealthStateV1 health = 2;
  repeated MetricSampleV1 metrics = 3;
  repeated HealthConditionV1 conditions = 4;
}

enum HealthStateV1 {
  HEALTH_STATE_UNSPECIFIED = 0;
  HEALTHY = 1;
  DEGRADED = 2;
  UNAVAILABLE = 3;
  RESYNCING = 4;
}
```

## 16. Metric sample

```proto
message MetricSampleV1 {
  string name = 1;
  repeated LabelV1 labels = 2;
  MetricValueV1 value = 3;
  uint64 observed_at_unix_millis = 4;
}

message LabelV1 {
  string key = 1;
  string value = 2;
}

message MetricValueV1 {
  oneof value {
    sint64 int_value = 1;
    uint64 uint_value = 2;
    double double_value = 3;
    bool bool_value = 4;
  }
}
```

labelsはkey ASCII ascending、duplicate key禁止、最大16 labels/sample。

metric naming/cardinalityはP4-07で固定する。

## 17. Health condition

```proto
message HealthConditionV1 {
  string code = 1;
  HealthConditionSeverityV1 severity = 2;
  string diagnostic = 3;
}

enum HealthConditionSeverityV1 {
  HEALTH_CONDITION_UNSPECIFIED = 0;
  INFO = 1;
  WARNING = 2;
  ERROR = 3;
  CRITICAL = 4;
}
```

## 18. Structured log query

Admin message type:

```text
component.log.query
component.log.page
```

```proto
message LogQueryV1 {
  repeated ComponentTargetV1 targets = 1;
  optional uint64 from_unix_millis = 2;
  optional uint64 to_unix_millis = 3;
  repeated string event_kinds = 4;
  optional bytes correlation_id = 5;
  optional bytes operation_id = 6;
  optional uint64 basis_step = 7;
  uint32 page_size = 8;
  optional bytes cursor = 9;
}
```

Constraints:

- page_size: 1..1000, default 200。
- cursor: <=256 bytes opaque operational token。
- queryはworld mutationではない。

```proto
message LogPageV1 {
  repeated StructuredLogRecordV1 records = 1;
  optional bytes next_cursor = 2;
}

message StructuredLogRecordV1 {
  bytes record_id = 1;
  uint64 timestamp_unix_millis = 2;
  LogSeverityV1 severity = 3;
  string event_kind = 4;
  ComponentTargetV1 source = 5;
  optional bytes correlation_id = 6;
  optional bytes operation_id = 7;
  optional bytes batch_id = 8;
  optional uint64 simulation_step = 9;
  repeated KeyValueV1 attributes = 10;
  string diagnostic = 11;
}
```

credential/token secretをattributesへ出さない。

## 19. Config read wire

Admin message type:

```text
config.read
config.read.result
```

```proto
message ConfigReadRequestV1 {
  ComponentTargetV1 target = 1;
  repeated string keys = 2;
}

message ConfigReadResultV1 {
  ResultV1 result = 1;
  ComponentTargetV1 target = 2;
  uint64 config_generation = 3;
  bytes config_digest = 4;
  repeated ConfigEntryWireV1 entries = 5;
}

message ConfigEntryWireV1 {
  string key = 1;
  ConfigValueWireV1 effective_value = 2;
  string impact = 3;
  string mutability = 4;
  bool sensitive = 5;
}
```

sensitive=true fieldのvalueをAdmin Viewへ返すかはP4-03公開policyで決定する。secretはdefault非公開。

## 20. Config value wire type

```proto
message ConfigValueWireV1 {
  oneof value {
    bool bool_value = 1;
    sint64 int_value = 2;
    uint64 uint_value = 3;
    double double_value = 4;
    string string_value = 5;
    bytes bytes_value = 6;
  }
}
```

Config TOML typeとのmappingはP4-03で固定する。

## 21. Config change wire

```proto
message ConfigChangeRequestV1 {
  ComponentTargetV1 target = 1;
  bytes operation_id = 2;
  bytes immutable_payload_digest = 3;
  uint64 expected_base_generation = 4;
  repeated ConfigChangeEntryV1 changes = 5;
  optional uint64 requested_effective_step = 6;
}

message ConfigChangeEntryV1 {
  string key = 1;
  ConfigValueWireV1 value = 2;
}

message ConfigChangeResultV1 {
  ResultV1 result = 1;
  uint64 resulting_generation = 2;
  bytes resulting_config_digest = 3;
  optional uint64 effective_step = 4;
}
```

changesはkey ASCII ascending、duplicate key禁止。

simulation-affecting changeのeffective Stepはtarget owner componentが確定する。

## 22. Operational command

Admin message type:

```text
operational.command
```

```proto
message OperationalCommandV1 {
  ComponentTargetV1 target = 1;
  string command_kind = 2;
  optional bytes operation_id = 3;
  optional bytes immutable_payload_digest = 4;
  string payload_schema_id = 5;
  SchemaVersionWireV1 payload_schema_version = 6;
  bytes payload = 7;
}
```

state-changing commandではOperationId/digest required。

command registryはP4-09 implementation work itemへ分解する前にP4-07とcross-reviewする。

## 23. Audit query

Admin message type:

```text
audit.query
audit.page
```

```proto
message AuditQueryV1 {
  optional uint64 from_unix_millis = 1;
  optional uint64 to_unix_millis = 2;
  repeated string audit_event_kinds = 3;
  optional bytes operation_id = 4;
  optional uint64 simulation_step = 5;
  uint32 page_size = 6;
  optional bytes cursor = 7;
}

message AuditPageV1 {
  repeated AuditRecordWireV1 records = 1;
  optional bytes next_cursor = 2;
}

message AuditRecordWireV1 {
  bytes audit_record_id = 1;
  uint64 timestamp_unix_millis = 2;
  string audit_event_kind = 3;
  bytes actor_account_ref = 4;
  optional bytes operation_id = 5;
  optional bytes immutable_payload_digest = 6;
  optional uint64 simulation_step = 7;
  string target_kind = 8;
  string result_code = 9;
  repeated KeyValueV1 attributes = 10;
}
```

Admin Viewへ公開するactor account referenceはopaque internal referenceとし、credentialを含めない。

## 24. Generic key/value diagnostic pair

```proto
message KeyValueV1 {
  string key = 1;
  string value = 2;
}
```

- max key 64 UTF-8 bytes。
- max value 4096 UTF-8 bytes。
- canonical emissionはkey bytewise ascending。
- duplicate key禁止。

world-affecting semantic payloadにKeyValueV1をgeneric extension mechanismとして使用しない。

## 25. Capability registry

Standard P4-02 capabilities:

```text
protocol.protobuf.v1
protocol.state-full.v1
protocol.state-delta.v1
protocol.operation-batch.v1
protocol.operation-status.v1
protocol.auth-bff.v1
protocol.session-generation.v1
protocol.view-projection.v1
protocol.admin-health.v1
protocol.admin-log.v1
protocol.admin-config.v1
protocol.admin-audit.v1
wire.gzip.v1
```

Required baseline:

| protocol | required capabilities |
|---|---|
| `mv.core-gateway` | `protocol.protobuf.v1`, `protocol.state-full.v1`, `protocol.operation-batch.v1`, `protocol.operation-status.v1` |
| `mv.gateway-gateway` | `protocol.protobuf.v1`, `protocol.operation-batch.v1`, `protocol.operation-status.v1`, `protocol.auth-bff.v1` |
| `mv.gateway-view` | `protocol.protobuf.v1`, `protocol.state-full.v1`, `protocol.auth-bff.v1`, `protocol.session-generation.v1`, `protocol.view-projection.v1` |
| `mv.gateway-admin-view` | `protocol.protobuf.v1`, `protocol.auth-bff.v1`, `protocol.session-generation.v1`, `protocol.admin-health.v1` |

`protocol.state-delta.v1`等はoptional negotiated capability。

## 26. Message required capability mapping

| message | required capability |
|---|---|
| `world.state.begin` FULL | `protocol.state-full.v1` |
| `world.state.begin` DELTA | `protocol.state-delta.v1` |
| `operation.batch.submit` | `protocol.operation-batch.v1` |
| `operation.status.query` | `protocol.operation-status.v1` |
| `world.subscribe` | `protocol.view-projection.v1` |
| `config.read` / `config.change` | `protocol.admin-config.v1` |
| `component.log.query` | `protocol.admin-log.v1` |
| `audit.query` | `protocol.admin-audit.v1` |

capability不足messageをgeneric unknown messageとして処理せず`protocol.capability-missing`を返す。

## 27. Standard world Operation registry boundary

P4-02ではOperation wire registrationのexact shapeを固定する。

```text
OperationKindRegistrationV1 {
  operation_kind: StableToken,
  payload_schema: SchemaRefV1,
  target_domain: DomainToken,
  required_permission: StableToken | NONE,
  conflict_scope_recipe: SchemaRefV1,
  scheduling_class: StableToken,
  result_schema: SchemaRefV1
}
```

Individual world OperationKindとpayload fieldはP4-05 domain specificationで登録する。

これをP4-02未完了扱いにはしない。Protocolはregistry-known operationをtransportする契約を固定し、world-domain semanticsはP4-05 ownerとする。

P4-05でunknown OperationKindを登録なしに使用できない。

## 28. Message context matrix

`R`=required, `O`=optional, `N`=must be absent。

| message category | WorldContext | OperationContext |
|---|---:|---:|
| handshake | N | N |
| auth bootstrap/session | N/O | N |
| state publication | R | N |
| world subscription/resync | R | N |
| Operation submit/result | R | R |
| batch transfer/result | R | R |
| component health/log | O | N |
| Config read | O | N |
| Config change | O/R by impact | R |
| operational command read-only | O | N |
| operational command state-changing | O/R | R |
| audit query | O | N |

## 29. Slow consumer / backpressure semantics

### View publication

- intermediate publicationはcoalesce/drop可能。
- latest confirmed continuityを維持する。
- terminal Operation resultと同一lossy queueを使用しない。

### Admin log/metrics

- pagination/stream sample dropを許容する。
- audit recordをhigh-volume diagnostic drop policyへ入れない。

### Operation/custody

- queue pressureでaccepted/custody-held Operationをsilent dropしない。
- capacity不足はnew request admission時にstable errorを返す。

## 30. Protocol error additions

```text
protocol.subscription-invalid
protocol.projection-unsupported
protocol.chunk-mismatch
protocol.chunk-digest-mismatch
protocol.cursor-invalid
protocol.command-unsupported
protocol.target-unsupported
protocol.gateway-not-ready
protocol.master-not-ready
```

WorldStateを変更せずmessage/request単位rejectする。

## 31. Acceptance criteria

- Core/Gateway Master roleをexplicit generation付きmessageで表現できる。
- heartbeat arrival timingをworld orderingへ使用しない。
- Master receipt ACKとCore acceptanceを区別できる。
- FULL/DELTA projectionをchunked protobufで再構築できる。
- delta DELETEとFULL semanticsを区別できる。
- View projection schemaをauthoritative persistence schemaから分離できる。
- Admin health/log/config/auditをgeneric internal object参照なしに取得できる。
- capability不足messageを明示rejectできる。
- world Operation payload semanticsをowner domain P4-05へ分離しつつwire registryを固定できる。
- slow View/log consumerがOperation custodyをlossyにしない。

## 32. P4-02 remaining cross-check

P4-02 completion reviewでは次をcross-checkする。

- `phase4-protocol-schema.md` common envelopeとのfield/schema整合
- `phase4-auth-session-protocol.md` login/session message registry整合
- `docs/protocols/*` の未決定事項をP4-02/P4-03/P4-05/P4-07へ明示的に引き渡したこと
- unknown standard message/payload generic pass-throughが残っていないこと
