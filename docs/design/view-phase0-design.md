# View Phase 0 設計確定

Status: In Progress  
Tracking: Issue #37  
Responsibility: General View / documentation

## 1. 目的

本書は、General View の実装開始前に Issue #37 の Phase 0 項目を確定するための作業設計書です。

正本は既存どおり `docs/architecture/` および `docs/protocols/` とし、本書だけを根拠に未承認仕様を確定しません。Phase 0 の各判断が確定した後、該当する正本文書へ反映します。

参照する既存設計:

- `docs/architecture/view.md`
- `docs/architecture/general-view-synchronization.md`
- `docs/protocols/gateway-view.md`
- `docs/design/phase2-general-view-internal-design.md`

## 2. 現時点で確定済みの前提

以下は既存正本で確定済みのため、本 Phase 0 では前提として扱います。

- General View は Web application として提供する。
- full-3D world rendering には Three.js を使用する。
- Simulation Core へ直接接続せず、Gateway 経由でのみ通信する。
- Gateway が publish した confirmed state を表示の authoritative basis とする。
- View 側 interpolation / prediction / correction は presentation-only とし、world outcome の authority を持たせない。
- Diver は existing resident と binding し、world 内の一住民として通常の world rule に従う。
- Spectator / Moderator / Administrator を含む General View role と Admin View operator は別 auth/authz domain とする。
- View-local state と authoritative World State を分離する。
- addon functional payload を standard `mv.gateway-view` protocol へ混在させない。
- 大規模 world の表示最適化で Entity を描画しないことと、world から Entity が存在しなくなることを同一視しない。

## 3. Phase 0 項目と現在地

| # | 項目 | 現在地 | Phase 0 で必要な決定 |
|---|---|---|---|
| 1 | Gateway↔View Protocol 詳細要件 | 共通 envelope、continuity、Operation identity 等は定義済み | transport、serialization、compression、full/delta、range、resync、role payload の具体化 |
| 2 | Web 技術 stack | Web application / Three.js のみ確定 | language、build tool、UI framework、state/data boundary |
| 3 | Three.js version / 更新方針 | version 未確定 | 初期固定 version、更新ルール、互換性検証 |
| 4 | WebGL / WebGPU | 未確定 | primary backend、fallback、required feature boundary |
| 5 | 3D scene / rendering / LOD / asset / shader | 責務境界のみ定義済み | scene partition、LOD policy、asset format、shader abstraction、render budget |
| 6 | role ごとの表示・操作 | 上位 role 定義のみ | exact permission matrix、public status、critical operation |
| 7 | Diver 視点・操作・feedback | 上位体験原則のみ | camera/control、local feedback、prediction 対象、correction UX |
| 8 | 大量 world state の可視化 | continuity/backpressure 原則のみ | interest/streaming、aggregation、LOD/culling、update coalescing |
| 9 | 対応 browser / device | 未確定 | support matrix、minimum graphics capability、degraded mode |
| 10 | addon を考慮した View 責務境界 | standard protocol 境界は定義済み | View extension point と禁止境界 |

## 4. 外部技術調査ベースライン

調査日: 2026-09-07

### 4.1 Three.js

- upstream GitHub の最新 release は `r185`。
- npm `three` の latest は調査時点で `0.185.1`。
- 初期実装で floating range に依存せず、検証済み exact version を lockfile で固定する方針を候補とします。

現時点の提案:

- Phase 0 の初期 baseline 候補を Three.js `0.185.1` とする。
- dependency update は自動追従で即時採用せず、migration guide、renderer behavior、shader/asset compatibility、代表 scene の performance regression を確認してから更新する。
- version update と world/protocol semantics を結合しない。

この項目は未承認の提案であり、正本へはまだ確定事項として反映しません。

### 4.2 WebGPU / WebGL2

Three.js の現行 `WebGPURenderer` は WebGPU を利用可能なら使用し、利用できない環境では WebGL2 backend へ fallback できる設計です。一方、WebGPU 自体はブラウザー互換性上まだ一律に必須化できる状態ではありません。

現時点の提案:

- renderer boundary は backend 非依存に維持する。
- WebGPU を優先利用できる構成を目指す。
- WebGL2 fallback を標準経路として維持し、WebGPU 非対応だけを理由に General View 全体を利用不可にしない。
- WebGPU 専用機能を standard scene semantics の必須条件にしない。
- shader/material は将来の backend 差し替えを妨げないよう、Three.js renderer detail を `SceneProjection` 側へ漏らさない。

`WebGPURenderer` 自体の成熟度と migration cost を踏まえ、初期実装で `WebGPURenderer` を即 primary とするか、`WebGLRenderer` から開始して段階移行するかは Phase 0 の明示判断事項とします。

## 5. Gateway↔View Protocol の Phase 0 整理

既存 `mv.gateway-view` 契約を置き換えず、未確定部分を埋めます。

### 5.1 既存契約を維持する事項

