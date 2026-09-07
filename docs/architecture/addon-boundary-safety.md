# アドオン境界・互換性・運用安全設計

Status: Phase 0 contract complete

本書はQ255〜Q259とAdministration View Phase 0で確定したAddon boundary/trust/management semanticsを統合します。

## 1. Component-scoped Addon

- Addonはcomponent単位で設定する。
- Simulation Core、Gateway、General View、Admin Viewは自分の責務内でAddonを導入できる。
- Addon導入によってcomponent間にdirect code dependency、shared internal type dependencyを作らない。
- target componentがAddon lifecycle、Config、dependency、activation boundaryのownerとなる。

## 2. Standard Protocol boundary

Standard ProtocolへAddon固有function payload、Addon専用generic command、arbitrary extension data areaを載せません。

Standard Protocolに載せてよいAddon情報はconnection/operation safetyとmanagementに必要なmetadataです。

- installed inventory
- identity/version
- target component
- required/provided Capability
- dependency/version range
- compatibility
- trust/signature/digest state
- install/update/disable/remove plan/result
- activation/restart/persistent-data impact

Addon固有cross-component functional communicationが必要な場合は、別のframework Addonとadditional protocolを成立させます。Standard Protocolをsilent extension channelとして使用しません。

## 3. Addon identity

Standard v1 minimum manifest metadata:

- `addon_id`: reverse-DNS style stable identifier
- `version`: SemVer 2.0.0
- target component kinds
- required protocol/version range
- required/provided Capability
- dependency addon/version range
- Config schema version
- persistent-data compatibility/migration declaration
- artifact SHA-256 digest
- publisher identity/signature metadata when present
- trust source

### 3.1 `addon_id`

- lowercase ASCII reverse-DNS styleを推奨canonical formとする。
- segmentは英小文字、数字、`-`を使用し、`.`で区切る。
- comparisonはcase-sensitive canonical string comparisonとする。
- display nameとは別identityとする。

例: `jp.suiren.machiverse.example-addon`

### 3.2 Version range

Version operandはSemVer 2.0.0です。

Standard v1 range grammarはportable comparator conjunctionとします。

```text
=1.2.3
>=1.2.0 <2.0.0
>=2.1.0
<3.0.0
```

- comparator: `=`, `>`, `>=`, `<`, `<=`
- whitespace区切りはAND
- OR expressionはStandard v1では使用しない
- empty rangeはinvalid

## 4. Compatibility validation

Addon activation前に少なくとも次を検証します。

- target component kind/version
- Standard Protocol compatibility
- required Capability
- provided Capability collision/contract
- dependency Addon/version range
- Config schema/validation
- persistent-data compatibility/migration requirement

required Capability、dependency、versionが不足/非互換ならAddonを有効化しません。

## 5. Startup safety

- Addon構成、dependency、Config、Capability、target compatibilityに不整合があればtarget componentをstandard startupしない。
- 「重大な不整合」だけに限定しない。
- incomplete/partial Addon stateで起動しない。
- validation failure理由をoperatorが診断可能なstable codeで提示する。
- 自動disableしてsilent degraded startupする挙動をstandardとしない。
- saved worldが依存するAddon/version/Capabilityに不整合がある場合、explicit migrationが完全成功しない限りworld startupを拒否する。

## 6. Administration View management boundary

Administration ViewをStandard Addon managementのoperator入口とします。

```text
Administration View -> Gateway -> target component owner
```

Admin Viewはtarget component filesystemへ直接package copy/editしません。

Standard management対象:

- inventory
- official catalog
- staging metadata
- compatibility/trust preflight
- install
- update
- disable
- remove
- activation/restart boundary
- result/audit

Wire contractは `../protocols/gateway-admin-view.md` と `../protocols/schema/message-registry-v1.md` を正本とします。

## 7. Trust tier

Canonical trust tier:

```text
OFFICIAL
THIRD_PARTY_LOCAL_TRUST
THIRD_PARTY_UNKNOWN
```

- `THIRD_PARTY_LOCAL_TRUST`を`OFFICIAL`として表示/認可しない。
- trust tierはpackage sourceだけで決めず、signature/trust-root validation resultに基づく。
- unknown/unverified signatureをverified扱いしない。

## 8. Official Addon distribution

Official Addon store/catalogをstandard distribution routeとしてsupportします。

Gateway Configは少なくとも次を持ちます。

- official catalog/store endpoint
- pinned official trust root/keyset
- fetch/size/time limits
- staging retention

Admin ViewはGateway経由でcatalogを参照し、browserがstore responseを直接trust decisionに使用しません。

## 9. Official verification

Official package verification order:

1. HTTPS transport success
2. catalog/manifest signature verification
3. Ed25519 signer chain to pinned official trust root
4. artifact SHA-256 exact match
5. manifest identity/version/target consistency
6. dependency/Capability/protocol compatibility
7. archive extraction safety
8. target owner preflight

failureはterminal rejectです。warning-onlyでofficial activationを続行しません。

stable result code baseline:

```text
addon.signature-invalid
addon.digest-mismatch
addon.publisher-untrusted
addon.manifest-invalid
addon.incompatible-target
addon.protocol-incompatible
addon.capability-missing
addon.dependency-unsatisfied
addon.archive-unsafe
addon.persistent-data-conflict
```

