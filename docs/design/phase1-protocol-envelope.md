# 詳細設計 Phase 1: Protocol 共通 envelope・互換性契約

Status: Draft / P1-04 complete  
Tracking: Issue #13  
Parent: `docs/design/phase1-common-foundation-contracts.md`

## 1. 目的

本書は Phase 1 の P1-04 として、MachiVerse の標準 component 間 protocol が共有する message envelope、version / Capability negotiation、result / error、correlation / causation、World Time / generation context の詳細契約を定義する。

対象は次の4境界とする。

- Simulation Core ↔ Gateway
- Gateway ↔ Gateway
- Gateway ↔ General View
- Gateway ↔ Admin View

本書は各 protocol 固有 payload の内容を統一するものではない。各 protocol owner は本共通 envelope を用い、その上で自身の message type と payload schema を定義する。

## 2. 設計原則

1. component 間の契約正本は protocol document とし、shared DTO library を契約正本にしない。
2. protocol version、Capability、message type、identity context を暗黙の connection state のみに依存させない。
3. network arrival time、retry count、MessageId、CorrelationId、sender instance identity を world outcome の入力にしない。
4. world-affecting Operation の end-to-end identity は `OperationId` と immutable payload digest で表し、message transport identity と分離する。
5. Major incompatibility、required Capability 不足、stale Master generation 等を silent degradation で隠さない。
6. user-facing diagnostic text ではなく stable machine code を protocol 分岐の正本とする。
7. common envelope に addon functional payload 用の generic extension slot を設けない。
8. physical transport / compression / serialization は protocol owner の詳細設計事項として残す。ただし本書の field 型・意味・互換性規則を失ってはならない。

## 3. 共通型

### 3.1 `ProtocolId`

```text
ProtocolId := StableToken
```

標準 protocol id は次で固定する。

| Boundary | ProtocolId |
|---|---|
| Core ↔ Gateway | `mv.core-gateway` |
| Gateway ↔ Gateway | `mv.gateway-gateway` |
| Gateway ↔ General View | `mv.gateway-view` |
| Gateway ↔ Admin View | `mv.gateway-admin-view` |

一度公開した `ProtocolId` の意味を変更しない。

### 3.2 `ProtocolVersion`

```text
ProtocolVersion {
  major: uint16,
  minor: uint16
}
```

- backward-incompatible semantic change は major を増加させる。
- same major 内の backward-compatible change は minor を増加させる。
- leading zero 等の text 表現規則は diagnostic / schema tooling 側の責務であり、wire meaning は整数値を正本とする。

### 3.3 message / tracing identity

```text
MessageId           := 128-bit opaque value
CorrelationId       := 128-bit opaque value
ComponentInstanceId := 128-bit opaque value
```

共通規則:

- binary representation は16 octets。
- human-readable canonical form は32桁 lowercase hexadecimal。
- ZERO は invalid / absent sentinel とし、発行値には使用しない。
- 通常は cryptographically secure random source 等、process / connection を跨いで衝突可能性を十分低くする方式で生成する。
- これらの値は simulation ordering、random context、EntityId derivation、business priority に使用しない。
- security credential / authentication proof として扱わない。

### 3.4 `MessageType`

```text
MessageType := StableToken
```

- protocol owner が namespace を管理する。
- 一度公開した token の意味を変更しない。
- incompatible semantic change は新 token または protocol major change とする。
- user-facing label を MessageType として使用しない。

共通予約 token:

```text
protocol.hello
protocol.accept
protocol.reject
protocol.goodbye
protocol.ping
protocol.pong
```

個別 protocol は例えば `state.snapshot`, `operation.submit`, `operation.result` 等を定義できる。

## 4. `ProtocolEnvelopeV1`

全 normal message の共通論理 envelope を次で固定する。

```text
ProtocolEnvelopeV1 {
  envelope_version:       uint8,
  protocol_id:            ProtocolId,
  protocol_version:       ProtocolVersion,
  negotiation_generation: uint32,
  message_type:           MessageType,
  message_id:             MessageId,
  correlation_id:         CorrelationId,
  causation_id:           MessageId | ZERO,
  sender_instance_id:     ComponentInstanceId,
  world_context:          WorldContextV1 | NONE,
  operation_context:      OperationContextV1 | NONE,
  payload:                ProtocolOwnedPayload
}
```

### 4.1 `envelope_version`

Phase 1 標準値は `1`。

