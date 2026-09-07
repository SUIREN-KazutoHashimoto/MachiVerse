# Administration View Phase 0 設計確定

Status: Complete / Issue #38  
Tracking: Issue #38  
Roadmap: `../roadmaps/administration-view.md`

## 1. 目的

Administration View Phase 0では、実装開始前にsystem-operator向けmanagement boundaryをArchitecture/Protocol/schemaまで固定します。

Canonical parent documents:

- `admin-view.md`
- `admin-operation-safety.md`
- `addon-boundary-safety.md`
- `../protocols/gateway-admin-view.md`
- `../protocols/gateway-admin-view-phase0.md`
- `../protocols/schema/payloads.proto`
- `../protocols/schema/message-registry-v1.md`

本書はPhase 0のdecision index/acceptance recordであり、wire field number等はprotobuf schemaを正本とします。

## 2. External management boundary

```text
Administration View -> connected Gateway -> authoritative owner/component
```

- Administration ViewはSimulation Coreへ直接接続しない。
- component filesystem/process private API/database/internal DTO/DLLへ直接依存しない。
- Gatewayはexternal authn/authz、permission、format、target、allowed conditionを検証する。
- target ownerは自身のConfig consistency、state invariant、dependency、safe apply boundaryを検証する。
- Core-bound Operationはexisting authority/MasterGeneration/scheduling contractに従う。
- terminal owner acknowledgement前にstate-changing actionをsuccessとしない。

## 3. Permission boundary

Gatewayがstable permission tokenをdeny-by-defaultで評価します。

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

General View AdministratorはAdmin View permissionを意味しません。UI表示だけをauthorizationに使用しません。state-changing commit時にsession generation/permissionを再評価します。

## 4. Observability contract

### Health / status

- component identity/readiness/health
- protocol/Capability state
- CPU/memory/connection
- ConfigGeneration/validation
- current Simulation Step/lag/pause
- Master identity/MasterGeneration/resync
- retry/dedup diagnostics
- save/recovery state

baseline pollingは5秒、15秒超の未更新sampleはSTALEです。値はoperational Configで変更可能です。

### Structured log

standardはquery + opaque cursor paginationです。

- page default 200、range 1..1000
- target/time/severity/event/CorrelationId/OperationId/Simulation Step/MasterGeneration filter
- cursor最大256 bytes、query filterへbind
- source/collector側でsecret redaction
- audit logとdiagnostic logは別retention

## 5. Config contract

- owner componentのConfig fileをAdmin View/Gatewayがdirect read/writeしない。
- `config.read`でeffective value/redacted state、impact、mutability、validation、ConfigGenerationを取得する。
- secret valueはread-backしない。
- `config.change`はOperationId + immutable digest + expected ConfigGenerationを使用する。
- stale generationは明示reject。
- change setはatomic、partial apply禁止。
- simulation-affecting changeはauthoritative effective Step/historyへ結び付ける。
- restart/world-regeneration required itemをruntime applyしない。
- revertもnew Config changeとする。

simulation-affecting Config changeはhigh-impactです。

## 6. Operational command contract

Phase 0 baseline registry:

```text
gateway.resync.request
world.save.create
world.pause
world.resume
component.restart.request
component.shutdown.request
diagnostic.snapshot.create
```

arbitrary shell/script/pathは許可しません。command-specific payloadはregistered schemaで固定し、standard commandはOperationId/digestを持ちます。

`world.pause`, `world.resume`, component restart/shutdownはhigh-impactです。

## 7. Simulation Admin Operation

Simulation-affecting Admin OperationはAdmin View→Gateway→Core pathを使用します。

GatewayはAdmin permission/admissionを担当し、CoreはUI roleを解釈せずWorld State invariantとdeterministic state transitionを維持します。

Admin由来だけを理由にunconditional highest priorityにせず、network arrival/UI timingをauthoritative world orderingに使用しません。

## 8. High-impact confirmation

high-impact actionはordinary direct one-shot applyを禁止します。

```text
prepare -> plan -> confirm -> confirmed -> commit -> result
```

Canonical payload:

- `AdminActionPrepareV1`
- `AdminActionPlanV1`
- `AdminActionConfirmV1`
- `AdminActionConfirmationV1`
- `AdminActionCommitV1`
- `AdminActionResultV1`

Plan/confirmation artifactはOperationIdとは別identityでexpiryを持ちます。confirmation artifactはserver-side stateへbindしsingle-useです。commit時にsession/permission/target generation/dependency/trust stateを再検証します。