- common envelope / version / Capability / tracing
- WorldContext / OperationContext
- StateContinuityToken
- stable OperationId / immutable payload digest
- reconnect 時の renegotiation
- authn/authz の Gateway enforcement
- prediction state と confirmed state の分離
- addon functional payload を standard protocol へ載せない

### 5.2 Phase 0 で決める transport/data 項目

未確定:

- browser↔Gateway physical transport
- binary/text serialization
- compression 適用単位
- confirmed state の full snapshot / delta strategy
- visible/interest range request
- large collection の pagination/chunking
- resync request/status payload
- role/permission projection payload
- Diver binding / join preference / absence policy payload
- prediction/correction に protocol message を追加する必要があるか

設計原則:

- render frame rate と protocol publication rate を結合しない。
- delta を coalesce/drop する場合も continuity dependency を壊さない。
- visible range 外の state を「world に存在しない」と表現しない。
- client request が server-side authorization scope を拡張しない。
- threshold、buffer capacity、range 等の運用値は View/Gateway の責務に応じた Config へ置き、source code の固定値を仕様としない。

## 6. Web application 内部境界

既存 Phase 2 の module boundary を維持します。

```text
Gateway
  ↓
GatewayProtocolBoundary
  ↓
PublicationConsumer
  ↓
ConfirmedWorldStore
  ↓
ReconciliationCoordinator
  ↓
SceneProjection
  ↓
ThreeRenderer
```

UI framework を選択しても、Three.js object tree を UI framework の component tree と同一の authority/model としません。

現時点の技術 stack 候補:

### 案 A: TypeScript + Vite + React

- React は panel、HUD、menu、settings、status presentation を担当する。
- Three.js render loop / scene object lifecycle は `ThreeRenderer` が所有する。
- React reconciliation へ大量 3D entity の object lifecycle を直接載せない。

### 案 B: TypeScript + Vite + Vue

- UI と Three.js の責務分離は案 A と同じ。
- framework 固有 state を authoritative state と扱わない。

### 案 C: TypeScript + Vite + framework-light UI

- UI dependency を最小化する。
- 複雑な panel / accessibility / localization / role-aware UI の自前実装量は増える。

現時点の推奨は案 A ですが、未承認です。

## 7. 3D scene / rendering 設計方針

### 7.1 SceneProjectionModel を境界とする

wire payload を直接 Three.js object へ変換して保持しません。

```text
ConfirmedWorldView
+ PredictedPresentationState
+ PresentationState
        ↓
SceneProjectionModel
        ↓
ThreeRenderer
```

これにより protocol schema、world state storage、prediction、renderer backend を分離します。

### 7.2 scene partition

初期方針候補:

- world 全体を単一巨大 scene object hierarchy として常時 materialize しない。
- camera / role-visible scope / interest scope に応じた render partition を持つ。
- static-ish geometry、dynamic entity、effects、UI overlay を lifecycle 上分離する。
- underground / cave / tunnel / basement / overhang / same XY different Z を terrain heightmap だけへ還元しない。

partition の具体的空間単位は spatial model と publication strategy に合わせて別途確定します。

### 7.3 LOD / culling

LOD は presentation optimization とし、simulation detail level と混同しません。

候補:

- frustum culling
- distance / projected-size based LOD
- instancing / batching
- occlusion strategy
- far-distance aggregation / impostor
- update frequency tiering

具体 threshold は View Config とし、世界の存在判定へ使用しません。

### 7.4 asset

Phase 0 で確定する必要がある項目:

- primary model format
- texture format / compression
- material representation
- asset version / cache key
- placeholder / degraded rendering
- asset streaming / preload policy
- addon asset namespace

world identity と asset identity を同一視せず、asset load failure で authoritative Entity を消しません。

### 7.5 shader

- backend 固有 shader code を domain / protocol layer へ漏らさない。
- standard shader/material set と addon-provided presentation extension を分離する。
- renderer 更新時の migration surface を限定する。

`WebGPURenderer` を採用する場合、Three.js の node material / TSL を shader abstraction 候補として評価します。

## 8. role 表示・操作範囲

既存の上限だけを現時点の確定事項とします。

| Role | 現在確定している制約 |
|---|---|
| Diver | binding された resident として参加。通常 resident と同じ world rule に従う |
| Spectator | simulation mutation 不可。公開 non-vital status のみ |
| Moderator | 定義済み限定 operation のみ。critical operation 不可 |
| Administrator | General View で定義した広い simulation interference。Admin View command は含めない |

Phase 0 で別表として確定すべきもの:

- information category × role の read permission
- operation category × role の request permission
- target scope
- self / nearby / public / world aggregate の visibility
- critical operation definition
- role change / revoke 時の UI transition

UI の hide/disable は UX のみであり、authorization authority は Gateway に残します。

## 9. Diver 体験

最上位原則は「world 外の camera operator」ではなく「world の一住民として存在している感覚」です。

Phase 0 で確定する必要がある項目:

### 9.1 camera/control 候補

- 案 A: first-person を標準かつ原則固定
- 案 B: first-person を標準とし、world semantics を変えない限定的な補助 camera を許可
- 案 C: first-person / third-person を同格の標準 mode とする

