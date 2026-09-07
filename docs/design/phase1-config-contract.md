# 詳細設計 Phase 1: Config schema・適用・履歴契約

Status: Draft / P1-03 complete  
Tracking: Issue #13  
Parent: `docs/design/phase1-common-foundation-contracts.md`

## 1. 目的

本書は Phase 1 の P1-03 として、MachiVerse の全標準コンポーネントに共通する Config の詳細契約を定義する。

対象は次の通り。

- Config document の形式と ownership
- schema version と migration
- Config field の分類
- startup validation と起動拒否条件
- runtime change の atomic apply
- simulation-affecting Config の effective Step
- default 補完と Config file への書き戻し
- Config generation / digest / history
- save / replay / recovery との関係
- コンポーネント間へ必要情報を配布する場合の責務境界

本書は `docs/architecture/configuration.md` と `docs/architecture/config-semantics.md` の詳細化である。

## 2. 基本原則

1. Config file はコンポーネントごとに独立して所有する。
2. 他コンポーネントの Config file を直接参照しない。
3. Config file の更新時刻、filesystem event、読み込み thread の timing を world outcome の入力にしない。
4. startup Config は起動前に全体検証し、不整合があれば起動しない。
5. runtime change は change set 全体を検証し、部分適用しない。
6. simulation-affecting change は明示的 `SimulationStep` で atomic に有効化する。
7. replay では実際に有効だった simulation-affecting Config とその effective Step を再現する。
8. 古い compatible Config の不足項目は schema default で補完し、Config file へ永続的に追記する。
9. unknown field や解釈不能な値を黙って無視しない。
10. runtime Config を元へ戻す場合も履歴を消さず、新しい change として適用する。

## 3. Config document

### 3.1 file format

標準の operator-editable Config document は **UTF-8 TOML 1.0** とする。

標準ファイルは次の metadata table を必須とする。

```toml
[meta]
format = "machiverse-config"
schema_version = "1.0"
component = "simulation-core"
```

契約:

- `meta.format` は固定文字列 `machiverse-config`。
- `meta.schema_version` は本書 4 節の version。
- `meta.component` は Config owner を表す StableToken。
- owner component と `meta.component` が一致しなければ読み込みを拒否する。
- Config file 内の key は schema が定義したものだけを許可する。
- duplicate key は parse error とする。
- TOML の日時型は標準 Config schema では使用しない。時刻・期間は schema が明示する integer / rational representation を用いる。
- simulation-affecting field で意味上不要な floating point を使用しない。

### 3.2 file location

各 component process は launch/deployment 設定から自身の Config path を 1 つ解決する。

- path は process launch argument または deployment environment で与えてよい。
- Config path 未指定時の既定 path は component package が定義してよい。
- Config path の決定自体を他 component の Config file に依存させない。
- 他 component は resolved path を知る必要がない。

Phase 1 の共通契約は絶対 path を固定しない。

### 3.3 secrets

本 Config document に credential、private key、password、token の実値を保存しない。

- schema は必要に応じ secret reference の識別子を保持できる。
- secret material の取得・保管は component の認証/secret provider 境界で扱う。
- simulation-affecting Config history へ secret material を複製しない。

## 4. Config schema version

### 4.1 representation

```text
ConfigSchemaVersion {
  major: uint16,
  minor: uint16
}
```

TOML 上は `"<major>.<minor>"` の decimal canonical form とする。

例:

```text
1.0
1.7
2.0
```

leading zero を持つ表記は canonical form としない。

### 4.2 compatibility

- `major` は意味互換性を破る schema change で増加する。
- `minor` は同一 major 内の backward-compatible change で増加する。
- current reader が同一 major の古い minor を読む場合、定義済み migration chain を current minor まで適用する。
- current reader より新しい minor の Config は、未知 field / semantic を黙って無視せず起動を拒否する。
- major mismatch は explicit migration が完全成功しない限り拒否する。
- migration path が存在しない schema version は拒否する。

### 4.3 migration chain

migration は隣接する schema version 間の deterministic transformation とする。

```text
1.0 -> 1.1 -> 1.2 -> ... -> current
```

各 migration は次を満たす。

