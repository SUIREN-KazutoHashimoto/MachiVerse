# Standard Protocol v1 Protobuf Schema

Status: Canonical wire declaration / Standard Protocol v1

## 1. Scope

このdirectoryの`.proto`はMachiVerse Standard Protocol v1のversion-controlled wire contractである。

```text
common.proto   common envelope / handshake / result / publication / operation

auth.proto     browser auth/session/login proxy payload

payloads.proto Gateway/View/Admin standard payload
```

MessageTypeとpayload typeのexact mappingは `message-registry-v1.md` を参照する。

## 2. Source-of-truth boundary

`.proto` が正本:

- protobuf package/import。
- message/enum/service symbol。
- field number。
- protobuf wire type。
- enum numeric value。
- gRPC service signature。

Phase 4 design文書が正本:

- StableToken lexical constraint。
- Id128/Hash256 length/non-zero rule。
- field requirednessのsemantic rule。
- WorldContext/OperationContext requiredness。
- ordering/canonicalization。
- Capability/version negotiation。
- authn/authz。
- retry/dedup/custody/durability。
- deterministic digest semantics。
- security limit。

protobuf自体にrequired fieldを持たせずproto3 presenceとapplication validationを併用する。

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

Internal component credential/private keyはprotobufへ載せない。Core/Gateway production authenticationはmutual TLSを使用する。

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

## 4. Enum naming

Markdown設計では読みやすさのため `READY`, `FULL`, `REJECTED` 等のsemantic short nameを記載する場合がある。

Code generation上のcanonical enum symbolは`.proto`に記載したprefix付きidentifierである。

例:

```text
semantic FULL  -> PUBLICATION_KIND_FULL
semantic READY -> GATEWAY_READINESS_READY
```

Numeric valueは`.proto`を正本とし、published valueを再利用・renumberしない。

## 5. Numeric mapping

Browser側で`uint64`をECMAScript `Number`へlossy conversionしない。

対象例:

- SimulationStep。
- MasterGeneration。
- ConfigGeneration。
- revision。
- timestamp where exact integer representation is required by schema。

`BigInt`またはlossless uint64 wrapperを使用する。

## 6. Binary scalar validation

Application validation:

```text
Id128   = exactly 16 bytes
Hash256 = exactly 32 bytes
```

ZERO Id128はschema/designがNONE sentinelを明示した場合以外invalid。

`bytes`型を使用していることは任意長を許可する意味ではない。

## 7. Compatibility

Same protocol majorでcompatible updateする場合:

- existing field numberを変更しない。
- existing field meaning/typeを変更しない。
- removed field number/nameはreserveする。
- optional additive fieldを基本とする。
- required semantic additionはCapabilityまたはprotocol major changeでguardする。
- enum numeric valueを再利用しない。

Unknown optional fieldのprotobuf compatibilityだけを理由にrequired semanticをsilent degradeしない。

## 8. Code generation

各componentはrepository内の同一schema sourceを入力としてlocal code generationする。

Generated artifactは正本ではない。

推奨build gate:

1. schema compile。
2. descriptor digest生成。
3. message registry completeness check。
4. generated source drift check。
5. contract fixture round-trip。

Exact generator/package patch versionはcomponent tool lockで固定する。

## 9. Determinism boundary

protobuf wire encodingはtransport contractであり、MachiVerse authoritative deterministic digestのcanonical encodingではない。

Operation immutable digest、state diagnostic digest、EntityId/TransactionId等のdeterministic hashはPhase 1/4のMV-DCBOR/domain-hash contractを使用する。

protobuf map/list iterationやunknown field preservation差異をworld outcomeへ使用しない。

## 10. Security

- auth secret/private keyをprotobuf payloadへ入れない。
- browser SessionHandle cookie valueをpayloadへ入れない。
- internal component authenticationはmTLSでprotocol HELLO前にpeer検証する。
- MessageId/CorrelationId/ComponentInstanceIdをcredentialとして扱わない。

## 11. Change procedure

Schema変更時は同一change setで少なくとも次を更新する。

- `.proto`。
- `message-registry-v1.md`（mapping変更時）。
- Phase 4 semantic design/amendment。
- compatibility/version/Capability判断。
- P4-08 contract fixture/acceptance。

Implementationだけでwire contractをsilent forkしない。