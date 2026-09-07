# Gateway↔Administration View Phase 0 Protocol Addendum

Status: Draft / Issue #38 work in progress  
ProtocolId: `mv.gateway-admin-view`  
Parent: `gateway-admin-view.md`  
Architecture: `../architecture/admin-view-phase0-design.md`

## 1. Scope

本書は Issue #38 Phase 0 で `mv.gateway-admin-view` に追加する normative semantics を定義する。
共通 envelope/version/Capability/result/error は既存 Protocol v1 contract を継続使用する。

Administration View は connected Gateway 以外へ management connection を張らない。

## 2. Baseline capabilities

既存 `protocol.admin-health.v1` に加え、Phase 0 baseline を次とする。

```text
protocol.admin-health.v1
protocol.admin-log-query.v1
protocol.admin-config.v1
protocol.admin-operation.v1
protocol.admin-audit.v1
protocol.admin-confirmation.v1
protocol.admin-addon-management.v1
```

high-impact action を扱う接続で `protocol.admin-confirmation.v1` が不足する場合、high-impact action を silent downgrade せず reject する。

addon management を扱う接続で `protocol.admin-addon-management.v1` が不足する場合、addon inventory/catalog/action UI は unavailable とする。

## 3. Existing message semantics fixed by Phase 0

### 3.1 `component.health.query` / `component.health.result`

- `HealthQueryV1.targets` empty は "all Admin-visible components" を意味する。
- `metric_names` empty は baseline metric set を意味する。
- unknown metric name は query 全体を reject せず、unsupported metric condition を返してよい。
- response sample は observation timestamp を必須とする。
- stale sample を fresh として timestamp 更新してはならない。

### 3.2 `component.log.query` / `component.log.page`

- `page_size=0` は default 200 と解釈する。
- accepted range は 1..1000。
- cursor は opaque、最大256 bytes。
- cursor は query filter と結び付け、別 filter への再利用を reject する。
- log query は read-only で World State mutation ではない。
- secret redaction は source/collector 側で完了した record のみ Gateway が公開する。

### 3.3 `config.read` / `config.read.result`

- `keys` empty は permission により公開可能な全 key metadata を要求する。
- sensitive item は effective value を返さず `sensitive=true` とする。
- `config_digest` は公開 payload digest ではなく owner Config snapshot identity として扱う。
- secret value を digest reverse lookup 可能な形で UI へ公開しない。

### 3.4 `config.change` / `config.change.result`

- OperationId / immutable payload digest は必須。
- `expected_base_generation` mismatch は `config.stale-generation`。
- duplicate key は `protocol.invalid-argument`。
- one request は one target component の atomic change set。
- partial apply 禁止。
- simulation-affecting item を含む場合は high-impact prepare/commit flow が必須。
- restart/world-regeneration required item を runtime change として request した場合、必要 boundary を stable result code/diagnostic metadata で返す。

### 3.5 `operational.command`

- `command_kind` は registry token。shell/script/path を入れない。
- Phase 0 では全 command に OperationId と immutable payload digest を要求する。
- `payload_schema_id` と `payload_schema_version` は command registry と一致しなければ reject する。
- high-impact command は direct execute を reject し prepare/commit flow を要求する。
- accepted/queued は terminal success ではない。terminal state は `operation.result` / status query で追跡する。

### 3.6 `audit.query` / `audit.page`

- audit read permission は `admin.observe.audit`。
- audit query 自身も `audit.read` event として監査する。
- secret Config value、credential、private key material を audit payload に含めない。

## 4. Permission decision

Gateway は request admission 時と state-changing commit 時に permission を評価する。

permission token baseline:

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

commit 前に session generation が変化していた場合、prepare 時に permission があっても再認可する。

## 5. High-impact prepare/confirm/commit

### 5.1 Rule

high-impact action は直接の `config.change` / `operational.command` / addon apply を terminal apply しない。

flow:

```text
A -> G: admin.action.prepare
G -> A: admin.action.plan
A -> G: admin.action.commit
G -> A: admin.action.result
```

### 5.2 Plan semantics

`admin.action.plan` は server-generated immutable plan を返す。

minimum fields:

```text
PlanId: Id128
PlanDigest: SHA-256
ActionKind: stable token
OperationId: Id128
ImmutablePayloadDigest: SHA-256
Target: ComponentTargetV1
RiskLevel: LOW | MEDIUM | HIGH | CRITICAL
RequiredPermissions: repeated StableToken
SimulationAffecting: bool
RequiredBoundary: NONE | SAFE_STEP | RESTART | WORLD_REGENERATION
DependencyImpactSummary: repeated StableToken/diagnostic
WarningCodes: repeated StableToken
SessionGeneration
ExpiresAtUnixMillis
```

PlanDigest は normalized action + target + relevant owner generation/dependency snapshot + required boundary を cover する。

### 5.3 Commit validation

Gateway は commit で少なくとも次を再検証する。

- PlanId exists and not expired
- PlanDigest exact match
- OperationId/payload digest exact match
- session active
- session generation unchanged or explicitly revalidated
- required permission still present
- target generation/dependency/trust state not stale
- confirmation supplied

stale plan は `admin.plan-stale`。
expired plan は `admin.plan-expired`。
confirmation missing は `admin.confirmation-required`。

Phase 0 は single-operator confirmation。multi-person approval は baseline に含めない。

## 6. Standard operational command registry

