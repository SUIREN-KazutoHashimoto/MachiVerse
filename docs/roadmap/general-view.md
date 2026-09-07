# General View Implementation Roadmap

Status: Implementation Ready  
Work IDs: `VIEW-01..VIEW-05`  
Base branch: `view`  
Canonical breakdown: `docs/design/phase4-implementation-work-breakdown.md`

## 1. 目的

General View の実装を、確定済みWeb platform、Gateway protocol、confirmed state、Three.js renderer、prediction/reconciliation、Participation UX契約に従って進める。

旧 Phase 0 checklist に含まれていたWeb技術スタック、Three.js、WebGPU/WebGL、renderer、Gateway protocol、browser platform等はPhase 4で実装baselineが確定済みである。

## 2. Work Package

| ID | Stage | Scope | Main dependencies |
|---|---|---|---|
| `VIEW-01` | A | Web scaffold / Gateway protocol client | QA-01 protocol fixture |
| `VIEW-02` | B | Confirmed state store / publication consumer | VIEW-01 |
| `VIEW-03` | C | Three.js scene projection / renderer | VIEW-02 |
| `VIEW-04` | C | Prediction / reconciliation / Operation controller | VIEW-02 |
| `VIEW-05` | E | Participation UX | VIEW-04, GW-05 protocol contract |

## 3. Standard platform baseline

```text
standalone Blazor WebAssembly net10.0
Three.js THREE.WebGPURenderer
WebGPU preferred
WebGL 2 compatibility backend through WebGPURenderer fallback
TSL / node-material first
binary WebSocket over TLS
Protocol Buffers proto3
```

`THREE.WebGLRenderer` をstandard rendererとして直接採用しない。WebGPU/WebGL2 backend差をauthoritative world stateへ逆流させない。

## 4. Critical path

```text
VIEW-01 -> VIEW-02 -> VIEW-03
                   └-> VIEW-04 -> VIEW-05
```

RendererとOperation controllerはconfirmed state store成立後に並列実装可能。

## 5. Implementation gates

### Client foundation gate

`VIEW-01` 完了時:

- Web application lifecycle shell
- binary WebSocket/protobuf client
- presentation Config loader
- Gateway mockとのcontract test

### Confirmed state gate

`VIEW-02` 完了時:

- FULL/DELTA continuity validation
- atomic confirmed state swap
- mismatch時のresync lifecycle
- render/predictionがconfirmed authorityを上書きしない

### Renderer gate

`VIEW-03` 完了時:

- full 3D terrain/built/presenceをSceneProjectionから描画可能
- WebGPU backend / WebGL2 fallbackの両fixtureで動作
- camera、render LOD、GPU stateがworld outcomeへ影響しない

### Interaction gate

`VIEW-04` 完了時:

- local prediction/interpolationとconfirmed stateを明示分離
- stable OperationIdでretry/result/reconciliationが成立

### Participation gate

`VIEW-05` 完了時:

- Resident selection/preference/binding projection
- absence/reconnect/death UX
- Diver experienceがGateway/Participation contractへ整合

## 6. Non-negotiable acceptance

- Simulation Coreへ直接接続しない
- render/prediction stateをauthoritative stateとして送信しない
- retryでOperationId/immutable semanticsを変更しない
- device loss/reinitializeでworld/session identityを再発行しない
- backend差でconfirmed state、Operation result、Core diagnosticを変更しない
- client slow/render FPSをsimulation cadenceへ使用しない

## 7. Issue tracking

Component roadmap Issue は #37 を利用する。

#37 は旧Phase 0設計待ちIssueではなく、次を追跡する親Issueへ更新する。

- Architecture/Protocol/render baseline normalization
- `VIEW-01..VIEW-05` implementation package progress
- View-owned design amendmentの依存再評価

各 `VIEW-xx` 実装は原則独立Issueとして起票し、#37へ紐付ける。