Phase 0 standardはsingle-operator confirmationです。

## 9. Audit

state-changing Admin actionとsecurity-sensitive readを監査します。

minimum context:

- actor reference
- session generation / permission context
- OperationId / immutable digest
- CorrelationId
- action/target
- Plan identity when applicable
- effective Step/boundary
- result/reject code
- resulting ConfigGeneration / Addon inventory generation

secret/credential/private keyはaudit payloadへ含めません。audit read自体も監査します。

Retentionはdiagnostic logから独立し、baseline default 180日、deployment policyで延長可能です。

## 10. Addon identity / compatibility

Addonはcomponent-scopedです。

minimum manifest:

- reverse-DNS style `addon_id`
- SemVer 2.0.0 version
- target component
- protocol version range
- required/provided Capability
- dependency/version range
- Config schema
- persistent-data compatibility/migration
- artifact SHA-256
- publisher/trust metadata

Version rangeは`=`, `>`, `>=`, `<`, `<=` comparatorのwhitespace ANDをStandard v1とし、ORは使用しません。

Addon-specific functional payloadをStandard Protocolへ載せません。

## 11. Official / third-party trust

Official:

- configured official store/catalogをGatewayが扱う。
- HTTPS取得。
- Ed25519 signatureをpinned official trust rootへchain検証。
- artifact SHA-256 exact match。
- hash aloneをpublisher proofとしない。
- compatibility/dependency/archive safety/owner preflightをactivation前に実施。

Third-party trust tier:

```text
THIRD_PARTY_LOCAL_TRUST
THIRD_PARTY_UNKNOWN
```

local trustがあってもOFFICIALへ昇格しません。third-party install/updateは`admin.addon.manage.third-party`とhigh-impact confirmationを必須とします。

## 12. Addon staging / lifecycle

package bytesはnormal Standard Protocol WebSocketへ載せません。

Third-party uploadはauthenticated BFF HTTPS staging endpointを使用し、GatewayがSHA-256を計算してopaque StagedPackageIdを返します。uploadだけではinstall/loadしません。

Canonical lifecycle:

```text
STAGED -> VALIDATED -> PREPARED -> COMMITTED -> APPLY_PENDING -> APPLIED
                         |             |
                         +-> REJECTED  +-> FAILED
```

- validation前にcode loadしない。
- archive traversal/absolute path/symlink escape/duplicate canonical path/size-count violationをreject。
- target ownerがatomic install。
- in-place partial update禁止。
- live activationはexplicit safe-step contractがある場合のみ。
- failure時はprevious active versionを維持。
- dependency/persistent-data impactをdisable/remove前に検証。

## 13. Canonical wire closure

Phase 0で次をCanonical Standard Protocol v1へ反映済みです。

- existing health/log/config/command/audit semanticsの追加field
- high-impact plan/confirmation/commit payload
- Addon inventory/catalog/action intent payload
- `admin.action.*` Message Registry mapping
- `addon.inventory.*` / `addon.catalog.*` mapping
- feature Capability gate

High-impact actionをconfirmation Capability不足時にordinary pathへdowngradeしません。

## 14. Phase 0 acceptance mapping

| Issue #38 item | Resolution |
|---|---|
| Gateway↔Administration View Protocol | canonical protocol/schema/registry fixed |
| log/status requirements | health/log contract fixed |
| Config reference/change | optimistic concurrency + safe boundary fixed |
| operational command/permission | permission tokens + command registry fixed |
| Addon management | inventory/catalog/action lifecycle fixed |
| official store relation | Gateway-owned catalog/trust decision fixed |
| official hash/trust | Ed25519 + SHA-256 + pinned trust root fixed |
| third-party distinction | explicit trust tier/risk boundary fixed |
| Addon installation | staging/validation/atomic apply fixed |
| audit/safety | high-impact confirmation + audit contract fixed |

## 15. Explicit later-phase implementation choices

次はPhase 0 blockerではありません。

- UI framework/component library
- IdP/session storage implementation
- observability backend
- deployment supervisor integration
- official store hosting product
- audit storage engine
- browser state-management library
- optional multi-person approval
- Addon functional extension framework/additional protocol API
- exact Addon archive container format（Phase 6 implementation前にpackage-format versionとして固定）

これらは後続Phaseで選択できますが、Phase 0のsecurity/protocol boundaryをsilentに変更してはなりません。
