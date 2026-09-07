# Admin View設計

Status: Phase 0 contract complete

## 1. 目的

Admin ViewはMachiVerse各componentを運用・診断・設定するsystem operator向けUIです。

General Viewの利用者roleとは別auth/authz domainであり、General View Administrator権限をAdmin View permissionとして扱いません。

Admin Viewは各componentの公開可能なhealth/status/metrics、structured log、Config、operational command、simulation Admin Operation、audit、Addon managementを扱います。

Phase 0詳細は `admin-view-phase0-design.md`、wire contractは `../protocols/gateway-admin-view.md` と `../protocols/schema/` を正本とします。

## 2. External boundary

Admin Viewが接続するmanagement endpointはGatewayのみです。

```text
Admin View -> connected Gateway -> authoritative owner/component
```

- Simulation Coreへ直接接続しない。
- component filesystem、process private API、database、internal DTO、DLLへ直接依存しない。
- Gatewayがexternal authn/authz、permission、request format、target、allowed conditionを検証する。
- target ownerは自身が所有するConfig consistency、state invariant、dependency、safe apply boundaryを検証する。
- target ownerのterminal acknowledgement前にstate-changing actionをsuccess表示しない。

## 3. Auth / permission domain

- General ViewとAdmin Viewのauth/authz domainを分離する。
- General View AdministratorをAdmin View operatorへ自動昇格しない。
- loginはGateway経由とし、既存Master-auth contractに従う。
- privilege change/revokeはsession generationへ反映し、old privilegeでnew Admin actionを継続させない。
- authorization outage時もpermission checkをbypassしない。
- permissionはUI表示だけでなくGatewayがdeny-by-defaultで強制する。

Phase 0 permission token:

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

role→permission mapping、IdP、credential/session storage technologyはdeployment policy/implementation choiceです。

## 4. 主な責務

- component inventory/reachability表示
- health/status/metrics表示
- structured log検索・correlation
- Config参照・validation/classification表示
- permitted Config change request
- operational command
- simulation Admin Operation
- high-impact prepare/confirm/commit
- audit trail参照
- protocol / Capability mismatch診断
- Gateway resync / MasterGeneration表示
- save / replay / recovery status表示
- Addon inventory/catalog/install/update/disable/removeの運用入口

## 5. 責務外

- General View利用者向け参加機能
- Diver / Spectator / Moderator / General View Administrator role提供
- authoritative World State管理
- simulation rule implementation
- Core internal mutable stateへのdirect access
- other component Config fileのdirect edit
- component internal code/APIへのdirect dependency
- UI-only authorization
- arbitrary shell/script実行
- generic Undoによるhistory消去
- addon implementationそのものの実行・改変
- standard protocolへのaddon-specific functional payload追加

## 6. Component health / metrics

Admin Viewは一般metricとMachiVerse固有stateを表示します。

baseline共通表示:

- component kind / logical instance
- readiness / health
- protocol version / Capability state
- uptime
- CPU / memory
- connection state
- ConfigGeneration / validation state
- last observation time
- active warning/error condition

baseline polling intervalは5秒、15秒を超えて更新できないsampleはSTALEとして表示します。値はoperational Configで変更可能です。

Core:

- current Simulation Step
- running/pause state
- target step rate / observed lag
- pending operation count
- save/replay/recovery state
- last completed savepoint step

Gateway:

- readiness
- Master/non-Master/transition role
- MasterGeneration/current Master identity
- resync state / last confirmed basis step
- publication buffer utilization
- retry/dedup diagnostics
- General View / Admin View connection count

General/Admin View側のclient-private metricをauthoritative server stateとして扱いません。

## 7. Structured log

standardはquery + bounded cursor paginationです。live tailはoptional capabilityです。

query filter:

- component/instance
- time range
- severity/event kind
- CorrelationId
- OperationId
- Simulation Step
- MasterGeneration
- page size / opaque cursor

`page_size=0`はdefault 200、accepted rangeは1..1000、cursorは最大256 bytesです。

secret/credential/private key/secret Config valueはsource/collector側でredactし、Gateway/Admin Viewがsecretを受信後に隠す設計を標準としません。

