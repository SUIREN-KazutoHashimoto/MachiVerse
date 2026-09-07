# Gateway↔Administration View Phase 0 Protocol Addendum

Status: Complete / Issue #38  
ProtocolId: `mv.gateway-admin-view`  
Parent: `gateway-admin-view.md`  
Architecture: `../architecture/admin-view-phase0-design.md`

## 1. Scope

本書はIssue #38 Phase 0で確定した`mv.gateway-admin-view`固有normative semanticsを定義します。

Wire type/field numberは`schema/*.proto`、MessageType mappingは`schema/message-registry-v1.md`が正本です。

Administration Viewはconnected Gateway以外へmanagement connectionを張りません。

## 2. Capability model

Required baselineはMessage Registryの最小bootstrap/health集合を維持します。

Phase 0 feature Capability:

```text
protocol.admin-health.v1
protocol.admin-log-query.v1
protocol.admin-config.v1
protocol.admin-operation.v1
protocol.admin-audit.v1
protocol.admin-confirmation.v1
protocol.admin-addon-management.v1
```

- feature Capability不足は`protocol.capability-missing`で明示rejectする。
- high-impact actionで`protocol.admin-confirmation.v1`不足時にordinary pathへdowngradeしない。
- Addon management Capability不足時はAddon management messageを使用しない。

## 3. Health semantics

`component.health.query` / `component.health.result`:

- empty `targets` = all permission-visible components。
- empty `metric_names` = baseline metric set。
- unknown metricはquery全体を必ずしもrejectせずunsupported conditionで返せる。
- sample observation timestampを保持する。
- stale sampleをfreshとして再timestampしない。

## 4. Log semantics

`component.log.query` / `component.log.page`:

- `page_size=0` = default 200。
- accepted range 1..1000。
- cursorはopaque、最大256 bytes、query filterへbindする。
- severity/event/time/target/CorrelationId/OperationId/SimulationStep/MasterGenerationをfilter可能。
- queryはread-only。
- secret redactionはsource/collector側で完了してからGatewayへ公開する。

## 5. Config semantics

### Read

- empty `keys` = permission上公開可能な全key metadata。
- sensitive itemはeffective valueを返さず`value_redacted=true`とする。
- `config_digest`はowner Config snapshot identityでありsecret valueのreverse lookup sourceとして公開しない。

### Change

- OperationId / immutable payload digest必須。
- `expected_base_generation` mismatch = `config.stale-generation`。
- duplicate key = `protocol.invalid-argument`。
- one request = one target componentのatomic change set。
- partial apply禁止。
- simulation-affecting changeはhigh-impact flow必須。
- restart/world-regeneration required itemはrequired boundaryをresult/planで返す。

## 6. Operational command semantics

`operational.command`:

- `command_kind`はstable registry token。
- arbitrary shell/script/pathは禁止。
- Phase 0 standard commandはOperationId / immutable payload digest必須。
- payload schema id/versionはcommand registryと一致させる。
- high-impact commandはdirect applyをrejectする。
- accepted/queuedはterminal successではない。

Phase 0 command registry:

| command_kind | Required permission | High-impact |
|---|---|---:|
| `gateway.resync.request` | `admin.operation.execute` | no |
| `world.save.create` | `admin.operation.execute` | no |
| `world.pause` | `admin.operation.execute` + `admin.operation.high-impact` | yes |
| `world.resume` | same | yes |
| `component.restart.request` | same | yes |
| `component.shutdown.request` | same | yes |
| `diagnostic.snapshot.create` | `admin.operation.execute` | no |

restart/shutdownはdeployment supervisor capabilityがない場合`operation.unsupported`です。

## 7. Permission tokens

```text
admin.observe.health
admin.observe.logs
admin.observe.audit
admin.config.read
admin.config.change.operational
admin.config.change.simulation
admin.operation.execute
admin.operation.high-impact
admin.addon.read
admin.addon.manage.official
admin.addon.manage.third-party
```

Gatewayはrequest admission時とstate-changing commit時にpermission/session generationを評価します。

## 8. High-impact flow

Canonical message sequence:

```text
A -> G: admin.action.prepare
G -> A: admin.action.plan
A -> G: admin.action.confirm
G -> A: admin.action.confirmed
A -> G: admin.action.commit
G -> A: admin.action.result
```

### 8.1 Prepare

`AdminActionPrepareV1`は次のstandard action familyだけをoneofで受け付けます。

- Config change
- Operational command
- Simulation Admin Operation
- Addon action intent

generic arbitrary admin payload channelとして使用しません。

### 8.2 Plan

`AdminActionPlanV1`はserver-generated immutable planです。

minimum semantics:

```text
PlanId: Id128
PlanDigest: Hash256
ActionKind: StableToken
OperationId: Id128
ImmutablePayloadDigest: Hash256
Target: ComponentTargetV1
RiskLevel
RequiredPermissions
SimulationAffecting
RequiredBoundary
DependencyImpactCodes
WarningCodes
SessionGeneration
ExpiresAtUnixMillis
ConfirmationChallengeId: Id128
ConfirmationChallengeExpiresAtUnixMillis
```

PlanDigestはnormalized action + target + relevant owner generation/dependency/trust snapshot + required boundaryをcoverします。

### 8.3 Confirm

`AdminActionConfirmV1`はPlanId/PlanDigest、challenge、OperationId、session generationをserverへ返します。

