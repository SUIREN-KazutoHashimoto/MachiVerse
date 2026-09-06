# 詳細設計 Phase 4: Platform / Runtime / Web Rendering Profile

Status: Complete / P4-09 supplemental platform profile  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`

## 1. 目的

Phase 1〜4で実装契約を確定した後も残っていたruntime、language、Web application hosting、Three.js rendererの標準実装profileを固定し、component実装開始時にmajor technologyを追加判断する状態を解消する。

本書はprotocol/state/persistenceのsemantic contractをruntime/library implementationへ従属させない。compatible patch updateやpresentation-only library差分までworld schema versionへ結び付けない。

## 2. Standard runtime baseline

Simulation Core / Gateway / General View / Admin Viewの標準実装baseline:

```text
.NET 10 LTS
TargetFramework = net10.0
C# language version = C# 14
64-bit runtime required for Core/Gateway
```

選定理由:

- MachiVerse repositoryのC#主体方針と整合する。
- .NET 10はPhase 4設計時点のactive LTSである。
- C# 14は.NET 10でsupportされるcurrent language generation。
- Coreで必要な`Int128`、Span/Memory、async networking、ASP.NET Core/gRPC、WebAssembly clientを同一platform familyで利用できる。

Exact SDK/runtime patchはimplementation/release lock fileでactive supported .NET 10 servicing releaseへpinする。compatible servicing patch変更はworld semantic migrationを要求しないが、P4-08 determinism/contract suiteを再実行する。

## 3. Simulation Core profile

```text
Application: .NET console/service executable
Language: C# 14
Target: net10.0
Architecture: x64 standard reference; arm64 permitted after determinism acceptance
GC: server GC permitted/recommended for service deployment
NativeAOT: optional optimization, not initial required profile
```

Core authoritative algorithmはCLR collection enumeration、floating behavior、thread schedulerへsemantic依存しない。

NativeAOT/JIT差でStateDiagnosticが変化する実装は不適合。

## 4. Gateway profile

```text
Application: ASP.NET Core 10 service
Language: C# 14
Target: net10.0
Internal protocols: gRPC bidirectional streaming
Browser boundary: HTTPS + binary WebSocket
Auth: OIDC Authorization Code + PKCE, Gateway BFF session
```

GatewayはGeneral/Admin View static assetsをhostできるが、component independenceを保つためView/Admin build artifactをGateway code dependencyにしない。

Deploymentで同一HTTP originから配信する場合も、logical component/build contractは独立させる。

## 5. General View profile

Standard General View:

```text
Application: standalone Blazor WebAssembly
Language: C# 14 / Razor
Target: net10.0 browser-wasm
Rendering: Three.js WebGPURenderer through a thin ECMAScript module interop boundary
Protocol: binary WebSocket + Protocol Buffers
```

Blazor clientへcredential/client secret/private keyを埋め込まない。Gateway BFFがserver-side session/token custodyを持つ。

General View production codeはGateway/Core project/DLLをreferenceしない。

## 6. Admin View profile

Standard Admin View:

```text
Application: standalone Blazor WebAssembly
Language: C# 14 / Razor
Target: net10.0 browser-wasm
Protocol: binary WebSocket + Protocol Buffers
```

Admin ViewはThree.jsをrequireしない。

General View AdministratorとAdmin View permission domainをclient framework都合で統合しない。

## 7. Three.js / WebGPU boundary

General View full-3D presentation library:

```text
Three.js
```

Standard renderer:

```text
THREE.WebGPURenderer
import profile = three/webgpu
preferred backend = WebGPU
automatic compatibility backend = WebGL 2
forceWebGL = false in normal production profile
```

**Phase 4 standard implementationは`WebGPURenderer`を使用する。** `WebGLRenderer`をstandard rendererとして直接使用しない。

Three.js `WebGPURenderer`はWebGPU対応browser/deviceではWebGPU backendを使用し、WebGPU非対応時はrenderer自身がWebGL 2 backendへfallbackできる。このfallbackでもapplication側renderer abstractionは`WebGPURenderer`のままとする。

### 7.1 WebGPU-first設計理由

- MachiVerseの大規模full-3D worldで将来的にGPU compute、modern render pipeline、MRT等を活用しやすい。
- Three.jsの開発重点が`WebGPURenderer`、node material、TSLへ移っている。
- WebGPU利用可否でrenderer implementationを二重管理せず、`WebGPURenderer`のbackend abstractionを利用できる。
- authoritative simulationとpresentation rendererは分離済みであり、renderer maturityやbrowser差がworld stateへ影響しない。

### 7.2 WebGL compatibility fallback

WebGPU unavailable時は:

```text
WebGPURenderer
 -> automatic WebGL 2 backend
