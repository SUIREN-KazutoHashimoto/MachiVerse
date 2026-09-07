# Administration View Phase 0 設計確定

Status: Draft / Issue #38 work in progress  
Tracking: Issue #38  
Related: `admin-view.md`, `admin-operation-safety.md`, `addon-boundary-safety.md`, `../protocols/gateway-admin-view.md`

## 1. 目的

Administration View Phase 0 では、実装開始前に Administration View の system-operator 向け管理境界を固定する。

本書は UI の見た目ではなく、次を実装可能な contract として定義する。

- Gateway↔Administration View の管理 protocol の責務
- component health/status/metrics/log の表示要件
- Config 参照・変更 semantics
- operational command と permission boundary
- high-impact operation の確認・監査
- addon inventory / install / update / disable / remove
- official addon store と trust verification
- third-party addon の区別と自己責任導入境界

General View Administrator は Administration View permission を意味しない。
Administration View は Simulation Core へ直接接続しない。

## 2. External management boundary

Administration View が接続する system-management endpoint は Gateway のみとする。

```text
Administration View -> connected Gateway -> authoritative owner/component
```

- Administration View は component filesystem、process private API、internal DTO、DLL、database へ直接依存しない。
- Gateway は external authn/authz、permission、request format、target、allowed condition を検証する。
- target owner は自身が所有する Config consistency、state invariant、dependency、safe apply boundary を検証する。
- Gateway は target owner の terminal acknowledgement を得る前に state-changing action を success としない。
- Core に影響する operation は既存の Gateway→Core operation path を使用する。
- Core 以外の component management を内部でどの transport に載せるかは実装詳細であるが、Administration View から見た contract は Gateway-owned のままとする。

## 3. Permission model

単一の `Admin` role だけで全操作を許可しない。Gateway が session に付与された stable permission token を deny-by-default で評価する。

Phase 0 baseline permission:

| Permission | 許可範囲 |
|---|---|
| `admin.observe.health` | health/status/metrics の参照 |
| `admin.observe.logs` | structured diagnostic log の参照 |
| `admin.observe.audit` | audit trail の参照 |
| `admin.config.read` | non-secret Config metadata/effective value の参照 |
| `admin.config.change.operational` | simulation outcome に影響しない runtime/ops Config 変更 |
| `admin.config.change.simulation` | simulation-affecting Config 変更 |
| `admin.operation.execute` | standard operational command 実行 |
| `admin.operation.high-impact` | high-impact operation の commit |
| `admin.addon.read` | addon inventory/catalog/trust state の参照 |
| `admin.addon.manage.official` | official addon の install/update/disable/remove |
| `admin.addon.manage.third-party` | third-party addon の stage/install/update/disable/remove |

- permission token は UI 表示制御ではなく Gateway で強制する。
- missing permission は `auth.unauthorized` として reject する。
- privilege revoke/session generation change は接続中にも反映する。
- high-impact operation は通常 permission に加えて `admin.operation.high-impact` を要求する。
- third-party addon の導入は `admin.addon.manage.third-party` を必須とし、official addon 管理 permission だけでは許可しない。
- role→permission mapping は deployment policy で設定可能とし、protocol は role 名へ依存しない。

## 4. Health / status / metrics display requirement

### 4.1 共通表示

各 component row/detail は最低限次を表示する。

- component kind / logical instance
- readiness / health state
- protocol version / negotiated Capability state
- uptime
- process CPU / memory
- connection state
- ConfigGeneration / Config validation state
- last successful observation time
- active warning/error condition code

UI は last observation time を常時表示し、古い sample を現在値のように見せない。

baseline polling interval は 5 秒、15 秒を超えて更新できない場合は `STALE` 表示とする。これらの値は operational Config で調整可能とする。

### 4.2 Simulation Core

最低限次を表示する。

- current Simulation Step
- pause/running state
- target step rate / observed lag
- pending operation count
- save/replay/recovery state
- last completed savepoint step
- protocol/Capability mismatch condition

### 4.3 Gateway

最低限次を表示する。

- Gateway readiness
- Master / non-Master / transition role
- MasterGeneration
- current Master identity
- resync state / last confirmed basis step
- publication buffer utilization
- operation retry/dedup diagnostic count
- General View / Administration View connection count

### 4.4 General View / Administration View

Gateway が観測可能な範囲で次を表示する。

- connected instance count
- protocol/Capability mismatch
- connection/auth/session error aggregate
- current deployment/version identity

browser/client-private metric を authoritative server metric として扱わない。

## 5. Structured log requirement

Phase 0 では query + cursor pagination を standard とし、live tail は optional capability とする。

baseline query:

- target component
- from/to timestamp
- severity/event kind
- CorrelationId
- OperationId
- BatchId
- Simulation Step
- MasterGeneration
- page size / opaque cursor

`page_size` は 1..1000、default 200。

log record は最低限次を持つ。

