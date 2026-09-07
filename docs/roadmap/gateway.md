# Gateway Implementation Roadmap

Status: Implementation Ready  
Work IDs: `GW-01..GW-07`  
Base branch: `gateway`  
Canonical breakdown: `docs/design/phase4-implementation-work-breakdown.md`

## 1. 目的

Gateway の実装順序を、確定済みProtocol/Auth/Config/Cache/Custody設計とPhase 4 dependency DAGに従って追跡する。

旧 Phase 0 checklist の Protocol詳細化、認証・認可、要求集約、Master切替、cache/publication、Config、addon境界は詳細設計で実装可能レベルまで確定済みであり、未確定設計フェーズとして再実施しない。

## 2. Work Package

| ID | Stage | Scope | Main dependencies |
|---|---|---|---|
| `GW-01` | A | Gateway scaffold / protocol-config foundation | QA-01 fixture |
| `GW-02` | B | Core protocol / confirmed cache / resync | GW-01 |
| `GW-03` | C | Peer / Master / custody / retry | GW-01, GW-02 |
| `GW-04` | C | OIDC/BFF session / authentication | GW-01, GW-03 |
| `GW-05` | D | Authorization / View+Admin boundaries | GW-04 |
| `GW-06` | E | Publication / result routing / backpressure | GW-02, GW-05 |
| `GW-07` | D | Observability / management audit | GW-05 |

## 3. Critical path

```text
GW-01 -> GW-02 -> GW-03 -> GW-04 -> GW-05 -> GW-06
                                      └-------> GW-07
```

`GW-02` は Core production implementationを待たず、QA-01とProtocol fixtureで先行可能。`GW-03` 以降もpeer/Core fixtureを使い、component independenceを維持する。

## 4. Implementation gates

### Foundation gate

`GW-01` 完了時:

- ASP.NET Core Gateway executable/test projectが成立
- local protobuf type generation、common envelope validation、Gateway Config 1.0 loaderが成立

### Core connectivity gate

`GW-02` 完了時:

- confirmed cacheがauthoritative stateと分離される
- FULL/DELTA continuity、resync、scheduling policy viewをfixtureで検証可能

### Master/custody gate

`GW-03` 完了時:

- Gateway-Gateway peer protocol、Master role、custody/retry/status convergenceが成立
- Master receipt ACKをCore durable acceptanceと混同しない

### Security gate

`GW-05` 完了時:

- General View role permissionとAdmin permission domainが分離
- unauthorized OperationをCoreへforwardしない
- WebSocket/session validationが成立

### Publication gate

`GW-06` 完了時:

- publication buffer/coalesce、result routing、slow consumer/backpressureを安全に扱える
- lossy handlingがaccepted Operation/result custodyへ波及しない

## 5. Non-negotiable acceptance

- Simulation Core / View / Adminのcompiled implementationへ依存しない
- Core confirmed stateより新しいstateをauthorityとして扱わない
- retry/reconnect/Master failoverでOperation identityを変更しない
- auth/session tokenをbrowser JavaScriptへ露出しない
- General View authorizationとAdmin authorizationを混同しない
- cache/publication timingをworld outcomeへ使用しない
- audit protected pathをfail-openにしない

## 6. Issue tracking

Component roadmap Issue は #36 を利用する。

#36 は旧Phase 0の前提Issueではなく、次を追跡する親Issueへ更新する。

- Architecture/Protocol stale baseline normalization
- `GW-01..GW-07` implementation package progress
- Gateway-owned design amendmentの依存再評価

各 `GW-xx` 実装は原則独立Issueとして起票し、#36へ紐付ける。
