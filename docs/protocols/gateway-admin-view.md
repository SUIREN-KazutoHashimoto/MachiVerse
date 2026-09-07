# Gateway・Admin View間Protocol設計書

Status: Phase 0 contract complete  
ProtocolId: `mv.gateway-admin-view`

## 1. 所有者と正本

本protocolのexternal boundary ownerはGatewayです。

共通 envelope / version / Capability / result / error / correlation contractは `docs/design/phase1-protocol-envelope.md`、wire payloadは `docs/protocols/schema/*.proto`、MessageType mappingは `docs/protocols/schema/message-registry-v1.md` を正本とします。

Administration View Phase 0固有semanticsは `gateway-admin-view-phase0.md` を併読します。

## 2. 目的

Admin ViewがMachiVerse各componentを運用・診断し、許可されたConfig change、operational command、simulation Admin Operation、audit参照、Addon managementを要求するためのexternal contractです。

Admin ViewはGeneral Viewの上位roleではなく、system operator向けの別auth/authz domainです。

## 3. External management boundary

Administration Viewが接続するmanagement endpointはGatewayのみです。

```text
Administration View -> connected Gateway -> authoritative owner/component
```

- Admin ViewからSimulation Coreへ直接接続しない。
- Admin Viewからcomponent filesystem、process private API、database、internal DTO、DLLへ直接依存しない。
- Gatewayがexternal authn/authz、permission、request format、target、allowed conditionを検証する。
- target ownerは自身が所有するConfig consistency、state invariant、dependency、safe apply boundaryを検証する。
- Coreへ影響するOperationは既存Gateway/Core authority・MasterGeneration・scheduling contractに従う。
- Core以外のcomponent managementについてもAdmin Viewから見たexternal contractはGateway-ownedとし、direct component management connectionを標準としない。
- state-changing actionはtarget ownerのterminal acknowledgement前にsuccessとして扱わない。

## 4. Common envelope / Version / Capability

normal messageは `WireEnvelopeV1` を使用します。

- protocol id: `mv.gateway-admin-view`
- negotiated ProtocolVersion / NegotiationGenerationを明示する。
- MessageId / CorrelationId / CausationIdはtrace用でありcredential/dedup identityではない。
- world-related request/resultはWorldContextを使用する。
- state-changing requestは必要なOperationContextを使用する。
- reconnect時にCapability negotiationをやり直す。
- required Capability不足をsilent degradationしない。

最小baseline capabilityはMessage Registryに従い、log/config/operation/audit/high-impact/addon等はfeature capabilityでgateします。

## 5. Auth / session / permission

- General ViewとAdmin Viewのauth/authz domainを分離する。
- General View AdministratorをAdmin View operatorへ自動昇格しない。
- login requestはconnected Gatewayから既存Master-auth contractへproxyし、non-Masterが独立finalizeしない。
- authorization outage時もpermission checkをbypassしない。
- privilege change/revokeはsession generationへ反映し、old privilegeによるnew privileged actionを許可しない。
- state-changing commit時にもpermission/session generationを再評価する。

Phase 0 permission tokenは `gateway-admin-view-phase0.md` を正本とし、role名ではなくstable permission tokenをGatewayがdeny-by-defaultで評価します。

Credential、IdP、session storage等の実装技術はdeployment implementation choiceであり、上記contractを変更しません。

## 6. Health / status / metrics

`component.health.query` / `component.health.result` を使用します。

Admin Viewは少なくとも次を診断可能にします。

- Core current Simulation Step / lag / pause state
- Master Gateway identity / generation
- Gateway readiness / resync state
- protocol / Capability mismatch
- Config generation / validation state
- Operation retry / dedup diagnostics
- save / recovery state
- CPU / memory / connection等のoperational metric

`HealthQueryV1.targets` emptyはpermission上visibleな全component、`metric_names` emptyはbaseline metric setを意味します。

sample timestampを保持し、stale sampleをfreshとして再timestampしません。

## 7. Structured log

`component.log.query` / `component.log.page` を使用します。

standard queryはtarget、time range、severity/event kind、CorrelationId、OperationId、Simulation Step、MasterGeneration、bounded page/cursorを扱えます。

- `page_size=0` はdefault 200。
- accepted rangeは1..1000。
- cursorはopaque、最大256 bytes、query filterへbindする。
- secret/credential/private keyはsource/collector側でredactし、Gatewayへ渡さない。
- audit logとhigh-volume diagnostic logは別retentionを持てる。

live tailは将来optional Capabilityとし、Phase 0 standard requirementではありません。

## 8. Config read / change

### 8.1 Read

`config.read` / `config.read.result` を使用します。

- Admin View/Gatewayはother component Config fileを直接readしない。
- owner componentがeffective value、classification、validation、ConfigGenerationを公開する。
- sensitive itemはvalueを返さずredacted stateを返す。

### 8.2 Change

`config.change` / `config.change.result` を使用します。

- OperationId / immutable payload digest必須。
- `expected_base_generation` mismatchは `config.stale-generation`。
- one request = one target componentのatomic change set。
- invalid itemを含む場合はpartial applyしない。
- simulation-affecting changeはauthoritative effective Step/historyへ結び付ける。
- restart/world-regeneration required itemはruntime applyしない。
- generic Undoは設けず、revertもnew Config changeとする。