- RecordId
- timestamp
- severity
- event kind
- source component
- correlation/operation/batch context when applicable
- Simulation Step when applicable
- stable attributes
- human-readable diagnostic

禁止事項:

- credential、session token、private key、secret Config value を log payload へ出さない。
- authorization decision を diagnostic string の比較で実装しない。
- audit log と high-volume diagnostic log を同じ retention policy に固定しない。

secret/redaction は source component で行い、Gateway/Admin View が secret を受信してから隠す設計を標準としない。

## 6. Config read

Administration View は component-owned Config file を直接開かない。
`config.read` により owner が公開した metadata/effective state を取得する。

各 item は少なくとも次の意味を提供する。

- stable key
- effective value または redacted state
- impact: `operational` / `simulation`
- mutability: `runtime` / `restart_required` / `world_regeneration_required`
- validation state
- sensitive flag
- current ConfigGeneration

secret item は current value を返さず、`sensitive=true` と redacted state のみ表示する。secret の write は可能でも read-back は不可とする。

## 7. Config change

Config change は `ConfigChangeRequestV1` の optimistic concurrency contract を使用する。

必須 semantics:

1. stable OperationId と immutable payload digest を付与する。
2. `expected_base_generation` が current ConfigGeneration と一致しなければ `config.stale-generation` で reject する。
3. change set を key canonical order で正規化する。
4. Gateway が permission / target / classification / allowed condition を検証する。
5. target owner が type/range/cross-constraint/state consistency を検証する。
6. 1 item でも invalid なら全体を reject し、partial apply しない。
7. runtime mutable operational Config は owner-defined atomic boundary で反映する。
8. simulation-affecting change は authoritative effective Simulation Step を target owner/Core が確定し、history/audit と結び付ける。
9. restart/world regeneration required item は runtime apply せず、required boundary を結果に返す。
10. 元へ戻す操作も new ConfigChange Operation とする。

simulation-affecting Config change は high-impact とする。

## 8. Operational command registry

`OperationalCommandV1.command_kind` は arbitrary shell command ではなく、stable command registry token とする。

Phase 0 baseline command:

| command_kind | class | high-impact | safe boundary |
|---|---|---:|---|
| `gateway.resync.request` | operational | no | Gateway-owned resync boundary |
| `world.save.create` | non-world-mutating operation | no | consistent savepoint boundary |
| `world.pause` | simulation scheduling | yes | authoritative Step boundary |
| `world.resume` | simulation scheduling | yes | authoritative Step boundary |
| `component.restart.request` | deployment operation | yes | component/deployment capability dependent |
| `component.shutdown.request` | deployment operation | yes | component/deployment capability dependent |
| `diagnostic.snapshot.create` | diagnostic | no | component-defined non-world-mutating boundary |

- shell text、executable path、free-form script を standard command payload として送らない。
- command-specific parameter は registered `payload_schema_id` で型を固定する。
- state-changing command は OperationId/digest 必須。
- Phase 0 では non-state-changing command も追跡性のため OperationId を付与することを標準とする。
- unsupported deployment action は明示的 `operation.unsupported` とし、Gateway が OS command を推測して実行しない。

## 9. High-impact operation

次を high-impact baseline とする。

- simulation-affecting Config change
- world pause/resume/time-control family
- component restart/shutdown
- world reset/delete/bulk destructive operation が将来追加された場合
- simulation-affecting addon install/update/disable/remove
- persistent save/world compatibilityへ影響する addon action
- third-party addon install/update

high-impact action は direct one-shot apply を許可せず `prepare -> confirm -> commit` とする。

Prepare result は immutable plan として最低限次を持つ。

- PlanId
- PlanDigest
- actor/session generation
- required permissions
- target
- requested operation digest
- impact classification
- required restart/regeneration/safe Step
- dependency impact
- warning codes
- expiration time

Commit 時に Gateway は PlanId/PlanDigest、session generation、permission、target/current state が prepare 時点から有効か再確認する。
変化していれば plan を stale として reject し、再 prepare を要求する。

Phase 0 standard は single-operator confirmation とし、multi-person approval は標準必須にしない。ただし将来 capability で追加可能とする。

## 10. Audit requirement

state-changing Admin action と security-sensitive read は audit 対象とする。

最低限記録する。

- AuditRecordId
- actor account reference
- session generation / permission decision context
- request timestamp
- OperationId / immutable payload digest
- CorrelationId when present
- operation/command/action kind
- target
- PlanId/PlanDigest when high-impact
- requested change summary（secret value は記録しない）
- effective Simulation Step / restart boundary when applicable
- result/status code
- reject reason code
- resulting ConfigGeneration / addon inventory generation when applicable

`admin.observe.audit` による audit read 自身も security audit event として残す。

audit retention は diagnostic log から独立した Config とし、Phase 0 baseline default は 180 日とする。deployment policy はこれを延長可能とする。

## 11. Addon identity / package metadata

addon は component-scoped package とする。

minimum manifest metadata:

- `addon_id`: reverse-DNS style stable token
- `version`: SemVer 2.0.0
- target component kinds
- compatible MachiVerse protocol/version range
- required/provided Capability
- dependency addon/version range
- config schema version
- persistent-data compatibility/migration declaration
- artifact SHA-256 digest
- publisher identity metadata when signed
- trust source

addon package bytes を standard component protocol の functional payload として流さない。
standard protocol で扱うのは inventory、manifest、compatibility、trust、plan、operation result など management/safety metadata に限定する。

## 12. Official addon store

Official addon store は catalog/metadata distribution source として standard support する。

Gateway Config は official store endpoint と pinned official trust root を保持する。
Administration View は catalog を Gateway 経由で参照し、store へ直接 trust decision を委譲しない。

official package install は次を全て満たす必要がある。

1. HTTPS で catalog/artifact を取得する。
2. catalog/manifest signature を pinned official trust root から検証する。
3. artifact SHA-256 を manifest の expected digest と照合する。
4. addon_id/version/target/dependency/Capability compatibility を検証する。
5. package extraction safety を検証する。
6. prepare plan を生成して operator に impact を表示する。
7. commit 後にのみ target owner の safe boundary で activate する。

hash 一致だけを publisher identity proof としない。

Phase 0 の official signature algorithm は Ed25519 とし、artifact digest は SHA-256 とする。
trust root rotation は old trusted root で署名された keyset update、または operator による explicit trust-root Config change で行う。

## 13. Third-party addon

third-party addon は official と同じ badge/color/wording で表示しない。

UI は最低限次を明示する。

- `THIRD-PARTY` trust label
- source
- SHA-256 digest
- signature presence / signer identity when available
- signer が locally trusted か unknown か
- requested target/permissions/Capability/dependencies
- simulation/persistent-data impact
- "official verification not provided" state

third-party package の導入は `admin.addon.manage.third-party` と high-impact confirmation を常に要求する。

operator が configured local trust key を持つ signer を trusted として扱うことは可能だが、それを `OFFICIAL` へ昇格しない。

## 14. Addon staging / installation

package installation は filesystem direct copy ではなく次の state machine とする。

```text
STAGED -> VALIDATED -> PREPARED -> COMMITTED -> APPLIED
                         |             |
                         +-> REJECTED  +-> FAILED
```

### Official

- Gateway が official catalog item を解決し package を staging area へ取得する。
- digest/signature/manifest/compatibility validation 完了前に executable addon code を load しない。

### Third-party

- Admin View BFF の authenticated HTTPS upload endpoint で Gateway staging area へ upload する。
- upload 完了時に Gateway が SHA-256 を計算し、opaque StagedPackageId を返す。
- upload だけでは install/execute しない。
- archive traversal、absolute path、symlink escape、duplicate canonical path、size/count limit violation を reject する。

### Apply

- target owner は package content を component-owned addon area に atomic install する。
- existing version を in-place 部分更新しない。
- activation が restart required の場合、install と activation を区別して status 表示する。
- simulation-affecting live activation は explicit safe Step contract がある addon のみ許可する。未宣言なら restart required とする。
- apply failure は previous active version を保持し、不完全な mixed version state で起動しない。

## 15. Addon update / disable / remove

全て explicit management action とする。

- dependency graph を apply 前に検証する。
- persistent world/save data impact を表示する。
- required migration がある場合、migration plan 成功前に activation しない。
- disable/remove 後に required Capability が失われる場合 reject する。
- world/save が addon data を必要とする場合、explicit migration/retention policy がない remove は reject する。
- generic Undo は提供せず、previous version reinstall 等も new audited action とする。

## 16. Failure / safety rule

- authorization outage 時に bypass しない。
- stale plan/config generation を silent apply しない。
- official verification failure を warning-only で継続しない。
- third-party trust を official と同等扱いしない。
- addon validation failure 時に自動 disable して component 起動を継続することを標準としない。
- operation ACK を terminal success としない。
- Admin View disconnect 後も server-side commit 済み operation の audit/terminal result を保持する。

## 17. Phase 0 acceptance mapping

| Issue #38 item | 本書の確定箇所 |
|---|---|
| Gateway↔Administration View Protocol | §2, §3, §6-10 |
| log/status display | §4, §5 |
| Config read/change | §6, §7 |
| operational command / permission | §3, §8, §9 |
| addon management | §11, §14, §15 |
| official store | §12 |
| hash/trust verification | §12, §13 |
| third-party distinction | §13 |
| addon install | §14 |
| audit/safety | §9, §10, §16 |

## 18. Phase 0 で implementation choice として残すもの

以下は上記 contract を変更しない限り実装時に選択可能であり、Phase 0 blocker ではない。

- Administration View の具体 UI framework/component library
- observability collector/storage vendor
- exact deployment supervisor implementation
- official store hosting product
- audit storage engine
- BFF upload storage backend
- multi-person approval の将来 extension UI

Protocol wire message/schema と既存文書の整合更新を完了した時点で本書 status を `Complete` とする。