- protocol major/minor とは独立した共通 envelope schema version。
- `ProtocolEnvelopeV1` を解釈できない相手と normal communication を継続しない。
- envelope 自体に backward-incompatible change が必要な場合は envelope version を増加する。

### 4.2 `protocol_id`

- connection 上で期待する protocol と一致しなければ reject する。
- `mv.gateway-view` connection で `mv.core-gateway` message を受理しない。

### 4.3 `protocol_version`

- handshake 完了後の normal message では negotiated version と一致させる。
- sender の実装最新版ではなく、当該 connection で合意した version を記録する。
- negotiated minor を超える field / message semantics を無条件送信しない。

### 4.4 `negotiation_generation`

```text
NegotiationGeneration := uint32
```

- initial handshake 前は `0`。
- initial handshake 成功後の normal connection state は `1`。
- safe live renegotiation を protocol 固有に実装する場合、成功ごとに1増加する。
- reconnect は新 connection として generation `1` から開始する。
- stale generation の normal message を current Capability set の message として解釈しない。
- wrap-around を許可しない。上限到達前に connection を再確立する。

Phase 1 の標準挙動では Capability set の変更が必要になった場合は **reconnect を基本**とする。live renegotiation は protocol 固有設計が明示的な quiesce / barrier を定義し、双方が capability `protocol.live-renegotiation.v1` を提供する場合のみ許可する。

### 4.5 `message_id`

- 1つの送信 envelope を trace する identity。
- transport-level retransmissionで完全に同じ envelope を再送する場合は同一 MessageId を維持してよい。
- reconnect/failover等で envelope を再生成する logical retry は new MessageId を発行してよい。
- Operation dedup key として使用しない。

### 4.6 `correlation_id`

- request / response / async result / related event を1つの interaction として追跡する identity。
- interaction root で発行し、関連 message chain へ維持する。
- Gateway hop、Master proxy、Core result routing で可能な限り維持する。
- `OperationId` と同一である必要はない。
- world mutation dedup / ordering に使用しない。

### 4.7 `causation_id`

- 直接この message の生成原因となった protocol message が明確な場合、その `MessageId` を指定する。
- root event、timer-driven health message、直接原因を単一 message にできない場合は ZERO。
- retry / failover の world semantics を causation_id へ依存させない。

### 4.8 `sender_instance_id`

- process / service instance の operational identity。
- process restart で新しい値を発行する。
- Gateway identity、Master identity、user identity、Diver identity の代替ではない。
- authn/authz の証明として信頼しない。
- world outcome の入力にしない。

## 5. `WorldContextV1`

world-related message が共通して必要とする context を次で定義する。

```text
WorldContextV1 {
  world_id:          WorldId,
  basis_step:        SimulationStep | NONE,
  effective_step:    SimulationStep | NONE,
  master_generation: MasterGeneration | NONE,
  config_generation: ConfigGeneration | NONE
}
```

### 5.1 `world_id`

- WorldContext が存在する場合は必須。
- 対象 world を明確化する。
- WorldId mismatch を別 world の current state / Operation として黙って扱わない。

### 5.2 `basis_step`

`basis_step = S` は message payload が authoritative / confirmed `State(S)` またはその派生状態を basis とすることを意味する。

代表例:

- Core → Gateway state snapshot / delta
- Gateway → View confirmed publication state
- resync basis
- state-dependent diagnostic result

`basis_step` を candidate application Step の意味で使用しない。

### 5.3 `effective_step`

`effective_step = S` は world-affecting change が `State(S) -> State(S+1)` transition に参加する **Core が確定した authoritative Step** を意味する。

- Gateway / Master が形成した candidate Step を `effective_step` として表現しない。
- candidate Step / deadline / grace は protocol-owned payload field で明示的に `candidate_*` として定義する。
- Core final assignment 前は `effective_step = NONE`。
- applied Operation result では、該当する場合 authoritative `effective_step` を返す。

### 5.4 `master_generation`

- message の authority / routing / validity が current Master generation に依存する場合に設定する。
- Core-facing final batch、Master handoff、stale-generation result 等では該当 protocol schema が required とできる。
- Master identity そのものの代替ではない。
- stale generation を current output として受理しない。

### 5.5 `config_generation`

- sender component が公開する effective protocol/world behavior の意味が自身の Config generation に依存し、consumer がその generation を識別する必要がある場合に設定する。
- generation の owner は **sender component** とする。
-別 component の Config generation を曖昧に代入しない。必要なら protocol-owned payload に owner を明示した Config context を定義する。
- ConfigGeneration の大小を world priority に使用しない。