- input version と output version を明示する。
- 同一 input から同一 normalized output を生成する。
- wall clock、filesystem enumeration order、network、random に依存しない。
- field rename / split / merge / type conversion の意味を明示する。
- 情報を破棄する migration を silent に行わない。
- migration failure は Config 全体の読み込み失敗とする。

## 5. Config schema definition

各 owner component は Config field ごとに machine-readable な論理 schema を所有する。

共通 field definition は次の意味を持つ。

```text
ConfigFieldDefinition {
  path:              StableToken,
  value_type:        ValueType,
  required:          bool,
  default_value:     Value | NONE,
  unit:              StableToken | NONE,
  constraints:       Constraint[],
  impact:            ConfigImpact,
  mutability:        ConfigMutability,
  protocol_exposure: ProtocolExposure
}
```

### 5.1 `path`

- dotted path を許可する StableToken とする。
- 例: `simulation.step-rate.numerator`, `runtime.worker-count`, `logging.audit.retention-days`。
- 一度永続化された path の意味を変更しない。
- rename は schema migration として扱う。

### 5.2 `ValueType`

共通型は少なくとも次を扱える。

```text
bool
uint8 / uint16 / uint32 / uint64
int8 / int16 / int32 / int64
binary64
string
stable-token
enum
array<T>
object<schema>
```

規則:

- integer の signedness と bit width を schema で固定する。
- unit を持つ scalar は `unit` を必須とする。
- duration を裸の floating-point seconds として曖昧に保持しない。
- `binary64` は schema が必要性を明示した field に限定する。
- NaN / Infinity は標準 Config 値として禁止する。

### 5.3 default source

既定値の正本は component の **current Config schema** とする。

- source code 内に別の hidden default を重複定義しない。
- docs の例示値だけを実行時 default と解釈しない。
- `default_value = NONE` の required field が欠落している場合は補完せず error とする。

## 6. Config classification

### 6.1 `ConfigImpact`

各 field は次のいずれか 1 つに分類する。

```text
SIMULATION
OPERATIONAL
PRESENTATION
```

#### `SIMULATION`

World State、Simulation Step 上の因果、Operation result、乱数 context、domain dependency、detail level 等、world outcome を変え得る値。

- replay condition に含める。
- effective Step を持つ runtime change は world history に記録する。
- wall-clock timing だけで effective value を変えない。

#### `OPERATIONAL`

性能、監視、timeout、retry、buffer、ログ保持等を制御するが、同一の有効 world input に対する world outcome を変えてはならない値。

- wall clock を利用する運用機能を設定できる。
- world replay condition には原則含めない。
- OPERATIONAL field を変更したことで world ordering や乱数結果を変えてはならない。

#### `PRESENTATION`

表示、UI、非権威な補間・可視化等のみを変更する値。

- authoritative state を変更しない。
- world replay condition に含めない。

### 6.2 `ConfigMutability`

各 field は次のいずれか 1 つに分類する。

```text
RUNTIME_SAFE
RESTART_REQUIRED
WORLD_REGENERATION_REQUIRED
```

#### `RUNTIME_SAFE`

running component 上で atomic change set の一部として変更可能。

#### `RESTART_REQUIRED`

running instance では変更を有効化しない。次回の正常 startup validation 後に有効化する。

#### `WORLD_REGENERATION_REQUIRED`

既存 WorldId の継続中には変更できない。新規 world generation または明示的 world migration が必要。

### 6.3 classification combination

`impact` と `mutability` は独立属性とする。

例:

| Field | Impact | Mutability |
|---|---|---|
| Core StepRate | SIMULATION | RUNTIME_SAFE |
| world generation geography seed policy | SIMULATION | WORLD_REGENERATION_REQUIRED |
| Core worker count | OPERATIONAL | RUNTIME_SAFE |
| low-level allocator mode | OPERATIONAL | RESTART_REQUIRED |
| View overlay visibility | PRESENTATION | RUNTIME_SAFE |

具体 field の分類は各 component Config schema が所有する。

## 7. Config normalization と digest

### 7.1 effective Config

Config file を parse した raw representation をそのまま実行時正本にしない。

次の pipeline をすべて成功した結果を `EffectiveConfig` とする。