```

をstandard fallbackとする。

`new THREE.WebGLRenderer(...)`へapplication-levelで別rendererを切り替えるfallbackを標準にはしない。

release testではWebGPU backendと`forceWebGL=true`によるWebGL 2 backendの両方を検証する。

## 8. Material / shader policy

`WebGPURenderer`標準化に伴い、General Viewのcustom material/shaderは**TSL / node material first**とする。

Standard production renderer pathで禁止:

- `ShaderMaterial`へ依存する新規custom material
- `RawShaderMaterial`へ依存する新規custom material
- built-in materialの`onBeforeCompile()` monkey patchをrenderer contractにすること
- WGSL専用実装とGLSL専用実装を別semanticとして二重管理すること

custom rendering logicは可能な限りTSL/node graphで記述し、WebGPU backendではWGSL、WebGL 2 fallbackでは対応backend codeへtranspileされるrenderer architectureを利用する。

既存addonがWebGL-only shaderを要求する場合はstandard renderer capabilityとは分離し、addon compatibilityで明示する。

## 9. Post-processing policy

Standard post-processingは`WebGPURenderer`のnode-based post-processing stackを使用する。

`WebGLRenderer`向け`EffectComposer`をstandard General View renderer pipelineの前提にしない。

MRT、post effect、future compute-assisted effectはWebGPURenderer/TSL pipeline上へ実装する。

## 10. WebGPU initialization / render loop

WebGPU initializationはasyncであるため、standard renderer adapterは次のどちらかを明示的に実装する。

```text
renderer.setAnimationLoop(render)
```

または

```text
await renderer.init()
requestAnimationFrame(...)
```

initialization完了前のrenderer利用を暗黙timingへ依存させない。

WebGPU adapter/device初期化failureはView presentation failureとして扱い、authoritative Core stateを変更しない。

## 11. Three.js interop rule

Blazor側`SceneProjectionModel`からECMAScript moduleへ渡すのはpresentation DTOのみ。

```text
ConfirmedWorldView
 + PredictedPresentationState
 + PresentationState
 -> SceneProjectionModel
 -> JS interop renderer adapter
 -> THREE.WebGPURenderer
 -> Three.js scene/node objects
