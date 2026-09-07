# Standard Protocol v1 Protobuf Schema

Status: Canonical wire declaration / Standard Protocol v1

## 1. Scope

このdirectoryの`.proto`はMachiVerse Standard Protocol v1のversion-controlled wire contractです。

```text
common.proto   common envelope / handshake / result / publication / operation

auth.proto     browser auth/session/login proxy payload

payloads.proto Gateway/View/Admin standard payload
```

MessageTypeとpayload typeのexact mappingは `message-registry-v1.md` を参照します。

## 2. Source-of-truth boundary

`.proto` が正本:

- protobuf package/import
- message/enum/service symbol
- field number
- protobuf wire type
- enum numeric value
- gRPC service signature

Architecture/Protocol/design文書が正本:

- StableToken lexical constraint
- Id128/Hash256 length/non-zero rule
- field requirednessのsemantic rule
- WorldContext/OperationContext requiredness
- ordering/canonicalization
- Capability/version negotiation
- authn/authz
- retry/dedup/custody/durability
- deterministic digest semantics
- security limit

protobuf自体にrequired fieldを持たせずproto3 presenceとapplication validationを併用します。

## 3. Files

### `common.proto`

- `MachiVerseInternalProtocolV1.Connect`
- `WireEnvelopeV1`
- handshake/version/capability types
- Result/Error transport types
- state publication types
- Operation/Batch/status types
- resync request

### `auth.proto`

- `AuthDomainWireV1`
- login begin/result/proxy/assertion
- session attach/state/query/revoke
- verified identity assertion

Internal component credential/private keyはprotobufへ載せません。Core/Gateway production authenticationはmutual TLSを使用します。

### `payloads.proto`

- Gateway registration/heartbeat/role
- peer heartbeat
- scheduling policy
- Gateway custody/Batch ACK/result
- View subscription/projection/resync
- Participation projection
- health/metrics/log
- Config read/change
- operational command
- audit query/page
- Administration View high-impact action prepare/plan/confirm/commit/result
- Addon inventory/catalog/action management metadata

Addon package bytesやAddon-specific functional payloadはStandard Protocol payloadとして定義しません。

## 4. Administration View Phase 0 additions

Issue #38でStandard Protocol v1へ次をcanonical化しました。

### Observability / Config / Audit additive fields

- log severity/MasterGeneration filter
- structured log MasterGeneration context
- Config redaction/validation metadata
- audit session/correlation/plan/effective boundary/resulting generation context

既存field number/meaningを変更せずadditive fieldとして追加しています。

### High-impact action

```text
AdminActionPrepareV1
AdminActionPlanV1
AdminActionConfirmV1
AdminActionConfirmationV1
AdminActionCommitV1
AdminActionResultV1
```

Plan/confirmation artifactはOperationIdとは別identityです。requiredness、expiry、single-use、digest coverageは `../gateway-admin-view-phase0.md` を正本とします。

### Addon management

```text
AddonInventoryQueryV1
AddonInventoryItemV1
AddonInventoryResultV1
AddonCatalogQueryV1
AddonCatalogItemV1
AddonCatalogPageV1
AddonActionIntentV1
```

これらはmanagement/safety metadataのみを扱います。

## 5. Enum naming

Markdown設計では読みやすさのため `READY`, `FULL`, `REJECTED` 等のsemantic short nameを記載する場合があります。

Code generation上のcanonical enum symbolは`.proto`に記載したprefix付きidentifierです。

例:

```text
semantic FULL  -> PUBLICATION_KIND_FULL
semantic READY -> GATEWAY_READINESS_READY
```

Numeric valueは`.proto`を正本とし、published valueを再利用・renumberしません。

## 6. Numeric mapping

Browser側で`uint64`をECMAScript `Number`へlossy conversionしません。

対象例:

- SimulationStep
- MasterGeneration
- ConfigGeneration
- session generation
- Addon inventory generation
- revision
- timestamp where exact integer representation is required by schema

`BigInt`またはlossless uint64 wrapperを使用します。

## 7. Binary scalar validation

Application validation:

```text
Id128   = exactly 16 bytes
Hash256 = exactly 32 bytes
```

ZERO Id128はschema/designがNONE sentinelを明示した場合以外invalidです。

Administration ViewではPlanId、ConfirmationId、confirmation challenge id、OperationId等のId128と、PlanDigest、ConfirmationDigest、artifact SHA-256等のHash256に同じbinary scalar ruleを適用します。

`bytes`型を使用していることは任意長を許可する意味ではありません。

## 8. Compatibility

Same protocol majorでcompatible updateする場合:

- existing field numberを変更しない
- existing field meaning/typeを変更しない
- removed field number/nameはreserveする
- optional additive fieldを基本とする
- required semantic additionはCapabilityまたはprotocol major changeでguardする
- enum numeric valueを再利用しない

Unknown optional fieldのprotobuf compatibilityだけを理由にrequired semanticをsilent degradeしません。

Administration View high-impact/Add-on managementはfeature Capabilityでgateし、Capability不足時にolder direct actionへdowngradeしません。

## 9. Code generation

各componentはrepository内の同一schema sourceを入力としてlocal code generationします。

Generated artifactは正本ではありません。

推奨build gate:

1. schema compile
2. descriptor digest生成
3. message registry completeness check
4. generated source drift check
5. contract fixture round-trip

Exact generator/package patch versionはcomponent tool lockで固定します。

## 10. Determinism boundary

protobuf wire encodingはtransport contractであり、MachiVerse authoritative deterministic digestのcanonical encodingではありません。

Operation immutable digest、state diagnostic digest、EntityId/TransactionId等のdeterministic hashはPhase 1/4のMV-DCBOR/domain-hash contractを使用します。

protobuf map/list iterationやunknown field preservation差異をworld outcomeへ使用しません。

## 11. Security

- auth secret/private keyをprotobuf payloadへ入れない
- browser SessionHandle cookie valueをpayloadへ入れない
- internal component authenticationはmTLSでprotocol HELLO前にpeer検証する
- MessageId/CorrelationId/ComponentInstanceIdをcredentialとして扱わない
- Admin confirmation artifactをcredentialやOperationId代替として扱わない
- Addon package bytesをgeneric Standard Protocol payloadとして扱わない

## 12. Change procedure

Schema変更時は同一change setで少なくとも次を更新します。

- `.proto`
- `message-registry-v1.md`（mapping変更時）
- semantic design/amendment
- compatibility/version/Capability判断
- contract fixture/acceptance

Implementationだけでwire contractをsilent forkしません。