```text
read
 -> parse
 -> version resolve
 -> migration
 -> default completion
 -> structural validation
 -> field constraint validation
 -> cross-field validation
 -> compatibility validation
 -> normalization
 -> EffectiveConfig
```

途中で 1 件でも error があれば EffectiveConfig を生成しない。

### 7.2 canonical value set

`EffectiveConfig` の digest 用 representation は field path の ASCII bytewise ascending order で構成する。

```text
EffectiveConfigCanonical {
  schema_version,
  component,
  fields: [
    { path, normalized_value },
    ...
  ]
}
```

schema metadata の `impact` / `mutability` は schema 自体の定義であり、instance digest の fields へ重複格納しない。

### 7.3 `ConfigDigest`

P1-02 の `MV-DCBOR-v1` / SHA-256 suite を使用する。

```text
ConfigDigest := DomainHash(
  "mv.config.v1",
  EffectiveConfigCanonical
)
```

`ConfigDigest` は 256 bit とする。

用途:

- startup 時の effective Config 識別
- save metadata と current Config の比較
- Config history の before / after 同一性確認
- diagnostic / audit correlation

ConfigDigest の byte 値自体を priority / ordering に使用しない。

## 8. startup load contract

### 8.1 new component / new world

startup は次の順序を必須とする。

1. Config file を読み込む。
2. parse / version resolve を行う。
3. 必要な migration を実行する。
4. 不足 field を current schema default で補完する。
5. Config 全体を validation する。
6. normalized EffectiveConfig と ConfigDigest を生成する。
7. migration/default completion により file 内容が変化した場合、9 節の atomic write-back を完了する。
8. 全工程成功後にのみ component の通常起動へ進む。

validation または required write-back に失敗した場合は起動しない。

### 8.2 unknown / invalid value

次は起動拒否とする。

- unknown field
- unsupported schema version
- parse error
- type mismatch
- out-of-range
- cross-field constraint violation
- dependency / addon / Capability inconsistency
- migration failure
- required default write-back failure

無効 field だけを削除して継続しない。

## 9. default completion と atomic write-back

Q214 の「不足項目を既定値で補完し Config file へ追加する」を次のように具体化する。

### 9.1 completion 対象

補完できるのは次をすべて満たす field のみ。

- current schema に存在する。
- input Config で欠落している。
- migration 上、その欠落が backward-compatible と定義されている。
- schema に `default_value` が存在する。

### 9.2 write-back

補完・migration 後の current canonical TOML document を owner Config file へ atomic replace する。

必須意味論:

1. 同一 filesystem 上に temporary file を作成する。
2. complete content を書き込む。
3. file content を durable に flush する。
4. 既存 file を atomic replace する。
5. replace 後の file を再読込し、期待する schema version / ConfigDigest と一致することを確認する。

OS / filesystem が必要な atomic replace semantics を提供できない deployment は、同等の crash-safe persistence mechanism を用意しなければならない。

### 9.3 write-back failure

- startup 中の required migration/default completion の write-back failure は起動拒否。
- 元 file を破損した状態で残さない。
- failure reason と対象 path を diagnostic 可能にする。
- temporary artifact を active Config として扱わない。

## 10. `ConfigGeneration`

有効 Config の atomic revision を表すため次を導入する。

```text
ConfigGeneration := uint64
```

規則:

- startup で最初の `EffectiveConfig` が成立した時点を generation `1` とする。
- successful runtime change ごとに 1 増加する。
- rejected change は generation を増加させない。
- normalized before / after が同一の no-op は `NO_CHANGE` とし generation を増加させない。
- wrap-around を禁止する。
- generation は Config field の意味的 priority に使用しない。
- simulation-affecting Core Config の generation は world save / recovery で保持する。
- operational / presentation の process-local generation は protocol が永続 identity を要求しない限り instance lifetime scope でよい。

## 11. runtime Config change

### 11.1 implicit reload の禁止

Config file の mtime 変化や filesystem watch event だけを理由に、running component の effective Config を変更しない。

file watcher を実装する場合は「変更候補が存在する」という diagnostic / UI signal に限定できる。

runtime activation は必ず explicit Config change action とする。

### 11.2 logical change set