## 6. `OperationContextV1`

Operation / Batch を transport する message の共通 identity context を次で定義する。

```text
OperationContextV1 {
  operation_id:             OperationId | NONE,
  operation_payload_digest: Hash256 | NONE,
  batch_id:                 BatchId | NONE
}
```

規則:

- `operation_id` または `batch_id` の少なくとも一方を必須とする。
- single Operation message では `operation_id` と immutable payload digest を原則必須とする。
- batch message では `batch_id` を必須とし、各 Operation entry に個別 OperationId / digest を保持する。
- retry / reconnect / failover で同じ logical Operation を再送する場合 `OperationId` と immutable digest を変更しない。
- `MessageId`、`CorrelationId`、`BatchId`、MasterGeneration を Operation identity の代替にしない。

## 7. immutable Operation payload digest の protocol 境界

P1-02 の `mv.operation-payload.v1` digest へ含める / 含めない protocol 情報を本節で確定する。

### 7.1 digest に含めるもの

Operation の logical meaning を変更する immutable data を含める。

- operation type
- target identity / target selector
- requested semantic content / parameters
- origin が指定し後段で変更してはならない semantic constraint
- origin が指定する deadline / requested time constraint が Operation meaning の一部である場合、その normalized value
- protocol schema が immutable と宣言するその他の field

### 7.2 digest に含めないもの

retry / routing / authority handoff / observation により変化し得る metadata を除外する。

- `ProtocolEnvelopeV1` 全体
- MessageId
- CorrelationId / CausationId
- sender / receiver instance identity
- BatchId
- MasterGeneration
- NegotiationGeneration
- retry count / retry timing
- network arrival timestamp
- hop-local routing context
- Gateway / Master が計算した candidate Step
- Core が確定した final/effective Step
- result / ACK metadata

これにより、Master failover や retry によって envelope が変化しても同じ OperationId / immutable digest を維持できる。

### 7.3 mismatch

同一 OperationId で異なる immutable payload digest を受けた場合:

- duplicate として黙って扱わない。
- `protocol.operation-payload-mismatch` として reject する。
- authoritative world mutation を行わない。
- security / diagnostic event として追跡可能にする。

## 8. protocol handshake

### 8.1 normal message 前提

normal application message の送受信前に protocol handshake を完了する。

handshake 完了前に world-affecting Operation、state publication、Config change 等を normal message として処理しない。

### 8.2 `ProtocolHelloV1`

```text
ProtocolHelloV1 {
  protocol_id: ProtocolId,
  supported_versions: [
    {
      major: uint16,
      min_minor: uint16,
      max_minor: uint16
    }, ...
  ],
  provided_capabilities: [CapabilityId...],
  required_capabilities: [CapabilityId...],
  addons: [AddonDescriptorV1...]
}
```

規則:

- version range は major ごとに contiguous な minor range を表す。
- `min_minor <= max_minor`。
- set / list は StableToken または version tuple の canonical ascending order に normalize する。
- duplicate entry を禁止する。
- unsupported major/minor を相手へ理解可能であるかのように宣言しない。

### 8.3 version selection

双方の supported range から negotiated version を次で決定する。

1. 共通する major を求める。
2. 共通 major が複数ある場合、数値最大の major を選ぶ。
3. 選択 major で minor range の intersection を求める。
4. intersection 内の最大 minor を negotiated minor とする。
5. 共通 version が存在しなければ connection を reject する。

単一 major のみを実装する component では、相手の major が異なる場合が従来要件の「Major mismatch」に該当する。

### 8.4 Capability negotiation

```text
CapabilityId := StableToken
```

Capability の incompatible semantic revision は token を変更し、version を token suffix に含める。

例:

```text
state.delta.v1
operation.batch.v1
protocol.live-renegotiation.v1
```

規則:

- capability token の意味は公開後変更しない。
- `provided_capabilities` は自身が当該 negotiated protocol version で提供可能な機能。
- `required_capabilities` は connection / role / function を安全に成立させるため相手に必須の機能。
- A.required が B.provided の subset でない場合 reject。
- B.required が A.provided の subset でない場合 reject。
- effective optional capability set は双方 provided の intersection。
- required Capability 不足を silent downgrade しない。
- capability 不足により connection 全体ではなく特定 feature のみ無効化できる場合、その feature の message type 自体を送らない。

### 8.5 negotiated result

handshake success は少なくとも次を確定する。