```

Three.js object/Vector3/Matrix/Scene/MaterialをCore/Gateway protocol typeやauthoritative domain typeとして使用しない。

Three.js object identityをEntityIdとして使用しない。

WebGPU buffer/texture/pipeline identityをworld identityとして使用しない。

## 12. Three.js version locking

Three.js release番号はimplementation package lockへexact pinする。

Phase 4 designは特定release numberをwire/persistence/world schemaの一部にしない。

WebGPURendererは継続的に改善されているため、standard implementationではThree.js upgradeを通常のpresentation dependencyより慎重に扱い、次を必須とする。

- package lock diff review
- WebGPU backend rendering contract tests
- forced WebGL 2 backend rendering contract tests
- TSL/node material compatibility tests
- protocol/state projection tests
- camera/render changeでCore StateDiagnostic不変
- required browser baseline regressionなし

## 13. Browser capability baseline

Standard browser requirementはspecific vendor version numberではなくcapability baselineで固定する。

Preferred production capability:

```text
WebAssembly
WebGPU
WebSocket binary frames
ES modules
secure context/HTTPS
modern SameSite/Secure/HttpOnly cookie behavior for Gateway BFF
```

Compatibility minimum:

```text
WebAssembly
WebGL 2
WebSocket binary frames
ES modules
secure context/HTTPS
```

WebGPU非対応でもWebGL 2を満たすbrowserは`WebGPURenderer`のfallback backendで利用可能とする。

Supported-browser release matrixはimplementation/release documentationでcurrent evergreen browser versionsへpinする。

## 14. Renderer capability / diagnostics

View operational diagnosticsは少なくとも次を識別できるようにする。

```text
renderer = webgpu-renderer
backend = webgpu | webgl2-fallback
adapter/device initialization state
fallback reason category
GPU device lost count/recovery state
```

backend種別、GPU vendor、adapter timingをauthoritative world ordering/detail level/randomへ使用しない。

Standard presentation capability:

```text
view.render.webgpu-renderer.v1
```

WebGPU backend availabilityそのものをrequired protocol Capabilityにはしない。WebGL 2 fallbackが利用可能だからである。

## 15. Device loss / fallback behavior

WebGPU device lossやrenderer initialization failure時:

1. Viewのconfirmed world stateを破棄しない。
2. pending Operation identityを変更しない。
3. renderer adapterをpresentation degraded stateへ移す。
4. renderer再初期化を試行できる。
5. backend fallbackが安全に成立する場合はWebGL 2 backendへ移行できる。
6. renderer recovery結果をCore/Gateway world resultへ反映しない。

render recoveryに失敗してもsession/protocolを可能な限り維持し、UIでrender degraded/unavailableを表示できる。

## 16. UI framework boundary

Standard UI frameworkはBlazor WebAssembly / Razor Componentsで固定する。

General View render sceneだけThree.js ECMAScript moduleへ分離する。

追加JavaScript UI frameworkをstandard implementationへ導入しない。必要なaddon/experimental UIはView component内部presentation choiceとする。

## 17. Protocol code generation

`.proto` schemaから各componentがlocal source/generated codeを生成する。

禁止:

- shared compiled DTO DLLを4 componentのcontract authorityにする。
- Gateway generated assemblyをView/Admin/Coreがreferenceする。

許可:

- same source `.proto` artifactをcomponent build入力としてconsumeする。
- generated sourceをcomponent local namespaceへ配置する。

## 18. Persistence library boundary

Simulation Core persistence implementationはSQLite 3 / Zstandard contractへ従う。

Exact native/package patchはrelease lockへpinする。

Upgrade時はP4-04 crash/recovery suite、snapshot/history golden fixture、SQLite durability setting validation、logical digest unchangedを必須とする。

## 19. OpenTelemetry implementation boundary

P4-07 standard telemetry model/OTLP/W3C Trace Contextへ対応する.NET OpenTelemetry SDK/providerを使用できる。

Exact package patch/export backendはoperational dependencyでありworld semantic versionへ含めない。

Exporter on/off/package patchでStateDiagnosticが変化してはならない。

## 20. Build reproducibility

各component repository build stateは最低限:

```text
TargetFramework lock
NuGet lock/central package version resolution or equivalent deterministic package lock
Web dependency lock for Three.js/WebGPURenderer module artifact
compiler/runtime SDK version record
protocol schema digest
Config schema digest
```

をrelease artifact metadataへ保存する。

## 21. Supported architecture rule

Standard release target:

```text
Core/Gateway: linux-x64 first reference deployment
General/Admin View: browser-wasm
```

Additional Core/Gateway targetとして`linux-arm64`、`windows-x64`はcomponent contract/determinism suite PASS後にsupported profileへ追加できる。

OS/platform/GPU差をworld outcomeへ入力しない。

## 22. Container/deployment boundary

Container runtime/Kubernetes/systemd/IIS/reverse proxy等はdeployment choiceでありPhase 4 world contractでは固定しない。

ただしproduction deploymentはTLS termination trust boundary、persistent storage durability、Gateway BFF secure cookie/origin policy、Core persistence durable filesystem semanticsを維持する。

## 23. Non-blocking implementation-local choices

Phase 4 completion後も実装側で選択可能:

- internal class/project/folder names
- lock-free vs locked container implementation
- allocator/pool/arena implementation
- compatible .NET 10 servicing patch
- exact Three.js release pin
- CSS/layout component details
- telemetry backend/vendor
- container/orchestrator
- CI provider

次はimplementation-local choiceではなくstandard contract:

- General View renderer class = `THREE.WebGPURenderer`
- WebGPU preferred backend
- WebGL 2 automatic fallback
- custom shader/material = TSL/node-material first

## 24. Acceptance

Platform profile acceptance:

- Core/Gatewayは.NET 10 LTS/C# 14でimplementation pathが明確。
- View/AdminはBlazor WebAssemblyとしてimplementation pathが明確。
- General ViewはThree.js `WebGPURenderer`をstandard rendererとして使用する。
- WebGPU対応環境ではWebGPU backendを選択する。
- WebGPU非対応環境では同じ`WebGPURenderer`のWebGL 2 backendへfallbackできる。
- standard custom material/post-processingはTSL/node-based pipelineへ適合する。
- renderer backend差でCore StateDiagnostic/Operation outcomeが変化しない。
- browser/clientへsecretを埋め込まない。
- protocol generated codeをcompiled shared DTO dependencyにしていない。
- package patch pinとsemantic schema versionを分離した。

blocker: なし。