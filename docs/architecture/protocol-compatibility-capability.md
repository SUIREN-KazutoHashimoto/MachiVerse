# プロトコル互換性・Capability Negotiation設計

## 確定方針

第245〜249問はすべてCを採用する。Capability Negotiationでは通常機能だけでなく、接続安全性・互換性の判断に必要なアドオンのメタ情報も交換対象とする。

Q255により、**標準プロトコル上のアドオン情報はメタ情報に限定し、アドオン固有機能データ・追加操作・任意拡張ペイロードを標準プロトコルへ載せない**。

Phase 1 P1-04 で共通 envelope / handshake の具体契約を確定した。詳細の正本は `docs/design/phase1-protocol-envelope.md` とする。

## Major / Minor互換性

Protocol versionは次で表現する。

```text
ProtocolVersion {
  major: uint16,
  minor: uint16
}
```

- backward-incompatible semantic changeはMajorを更新する。
- same Major内のbackward-compatible changeはMinorを更新する。
- handshakeでは双方のsupported version rangeから共通Majorの最大値を選び、そのMajorの共通Minor範囲で最大Minorをnegotiated versionとする。
- 共通versionが存在しない場合はconnectionを拒否する。
- normal messageは実装最新版ではなくnegotiated versionを明示する。

Minor updateではexisting required fieldを削除せず、existing fieldのtype / unit / semantic meaningを互換不能に変更しない。

New fieldはabsent時に旧Minorと同じ意味になるoptional fieldとし、新message type / featureはnegotiated MinorまたはCapabilityで送信可否を制御する。

## Capability Negotiation

Capability identifierはStableTokenとする。

```text
CapabilityId := StableToken
```

incompatible semantic revisionは別tokenとし、例えば `state.delta.v1` のようにversionをtokenへ含める。

双方はconnection handshakeで次を交換する。

- provided Capability
- required Capability
- connection safety / compatibility判定に必要なaddon metadata

判定規則:

- A.requiredがB.providedのsubsetでなければreject。
- B.requiredがA.providedのsubsetでなければreject。
- effective optional Capability setは双方providedのintersection。
- required Capability不足をsilent downgradeしない。
- 特定featureのみ無効化可能な場合、相手が理解できないmessage type自体を送らない。

## NegotiationGeneration

connection上のnegotiated semanticsを識別するため `NegotiationGeneration := uint32` を持つ。

- handshake前は0。
- initial handshake成功後は1。
- safe live renegotiation成功ごとに1増加する。
- reconnectはnew connectionとして1から開始する。
- stale NegotiationGeneration messageをcurrent semanticsとして解釈しない。
- wrap-around前にconnectionを再確立する。

NegotiationGenerationをworld orderingやbusiness priorityに使用しない。

## 接続後のCapability変化

Phase 1標準では、connection中にeffective Capability setの変更が必要になった場合は**reconnectして再negotiation**する。

live renegotiationは次をすべて満たす場合のみ許可する。

- 双方が `protocol.live-renegotiation.v1` を提供する。
- protocol ownerが明示的なquiesce / barrierを定義する。
- old/new NegotiationGenerationの境界が曖昧にならない。
- barrier完了前にnew-only messageを送らない。
- renegotiation timingがworld outcomeへ影響しない。

## Addon compatibility metadata

標準protocolで交換できるaddon情報はconnection safety / compatibility用metadataに限定する。

Phase 1共通形は少なくとも次を表現可能にする。

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

標準protocolへ載せないもの:

- addon固有world data
- addon固有command
- addon固有function payload
- generic opaque extension bytes
- addon都合で標準message semanticsを書き換える仕組み

Addon固有functionをcomponent間通信する必要がある場合はadditional protocol / framework addon側の責務とする。

## アドオン不整合時の扱い

- required Capability、required addon、required version等が不足または非互換ならunsafe featureを黙って有効化しない。
- Q257に従い、起動時のaddon構成・依存・Capability・Configに不整合がある場合は重大度に関係なく対象componentを起動しない。
- Q267に従い、保存worldが依存するaddon条件に不整合がある場合は、明示的migrationが完全成功して整合性を確認できない限りworldを起動しない。

## New MinorからOld Minorへの送信

- senderはnegotiated Minorを超えるfield/messageを無条件送信しない。
- 相手Capabilityに応じて送信内容を決定する。
- receiverが未知semanticをsilent ignoreすることを前提にしない。
- safe-to-ignoreかどうかはprotocol schema / negotiated versionで定義する。

## incompatibility diagnostic

handshake reject時は可能な範囲で次を構造化して返す。

- stable error code
- local supported versions
- peer offered versions
- missing Capability
- incompatible addon
- update direction
- human-readable diagnostic message

Version incompatibilityでは双方versionと必要なupdate directionを利用者・運用者が確認可能にする。

Machine behaviorはdiagnostic textではなくstable codeで分岐する。

## Common error code

互換性関連では少なくとも次を共通codeとして使用できる。

```text
protocol.version-incompatible
protocol.capability-missing
protocol.negotiation-stale
protocol.wrong-protocol
protocol.unknown-message-type
```

individual protocolは自身のstable code namespaceを追加できる。

## コンポーネント独立性との関係

- Capability Negotiationは各componentの独立更新を支えるcontract mechanismとする。
- component間でinternal typeやshared DTO libraryを共有する仕組みにはしない。
- generated codeを利用してもprotocol documentを契約正本から外さない。
- addon compatibility metadataはstandard protocolを介して交換し、相手componentのinternal implementationへ依存しない。

## 独立test

少なくとも次をcomponent単独のprotocol contract testで再現可能にする。

- same Major / same Minor
- same Major / different Minor
- no common version reject
- required Capability mismatch
- addon dependency mismatch
- stale NegotiationGeneration reject
- reconnect後のrenegotiation

## 今後決定が必要な事項

P1-04で次は確定済み。

- ProtocolVersion concrete type
- supported range selection algorithm
- CapabilityId形式
- required / provided判定
- NegotiationGeneration
- addon identity/version/dependency metadata
- incompatibility common error code
- reconnectを基本とするCapability change rule

個別protocolまたは後続設計へ残す事項:

- physical transport / connection establishment
- serialization / compression
- protocol-specific Hello payload追加項目
- live renegotiation barrierの具体algorithm（採用するprotocolのみ）
- auth credential / mutual authenticationとの結合
- additional addon protocol frameworkの具体仕様