```text
ProtocolAcceptV1 {
  protocol_id,
  negotiated_version,
  negotiation_generation: 1,
  effective_capabilities,
  peer_instance_id
}
```

双方が同じ negotiated version / effective capability set を確認できなければ normal communication へ移行しない。

## 9. addon compatibility metadata

標準 protocol が扱える addon 情報を次に限定する。

```text
AddonVersionV1 {
  major: uint32,
  minor: uint32,
  patch: uint32
}

AddonDependencyV1 {
  addon_id: StableToken,
  min_inclusive: AddonVersionV1 | NONE,
  max_exclusive: AddonVersionV1 | NONE
}

AddonDescriptorV1 {
  addon_id: StableToken,
  version: AddonVersionV1,
  enabled: bool,
  provided_capabilities: [CapabilityId...],
  required_capabilities: [CapabilityId...],
  dependencies: [AddonDependencyV1...]
}
```

規則:

- 標準 protocol は connection safety / compatibility 判定用 metadata としてのみ扱う。
- addon 固有 command、world data、function payload を本 descriptor に格納しない。
- generic opaque extension bytes を addon functional payload 用に設けない。
- addon functional cross-component communication は additional protocol / framework addon の責務とする。
- dependency / required Capability 不整合が安全な接続を阻害する場合、handshake を reject する。

## 10. handshake reject

handshake reject は可能な範囲で構造化 result を返す。

```text
ProtocolRejectV1 {
  code: ErrorCode,
  local_supported_versions,
  peer_offered_versions,
  missing_capabilities: [CapabilityId...],
  incompatible_addons: [StableToken...],
  update_direction: UpdateDirection,
  diagnostic_message: string | NONE
}
```

`UpdateDirection`:

```text
NONE
LOCAL_UPDATE_REQUIRED
PEER_UPDATE_REQUIRED
BOTH_OR_MIGRATION_REQUIRED
UNKNOWN
```

Major / version incompatibility時に、可能なら双方 version と必要な update direction を診断可能にする。

## 11. Minor compatibility rule

same Major の minor update は次を満たす。

- existing required field を削除しない。
- existing field の type / unit / semantic meaning を互換不能に変更しない。
- new field は absent 時に旧 minor と同じ意味になる optional field とする。
- new message type / new semantic behavior は Capability または negotiated minor で送信可否を制御する。
- sender は peer が negotiated minor で理解できない field/message を無条件送信しない。
- receiver が理解できない semantic を silent ignore することを前提にしない。

unknown data を安全に無視できるかどうかは sender が勝手に判断せず、protocol schema の negotiated version 契約で決定する。

## 12. Capability change after connection

### 12.1 default

connection 中に addon enable state、role prerequisite、Master-related requirement 等により effective Capability set を変更する必要が生じた場合、Phase 1 標準は **connection 再確立** とする。

- current connection の意味を途中で silent に変更しない。
- reconnect handshake で新しい Capability set を確定する。
- in-flight Operation は OperationId / BatchId を維持し、個別 protocol の retry/failover semantics に従う。

### 12.2 optional live renegotiation

`protocol.live-renegotiation.v1` を双方が提供し、protocol owner が明示的な message barrier / quiesce algorithm を定義した場合のみ live renegotiation 可能とする。

- apply 前後の NegotiationGeneration を明確に分ける。
- old generation message を new semantics で解釈しない。
- barrier 完了前に new-only message を送らない。
- world outcome を renegotiation timing に依存させない。

Phase 1 共通契約は live barrier の具体 transport algorithm を固定しない。

## 13. request / response / event の correlation

### 13.1 request

request root は new CorrelationId を発行する。

- downstream proxy は同じ correlation を維持できる。
- protocol hop ごとの MessageId は変更してよい。
- world-affecting request は stable OperationId を別途持つ。

### 13.2 response

response / ACK / result は request と同じ CorrelationId を使用する。

- `causation_id` は対応 request message を指せる。
- asynchronous final result が複数 hop を通る場合も correlation を維持する。

### 13.3 event

request 起因でない event は event producer が new CorrelationId を発行する。

ある request から派生した event は元 correlation を維持してよいが、そのことを world causality / ordering の証明として使用しない。

## 14. 共通 result model

protocol request の machine-readable result を次で定義する。

```text
ProtocolResultV1 {
  status: ResultStatus,
  code: ResultCode,
  retry: RetryAdviceV1,
  diagnostic_message: string | NONE,
  details: ProtocolOwnedResultDetails | NONE
}
```

### 14.1 `ResultStatus`