```text
ConfigChangeSet {
  operation_id:             OperationId,
  expected_base_generation: ConfigGeneration,
  changes: [
    { path, requested_value },
    ...
  ]
}
```

規則:

- `operation_id` は retry を跨いで stable。
- `expected_base_generation` は stale editor / concurrent update を検出する optimistic concurrency boundary。
- same path を 1 change set 内に複数回指定することを禁止する。
- change set は path ASCII bytewise ascending に normalize して検証する。
- `RESTART_REQUIRED` / `WORLD_REGENERATION_REQUIRED` field を runtime apply 対象に含めた場合、change set 全体を拒否する。

### 11.3 candidate generation

validation 前に generation を予約しない。

successful candidate は次を持つ。

```text
ValidatedConfigChange {
  operation_id,
  base_generation,
  next_generation,
  before_digest,
  after_digest,
  normalized_changes
}
```

`next_generation = base_generation + 1` とする。

## 12. atomic validation

runtime change は次の順序で検証する。

1. OperationId dedup / immutable request 一致確認。
2. `expected_base_generation == current generation` を確認。
3. path existence を確認。
4. mutability を確認。
5. value type / unit / range を確認。
6. current EffectiveConfig へ change set 全体を仮適用する。
7. cross-field constraints を candidate 全体で検証する。
8. addon / dependency / Capability 等の component-owned compatibility を検証する。
9. candidate EffectiveConfig / ConfigDigest を生成する。
10. no-op 判定を行う。

1 件でも失敗した場合は candidate の一部を有効化しない。

## 13. runtime apply boundary

### 13.1 simulation-affecting change

`SIMULATION + RUNTIME_SAFE` は `effective_step` を必須とする。

```text
SimulationConfigChange {
  validated_change,
  effective_step: SimulationStep
}
```

`effective_step = S` の意味:

- `State(S)` から `State(S+1)` を生成する transition の **開始前** に new Config generation を active にする。
- transition 内の一部処理だけ old/new Config を混在させない。
- `effective_step < current next-applicable Step` は受理しない。
- 同じ Step へ複数 Config change を適用する場合、P1-02 の same-Step logical ordering に従い全順序を確定する。
- Config change 自体の network arrival time を tie-break にしない。

### 13.2 operational / presentation runtime change

`OPERATIONAL` / `PRESENTATION` の `RUNTIME_SAFE` change は component-defined safe point で atomic に apply する。

- request handling の途中で field 単位に切り替えない。
- in-flight operation の意味を途中で変更しない。
- world outcome へ影響する safe-point choice をしてはならない。
- apply 成功時に generation を切り替える。

### 13.3 mixed-impact change set

1 change set に `SIMULATION` と非 simulation field が混在する場合、owner component は全 field を simulation effective Step の同一 atomic boundary で apply する。

これにより「simulation field だけ later、operational field だけ immediate」という部分適用を禁止する。

## 14. durable runtime change

runtime change は success result を返す前に、restart 後も意図した effective Config を復元できる durable record を作成する。

最低限次を記録する。

```text
ConfigChangeRecord {
  operation_id,
  base_generation,
  next_generation,
  before_digest,
  after_digest,
  changed_paths,
  effective_step: SimulationStep | NONE,
  result
}
```

SIMULATION change では加えて replay に必要な normalized new values を world history に保存する。

- success ACK 後に change record が失われることを許可しない。
- retry された同一 `operation_id` へ同じ result を再構成できること。
- 同一 `operation_id` で異なる change payload を受けた場合は protocol violation とする。

Config file への operator-visible persistent state 更新と runtime history の crash consistency は component persistence 実装で成立させる。world-affecting truth は replay history を優先し、Config file の mtime を authoritative history としない。

## 15. Config history

### 15.1 simulation history

world replay condition として、少なくとも次を保持する。

```text
SimulationConfigHistoryEntry {
  generation: ConfigGeneration,
  effective_step: SimulationStep,
  operation_id: OperationId | NONE,
  config_digest: ConfigDigest,
  normalized_changed_values
}
```

初期 generation も `effective_step = 0` の initial entry として扱う。