現時点の推奨は案 B です。Diver の resident 性を主軸にしつつ、accessibility、操作困難時、world inspection では補助視点を利用できるためです。ただし補助 camera が server-side visibility permission を迂回してはなりません。

### 9.2 input feedback

- input receipt は即時 local feedback 可能。
- local animation / prediction は pending と confirmed を内部的に区別する。
- rejected / corrected / delayed result を user に識別可能にする。
- correction は視覚上滑らかにできるが、confirmed state への収束を遅延し続けない。

prediction 対象 operation、許容誤差、correction duration は未確定です。

## 10. 大量 world state の Web 可視化

基本方針候補:

1. Gateway から「全 world state を毎 publication で全量送信」することを標準前提にしない。
2. role permission と camera/interest scope に基づき、表示に必要な projection を段階取得できるようにする。
3. near field は entity/detail、far field は aggregate/LOD representation とする構成を許容する。
4. confirmed continuity は保持しつつ、presentation update は必要に応じ coalesce する。
5. render thread/frame drop が protocol receive/session/Operation terminal result を block しない。
6. client-side cache は再構築可能な non-authoritative cache とする。

Phase 0 では、interest/range contract と full/delta/chunk contract を `gateway-view.md` 側へ具体化する必要があります。

## 11. browser / device support

WebGPU を単独必須条件にすると現時点では support scope が狭くなるため、WebGL2 fallback を前提に support matrix を決める案を優先します。

選択肢:

### 案 A: desktop browser を Phase 1 の標準対象

- keyboard/mouse を基本 input とする。
- mobile/tablet は後続 phase で正式対応。
- 初期 performance budget を明確化しやすい。

### 案 B: desktop + tablet を標準対象

- touch control、画面密度、GPU/memory 差を Phase 1 から扱う。

### 案 C: desktop + tablet + smartphone を標準対象

- 最も広いが、full-3D world、UI、input、memory/performance の制約が大きい。

現時点の推奨は案 A です。mobile を排除する恒久方針ではなく、Phase 1 の初期標準範囲を限定する提案です。

具体 browser 名/version は採用 stack と renderer 方針決定後、compatibility table を作成して確定します。

## 12. addon を考慮した View 責務境界

既存 standard protocol の禁止境界を維持しつつ、View 内の presentation extension を将来可能にします。

候補 extension point:

- SceneProjection への追加 presentation projection
- ThreeRenderer の追加 render layer / material provider
- role-permitted UI panel / overlay
- InteractionController の additional input mapping
- asset resolver / asset namespace
- local diagnostics / visualization

禁止境界:

- `ConfirmedWorldStore` の confirmed truth を addon が直接改変すること
- `GatewayProtocolBoundary` を迂回して Core/Gateway internal API へ接続すること
- addon が server-side authz を UI 側で上書きすること
- standard `mv.gateway-view` protocol に addon functional payload を混在させること
- addon の存在を standard View の起動必須条件にすること

Concrete addon API、sandbox、load mechanism、version policy はこの原則だけから先行確定しません。

## 13. 次に確定する判断セット

Issue #37 を一度に曖昧なまま閉じないため、次の順で判断を確定します。

### 判断セット A: renderer / Web stack

1. UI stack: A `TypeScript + Vite + React` / B `TypeScript + Vite + Vue` / C framework-light
2. renderer: A `WebGPURenderer` primary + WebGL2 fallback / B `WebGLRenderer` primary から開始し後で移行
3. Three.js baseline: `0.185.1` を初期固定候補として採用するか

### 判断セット B: browser / device / input

1. 初期標準端末: desktop only / desktop+tablet / all major form factors
2. Diver camera: first-person fixed / first-person + limited assist camera / first+third equal
3. touch/gamepad 等を Phase 1 の標準入力へ含めるか

### 判断セット C: protocol / world visualization

1. browser↔Gateway transport / serialization
2. interest/range subscription model
3. full snapshot / delta / chunk contract
4. role-visible data projection
5. resync request/status representation

### 判断セット D: role / Diver participation

1. exact role permission matrix
2. public non-vital status set
3. critical operation set
4. Diver join preference schema
5. absence policy schema
6. prediction 対象 operation / correction UX

## 14. 完了条件

Phase 0 完了は、少なくとも以下を満たした状態とします。

- Issue #37 の全 checklist 項目について未確定事項が解消されている。
- 確定内容が `docs/architecture/view.md` と必要な関連 architecture 文書へ反映されている。
- Gateway↔View contract に影響する内容が `docs/protocols/gateway-view.md` および必要な schema へ反映されている。
- `docs/design/phase2-general-view-internal-design.md` と矛盾しない。
- runtime/performance 調整値の ownership と Config boundary が明確である。
- addon 将来拡張を理由に standard implementation を不要に複雑化していない。
- General View の設計だけで Core authority、Gateway authz、Admin View responsibility を侵食していない。
- Phase 1 の実装タスクを、追加の根本設計判断なしで分解できる。