```text
SUCCESS
ACCEPTED
PENDING
NO_CHANGE
DUPLICATE
REJECTED
FAILED
```

意味:

- `SUCCESS`: request の terminal success。
- `ACCEPTED`: request を受理したが terminal effect/result は後続。
- `PENDING`: processing / synchronization 等が継続中。
- `NO_CHANGE`: valid request だが normalized outcome が既存状態と同一。
- `DUPLICATE`: same logical identity が既に処理済みまたは処理中。可能なら original semantic result を返す。
- `REJECTED`: request semantics / permission / protocol / state condition により意図的に適用しない terminal result。
- `FAILED`: accepted processing 中に internal / dependency failure が発生し terminal failure となった状態。

`ACCEPTED` / `PENDING` を final world mutation success と解釈しない。

### 14.2 `ResultCode`

```text
ResultCode := StableToken
ErrorCode  := StableToken
```

- machine behavior は stable code で分岐する。
- diagnostic_message の文言比較を protocol behavior に使用しない。
- protocol owner は自身の code namespace を定義できる。
- common code の意味を incompatible に変更しない。

### 14.3 common result / error codes

標準 common code:

```text
ok
accepted
pending
no-change
duplicate

protocol.malformed
protocol.wrong-protocol
protocol.version-incompatible
protocol.capability-missing
protocol.unknown-message-type
protocol.negotiation-stale
protocol.operation-payload-mismatch

auth.unauthenticated
auth.unauthorized
auth.session-expired
auth.session-revoked

request.invalid
request.conflict
request.stale
request.timeout

world.not-found
world.invalid-state
world.late-operation
world.resync-required

master.stale-generation
config.stale-generation
config.invalid

component.unavailable
component.resyncing
internal.failure
```

個別 protocol は stable token namespace を追加できる。

## 15. retry advice

```text
RetryAdviceV1 {
  disposition: RetryDisposition,
  retry_after_millis: uint32 | NONE
}
```

`RetryDisposition`:

```text
DO_NOT_RETRY
RETRY_SAME_IDENTITY
RECONNECT_THEN_RETRY
RESYNC_THEN_RETRY
RENEGOTIATE_THEN_RETRY
```

規則:

- world-affecting logical Operation の retry は `RETRY_SAME_IDENTITY` 等でも OperationId / immutable digest を維持する。
- Batch retry は同一 logical batch なら BatchId を維持する。
- `retry_after_millis` は operational advisory であり、authoritative application Step / ordering を決める情報ではない。
- retry count / retry timing が world outcome を変えてはならない。

## 16. terminal result / duplicate

world-affecting Operation は end-to-end で terminal semantic result を一意に扱う。

- same OperationId が retry された場合、二重 mutation を行わない。
- 既に terminal result が存在する duplicate には、保持期間内で可能な限り同じ terminal semantic result を再構成する。
- original result が retention policy により失われた場合の exact duplicate response は P1-06 dedup retention で定義する。
- CorrelationId / MessageId が異なる duplicate でも OperationId が同じなら同一 logical Operation として扱う。

## 17. ACK と result の区別

ACK は transport / protocol hop 上の受理状態であり、world effect の terminal result と同一視しない。

例:

- Gateway → Master local batch accepted
- Master → Gateway batch receipt ACK
- Core → Master final batch accepted for processing

これらは「Core authoritative mutation 成功」を必ずしも意味しない。

protocol 固有 schema は ACK の scope を明記する。

## 18. malformed message / parse failure

### 18.1 envelope を解釈できない場合

共通 envelope 自体を安全に parse できない場合、構造化 response を返せるとは限らない。

- normal processing を行わない。
- connection close / protocol-specific recovery を許可する。
- diagnostic log へ parse failure を記録可能にする。
- untrusted raw payload をそのまま user-facing log へ無制限に出力しない。

### 18.2 envelope は解釈できるが payload が invalid な場合

可能なら同一 CorrelationId の `ProtocolResultV1` で `protocol.malformed` または protocol-specific validation code を返す。

partial mutation を行わない。

## 19. authentication / authorizationとの境界

- `sender_instance_id`、MessageId、CorrelationId は authentication credential ではない。
- handshake success は user/session authorization success を意味しない。
- General View / Admin View の login/session/role/permission は各 Gateway-owned protocol payload で扱う。
- unauthorized request は Gateway で downstream forwarding 前に reject する要件を維持する。
- Core は Admin UI role を解釈せず、common world-state invariant を維持する。