- initial world generation前にのみ意味を持つ `WORLD_REGENERATION_REQUIRED` values は world metadata / generation input として保存する。
- runtime history の並びは generation ascending かつ effective Step の論理順序を保持する。

### 15.2 operational / presentation history

標準 world replay には不要。

ただし監査・障害調査・運用要件がある field は component audit/log policy に従い保持できる。

## 16. save / replay / recovery

### 16.1 save

save metadata は少なくとも次を参照可能にする。

- active simulation `ConfigGeneration`
- active simulation `ConfigDigest`
- simulation Config history の replay continuation point
- world generation時に固定された Config values / digest

### 16.2 restore と current Config の差

保存 world を restore する場合、saved simulation-affecting Config を continuation の正本とする。

current local Config file と差がある場合:

- `SIMULATION + RUNTIME_SAFE`: saved value で restore を完了する。current file の差分を暗黙適用せず、必要なら restore 後に新規 ConfigChangeSet として明示適用する。
- `SIMULATION + RESTART_REQUIRED`: saved world が要求する値と current binary/config の組合せが互換であることを検証する。不整合を黙って置換しない。
- `SIMULATION + WORLD_REGENERATION_REQUIRED`: saved world の値を維持する。異なる値で既存 WorldId を起動しない。変更には明示的 world migration または新規 world generation が必要。
- `OPERATIONAL` / `PRESENTATION`: current component Config を適用できる。ただし world outcome を変えてはならない。

### 16.3 replay

replay は simulation Config change を元の `effective_step` と generation 順で再適用する。

- current Config file の値を replay 過去区間へ混入させない。
- default schema が後から変わっても、historically applied effective value を再計算で置換しない。
- historical schema を直接実行できない場合は deterministic save/config migration を経由する。

## 17. world generation boundary

`WORLD_REGENERATION_REQUIRED` field は world identity / initial causal state に属する。

- world generation開始後の同一 WorldId で runtime change しない。
- Config file を編集しても既存 world へ silent に反映しない。
- 新規 world generation時は generation input の normalized values と digest を world metadata に保存する。
- world migration で変更可能にする場合、migration が新しい world causal state へどう変換するかを明示する。

## 18. コンポーネント間への配布

Config file 自体を component boundary 越しに配布しない。

他 component が知る必要があるのは owner が公開契約として定義した **effective information** のみとする。

例:

- current Simulation Step semantics
- public buffer policy に必要な状態
- protocol compatibility に必要な effective limit / Capability

配布情報には必要に応じ次を付与できる。

```text
EffectiveConfigInfo {
  owner_component,
  config_generation,
  relevant_values,
  effective_step: SimulationStep | NONE
}
```

- recipient は owner Config file の path や未適用値を参照しない。
- protocol で配布された effective information を recipient 自身の Config file へ自動転記しない。
- wire envelope / version / Capability との具体統合は P1-04 で定義する。

## 19. addon Config

addon は対象 component 単位で独立した schema/version を所有する。

- base component Config に unknown addon key を混在させ、base parser が silent ignore する方式を禁止する。
- addon Config の存在、version、依存、Capability は component 起動時に検証する。
- addon Config 不整合は標準要件に従い component 起動拒否とする。
- simulation-affecting addon Config は本書と同じ effective Step / history / replay 契約に従う。

## 20. Config error taxonomy

P1-04 の protocol result/error へ mapping する論理 error code を次で予約する。

```text
CONFIG_PARSE_ERROR
CONFIG_FORMAT_MISMATCH
CONFIG_COMPONENT_MISMATCH
CONFIG_SCHEMA_UNSUPPORTED
CONFIG_MIGRATION_REQUIRED
CONFIG_MIGRATION_FAILED
CONFIG_UNKNOWN_FIELD
CONFIG_MISSING_REQUIRED
CONFIG_TYPE_MISMATCH
CONFIG_RANGE_VIOLATION
CONFIG_CONSTRAINT_VIOLATION
CONFIG_COMPATIBILITY_VIOLATION
CONFIG_PERSIST_FAILED
CONFIG_BASE_GENERATION_MISMATCH
CONFIG_CHANGE_NOT_RUNTIME_SAFE
CONFIG_WORLD_REGENERATION_REQUIRED
CONFIG_OPERATION_ID_REUSE
CONFIG_NO_CHANGE
```

