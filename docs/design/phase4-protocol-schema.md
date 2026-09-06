# 詳細設計 Phase 4: Protocol正式Schema / Error Catalog

Status: In Progress / P4-02  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`  
Predecessors: `phase1-protocol-envelope.md`, `docs/protocols/*`, `phase4-core-data-structures.md`, `phase4-domain-state-registry.md`

## 1. 目的

4つのstandard component protocolについて、実装者が独自解釈せずwire encoder/decoder、validation、request routing、compatibility testを実装できるexact schemaを定義する。

対象ProtocolId:

```text
mv.core-gateway
mv.gateway-gateway
mv.gateway-view
mv.gateway-admin-view
```

Phase 1で確定したlogical envelope、version/capability、Operation identity、WorldContext、Result/Error semanticsを変更しない。

## 2. Wire technology decision

### 2.1 Serialization

Standard Protocol v1のwire serializationは **Protocol Buffers proto3** とする。

理由:

- C# / browser JavaScript双方でstableな実装がある。
- field numberによるbackward-compatible schema evolutionが可能。
- binary sizeが比較的小さい。
- `.proto`をcontract artifactとして各componentが独立code generationでき、shared runtime DTO assemblyを要求しない。
- Phase 1の`MV-DCBOR-v1`は意味digest用であり、wire serializationをCBORへ固定していないため競合しない。

禁止:

- protobuf wire bytesそのものをOperation immutable digestやstate diagnostic digestの正本にすること。
- protobuf map iteration orderをauthoritative canonical orderにすること。
- generated C# assemblyをcontract正本にすること。

Contract source of truthはPhase 4 protocol schemaとversion-controlled `.proto` definitionとする。

### 2.2 Internal transport

次の2境界はHTTP/2上のgRPC bidirectional streamingをstandard transportとする。

```text
mv.core-gateway
mv.gateway-gateway
```

logical service:

```text
rpc Connect(stream WireEnvelopeV1) returns (stream WireEnvelopeV1)
```

- TLSをproduction defaultとする。
- request/response unary endpointへworld-affecting semanticsを分散しない。
- stream reconnect時はprotocol handshakeを再実行する。

### 2.3 Web transport

次の2境界はTLS上のWebSocket binary messageをstandard transportとする。

```text
mv.gateway-view
mv.gateway-admin-view
```

WebSocket 1 binary message = 1 serialized `WireEnvelopeV1`。

text frameでnormal protocol messageを送信しない。

標準path:

```text
/ws/v1/view
/ws/v1/admin
```

HTTP authentication bootstrapを利用する場合も、normal protocol auth/session semanticsはprotocol messageとして明示する。

### 2.4 Compression

Protocol v1のrequired compressionは`NONE`。

optional:

```text
wire.gzip.v1
```

Capability双方提供時のみpayload-level GZIPを使用できる。

WebSocket `permessage-deflate`やgRPC transport compressionはOPERATIONAL optimizationとして許可するが、Protocol Capabilityやworld semanticsへ影響させない。

## 3. Wire safety limits

Protocol major 1で固定するstructural hard limit:

| Item | Limit |
|---|---:|
| serialized `WireEnvelopeV1` | 8 MiB |
| `message_type` / StableToken | 64 ASCII chars |
| diagnostic human text | 4096 UTF-8 bytes |
| generic string field default | 4096 UTF-8 bytes |
| capability count | 1024 |
| addon descriptor count | 1024 |
| causality refs / message | 256 |
| result detail entries | 256 |
| batch operations / envelope | 4096 |
| publication chunk payload | 1 MiB uncompressed |
| publication chunks / publication | 65535 |
| nested protocol-owned message depth | 32 |

超過時:

```text
REJECTED / protocol.limit-exceeded / DO_NOT_RETRY
```

大きなstate publicationはchunkingを使用し、8 MiB envelope limitを変更しない。

## 4. JavaScript numeric mapping

Browser implementationで次の`uint64`値をECMAScript `Number`へ変換してはならない。

```text
SimulationStep
HistorySequence
MasterGeneration
ConfigGeneration
uint64 revision
```

Web implementationは`BigInt`またはlossless uint64 wrapperを使用する。

53-bitを超える値をNumberへroundしてから再encodeしたmessageは`protocol.field-out-of-range`として扱う。

## 5. Common scalar validation

```text
Id128 := bytes length exactly 16
Hash256 := bytes length exactly 32
StableToken := ASCII [a-z0-9][a-z0-9._/-]{0,63}
```

ZERO `Id128`はfield schemaが`NONE sentinel`と明示した場合だけ許可する。

`optional bytes`でNONEを表現できるfieldではZERO sentinelを使用せずpresenceを使用する。

## 6. `WireEnvelopeV1`

Normative protobuf logical schema:

```proto
message WireEnvelopeV1 {
  uint32 envelope_version = 1;              // required semantic value: 1
  string protocol_id = 2;                   // StableToken
  ProtocolVersionV1 protocol_version = 3;
  uint32 negotiation_generation = 4;
  string message_type = 5;                  // StableToken
  bytes message_id = 6;                     // Id128, non-zero
  bytes correlation_id = 7;                 // Id128, non-zero
  optional bytes causation_id = 8;          // Id128
  bytes sender_instance_id = 9;             // Id128, non-zero
  optional WorldContextWireV1 world_context = 10;
  optional OperationContextWireV1 operation_context = 11;
  string payload_schema_id = 12;            // StableToken
  SchemaVersionWireV1 payload_schema_version = 13;
  CompressionKindV1 payload_compression = 14;
  bytes payload = 15;                       // protocol-owned protobuf message bytes
}
```

Field number 1..15はProtocol Envelope major 1でreserveし、別意味へ再利用しない。

### 6.1 Envelope validation order

1. serialized size limit
2. protobuf structural decode
3. envelope_version
4. ProtocolId
5. negotiated ProtocolVersion
6. NegotiationGeneration
7. StableToken / fixed-length identity validation
8. payload schema id/version compatibility
9. decompression limit
10. message type ↔ payload schema registry match
11. WorldContext / OperationContext requiredness
12. protocol-specific semantic validation

validation failureでpayload handlerを呼ばない。

## 7. Version messages

```proto
message ProtocolVersionV1 {
  uint32 major = 1; // validated <= 65535
  uint32 minor = 2; // validated <= 65535
}

message SchemaVersionWireV1 {
  uint32 major = 1; // <= 65535
  uint32 minor = 2; // <= 65535
}
```

protobuf `uint32`を使用するがapplication validationで`uint16`範囲へ制限する。

## 8. World context

```proto
message WorldContextWireV1 {
  bytes world_id = 1;                       // Id128 non-zero
  optional uint64 basis_step = 2;
  optional uint64 effective_step = 3;
  optional uint64 master_generation = 4;
  optional uint64 config_generation = 5;
}
```

- submit前candidate Stepを`effective_step`へ入れない。
- confirmed publicationは`basis_step` required。
- Master-authority messageは`master_generation` required。

## 9. Operation context

```proto
message OperationContextWireV1 {
  optional bytes operation_id = 1;          // Id128
  optional bytes operation_payload_digest = 2; // Hash256
  optional bytes batch_id = 3;              // Id128
}
```

validation:

- operation_idまたはbatch_idの少なくとも一方required。
- operation_id presentならoperation payload messageではdigest required。
- status query等、payloadをtransportしないmessageはdigest optionalとschemaで明示できる。

## 10. Handshake schema

Handshakeはnormal negotiated envelope前のbootstrap messageとして同じprotobuf framingを使用する。

initial helloでは:

```text
negotiation_generation = 0
protocol_version = 0.0
```

を許可する。

```proto
message ProtocolHelloV1 {
  string protocol_id = 1;
  repeated SupportedVersionRangeV1 supported_versions = 2;
  repeated string provided_capabilities = 3;
  repeated string required_capabilities = 4;
  repeated AddonDescriptorWireV1 addons = 5;
}

message SupportedVersionRangeV1 {
  uint32 major = 1;
  uint32 min_minor = 2;
  uint32 max_minor = 3;
}

message ProtocolAcceptV1 {
  ProtocolVersionV1 negotiated_version = 1;
  uint32 negotiation_generation = 2; // initial = 1
  repeated string effective_optional_capabilities = 3;
}

message ProtocolRejectV1 {
  string code = 1;
  string diagnostic = 2;
}
```

Repeated set fieldsはStableToken ASCII ascendingへnormalizeしduplicate禁止。

## 11. Common result schema

```proto
message ResultV1 {
  ResultStatusV1 status = 1;
  string code = 2;
  RetryAdviceV1 retry_advice = 3;
  string diagnostic = 4;
  repeated ResultDetailV1 details = 5;
}

enum ResultStatusV1 {
  RESULT_STATUS_UNSPECIFIED = 0;
  SUCCESS = 1;
  ACCEPTED = 2;
  PENDING = 3;
  NO_CHANGE = 4;
  DUPLICATE = 5;
  REJECTED = 6;
  FAILED = 7;
}

enum RetryAdviceV1 {
  RETRY_ADVICE_UNSPECIFIED = 0;
  DO_NOT_RETRY = 1;
  RETRY_SAME_IDENTITY = 2;
  RECONNECT_THEN_RETRY = 3;
  RESYNC_THEN_RETRY = 4;
  RENEGOTIATE_THEN_RETRY = 5;
}

message ResultDetailV1 {
  string key = 1;
  string value = 2;
}
```

`diagnostic`はhuman-readableでmachine branchに使用しない。

## 12. Error catalog common registry

### 12.1 Protocol

| code | default status | retry |
|---|---|---|
| `protocol.malformed` | REJECTED | DO_NOT_RETRY |
| `protocol.wrong-protocol` | REJECTED | DO_NOT_RETRY |
| `protocol.version-incompatible` | REJECTED | RENEGOTIATE_THEN_RETRY |
| `protocol.capability-missing` | REJECTED | RENEGOTIATE_THEN_RETRY |
| `protocol.unknown-message-type` | REJECTED | DO_NOT_RETRY |
| `protocol.negotiation-stale` | REJECTED | RENEGOTIATE_THEN_RETRY |
| `protocol.operation-payload-mismatch` | REJECTED | DO_NOT_RETRY |
| `protocol.batch-payload-mismatch` | REJECTED | DO_NOT_RETRY |
| `protocol.limit-exceeded` | REJECTED | DO_NOT_RETRY |
| `protocol.field-out-of-range` | REJECTED | DO_NOT_RETRY |
| `protocol.missing-required` | REJECTED | DO_NOT_RETRY |
| `protocol.invalid-id` | REJECTED | DO_NOT_RETRY |
| `protocol.schema-unsupported` | REJECTED | RENEGOTIATE_THEN_RETRY |
| `protocol.payload-schema-mismatch` | REJECTED | DO_NOT_RETRY |
| `protocol.continuity-mismatch` | REJECTED | RESYNC_THEN_RETRY |

### 12.2 Auth / request / world / component

Existing Phase 1 codes remain stable:

```text
auth.unauthenticated
auth.unauthorized
auth.session-expired
auth.session-revoked
request.invalid
request.conflict
request.stale
request.timeout
operation.accepted
operation.scheduled
operation.result-details-expired
world.not-found
world.invalid-state
world.late-operation
world.deadline-exceeded
world.late-deferred
world.pause-deferred
world.resync-required
master.stale-generation
config.stale-generation
config.invalid
batch.partial
batch.complete
component.unavailable
component.resyncing
internal.failure
```

新codeを既存codeの意味変更に使わない。

## 13. Typed payload registry

`payload`は任意dynamic extensionではない。

message typeごとにexactly 1つのstandard payload schemaをregistryで固定する。

```text
MessageSchemaRegistryEntryV1 {
  protocol_id,
  message_type,
  payload_schema_id,
  payload_schema_version,
  world_context_policy,
  operation_context_policy,
  direction,
  min_protocol_version,
  required_capabilities
}
```

unknown schema idやmessage type/schema不一致をgeneric pass-throughしない。

Addon functional payloadはstandard registryへ追加せずadditional protocolを使用する。

## 14. State publication common schema

```proto
message StatePublicationV1 {
  bytes publication_id = 1;                 // Id128
  PublicationKindV1 kind = 2;
  bytes state_continuity_token = 3;         // fixed length defined by P4-04
  optional bytes base_state_continuity_token = 4;
  uint32 chunk_count = 5;
  bytes projection_schema_digest = 6;       // Hash256
}

enum PublicationKindV1 {
  PUBLICATION_KIND_UNSPECIFIED = 0;
  FULL = 1;
  DELTA = 2;
}

message StatePublicationChunkV1 {
  bytes publication_id = 1;
  uint32 chunk_index = 2;                    // zero-based
  uint32 chunk_count = 3;
  bytes uncompressed_payload_digest = 4;     // Hash256
  CompressionKindV1 compression = 5;
  bytes payload = 6;
}
```

Rules:

- FULL: base token absent。
- DELTA: base token required。
- chunk_index < chunk_count。
- chunk_count一致しないmixを拒否。
- 全chunk digest検証完了前にconfirmed publicationとしてinstallしない。
- delta base mismatchは`protocol.continuity-mismatch`。
- chunk arrival orderはassembly convenienceでありworld semanticsではない。

## 15. Operation scheduling wire schema

```proto
message OperationSchedulingAdmissionWireV1 {
  uint64 admission_basis_step = 1;
  uint64 scheduling_policy_generation = 2;
  optional uint64 requested_not_before_step = 3;
  optional uint64 requested_deadline_step = 4;
}

message CandidateSchedulingWireV1 {
  uint64 candidate_step = 1;
}
```

Admissionはimmutable Operation digestへ含める。

Candidateは含めない。

## 16. Standard Operation wire container

```proto
message StandardOperationV1 {
  bytes operation_id = 1;                    // Id128
  bytes immutable_payload_digest = 2;        // Hash256
  string operation_kind = 3;                 // StableToken registry entry
  OperationSchedulingAdmissionWireV1 admission = 4;
  optional CandidateSchedulingWireV1 candidate = 5;
  string operation_payload_schema_id = 6;
  SchemaVersionWireV1 operation_payload_schema_version = 7;
  bytes operation_payload = 8;
}
```

`operation_payload_schema_id`は`operation_kind` registryとexact match required。

standard protocolでunknown operation kindをCoreへforwardしない。

## 17. Batch schema

```proto
message OperationBatchV1 {
  bytes batch_id = 1;                        // Id128
  bytes batch_digest = 2;                    // Hash256
  string batch_kind = 3;
  repeated StandardOperationV1 operations = 4;
}
```

Canonical BatchDigest計算時、`operations`はschema指定logical orderへnormalizeする。

wire arrival/list orderをdigest semanticsとして暗黙利用しない。

## 18. Operation lifecycle result

```proto
message OperationStatusQueryV1 {
  bytes operation_id = 1;
}

message OperationStatusResultV1 {
  bytes operation_id = 1;
  OperationLifecycleWireStateV1 state = 2;
  optional bytes operation_payload_digest = 3;
  optional uint64 effective_step = 4;
  optional ResultV1 terminal_result = 5;
  bool rich_result_details_available = 6;
}

enum OperationLifecycleWireStateV1 {
  OPERATION_STATE_UNSPECIFIED = 0;
  UNKNOWN = 1;
  ACCEPTED = 2;
  SCHEDULED = 3;
  TERMINAL = 4;
}
```

## 19. `mv.core-gateway` initial message registry

Protocol version initial target: `1.0`。

| direction | message_type | payload schema | context |
|---|---|---|---|
| G→C | `operation.batch.submit` | `protocol.operation-batch` | world + batch operation context |
| C→G | `operation.batch.result` | `protocol.operation-batch-result` | world + batch context |
| G→C | `operation.status.query` | `protocol.operation-status-query` | world |
| C→G | `operation.status.result` | `protocol.operation-status-result` | world |
| C→G | `world.state.begin` | `protocol.state-publication` | basis_step required |
| C→G | `world.state.chunk` | `protocol.state-publication-chunk` | basis_step required |
| G→C | `world.state.resync-request` | `protocol.state-resync-request` | world |
| C→G | `world.scheduling-policy` | `protocol.scheduling-policy` | world + config generation |
| C→G | `master.generation.changed` | `protocol.master-generation` | master generation required |
| C→G | `component.health` | `protocol.component-health` | optional world |

P4-02後半でheartbeat/election physical registryとbatch result fieldsを確定する。

## 20. `mv.gateway-gateway` initial message registry

| direction | message_type | payload schema | context |
|---|---|---|---|
| G→M | `gateway.batch.transfer` | `protocol.operation-batch` | world + master generation |
| M→G | `gateway.batch.ack` | `protocol.gateway-batch-ack` | world + master generation |
| M→G | `operation.result.route` | `protocol.operation-status-result` | world + operation context |
| G↔M | `operation.status.forward` | `protocol.operation-status-query` | world |
| G→M | `auth.login.proxy` | `protocol.auth-login-request` | none/world optional |
| M→G | `auth.login.result` | `protocol.auth-login-result` | none/world optional |
| M→G | `master.state` | `protocol.master-generation` | master generation |

Master receipt ACKとCore accepted resultを別message semanticsとして維持する。

## 21. `mv.gateway-view` initial message registry

| direction | message_type | payload schema | context |
|---|---|---|---|
| V→G | `auth.login` | `protocol.auth-login-request` | none |
| G→V | `auth.login.result` | `protocol.auth-login-result` | none |
| G→V | `auth.session.changed` | `protocol.auth-session-state` | none |
| V→G | `world.subscribe` | `protocol.view-subscription-request` | world |
| G→V | `world.state.begin` | `protocol.state-publication` | basis_step required |
| G→V | `world.state.chunk` | `protocol.state-publication-chunk` | basis_step required |
| V→G | `world.state.resync-request` | `protocol.state-resync-request` | world |
| V→G | `operation.submit` | `protocol.standard-operation` | operation context |
| G→V | `operation.result` | `protocol.operation-status-result` | operation context |
| V→G | `participation.binding.request` | `protocol.standard-operation` | world + operation context |
| G→V | `participation.binding.state` | `protocol.participation-binding-view` | basis_step |
| G→V | `component.resync-state` | `protocol.resync-state` | world |

Prediction/interpolation local stateはstandard confirmed publication schemaへ載せない。

## 22. `mv.gateway-admin-view` initial message registry

| direction | message_type | payload schema | context |
|---|---|---|---|
| A→G | `auth.login` | `protocol.auth-login-request` | none |
| G→A | `auth.login.result` | `protocol.auth-login-result` | none |
| A→G | `component.health.query` | `protocol.health-query` | optional world |
| G→A | `component.health.result` | `protocol.component-health` | optional world |
| A→G | `component.log.query` | `protocol.log-query` | optional world |
| G→A | `component.log.page` | `protocol.log-page` | optional world |
| A→G | `config.read` | `protocol.config-read-request` | optional world |
| G→A | `config.read.result` | `protocol.config-read-result` | optional world |
| A→G | `config.change` | `protocol.config-change-request` | operation context |
| G→A | `config.change.result` | `protocol.config-change-result` | operation context |
| A→G | `operation.submit` | `protocol.standard-operation` | operation context |
| G→A | `operation.result` | `protocol.operation-status-result` | operation context |
| A→G | `operational.command` | `protocol.operational-command` | operation context if state-changing |
| G→A | `audit.query` | `protocol.audit-page` | optional world |

P4-03/P4-07確定schemaを本registryへ参照する。

## 23. Resync request schema

```proto
message StateResyncRequestV1 {
  bytes world_id = 1;
  optional uint64 client_basis_step = 2;
  optional bytes client_continuity_token = 3;
  ResyncPreferenceV1 preference = 4;
}

enum ResyncPreferenceV1 {
  RESYNC_PREFERENCE_UNSPECIFIED = 0;
  CONTINUE_IF_POSSIBLE = 1;
  FORCE_FULL = 2;
}
```

serverがcontinuation safetyを証明できない場合はFULLへfallbackする。

## 24. Connection state machine

```text
TRANSPORT_CONNECTED
 -> HANDSHAKING
 -> NEGOTIATED
 -> AUTHENTICATING          // external boundary where required
 -> SYNCING                 // world subscription where required
 -> READY
 -> DRAINING
 -> CLOSED
```

Internal component boundaryはAUTHENTICATINGをcomponent credential/bootstrapへ置換できるが、normal protocol message前のidentity/authorization checkを省略しない。

`READY`前にworld-affectingOperationをnormal handlerへ渡さない。

## 25. Malformed/stale handling

- malformed protobuf: connection-scoped strike + message reject。
- wrong ProtocolId: immediate connection reject。
- unsupported envelope version: immediate connection reject。
- stale NegotiationGeneration: message reject。繰返しはconnection close可。
- stale MasterGeneration: world-affecting message reject、connection自体は必ずしもcloseしない。
- unknown optional protobuf field: proto3 ruleに従いpreserve/ignore可能。ただしそのfieldがrequired capability semanticならCapability negotiationで防ぐ。
- unknown message type: reject、generic passthrough禁止。

## 26. Schema compatibility rule

Same Protocol majorでpayload schema minor updateを行う条件:

- existing field number/meaning/typeを変更しない。
- field numberを再利用しない。
- optional field additionだけを基本とする。
- new enum valueをold receiverが受けた場合のfallbackをschemaで明示する。
- required semantic additionはCapability追加またはProtocol major update。

field removalはreserveし、再利用しない。

## 27. Security boundary

Protocol schemaはcredential technologyをまだ固定しないが、次を固定する。

- MessageId / ComponentInstanceIdはcredentialではない。
- auth tokenをWorldState、Operation immutable digest、state publicationへ含めない。
- auth secretをstructured diagnostic detailへ返さない。
- external WebSocketはTLS必須production profile。
- internal gRPCもproduction profileではmutual authentication可能なTLSを使用する。

Concrete credential/session schemaはP4-02後半またはsecurity implementation issueへ落とす。

## 28. P4-02 current acceptance status

確定済み:

- serialization technology
- internal/external transport family
- binary frame rule
- envelope field numbers / type / fixed-length validation
- hard structural limits
- handshake/version/capability common schema
- Result/Error common schema
- state publication chunking schema
- Operation/Batch/status common schema
- 4 protocol initial message registry
- malformed/stale/compatibility base rule

未確定:

- exact auth credential/session payload
- exact role/permission matrix
- all Core/Gateway heartbeat/election physical messages
- all batch ACK/result detail fields
- View subscription/projection field registry
- Admin health/log/config/audit exact field registry
- standard OperationKind payload catalog
- publication projection record schema
- message-by-message required capability matrix

P4-02は上記未確定を解消するまでIn Progressとする。