simulation-affecting Config changeはhigh-impact flowを必須とします。

## 9. Operational command

`operational.command` はregistered commandのみを表現します。

Phase 0 baseline command registry:

| command_kind | High impact |
|---|---:|
| `gateway.resync.request` | no |
| `world.save.create` | no |
| `world.pause` | yes |
| `world.resume` | yes |
| `component.restart.request` | yes |
| `component.shutdown.request` | yes |
| `diagnostic.snapshot.create` | no |

- arbitrary shell/script/pathを送らない。
- command-specific payloadはregistered schema id/versionと一致させる。
- Phase 0 standard commandはOperationId / immutable digestを持つ。
- unsupported supervisor/deployment capabilityは `operation.unsupported`。
- accepted/queuedをterminal successと同一視しない。

## 10. Simulation Admin Operation

Simulationへ影響するAdmin OperationはAdmin View→Gateway→Simulation Core pathを使用します。

GatewayはAdmin authn/authz、format、target、Admin operationとしてのallowed conditionを検証します。
CoreはUI roleを解釈せず、全Operation共通のWorld State invariant/state-transition consistencyを維持します。

Simulation-affecting Admin OperationをAdmin由来という理由だけでunconditional highest priorityにしません。network arrival/UI processing timingをauthoritative orderingに使用せず、Coreが最終effective Stepを確定します。

Admin ViewはMaster/non-Master routingを選択しません。connected Gatewayが既存authority/MasterGeneration contractに従って正しいowner pathへrouteします。

## 11. High-impact prepare / confirm / commit

high-impact actionはordinary direct applyを禁止します。

canonical flow:

```text
A -> G  admin.action.prepare
G -> A  admin.action.plan
A -> G  admin.action.confirm
G -> A  admin.action.confirmed
A -> G  admin.action.commit
G -> A  admin.action.result
```

Planとconfirmation artifactはOperationIdとは別identityです。

- PlanId / PlanDigestはnormalized action、target、owner generation/dependency snapshot、required boundaryをcoverする。
- confirmation challenge/artifactは期限付きとする。
- confirmation artifactはserver-side stateとbindし、commit成功またはterminal consumption後に再利用不可とする。
- session generation/permission/target stateが変わればcommit時に再検証する。
- stale/expired/missing confirmationを明示rejectする。
- Phase 0はsingle-operator confirmation。multi-person approvalは将来Capabilityで追加可能。

## 12. Audit

`audit.query` / `audit.page` を使用します。

state-changing Admin action、permission reject、high-impact prepare/confirm/commit/result、Addon stage/apply、official verification failure、audit read等を監査します。

Audit recordはactor reference、session generation、OperationId/digest、CorrelationId、action、target、Plan identity、effective boundary、result code、resulting generation等を必要に応じ保持し、secret value/credential/private keyを含めません。

Audit read自体も監査します。

## 13. Addon management

standard protocolに載せるAddon情報はmanagement/safety metadataのみです。

- `addon.inventory.query/result`
- `addon.catalog.query/page`
- `admin.action.*` によるinstall/update/disable/remove plan/commit/result

package bytesはnormal WebSocket payloadへ載せません。third-party package stagingはauthenticated BFF HTTPS endpointを使用します。

Official addonはpinned official trust rootからEd25519 signatureを検証し、artifact SHA-256を照合します。hash一致のみをpublisher proofとしません。

Third-party addonは`OFFICIAL`へ昇格せず、local-trust/unknownを明示します。third-party install/updateは常にhigh-impactです。

Addon-specific functional payload/command/generic extension areaをstandard protocolへ持ち込みません。

## 14. Result / retry / idempotency

共通ResultStatus / ResultCode / RetryAdviceを使用します。

少なくとも次を区別します。

- accepted / pending
- success / no-change
- authorization reject
- invalid format/target/allowed condition
- target invariant/config reject
- duplicate/already processed
- stale ConfigGeneration / stale plan
- late Operation
- temporarily unavailable / resyncing
- version/Capability incompatibility
- addon trust/compatibility failure
- internal failure

state-changing retryはOperationId + immutable payload digestをidentityとし、MessageId/CorrelationIdをdedup keyにしません。

## 15. Forbidden

- General View AdministratorのAdmin View permissionへのautomatic promotion
- Admin Viewからcomponent internal implementationへのdirect access
- Admin View/Gatewayによるother component Config fileのdirect edit
- UI-only authorization
- unauthorized Admin Operation forwarding
- arbitrary shell/script/path command
- generic Undoによるhistory消去
- simulation-affecting Admin Operationのunconditional highest priority
- candidate Stepをauthoritative effective_stepとして扱うこと
- stale ConfigGeneration silent apply
- required Capability不足のsilent degradation
- high-impact direct apply / confirmation bypass
- confirmation artifactをOperationId代替として扱うこと
- standard protocolへのaddon functional payload
- third-party trustのofficial昇格
- ACKをterminal effect successと同一視すること

## 16. Phase 0で確定し、後続Phaseへ委ねるもの

Phase 0でprotocol semantics、permission boundary、message schema、high-impact confirmation、Addon trust/management contractは確定済みです。

後続Phaseで選択してよいのはUI framework、IdP/session storage implementation、observability backend、supervisor implementation、official store hosting、audit storage engine等のimplementation technologyです。これらは本contractをsilentに変更してはなりません。