Audit logとhigh-volume diagnostic logは別retention policyを持てます。

## 8. Config参照・変更

### 8.1 Ownership

各componentが自身のConfig fileを所有します。
Admin Viewはfileをdirect read/writeせず、Gateway管理protocolを通じてowner-published state/change requestを扱います。

### 8.2 Read classification

Config itemは必要に応じ次を表示します。

- effective valueまたはredacted state
- operational / simulation impact
- runtime mutable / restart required / world regeneration required
- validation state
- sensitive flag
- ConfigGeneration

secretはcurrent valueをread-backしません。

### 8.3 Change semantics

- stable OperationId / immutable payload digestを使用する。
- expected base ConfigGenerationでoptimistic concurrencyを行う。
- Gatewayがpermission/target/classification/allowed conditionを検証する。
- target ownerがtype/range/cross-constraint/state consistencyを検証する。
- invalid change setをpartial applyしない。
- simulation-affecting runtime changeはauthoritative effective Simulation Step/historyへ結び付ける。
- restart/world-regeneration required itemはruntime applyしない。
- generic Undoは設けず、revertもnew Config changeとしてauditする。

simulation-affecting Config changeはhigh-impactです。

## 9. Operational command

Admin Viewはdefined command registryのみをrequestできます。

Phase 0 baseline:

```text
gateway.resync.request
world.save.create
world.pause
world.resume
component.restart.request
component.shutdown.request
diagnostic.snapshot.create
```

Commandごとにtarget、permission、payload schema、simulation impact、safe boundary、idempotency、timeout、result semanticsを固定します。

`world.pause` / `world.resume` / component restart/shutdownはhigh-impactです。

shell text、executable path、free-form scriptをstandard commandとして送りません。

## 10. Simulation Admin Operation

Simulationへ影響するAdmin OperationはAdmin View→Gateway→Simulation Coreの既存operation pathを使用します。

Gateway:

- Admin authn/authz
- permission
- operation format
- target
- Admin operationとしてのallowed condition
- protocol-level validation

Simulation Core:

- UI roleを解釈しない。
- World State invariant / state-transition consistencyを維持する。
- Gateway-approved Admin Operationでも一般不変条件を破壊するtransitionを無条件適用しない。

Simulation-affecting Admin OperationはAdmin由来という理由だけでunconditional highest priorityにしません。
network arrivalやUI processing orderをworld resultの決定要因にせず、existing scheduling/MasterGeneration contractに従います。

## 11. High-impact operation

Phase 0 baseline high-impact:

- simulation-affecting Config change
- world pause/resume/time-control family
- component restart/shutdown
- destructive/bulk world operationが将来追加された場合
- simulation/persistent-dataへ影響するAddon action
- third-party addon install/update

high-impactはdirect one-shot applyを禁止し、server-side `prepare → plan → confirm → confirmed → commit → result` flowを必須とします。

Plan/confirmation artifactはOperationIdとは別identityで、expiryを持ちます。confirmation artifactはsingle-useであり、commit成功またはterminal consumption後にreplayできません。

Phase 0 standardはsingle-operator confirmationです。multi-person approvalは将来Capabilityで追加可能です。

## 12. Audit / no generic Undo

state-changing Admin actionとsecurity-sensitive readをauditします。

少なくとも次を追跡可能にします。

- actor account reference
- session generation / permission context
- request time
- OperationId / immutable payload digest
- CorrelationId
- operation/action kind
- target
- PlanId/PlanDigest when high-impact
- request summary without secret values
- effective Simulation Step / restart boundary
- result / stable reject code
- resulting ConfigGeneration / Addon inventory generation where applicable

Audit read自体も監査します。

Generic Undoは設けません。元へ戻す場合もnew Operationを実行し、そのOperationもauditします。Savepoint recovery/replayは別conceptです。

Audit retentionはdiagnostic logと分離し、Phase 0 baseline defaultは180日、deployment policyで延長可能とします。

## 13. Pauseとの関係

- Pause中もAdmin requestの受信・auth・non-world mutation処理を可能にする。
- simulation-affecting Operationをstopped Stepへ曖昧applyしない。
- Resume後のexplicit valid Stepへexisting deterministic scheduling contractでassignmentする。
- simulation-non-affecting commandはPause中に実行可能なcategoryを持てる。

