# Admin操作・安全性設計

Status: Phase 4 implementation baseline aligned

本書はQ235〜Q239、Q275/Q276とPhase 2〜4詳細設計に従い、Admin操作の責務・安全境界をまとめます。

## 1. Admin Operation path

Simulationへ影響するAdmin Operationは次の経路を使用します。

```text
Admin View -> connected Gateway -> Simulation Core
```

Admin ViewからCoreへ直接接続しません。

Gateway:

- Admin authentication / authorization
- session generation validation
- required permission validation
- operation format / target
- Admin operationとしてのallowed condition
- Protocol admission

Simulation Core:

- General/Admin View等のUI roleを解釈しない
- World State invariantを維持する
- reference consistencyを維持する
- deterministic scheduling/state-transition contractを維持する

GatewayでAdmin authorizationが成功しても、Coreが一般的なWorld State invariantを破壊するstate transitionを無条件適用してはなりません。

## 2. Canonical permission boundary

Phase 4 Admin permission registryのうちAdmin mutationに関係するstandard permission:

```text
admin.config.write.operational
admin.config.write.presentation
admin.config.write.simulation
admin.command.execute.low-impact
admin.command.execute.high-impact
admin.operation.submit
admin.security.revoke-session
```

- permissionはexplicit setで保持する。
- General View Administratorから自動付与しない。
- role/permission changeでsession generationを更新する。
- old session generationによるnew protected requestをadmitしない。
- UI local checkだけでauthorizationを成立させず、Gatewayをauthorityとする。

## 3. Ordering / determinism

- network arrival orderをworld resultのordering sourceにしない。
- Gateway processing thread orderをworld orderingへ使用しない。
- browser/UI processing speedをworld orderingへ使用しない。
- simulation-affecting Admin Operationは通常Operationと同じscheduling/deadline/MasterGeneration semanticsに従う。
- candidate Stepをauthoritative effective Stepとして扱わない。
- authoritative effective StepはCore確定resultから得る。

Simulation-non-affecting operational actionだけをAdmin優先処理可能なcategoryとして扱えます。simulation-affecting actionをAdmin由来という理由でunconditional highest priorityにしません。

## 4. High-impact operation

World destruction、mass state change、time control、大規模simulation-affecting Config change等はhigh-impact confirmation/audit対象です。

Phase 2内部設計では`HighImpactConfirmation`が次のstateを管理します。

```text
NOT_REQUIRED
REQUIRED
CONFIRMING
CONFIRMED
EXPIRED_OR_INVALID
```

固定安全条件:

- high-impact commandは`admin.command.execute.high-impact`等の対応permissionを要求する。
- simulation Admin Operationは`admin.operation.submit`を要求する。
- confirmation state/tokenをOperationIdの代替にしない。
- confirmation state/tokenをauthorization credentialの代替にしない。
- confirmation後もactual submit時にGateway authorizationを通す。
- confirmationがexpire/invalidなら再確認を要求する。
- confirmation evidenceを別requestへ使い回さない。
- ACK/acceptedをterminal effect successとしない。

Standard Protocol v1に専用`admin.action.*` message familyは存在しません。`ADMIN-04`実装でconfirmation UX/evidence transportを具体化し、wire contract変更が必要なら先にdesign amendmentを行います。

Multi-person approvalはcurrent Standard v1 requirementではありません。

## 5. Config safety

Config changeはPhase 4 Config contractに従います。

- stable OperationId / immutable payload digest
- expected base ConfigGeneration
- canonical normalized change set
- Gateway permission/admission
- target owner validation
- atomic apply / no partial apply
- simulation-affecting changeのauthoritative effective Step

stale generationをsilent overwriteしません。

simulation-affecting Config changeでは`admin.config.write.simulation`を要求し、必要なhigh-impact confirmation classificationを`ADMIN-03/04`で適用します。

## 6. Command safety

`OperationalCommandV1`はdefined/registered commandだけを扱います。

- state-changing commandはOperationId/digest required。
- impactに応じ`admin.command.execute.low-impact`または`admin.command.execute.high-impact`を要求する。
- arbitrary shell/script/internal method invocationをgeneric commandにしない。
- exact command catalogは`ADMIN-03`とGateway implementation cross-reviewで固定する。

## 7. Retry / idempotency

- Operation identityはstable OperationId + immutable payload digestで追跡する。
- retry/reconnectでnew identityへ変えない。
- same OperationId / different digestを同じrequestとして扱わない。
- MessageId / CorrelationId / confirmation tokenをdedup identityにしない。
- delivery unknown時はoperation status/resultで収束させる。

## 8. Audit

Admin operationは少なくとも次を相関可能にします。

- actor reference
- session/authorization context
- OperationId / request identity
- CorrelationId
- operation type
- target
- requested content summary
- request time
- applicable/effective Simulation Step or boundary
- ConfigGeneration where applicable
- result status/code
- reject reason

Admin View local historyをauthoritative audit storeとしません。Gatewayのactor/session/authorization/routing factとtarget execution factを相関表示します。

Audit recordへcredential/token secretを含めません。

## 9. No generic Undo

- generic Undoを標準機能にしない。
- 過去のAdmin operation/audit factを消して状態を戻さない。
- 元のConfig/stateへ近づける場合はnew Operation/change requestとして実行する。
- compensating/revert requestも通常のauthorization、identity、validation、audit対象とする。
- Savepoint recovery/replayは別のrecovery conceptです。

## 10. Pause / failure

- Pause中のsimulation-affecting requestをstopped Stepへ曖昧applyしない。
- Gateway disconnect時はdelivery-unknownを保持し、reconnect/status queryで収束する。
- session revoke後はnew protected mutationを停止する。
- stale ConfigGenerationはrefresh/retry判断を要求する。
- target unavailable時にinternal APIへfallbackしない。
- protocol/Capability mismatch時はnormal mutationを継続しない。

## 11. Forbidden

- Admin View→Core direct access
- General View roleからAdmin permissionへのautomatic promotion
- UI-only authorization
- privilege outage/revoke時のpermission bypass
- high-impact confirmation stateをOperationId代替にすること
- confirmation-onlyでGateway authorizationを省略すること
- network arrival orderによるworld ordering
- stale ConfigGeneration silent overwrite
- arbitrary shell/script command
- generic Undoによるaudit/history消去
- ACK/acceptedをterminal successと同一視すること

## 12. Implementation mapping

- `ADMIN-03`: Config / operational command safety UX
- `ADMIN-04`: high-impact confirmation / simulation Admin Operation / revoke/failure/audit correlation

Phase 4で確定済みのcontractを再設計せず、この境界をimplementationへ写像します。
