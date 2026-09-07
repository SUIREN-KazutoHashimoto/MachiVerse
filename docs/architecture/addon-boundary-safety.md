# アドオン境界・互換性・運用安全設計

Status: Architecture baseline / future extension boundary

本書はQ255〜Q259で確定したAddon boundaryを維持し、Phase 4 production implementation scopeとの関係を明確化します。

## 1. Component-scoped Addon

- Addonはcomponent単位で設定可能とする。
- Simulation Core、Gateway、General View、Admin Viewは、それぞれ自身の責務範囲でAddonを導入できる設計とする。
- Addon導入によってcomponent間にdirect code dependency、shared internal type dependencyを作らない。

## 2. Standard Protocol boundary

Standard component protocolには次を載せません。

- Addon functional payload
- Addon-specific generic command
- arbitrary extension data area
- Addon都合で意味が変化するstandard message

一方、接続安全性・互換性判断のために必要なAddon metadataは将来Standard Protocolで交換可能です。

例:

- installed/known Addon identity
- version
- required/provided Capability
- dependency
- compatibility status

Addon固有のcross-component functional communicationが必要な場合は、Standard Protocolそのものをgeneric extension channelにせず、別framework Addon/additional protocolとして明示的に成立させます。

## 3. Compatibility

各Addonは少なくとも次を検証可能な設計とします。

- target component
- target/protocol version compatibility
- required Capability
- provided Capability
- dependency Addon/version
- Addon Config consistency

具体的なAddon identifier lexical rule、version-range grammar、manifest/package formatは現時点のStandard implementation baselineでは固定しません。

## 4. Startup safety

- Addon構成、dependency、Config、Capability、target compatibilityに不整合がある場合、target componentはstandard startupしない。
- 不整合を「重大なものだけ」に限定しない。
- incomplete/partial Addon apply stateで起動しない。
- 検出した不整合をoperatorが診断可能にする。
- 自動的にAddonをdisableしてsilent degraded startupすることをstandard挙動にしない。
- saved worldが依存するAddon/version/Capabilityに不整合がある場合、explicit migrationが完全成功しない限りworld startupを拒否する。

## 5. Update

Addon updateはexplicit operationとします。

apply前に少なくとも次を検証します。

- target/protocol compatibility
- required/provided Capability
- dependency
- Config impact
- persistent-data/save impact

Simulationへ影響する変更はsafe Simulation Step、restart boundary等の整合性を保てるapply pointを使用します。

Live update可能範囲、restart必須条件、migration方式は将来のAddon framework/package contractで固定します。

## 6. Disable / remove

Addon disable/remove前にdependencyとpersistent impactを確認します。

- dependent Addonへの影響
- Configへの影響
- save/world persistent dataへの影響
- migration requirement
- restart requirement

World Stateやsave dataにAddon由来dataが残る場合、それをsilent deletionして整合性を壊しません。

具体的なretain/convert/delete方式は対象Addon contractまたは将来Addon framework仕様で定義します。

## 7. Official / third-party trust boundary

MachiVerseはofficial Addonとthird-party Addonを区別する方針です。

- third-party Addonへofficialと同等の保証を自動付与しない。
- UIでofficial/third-party trust differenceを表示可能にする。
- integrity verificationだけでpublisher identityが証明されたとみなさない。
- third-partyであってもstandard component/protocol boundaryを黙って破壊してよいわけではない。

Official store/distribution route、signature algorithm、hash algorithm、trust-root model、package metadata、failure policyのexact contractは現行Phase 4 production implementationでは未固定です。

これらを実装する場合はdesign amendmentとして先に正本を更新します。

## 8. Administration Viewとの関係

Phase 2内部設計では`AddonManagementProjection`を将来拡張boundaryとして持ちます。

現時点で確定しているpresentation boundary:

- installed/known Addon compatibility metadata表示
- version/Capability/dependency mismatch表示
- target component startup safety state表示
- official/third-party trust classificationを表示可能なUI境界

ただし、Phase 4 implementation work breakdownの`ADMIN-01..ADMIN-04`にはAddon install/update/disable/removeのstandard implementation packageはありません。

したがってcurrent production implementationでは次を行いません。

- generic arbitrary file upload APIの先行実装
- Admin Viewからtarget filesystemへのdirect package copy
- undefined runtime code-loading API
-未登録Addon management Standard Protocol messageの独自追加

Addon management implementationを追加する場合は、roadmap work packageとProtocol/package/security contractをdesign amendmentで先に確定します。

## 9. Current implementation scopeとの関係

`ADMIN-01..ADMIN-04`は次を実装対象とします。

- Admin View scaffold / Gateway protocol client
- health/metrics/log/audit UI
- Config / operational command management
- high-impact / simulation Admin Operation

Addon managementはcurrent standard completion gateではありません。

Gateway/View/Core側についても、Addon frameworkを未確定のgeneric extension mechanismとして実装しません。

## 10. 未確定だがcurrent implementation blockerではない事項

- component単位のAddon配置/enable方式
- Addon identifier/version-range exact format
- dependency declaration format
- Addon framework responsibility/API
- additional protocol establishment/transport
- package/archive format
- official store metadata/distribution
- official/third-party signature/hash/trust-root details
- Admin View install/update/disable/remove UX/API
- persistent-data migration contract

これらは現在の`ADMIN-01..ADMIN-04`を開始するためのblockerではありません。

## 11. Forbidden

- Addon都合でStandard Protocolの意味をsilent変更すること
- Standard Protocolへのgeneric Addon functional payload/command
- component間direct code/internal type dependency
- Addon inconsistencyを抱えたsilent degraded startup
- incomplete Addon apply stateでstartupすること
- Admin Viewからtarget internal API/filesystemへdirect fallbackすること
-未確定Addon install APIをcurrent standard implementationとして先行実装すること