## 14. Addon management

Admin ViewをAddon managementのstandard operator入口とします。

対象:

- inventory
- official catalog
- staging metadata
- install/update/disable/remove plan
- dependency/Capability/protocol compatibility
- trust/signature/digest state
- restart/safe-boundary requirement
- persistent-data impact
- result/audit

Addonはcomponent-scopedであり、Admin Viewがtarget component filesystemへ直接copyしません。

## 15. Addon identity / compatibility

minimum manifest metadata:

- reverse-DNS style `addon_id`
- SemVer 2.0.0 `version`
- target component kinds
- compatible protocol/version range
- required/provided Capability
- dependency addon/version range
- config schema version
- persistent-data compatibility/migration declaration
- artifact SHA-256
- publisher identity/trust metadata when signed

Version rangeはPhase 0では次のportable comparator conjunctionを標準とします。

```text
=1.2.3
>=1.2.0 <2.0.0
>=2.1.0
<3.0.0
```

- comparatorは`=`, `>`, `>=`, `<`, `<=`。
- whitespace区切りはAND。
- OR expressionはStandard v1では使用しない。
- version operandはSemVer 2.0.0。

Addon構成/依存/Config/Capability不整合を抱えたままsilent degraded startupしません。

## 16. Official addon trust

Official addonはGateway-configured official store/catalogから管理します。

Official verification baseline:

1. HTTPS取得
2. catalog/manifest signature verification
3. Ed25519 signer chain to pinned official trust root
4. artifact SHA-256 exact match
5. addon identity/version/target consistency
6. dependency/Capability/protocol compatibility
7. archive extraction safety
8. target owner preflight

hashだけをpublisher identity proofとしません。

trust root rotationはold trusted rootで署名されたkeyset update、またはhigh-impact相当のexplicit trust-root Config changeで行います。

## 17. Third-party addon trust

Third-party addonはofficialと明確に区別します。

Trust tier:

```text
OFFICIAL
THIRD_PARTY_LOCAL_TRUST
THIRD_PARTY_UNKNOWN
```

- local trusted signerがあってもOFFICIALへ昇格しない。
- source、SHA-256、signature/signer、trust tier、target、Capability/dependency、simulation/persistent impactをcommit前に表示する。
- third-party install/updateは `admin.addon.manage.third-party` とhigh-impact confirmationを必須とする。

## 18. Addon staging / apply

package installationはfilesystem direct copyではなくstaging/validation/plan/commit/applyで行います。

```text
STAGED -> VALIDATED -> PREPARED -> COMMITTED -> APPLY_PENDING -> APPLIED
                         |             |
                         +-> REJECTED  +-> FAILED
```

package bytesはnormal Standard Protocol WebSocket payloadへ載せません。

third-party package uploadはauthenticated BFF HTTPS staging endpointを使用し、GatewayがSHA-256を計算してopaque StagedPackageIdを返します。upload完了だけではinstall/loadしません。

archive traversal、absolute path、symlink escape、duplicate canonical path、configured size/count limit violationをrejectします。

Target ownerはatomic installし、in-place partial updateを行いません。live activationはexplicit safe-step contractがある場合のみ許可し、それ以外はrestart boundaryを要求します。apply failure時はprevious active versionを維持します。

## 19. Protocol / Capability error display

Admin Viewは少なくとも次をstable machine codeで診断可能にします。

- Major protocol mismatch
- required Capability missing
- Addon compatibility/trust mismatch
- Config invalid/stale generation
- Master generation/problem
- Gateway resync
- save/recovery incompatibility
- high-impact stale/expired confirmation

Machine behaviorをdiagnostic string比較へ依存させません。

## 20. 後続Phaseへ委ねるimplementation choice

Phase 0でarchitecture/protocol semanticsは確定済みです。

後続Phaseで選択してよいもの:

- UI framework/component library
- IdP/session storage実装
- observability collector/storage product
- supervisor/deployment integration
- official store hosting product
- audit storage engine
- browser-side state management
- optional multi-person approval implementation

これらの実装選択は本contractをsilentに変更してはなりません。