| command_kind | Required permission | High-impact | Expected terminal path |
|---|---|---:|---|
| `gateway.resync.request` | `admin.operation.execute` | no | `operation.result` |
| `world.save.create` | `admin.operation.execute` | no | `operation.result` |
| `world.pause` | `admin.operation.execute` + `admin.operation.high-impact` | yes | prepare/commit + `operation.result` |
| `world.resume` | same | yes | prepare/commit + `operation.result` |
| `component.restart.request` | same | yes | prepare/commit + `operation.result` |
| `component.shutdown.request` | same | yes | prepare/commit + `operation.result` |
| `diagnostic.snapshot.create` | `admin.operation.execute` | no | `operation.result` |

`component.restart.request` / `component.shutdown.request` は deployment supervisor capability がない場合 `operation.unsupported`。

## 7. Addon management messages

Phase 0 は addon functional payload ではなく management/safety metadata のみ standard protocol に追加する。

### 7.1 Inventory

```text
A -> G: addon.inventory.query
G -> A: addon.inventory.result
```

inventory item minimum fields:

```text
AddonId
Version
TargetComponent
InstallState
ActivationState
TrustTier
ArtifactSha256
PublisherId optional
SignatureState
RequiredCapabilities
ProvidedCapabilities
Dependencies
ConfigSchemaVersion
PersistentDataState
UpdateAvailable optional
```

TrustTier:

```text
OFFICIAL
THIRD_PARTY_LOCAL_TRUST
THIRD_PARTY_UNKNOWN
```

`THIRD_PARTY_LOCAL_TRUST` を `OFFICIAL` と表示・扱いしない。

### 7.2 Official catalog

```text
A -> G: addon.catalog.query
G -> A: addon.catalog.page
```

catalog は configured official store を Gateway が query する。
Administration View が store response を直接 trust decision に使わない。

catalog item minimum fields:

```text
AddonId
Version
DisplayName
TargetComponentKinds
RequiredProtocolRange
RequiredCapabilities
Dependencies
ArtifactSha256
PublisherId
ManifestSignature
ReleaseNotesUri optional
```

### 7.3 Third-party staging

package bytes は normal WebSocket message に載せない。

BFF HTTPS endpoint:

```text
POST /api/v1/admin/addons/stage
```

requirements:

- authenticated Admin session
- `admin.addon.manage.third-party`
- streaming upload with configured size limit
- Gateway computes SHA-256
- upload result returns `StagedPackageId`, byte size, SHA-256, parsed manifest summary
- upload completion does not install or load code

staged package is opaque server-side object and expires by operational Config.

### 7.4 Addon action

install/update/disable/remove は high-impact `admin.action.prepare/commit` に統合する。

Addon action intent fields:

```text
Action: INSTALL | UPDATE | DISABLE | REMOVE
Source: OFFICIAL_CATALOG | STAGED_PACKAGE
CatalogItemRef optional
StagedPackageId optional
ExpectedAddonId
ExpectedVersion optional
ExpectedSha256
TargetComponent
```

Official install requires `admin.addon.manage.official`。
Third-party staged source requires `admin.addon.manage.third-party`。
Third-party install/update は常に high-impact。

## 8. Official trust verification

Official package verification order:

1. HTTPS transport success
2. official catalog/manifest signature verification
3. Ed25519 signer chain to pinned official trust root
4. artifact SHA-256 exact match
5. manifest identity/version/target consistency
6. dependency/Capability/protocol compatibility
7. archive extraction safety
8. target owner preflight

failure は terminal reject。warning-only continuation 禁止。

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

## 9. Third-party trust semantics

Third-party package は signature があっても official trust root へ chain しない限り OFFICIAL ではない。

locally configured trusted key で signature 検証できた場合は `THIRD_PARTY_LOCAL_TRUST`。
それ以外は `THIRD_PARTY_UNKNOWN`。

UI は source、digest、signer、trust tier、simulation/persistent impact を commit 前に表示する。

## 10. Addon apply semantics

Addon install/update/disable/remove は target owner acknowledgement を terminal success 条件とする。

state machine:

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
- restart-required activation は installed/active を別 state で返す
- live activation は addon が explicit safe-step contract を宣言し target owner が support する場合のみ
- apply failure は previous active version を維持する
- inconsistent addon config/dependency のまま component を standard startup しない

## 11. Retry/idempotency

- prepare は same immutable action digest に対して同一 plan を再利用してよいが expiration/state change を超えて再利用しない。
- commit は OperationId + immutable payload digest で idempotent。
- same OperationId / different digest は protocol violation/reject。
- MessageId/CorrelationId を dedup identity としない。
- network retry で package install を二重 apply しない。

## 12. Audit mapping

次は audit mandatory:

- failed/successful login security events
- permission reject
- Config change request/result
- operational command request/result
- high-impact prepare/commit/result
- addon stage metadata creation
- addon install/update/disable/remove
- official verification failure
- audit read

minimum correlation:

```text
AuditRecordId
ActorAccountRef
SessionGeneration
OperationId optional
ImmutablePayloadDigest optional
CorrelationId optional
PlanId/PlanDigest optional
ActionKind
Target
ResultCode
EffectiveStep/Boundary optional
ResultingConfigGeneration optional
AddonInventoryGeneration optional
```

## 13. Forbidden

- arbitrary shell command over `operational.command`
- direct component filesystem edit from Administration View
- direct Administration View→Simulation Core connection
- UI-only authorization
- high-impact direct apply without server-side prepare/commit
- stale ConfigGeneration silent overwrite
- package bytes in generic addon functional payload area
- official hash verification without publisher signature verification
- third-party trust label promotion to official
- upload completion = install success
- ACK = terminal target effect success

## 14. Implementation follow-up

Phase 0 completion requires the canonical protobuf/message registry to encode the new high-impact/addon management messages defined in this addendum and parent documents to reference this contract without contradictory "未確定" wording.