## 20. World Context の protocol 別必須例

### 20.1 Core → Gateway state

- `world_id`: required
- `basis_step`: required
- `effective_step`: NONE
- `master_generation`: protocol state に応じ required
- `config_generation`: Core effective behavior を識別する必要がある message で required

### 20.2 Master Gateway → Core final Operation batch

- `world_id`: required
- `basis_step`: protocol schema が要求する場合
- `effective_step`: Core確定前なので NONE
- `master_generation`: required
- OperationContext: BatchId required、各 OperationId/digest required
- candidate Step / deadline: payload に明示

### 20.3 Core → Gateway applied Operation result

- `world_id`: required
- `effective_step`: applied の場合 required
- `master_generation`: result routing / stale判定に必要なら required
- OperationId: required
- CorrelationId: request chain と同一

### 20.4 Gateway → General View confirmed publication

- `world_id`: required
- `basis_step`: required
- `effective_step`: NONE
- prediction/interpolation state を confirmed basis として表現しない

### 20.5 Admin Config change

- request envelope では target component/world context を明示する。
- simulation-affecting Config change の candidate/effective Step semantics は owner component protocol payload で区別する。
- apply result では ConfigGeneration と、simulation-affecting場合は authoritative effective Step を識別可能にする。

## 21. serialization / transport への要求

Phase 1 では physical transport、serialization、compression を固定しない。

各 protocol 詳細設計が具体技術を選択する場合、少なくとも次を満たす。

- 本書の integer width / signedness / optionality を lossless に表現する。
- 128-bit ID を文字列 round-trip に依存せず表現できる。
- schema evolution で negotiated minor を超える semantic を誤送信しない。
- maximum message size / allocation limit を定義できる。
- malformed input に対し bounded resource usage を維持できる。
- transport reconnect で OperationId / BatchId semantics を失わない。
- compression の有無が semantic digest / Operation identity を変えない。
- serialization-specific field ordering を world ordering として使用しない。

`MV-DCBOR-v1` は ID/hash/random の semantic canonicalization 用であり、standard protocol の physical serialization を CBOR に固定するものではない。

## 22. schema management

- protocol owner は各 message type の schema、required/optional field、unit、range、Capability prerequisite を protocol document で管理する。
- generated code を利用してよいが、generated/shared DTO library を複数 component の唯一の契約正本にしない。
- component は相手 implementation の internal type を参照せず contract test 可能にする。
- same Major / different Minor、missing Capability、stale generation、duplicate Operation を independent test で再現可能にする。

## 23. 禁止事項

- MessageId / CorrelationId を Operation dedup key とすること
- network arrival orderを application order とすること
- negotiated minor を超える field/message の無条件送信
- required Capability 不足の silent downgrade
- stale NegotiationGeneration message の current semantics 解釈
- stale MasterGeneration output の current authority 化
- same OperationId + different immutable digest の duplicate 扱い
- ACK を authoritative world success と同一視すること
- diagnostic text の文字列比較による machine behavior
- sender_instance_id を auth credential として扱うこと
- generic addon functional payload slot を common envelope に設けること
- transport retry による OperationId 再採番

## 24. P1-05 / P1-06 へ引き継ぐ事項

P1-04 で common envelope / compatibility contract は確定した。

次の事項は後続で詳細化する。

### P1-05 persistence / replay / recovery

- snapshot consistency boundary
- protocol-visible recovery checkpoint
- state publication continuity token / sequence
- Config / Operation history continuation point
- persisted terminal result boundary

### P1-06 pause / late / retry / dedup

- candidate Step / deadline / grace concrete fields
- Pause queue assignment
- late defer / reject algorithm
- dedup retention window
- duplicate result retention expiry後の応答
- Batch ACK / partial completion / retry state machine

## 25. P1-04 完了条件

P1-04 は次を満たしたため完了とする。

- 共通 ProtocolEnvelopeV1 が定義済み。
- ProtocolId / Version / MessageType / tracing identity が定義済み。
- WorldContextV1 / OperationContextV1 が定義済み。
- immutable Operation digest の protocol inclusion/exclusion が確定済み。
- handshake と negotiated version 選択が確定済み。
- Capability identifier / required/provided 判定が確定済み。
- addon compatibility metadata の標準境界が確定済み。
- reconnect を基本とする Capability change rule が確定済み。
- common result/error/retry taxonomy が確定済み。
- ACK と terminal result の違いが確定済み。
- P1-05 / P1-06 へ残す事項が分離済み。
