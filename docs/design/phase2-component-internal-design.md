# 詳細設計 Phase 2: コンポーネント内部設計

Status: In Progress  
Tracking: Issue #14  
Parent: `docs/design/phase1-cross-cutting-review.md`

## 1. 目的

Phase 1で確定した共通契約を再定義せず、Simulation Core、Gateway、General View、Admin Viewの内部責務、state ownership、queue、lifecycle、failure transition、backpressure、Config ownership、observabilityを実装者がmodule分割を判断できる粒度まで具体化する。

本Phaseでは特定のtransport、database、IdP、UI framework、task scheduler等の未承認技術を固定しない。

## 2. 正本と優先順位

Phase 2は次を前提契約として利用する。

- `docs/design/phase1-cross-cutting-review.md`
- `docs/design/phase1-determinism-ordering-random.md`
- `docs/design/phase1-config-contract.md`
- `docs/design/phase1-protocol-envelope.md`
- `docs/design/phase1-persistence-replay-recovery.md`
- `docs/design/phase1-operation-lifecycle-retry-dedup.md`
- `docs/protocols/core-gateway.md`
- `docs/protocols/gateway-gateway.md`
- `docs/protocols/gateway-view.md`
- `docs/protocols/gateway-admin-view.md`

Phase 2文書がPhase 1のidentity、ordering、durability、retry/dedup、Config、protocol version/Capability意味論を変更してはならない。

## 3. 成果物構成

| component | Phase 2正本 |
|---|---|
| Simulation Core | `phase2-simulation-core-internal-design.md` |
| Gateway | `phase2-gateway-internal-design.md` |
| General View | `phase2-general-view-internal-design.md` |
| Admin View | `phase2-admin-view-internal-design.md` |

Phase 2終了時に本書で4component間の整合性と未解決blockerを再確認する。

## 4. 共通内部設計原則

### 4.1 component-local state

各componentの内部型・module・queueはcomponent-localであり、protocol payload型を他componentと共有library化しない。

protocol境界では必ず次を分離する。

```text
wire/protocol representation
  -> boundary validation
  -> component-local command/event/state
  -> internal processing
  -> component-local result/publication model
  -> protocol representation
```

### 4.2 authoritative / confirmed / derived / presentation

内部stateは最低限、意味上次を区別する。

- `AUTHORITATIVE`: Coreが所有するWorld Stateとそのdurable lifecycle state。
- `CONFIRMED_DERIVED`: authoritative stateからprotocol境界を通って確認された派生state。
- `OPERATIONAL`: session、connection、queue、health、retry等の運用state。
- `PRESENTATION`: View内のcamera、interpolation、prediction、UI draft等。

非authoritative componentがlocal cacheやpredictionをauthoritativeとして昇格させてはならない。

### 4.3 queueの意味

queueは実装data structureではなく「所有責任と順序境界」として設計する。

各queueは最低限次を定義する。

```text
QueueContract {
  producer,
  consumer,
  accepted_item_kind,
  ordering_semantics,
  capacity_policy,
  backpressure_policy,
  retry_or_drop_semantics,
  observability
}
```

world outcomeへ影響するqueueではwall-clock arrival orderをcanonical orderingとして使用しない。

### 4.4 backpressure

backpressureはsilent dropで解決しない。

一般規則:

1. protocol入力はadmission limitを越えた場合stable result/errorで拒否またはretry adviceを返す。
2. durable acceptance済みOperationをqueue pressureだけで破棄しない。
3. confirmed state publicationはintermediate updateをcoalesceできるが、continuity/basisを偽装しない。
4. presentation-only frameやdiagnostic sampleはpolicyによりdrop/coalesce可能。
5. audit、安全性、terminal result、dedupに必要なidentity factはlossy queueへ置かない。

### 4.5 lifecycle state machine

各componentは少なくとも次のtop-level lifecycleを持つ。

```text
STOPPED
 -> STARTING
 -> READY
 -> DEGRADED
 -> STOPPING
 -> STOPPED
```

componentにより `SYNCING`, `RESYNCING`, `PAUSED`, `RECOVERING`, `FAILED_SAFE` 等のsubstateを追加する。

`DEGRADED` は一部機能制限で継続可能な状態、`FAILED_SAFE` は安全のためnormal処理を拒否する状態とする。

### 4.6 failure isolation

- boundary decode/validation失敗をinternal invariant violationへ伝播させない。
- protocol session失敗をWorld State corruptionへ波及させない。
- View rendering failureをOperation lifecycleのauthoritative結果へ影響させない。
- monitoring/audit failure時にauthorizationやworld invariantをbypassしない。
- persistence integrity failure時はCoreをfail-safeにする。

## 5. Config ownership

各componentは自身のConfigだけを読む。

| component | owner範囲 |
|---|---|
| Core | simulation rate、thread count、detail policy、persistence/recovery、scheduling policy等 |
| Gateway | session/connection limit、cache、publication buffer、aggregation、retry、flow control、resync等 |
| General View | rendering/presentation、local cache、prediction、input/UI、accessibility等 |
| Admin View | operator UI、local query/cache、display/refresh、confirmation UX等 |

他componentに必要な意味はprotocolで公開する。Config file pathやinternal schemaをcomponent間契約にしない。

## 6. observability共通契約

各componentは少なくとも次を観測可能にする。

- component lifecycle state
- ComponentInstanceId
- negotiated protocol/version/Capability state
- queue depth / saturation / rejection / coalesce count
- retry / duplicate / stale-generation / resync counts
- ConfigGenerationとConfig validation/apply state
- relevant WorldId / basis_step / effective_step / MasterGeneration
- OperationId / BatchId / CorrelationIdを用いたtrace relation
- failure transition reason

World outcomeの決定にはmetrics sampling timingやlog arrival timingを使用しない。

## 7. component境界とprotocol対応

| boundary | protocol | receiving boundary moduleの責務 |
|---|---|---|
| Core ↔ Gateway | `mv.core-gateway` | negotiation、envelope validation、continuity、Operation lifecycle mapping |
| Gateway ↔ Gateway | `mv.gateway-gateway` | Master generation、batch custody、merge input、failover convergence |
| Gateway ↔ General View | `mv.gateway-view` | session/role、publication、Operation request/result、resync |
| Gateway ↔ Admin View | `mv.gateway-admin-view` | admin session/permission、management request/result、audit context |

## 8. Phase 3 domain設計の受け皿

Phase 3のCore domainはCore内部の `DomainRuntime` 相当責務へ配置する。

Domainは次のcontractへ従う。

```text
DomainDefinition
  - stable DomainToken
  - dependency declaration
  - state ownership declaration
  - read-set/write-intent contract
  - deterministic update entrypoint
  - diagnostic partition schema
  - publication projection contribution
```

Phase 2ではdomain固有algorithmやstate schemaを定義しない。Core側にdomainを受け入れるmodule boundaryを確定する。

## 9. Phase 2完了判定

完了には次を満たす必要がある。

- 4componentすべてで内部module責務が明文化されている。
- state ownership、主要data flow、queue、lifecycle、failure transitionが定義されている。
- protocol境界と内部moduleの対応が明確である。
- Config ownershipとobservabilityがcomponent単位で定義されている。
- Phase 3 domainをCoreへ配置する受け皿が明確である。
- 未承認の実装技術を固定していない。
- component-level blockerが0件である。

## 10. 現在の作業順

1. Simulation Core内部設計
2. Gateway内部設計
3. General View内部設計
4. Admin View内部設計
5. Phase 2横断整合性レビュー
