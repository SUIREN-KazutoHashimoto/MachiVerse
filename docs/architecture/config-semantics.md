# Config意味論・変更適用・互換性設計

## 確定方針

第210〜214問は以下で確定する。

- 第210問: C
- 第211問: C
- 第212問: Cを基本とし、起動時に不整合がある場合は起動しない
- 第213問: カスタム方針
- 第214問: 下位互換を設け、古いConfigで不足する項目はデフォルト値をConfigファイルへ追加する

詳細契約の正本は `../design/phase1-config-contract.md` とする。

## Config document

- operator-editable Config は UTF-8 TOML 1.0 とする。
- 各Configは `meta.format = "machiverse-config"`、`meta.schema_version`、`meta.component` を持つ。
- Config schema versionは `major.minor`。
- 同一majorの古いminorは定義済みのdeterministic migration chainで現行へ移行する。
- future minor、major mismatch、unknown field、未定義migrationは黙って無視せず拒否する。
- Config fileはcomponentごとに独立所有し、他componentは直接参照しない。
- credential、private key、password、token等のsecret materialは標準Config documentへ保存しない。必要な場合はsecret referenceを保持する。

## Config分類

各fieldは世界結果への影響と変更可能性を独立に分類する。

### 世界結果への影響

- `SIMULATION`: 世界状態、因果、Operation結果、乱数、detail等へ影響し得る。replay条件に含める。
- `OPERATIONAL`: 性能、監視、timeout、retry、buffer、logging等。world outcomeを変えてはならない。
- `PRESENTATION`: 表示・UI・非権威な補間等。authoritative stateへ影響しない。

### 変更可能性

- `RUNTIME_SAFE`: running componentでatomic change可能。
- `RESTART_REQUIRED`: running instanceでは有効化せず、正常restart後に反映する。
- `WORLD_REGENERATION_REQUIRED`: 既存WorldIdへ適用せず、新規world generationまたは明示migrationを必要とする。

Config fieldの具体key、型、単位、default、constraint、impact、mutabilityはowner componentのschemaが正本となる。

## Config schemaとdefault

- Config fieldのdefault値はcurrent component Config schemaを唯一の正本とする。
- source code側に意味上別のhidden defaultを重複定義しない。
- 古いcompatible Configで新規fieldが欠落し、schema defaultが存在する場合はdefault補完する。
- required fieldでdefaultがない場合は欠落をerrorとする。
- 数値fieldはsignedness、bit width、unit、rangeをschemaで明示する。
- NaN / Infinityを標準Config値として許可しない。

## 起動時のConfig処理

startupでは次の順序を守る。

1. Config file read / parse
2. schema version resolve
3. deterministic migration
4. schema default completion
5. structural validation
6. field constraint validation
7. cross-field validation
8. addon / dependency / Capability等のcompatibility validation
9. normalization / EffectiveConfig生成
10. 必要なConfig file write-back
11. 全工程成功後に通常起動

次があれば起動しない。

- parse error
- owner component mismatch
- unsupported schema version
- unknown field
- type / range / cross-field violation
- addon / dependency / Capability inconsistency
- migration failure
- required default write-back failure

不正項目だけを無視するpartial startupは行わない。

## Default補完と書き戻し

Q214により、compatible old Configの不足fieldへschema defaultを補完した場合はConfig fileへ追記する。

書き戻しはcrash-safeなatomic replaceとする。

- temporary fileへcomplete contentを書き込む。
- durable flush後にactive Configをatomic replaceする。
- replace後に再読込し、期待するschema versionとConfigDigestを検証する。
- write-back failure時は不完全なConfigで起動しない。

## `ConfigGeneration` / `ConfigDigest`

有効Configのrevisionを次で識別する。

```text
ConfigGeneration := uint64
ConfigDigest      := 256-bit SHA-256 domain hash
```

- startupで成立した最初のEffectiveConfigをgeneration `1` とする。
- successful runtime changeごとに1増加する。
- rejected change、normalized no-opでは増加しない。
- simulation-affecting Core Config generationはsave/recoveryを跨いで保持する。
- EffectiveConfigのdigestは `MV-DCBOR-v1` とdomain label `mv.config.v1` を用いる。

## 実行中のConfig変更

- Config fileのmtimeやfilesystem watch eventだけでeffective Configを変更しない。
- runtime activationはexplicit Config change actionとする。
- change setはstable `OperationId` と `expected_base_generation` を持つ。
- change set全体をcandidate Configへ仮適用し、全constraintを検証してからatomicに切り替える。
- 一項目でも不整合ならchange set全体を拒否し、直前のEffectiveConfigを維持する。
- `RESTART_REQUIRED` / `WORLD_REGENERATION_REQUIRED` fieldをruntime apply対象へ含めた場合はchange set全体を拒否する。
- 同一OperationIdのretryで二重適用しない。
- 同一OperationIdで異なるpayloadを受信した場合はprotocol violationとする。

## Simulation影響Configの適用境界

`SIMULATION + RUNTIME_SAFE` changeはexplicit `effective_step = S` を必須とする。

- new Configは `State(S) -> State(S+1)` transition開始前にatomicにactiveとなる。
- transition途中でold/new Configを混在させない。
- past finalized Stepへretroactiveに適用しない。
- 同一Stepの複数changeはPhase 1のsame-Step deterministic orderingに従う。
- network arrival timingを適用順の根拠にしない。

OPERATIONAL / PRESENTATION runtime changeはcomponent-defined safe pointでatomicに切り替えるが、そのsafe-point選択がworld outcomeを変えてはならない。

## コンポーネント間のConfig責務

- Core、Gateway、General View、Admin Viewは、それぞれ自身のConfigのみを所有する。
- 他コンポーネントの挙動へ直接影響させる目的の共有Configファイルは用意しない。
- 他componentが知る必要がある情報は、Config fileそのものではなくownerが定義したeffective informationとしてprotocolで配布する。
- recipientはowner Config fileのpath、未適用値、編集状態を参照しない。
- protocolで受けたeffective informationをrecipient自身のConfig fileへ自動転記しない。

## Config history / replay

simulation-affecting ConfigはWorld replay条件として履歴化する。

最低限次を保持する。

- ConfigGeneration
- effective SimulationStep
- Config change OperationId
- ConfigDigest
- normalized changed values

初期Configもeffective Step 0のinitial history entryとして扱う。

Configを元へ戻す場合も履歴を削除せず、新しいConfig change Operationとして記録する。

## 保存世界との関係

保存worldをrestoreする場合は、saved simulation-affecting Config/historyをcontinuationの正本とする。

- current local Configとの差を過去のWorld Stateへsilent overrideしない。
- `SIMULATION + RUNTIME_SAFE`の差はsaved valueでrestore後、必要なら新規changeとして明示適用する。
- `WORLD_REGENERATION_REQUIRED`の差で既存WorldIdをそのまま起動しない。
- OPERATIONAL / PRESENTATIONはcurrent component Configを利用できるがworld outcomeを変えてはならない。
- old save/config schemaを直接解釈できない場合はexplicit deterministic migrationを要求する。

## Addon Config

- addonは対象component単位で独立schema/versionを所有する。
- base Configへunknown addon keyを混在させsilent ignoreしない。
- addon、依存、Config、Capability不整合があればcomponentを起動しない。
- simulation-affecting addon Configも同じeffective Step / history / replay契約に従う。

## 関連文書

- `../design/phase1-config-contract.md`
- `configuration.md`
- `deterministic-update-execution.md`
- `persistence-replay-recovery.md`
- `../requirements/requirements-qa-200-279.md`