Hashだけをpublisher identity proofとしません。

Official trust root rotationは、old trusted rootで署名されたkeyset update、またはexplicit high-impact相当のtrust-root Config changeで行います。

## 10. Third-party Addon

Third-party packageはoperator責任で導入可能ですが、officialと同等の保証を自動付与しません。

Admin Viewはcommit前に少なくとも次を明示します。

- THIRD-PARTY label
- source
- artifact SHA-256
- signature presence / signer identity
- local trust / unknown
- target component
- required/provided Capability
- dependency
- simulation impact
- persistent-data/save impact
- official verificationがないこと

Third-party install/updateは常にhigh-impactであり、`admin.addon.manage.third-party` とhigh-impact confirmationを必須とします。

Local trusted signerで検証できても`THIRD_PARTY_LOCAL_TRUST`であり、`OFFICIAL`へ昇格しません。

## 11. Package staging

package bytesはnormal Standard Protocol WebSocket messageへ載せません。

### Official

Gatewayがofficial catalog itemを解決し、staging areaへ取得します。validation完了前にexecutable Addon codeをloadしません。

### Third-party

authenticated BFF HTTPS staging endpointを使用します。

- required permission: `admin.addon.manage.third-party`
- streaming upload
- configured byte/count limits
- Gateway computes SHA-256
- opaque StagedPackageIdを返す
- upload完了だけではinstall/executeしない
- staged objectはconfigured retention後にexpire可能

## 12. Archive safety

少なくとも次をrejectします。

- `..` traversal
- absolute path
- extraction root外へのsymlink/hardlink escape
- duplicate canonical path
- case-fold collisionをtarget filesystem上で安全に扱えないpackage
- configured file count/total size/single file size超過
- malformed manifest/archive

Validationはexecutable code load前に行います。

Exact archive container formatはPhase 6 implementation前にmanifest/package versionとして固定し、Standard Protocol functional payloadとは分離します。

## 13. Install / update / disable / remove lifecycle

canonical operation state:

```text
STAGED -> VALIDATED -> PREPARED -> COMMITTED -> APPLY_PENDING -> APPLIED
                         |             |
                         +-> REJECTED  +-> FAILED
```

- install/update/disable/removeはexplicit operationとする。
- compatibility/dependency/Config/persistent-data impactをprepare前に検証する。
- high-impact actionはAdministration View high-impact confirmation contractに従う。
- target ownerがterminal effectをacknowledgeするまでsuccessとしない。
- existing versionをin-place partial updateしない。
- apply failure時はprevious active versionを維持する。
- install stateとactivation stateを区別する。

## 14. Activation boundary

- live activationはAddonがexplicit safe-step contractを宣言し、target ownerがsupportする場合のみ許可する。
- simulation-affecting Addon changeはauthoritative safe Simulation Stepまたはrestart boundaryでapplyする。
- live activation contractがない場合はrestartを要求する。
- world regeneration/migrationが必要な場合は通常restartとして誤表示せず、required boundaryを明示する。

## 15. Update

Addon updateは明示的operationです。

apply前に次を再検証します。

- target/protocol compatibility
- required/provided Capability
- dependency
- Config impact
- persistent-data migration
- trust/signature/digest
- required safe boundary

updateでidentity/version/digest expectationが変わった場合、stale planを再利用しません。

## 16. Disable / remove

Disable/remove前にdependencyとpersistent impactを確認します。

- dependent Addonがある場合はdependency policyに従いrejectまたはexplicit migration planを要求する。
- world/saveに由来dataが残る場合、silent deletionしない。
- migration、retention、destructive data removalのいずれかをAddon contractが明示する。
- destructive persistent-data removalが必要ならhigh-impact扱いとする。

## 17. Retry / idempotency

- package/action retryで二重install/applyしない。
- state-changing identityはOperationId + immutable payload digest。
- same OperationId / different digestをrejectする。
- MessageId/CorrelationId、PlanId、StagedPackageIdをOperation dedup identityにしない。
- stale dependency/trust/inventory generationをsilent applyしない。

## 18. Audit

少なくとも次をauditします。

- package staging metadata creation
- official verification failure
- install/update/disable/remove prepare/confirm/commit/result
- permission reject
- trust tier/result
- target/boundary/resulting inventory generation

secret/credential/private key materialをaudit payloadに含めません。

## 19. Protocol extension framework

Addon-specific cross-component functional communicationのためのframework Addon/additional protocolは将来拡張です。

Phase 0では次のみ固定します。

- Standard Protocolへgeneric functional extension fieldを追加しない。
- additional protocolは明示的ProtocolId/version/Capability negotiationを持つ。
- component independenceを維持する。

具体API、transport、package extension pointはPhase 6以降の別設計事項であり、Administration View Phase 0 blockerではありません。

## 20. Forbidden

- Addon都合でStandard Protocol semanticをsilent変更すること
- generic addon functional payload/command
- component間direct code/internal type dependency
- unverified packageをOFFICIAL表示すること
- hash一致のみでpublisher identityを保証すること
- validation前のexecutable code load
- partial in-place update
- dependency/config inconsistencyを抱えたsilent degraded startup
- upload完了をinstall successとみなすこと
- Admin Viewからtarget filesystemをdirect editすること
- third-party trustをofficialへ自動昇格すること