Gatewayはvalid confirmationに対して`AdminActionConfirmationV1`を発行します。

- ConfirmationId = Id128
- ConfirmationDigest = Hash256
- plan/sessionへbind
- expiryあり
- server-side unused stateを保持
- client-side booleanだけでは成立しない

### 8.4 Commit

`AdminActionCommitV1`はPlan identity、Confirmation identity、OperationId/digest、session generationを提示します。

Gatewayはcommitで次を再検証します。

- plan exists / not expired
- plan digest exact match
- confirmation exists / not expired / unused
- confirmation is bound to same plan/session
- OperationId/payload digest exact match
- session active
- permission still present
- target owner generation/dependency/trust snapshot not stale
- safe boundary remains valid

commit成功またはterminal confirmation consumption後、confirmation artifactを再利用不可にします。

Stable reject baseline:

```text
admin.plan-stale
admin.plan-expired
admin.confirmation-required
admin.confirmation-expired
admin.confirmation-used
admin.confirmation-mismatch
```

Phase 0はsingle-operator confirmationです。

## 9. Addon inventory

```text
A -> G: addon.inventory.query
G -> A: addon.inventory.result
```

`AddonInventoryItemV1`は少なくともidentity/version/target/install state/activation state/trust tier/artifact SHA-256/publisher/signature state/Capability/dependency/Config schema/persistent-data/update metadataを提供します。

TrustTier:

```text
OFFICIAL
THIRD_PARTY_LOCAL_TRUST
THIRD_PARTY_UNKNOWN
```

local trustをOFFICIALへ昇格しません。

## 10. Official catalog

```text
A -> G: addon.catalog.query
G -> A: addon.catalog.page
```

Gatewayがconfigured official storeをqueryします。Admin View/browserがstore responseを直接trust decisionに使用しません。

Catalog itemはAddonId/version/display name/target kinds/protocol range/Capability/dependency/artifact digest/publisher/signature metadata等を返します。

## 11. Third-party staging

package bytesはnormal WebSocketへ載せません。

BFF HTTPS endpoint:

```text
POST /api/v1/admin/addons/stage
```

requirements:

- authenticated Admin session
- `admin.addon.manage.third-party`
- streaming upload + configured size/count limits
- Gateway computes SHA-256
- resultはopaque StagedPackageId、byte size、digest、manifest summary
- upload完了だけではinstall/loadしない
- staged objectはoperational Configによりexpireする

## 12. Addon action

install/update/disable/removeは`AddonActionIntentV1`をhigh-impact `admin.action.*` flowへ統合します。

Intent:

```text
Action: INSTALL | UPDATE | DISABLE | REMOVE
Source: OFFICIAL_CATALOG | STAGED_PACKAGE
CatalogItemRef optional
StagedPackageId optional
ExpectedAddonId
ExpectedVersion optional
ExpectedArtifactSha256
TargetComponent
```

Official actionは`admin.addon.manage.official`、third-partyは`admin.addon.manage.third-party`を要求します。

Third-party install/updateは常にhigh-impactです。

## 13. Official verification

order:

1. HTTPS transport success
2. catalog/manifest signature verification
3. Ed25519 signer chain to pinned official trust root
4. artifact SHA-256 exact match
5. manifest identity/version/target consistency
6. dependency/Capability/protocol compatibility
7. archive extraction safety
8. target owner preflight

failureはterminal rejectです。

Stable result code baseline:

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

## 14. Addon apply lifecycle

```text
STAGED
VALIDATED
PREPARED
COMMITTED
APPLY_PENDING
APPLIED
REJECTED
FAILED
```

- validation before code load
- no in-place partial update
- install stateとactivation stateを分離
- live activationはexplicit safe-step contractがある場合のみ
- otherwise restart boundary
- apply failureはprevious active versionを維持
- inconsistent addon config/dependencyでsilent startupしない

## 15. Retry / idempotency

- state-changing identity = OperationId + immutable payload digest。
- same OperationId/different digestはreject。
- MessageId/CorrelationId/PlanId/ConfirmationId/StagedPackageIdをdedup identityにしない。
- prepare artifactはexpiry/state changeを越えて再利用しない。
- retryでAddon install/high-impact actionを二重applyしない。

## 16. Audit mapping

Mandatory baseline:

- login security event
- permission reject
- Config change request/result
- operational command request/result
- high-impact prepare/confirm/commit/result
- Addon stage
- Addon install/update/disable/remove
- official verification failure
- audit read

Audit payloadはsecret Config value、credential、private keyを含めません。

## 17. Forbidden

- arbitrary shell/script/path over operational command
- Admin Viewからcomponent filesystem/direct internal API access
- Admin View→Simulation Core direct connection
- UI-only authorization
- high-impact direct apply
- client-only confirmation
- confirmation replay
- stale ConfigGeneration silent overwrite
- package bytesのgeneric addon functional protocol化
- hash-only official publisher verification
- third-party trustのofficial昇格
- upload completion = install success
- ACK = terminal target effect success

## 18. Phase 0 closure

本書で定義したhigh-impact/Add-on management messageは`payloads.proto`と`message-registry-v1.md`へcanonical化済みです。

後続Phaseはimplementation technology/UIを選択できますが、本Protocol semanticsをsilentに変更してはなりません。
