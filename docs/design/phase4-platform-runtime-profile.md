# 詳細設計 Phase 4: Platform / Runtime / Web Rendering Profile

Status: Complete / P4-09 supplemental platform profile  
Tracking: Issue #16  
Parent: `phase4-implementation-ready-design.md`

## 1. 目的

Phase 1〜4で実装契約を確定した後も残っていたruntime、language、Web application hosting、Three.js rendererの標準実装profileを固定し、「component実装開始時にmajor technologyを追加判断する」状態を解消する。

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
- .NET 10はPhase4設計時点のactive LTSである。
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
Rendering: Three.js through a thin ECMAScript module interop boundary
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

## 7. Three.js boundary

General View full-3D presentation library:

```text
Three.js
```

Standard renderer baseline:

```text
THREE.WebGLRenderer
backend requirement = WebGL 2
```

`WebGPURenderer`はPhase4 initial standard rendererにしない。

理由:

- Three.js公式manualでWebGPURendererはnext-generation rendererだがexperimental stateとして扱われている。
- WebGLRendererは引き続きmaintainedで、pure WebGL 2 applicationのrecommended choiceとされる。
- Phase4 standard releaseではbrowser/device差によるrender implementation riskを減らし、authoritative world contractとpresentation rendererの移行を分離する。

将来WebGPURendererを利用する場合:

```text
presentation capability = view.render.webgpu-experimental
```

としてoptional presentation profileに追加可能。world state/protocol meaningを変更しない。

## 8. Three.js interop rule

Blazor側`SceneProjectionModel`からJavaScript moduleへ渡すのはpresentation DTOのみ。

```text
ConfirmedWorldView
 + PredictedPresentationState
 + PresentationState
 -> SceneProjectionModel
 -> JS interop renderer adapter
 -> Three.js objects
```

Three.js object/Vector3/Matrix/Scene/MaterialをCore/Gateway protocol typeやauthoritative domain typeとして使用しない。

Three.js object identityをEntityIdとして使用しない。

## 9. Three.js version locking

Three.js release番号はimplementation package lockへexact pinする。

Phase4 designは特定release numberをwire/persistence/world schemaの一部にしない。

理由:

- Three.jsはpresentation dependencyである。
- renderer/library patch/minor updateはworld resultを変えてはならない。
- exact release pinはreproducible web buildに必要だが、design documentよりpackage lockが正しいauthorityである。

Upgrade acceptance:

- package lock diff reviewed。
- View rendering contract tests。
- protocol/state projection tests。
- camera/render changeでCore StateDiagnostic不変。
- required browser baseline regressなし。

## 10. Browser baseline

Standard browser requirementはspecific vendor version numberではなくcapability baselineで固定する。

Required:

```text
WebAssembly
WebGL 2
WebSocket binary frames
ES modules
secure context/HTTPS production
modern SameSite/Secure/HttpOnly cookie behavior for Gateway BFF
```

Supported-browser release matrixはimplementation/release documentationでcurrent evergreen browser versionsへpinする。

Browser version変更はworld schema migration対象ではない。

## 11. UI framework boundary

Standard UI frameworkはBlazor WebAssembly / Razor Componentsで固定する。

General View render sceneだけThree.js ECMAScript moduleへ分離する。

追加JavaScript UI frameworkをstandard implementationへ導入しない。必要なaddon/experimental UIはView component内部presentation choiceとする。

## 12. Protocol code generation

`.proto` schemaから各componentがlocal source/generated codeを生成する。

禁止:

- shared compiled DTO DLLを4 componentのcontract authorityにする。
- Gateway generated assemblyをView/Admin/Coreがreferenceする。

許可:

- same source `.proto` artifactをcomponent build入力としてconsumeする。
- generated sourceをcomponent local namespaceへ配置する。

## 13. Persistence library boundary

Simulation Core persistence implementationはSQLite 3 / Zstandard contractへ従う。

Exact native/package patchはrelease lockへpinする。

Upgrade時:

- P4-04 crash/recovery suite。
- snapshot/history golden fixture。
- SQLite durability setting validation。
- logical digest unchanged。

を必須とする。

## 14. OpenTelemetry implementation boundary

P4-07 standard telemetry model/OTLP/W3C Trace Contextへ対応する.NET OpenTelemetry SDK/providerを使用できる。

Exact package patch/export backendはoperational dependencyでありworld semantic versionへ含めない。

Exporter on/off/package patchでStateDiagnosticが変化してはならない。

## 15. Build reproducibility

各component repository build stateは最低限:

```text
TargetFramework lock
NuGet lock/central package version resolution or equivalent deterministic package lock
Web dependency lock for Three.js module artifact
compiler/runtime SDK version record
protocol schema digest
Config schema digest
```

をrelease artifact metadataへ保存する。

## 16. Supported architecture rule

Standard release target:

```text
Core/Gateway: linux-x64 first reference deployment
General/Admin View: browser-wasm
```

Additional:

```text
linux-arm64
windows-x64
```

はcomponent contract/determinism suite PASS後にsupported profileへ追加できる。

OS/platform差をworld outcomeへ入力しない。

## 17. Container/deployment boundary

Container runtime/Kubernetes/systemd/IIS/reverse proxy等はdeployment choiceでありPhase4 world contractでは固定しない。

ただしproduction deploymentは:

- TLS termination trust boundary明示
- persistent storage durability contract維持
- Gateway BFF secure cookie/origin policy維持
- Core persistence local durable filesystem semantics維持

を満たす。

## 18. Non-blocking implementation-local choices

Phase4 completion後も実装側で選択可能:

- internal class/project folder names
- lock-free vs locked container implementation
- allocator/pool/arena implementation
- compatible .NET 10 servicing patch
- exact Three.js release lock
- CSS/layout component details
- telemetry backend/vendor
- container/orchestrator
- CI provider

これらはPhase4 contractを変更しない限りdesign blockerではない。

## 19. Acceptance

Platform profile acceptance:

- Core/Gatewayは.NET 10 LTS/C#14でimplementation pathが明確。
- View/AdminはBlazor WebAssemblyとしてimplementation pathが明確。
- General ViewはThree.js/WebGLRenderer/WebGL2標準。
- experimental WebGPUをrelease-critical dependencyにしていない。
- browser/clientへsecretを埋め込まない。
- protocol generated codeをcompiled shared DTO dependencyにしていない。
- package patch pinとsemantic schema versionを分離した。

blocker: なし。