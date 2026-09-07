# Admin操作・安全性設計

Status: Phase 0 contract complete

本書はQ235〜Q239、Q275/Q276とAdministration View Phase 0設計を統合したAdmin操作の安全境界です。

## 1. Admin View操作の経路と責務

- Admin ViewからSimulation Coreへ直接接続しない。
- external management pathは `Admin View -> connected Gateway -> authoritative owner/component` とする。
- Admin操作は明示的なOperation/change requestとして識別し、監査・追跡可能にする。
- GatewayはAdmin authn/authz、permission、操作形式、対象、allowed condition、Protocol整合性を検証する。
- target ownerは自身が所有するConfig consistency、dependency、safe boundaryを検証する。
- CoreはGeneral/Admin ViewのUI roleを解釈せず、全状態遷移共通のWorld State invariant、参照整合性、deterministic scheduling contractを維持する。
- GatewayがAdmin操作として許可したことだけを理由に、Coreが一般不変条件を破壊する操作を適用してはならない。

## 2. Admin操作とSimulation ordering

- network arrival order、Gateway thread order、browser/UI processing speedをworld resultの決定要因にしない。
- simulation-affecting Admin Operationは既存Operation scheduling、MasterGeneration、candidate/effective Step contractに従う。
- authoritative effective StepはCore/owner確定resultで返す。
- Admin ViewはMaster routingを選ばず、connected Gatewayが既存authority contractに従ってrouteする。

simulation-non-affecting operationのみAdmin優先実行を許容します。Phase 0 baseline:

- health/metrics/log/audit read
- protocol/session diagnostics
- diagnostic snapshot creation
- Gateway resync control
- save creationそのものがlogical World Stateを変更しない範囲

次はsimulation/system impactを持つため通常優先扱いにしません。

- simulation-affecting Config change
- simulation Admin Operation
- pause/resume/time-control
- component restart/shutdown
- Addon activation/deactivation/updateでsimulation/persistent stateへ影響するもの

## 3. High-impact classification

Phase 0 baseline high-impact:

- simulation-affecting Config change
- world pause/resume/time-control family
- component restart/shutdown
- destructive/bulk world operationが将来追加された場合
- simulation-affecting Addon install/update/disable/remove
- persistent world/save compatibilityへ影響するAddon action
- third-party Addon install/update

classificationはUI表示ではなくGateway permission/admissionで強制します。

## 4. High-impact prepare / confirm / commit

High-impact actionはordinary direct pathでterminal applyしません。

canonical flow:

```text
Admin View -> Gateway: admin.action.prepare
Gateway -> Admin View: admin.action.plan
Admin View -> Gateway: admin.action.confirm
Gateway -> Admin View: admin.action.confirmed
Admin View -> Gateway: admin.action.commit
Gateway -> Admin View: admin.action.result
```

### Plan

Planはserver-generated immutable planning artifactです。

最低限次をbindします。

- PlanId / PlanDigest
- ActionKind
- OperationId / immutable payload digest
- target
- risk level
- required permissions
- simulation impact
- required boundary
- owner generation/dependency snapshot
- warning codes
- session generation
- expiration
- confirmation challenge identity / expiration

### Confirmation

- confirmation challenge/artifactはOperationIdとは別identity。
- confirmationはserver-side plan/session stateへbindする。
- confirmation artifactはexpiryを持つ。
- commit成功またはterminal consumption後はsingle-useとして再利用不可。
- confirmation artifactをcredentialやOperation dedup identityとして扱わない。
- client-side booleanだけでhigh-impact confirmation成立とみなさない。

### Commit

Gatewayはcommit時に少なくとも次を再検証します。

- plan existence / expiry
- PlanDigest
- confirmation existence / expiry / unused state
- OperationId / immutable payload digest
- active session / session generation
- required permissions
- target owner generation/state
- dependency/trust snapshot
- required safe boundary

stale/expired stateは再prepareを要求し、old planをsilent applyしません。

Phase 0 standardはsingle-operator confirmationです。multi-person approvalはStandard v1の必須条件ではなく、将来Capabilityで拡張可能です。

## 5. Permission enforcement

Gatewayはdeny-by-defaultでstable permission tokenを評価します。

State-changing requestでは少なくともadmission時とcommit時に再評価します。

High-impactでは通常permissionに加えて `admin.operation.high-impact` を要求します。

Third-party Addonは `admin.addon.manage.third-party`、official Addonは `admin.addon.manage.official` を別permissionとして扱います。

Privilege revoke/session-generation change後にold privilegeでnew privileged commitを許可しません。

## 6. Idempotency / retry safety

- state-changing identityはOperationId + immutable payload digest。
- same OperationId / same digest retryは同じlogical operationとして扱う。
- same OperationId / different digestはrejectする。
- MessageId / CorrelationId / PlanId / confirmation idをOperation dedup identityにしない。
- ACK/accepted/queuedをterminal owner effect successと同一視しない。
- disconnect/retryでhigh-impact actionやAddon applyを二重実行しない。

## 7. Audit

state-changing Admin actionとsecurity-sensitive readを監査します。

minimum audit context:

- AuditRecordId
- actor account reference
- session generation / permission context
- request timestamp
- OperationId / immutable payload digest
- CorrelationId
- action/operation kind
- target
- PlanId / PlanDigest when applicable
- confirmation lifecycle event where applicable
- request summary without secret value
- effective Simulation Step / safe boundary
- result/status code
- reject reason code
- related ConfigGeneration / Addon inventory generation

Audit対象には少なくとも次を含めます。

- login security event
- permission reject
- Config change request/result
- operational command request/result
- high-impact prepare/confirm/commit/result
- Addon stage/apply
- official verification failure
- audit read

Audit retentionはdiagnostic logから分離し、baseline default 180日、deployment policyで延長可能とします。

## 8. No generic Undo

- 一般的なUndo機能は設けない。
- 実行済みAdmin操作をhistoryから消して巻き戻さない。
- 元の設定/状態へ戻す場合はnew Operation/change requestとして実行する。
- compensating/revert operationも通常のauthn/authz、OperationId、validation、audit対象とする。
- Savepoint recovery/replayは別のrecovery conceptでありUndoではない。

## 9. Pauseとの関係

- Pause中もauth、observation、non-world-mutating operational actionは可能。
- simulation-affecting Operationをstopped Stepへ曖昧applyしない。
- Resume後のexplicit valid Stepへexisting deterministic scheduling contractでassignmentする。
- high-impact planがPause/Resumeやowner generation変化でstaleになった場合、reprepareを要求する。

## 10. Forbidden

- Admin View→Core direct connection
- UI-only authorization
- privilege outage時のpermission bypass
- high-impact direct one-shot apply
- client-only confirmation flag
- confirmation artifact replay
- PlanId/confirmation idをOperationId代替にすること
- stale owner/config generation silent apply
- network arrival orderによるworld ordering
- generic Undoによるaudit/history消去
- arbitrary shell/script/path command
- ACKをterminal successと同一視すること

## 11. 後続Phaseへ委ねるもの

Phase 0で責務、high-impact classification、confirmation lifecycle、permission/idempotency/audit semanticsは確定済みです。

後続PhaseではUI interaction design、optional multi-person approval、storage engine、deployment supervisor等の実装方式を選択できますが、本安全境界を弱めてはなりません。