- human-readable message は diagnostic であり code の意味を置換しない。
- sensitive value を error message / structured log に平文出力しない。
- field-specific error は stable field path を含めてよい。

## 21. 代表 field への適用

本表は共通分類方針を示す。最終 key / default は各 component schema が所有する。

| 設定 | Owner | Impact | Mutability | 備考 |
|---|---|---|---|---|
| Simulation StepRate | Core | SIMULATION | RUNTIME_SAFE | effective Step必須 |
| simulation detail threshold | Core | SIMULATION | RUNTIME_SAFE | deterministic policy必須 |
| world generation geography condition | Core | SIMULATION | WORLD_REGENERATION_REQUIRED | world metadataへ保存 |
| Core worker count 1〜16 | Core | OPERATIONAL | RUNTIME_SAFE | world outcome不変 |
| Gateway heartbeat interval | Gateway | OPERATIONAL | RUNTIME_SAFE | wall-clock operational |
| Gateway retry/backoff | Gateway | OPERATIONAL | RUNTIME_SAFE | Operation identity/orderを変えない |
| Gateway publication buffer length | Gateway | OPERATIONAL | RUNTIME_SAFE | authoritative stateではない |
| log retention / rotation | each component | OPERATIONAL | RUNTIME_SAFE | log種別ごとに設定可能 |
| View interpolation tuning | General View | PRESENTATION | RUNTIME_SAFE | non-authoritative |

## 22. validation / conformance tests

各 component Config 実装は少なくとも次を自動試験する。

1. current valid Config が起動可能。
2. required field 欠落が起動拒否になる。
3. compatible old Config の不足 field が default 補完・write-backされる。
4. unknown field が拒否される。
5. future minor / incompatible major が拒否される。
6. migration input に対して deterministic output が得られる。
7. type / range / cross-field violation が部分適用されない。
8. runtime mixed change set が atomic に切り替わる。
9. stale `expected_base_generation` が拒否される。
10. same `OperationId` retry が二重 Config apply を起こさない。
11. same OperationId / different payload が拒否される。
12. simulation change が指定 `effective_step` より前後へずれない。
13. Config file mtime / watcher timing が world result を変えない。
14. save/replay で同一 Config history が再現される。
15. current local Config 差分が restore 過去状態へ silent に混入しない。
16. worker count 等 OPERATIONAL change で同一 world input の outcome が変化しない。
17. default write-back failure で不完全 startup を行わない。

## 23. P1-03 で確定した事項

- operator-editable Config は UTF-8 TOML 1.0。
- Config schema version は `major.minor`。
- owner component ごとの独立 Config とする。
- unknown field / future schema / invalid constraint は fail-fast。
- old compatible Config は deterministic migration + schema default completion を行う。
- default completion 後は atomic file write-back を必須とする。
- Config field は `SIMULATION / OPERATIONAL / PRESENTATION` を持つ。
- Config field は `RUNTIME_SAFE / RESTART_REQUIRED / WORLD_REGENERATION_REQUIRED` を持つ。
- `ConfigGeneration := uint64` を導入する。
- `ConfigDigest` は `MV-DCBOR-v1` + SHA-256 の `mv.config.v1` domain hash とする。
- runtime Config は filesystem event で暗黙適用せず explicit change action とする。
- runtime change は `OperationId` と expected base generation を持つ atomic change set とする。
- simulation-affecting runtime change は transition開始前の explicit effective Step で適用する。
- simulation Config history は save/replay/recovery の再現条件とする。
- saved world の simulation Config を restore continuation の正本とし、current file 差分を silent override しない。
- secret material は標準 Config document/historyへ格納しない。

## 24. P1-04 へ引き継ぐ事項

次は Protocol 共通 envelope で具体化する。

- `ConfigGeneration` / `ConfigDigest` を protocol 上で必要に応じ運ぶ共通 field
- Config change Operation / result の envelope integration
- logical Config error code の wire representation
- effective Step / Master generation / correlation / causation の共通 field
- Capability negotiation と Config-exposed effective information の関係

P1-03 時点で Config schema・分類・適用境界・履歴に関する横断 blocker はない。
