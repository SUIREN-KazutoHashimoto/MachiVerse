# 詳細設計 Phase 4: Test / Acceptance Addendum

Status: Complete / P4-08 addendum  
Tracking: Issue #17  
Parent: `phase4-test-acceptance.md`

## 1. 目的

Issue #17横断整合性解決で追加したversion-controlled protobuf schemaとinternal component mutual TLS profileについて、P4-08 release acceptanceへ追加する必須testを定義する。

本書は `phase4-test-acceptance.md` のnormative addendumである。

## 2. Protobuf schema acceptance

| TestCaseId | Scenario | Acceptance |
|---|---|---|
| `schema.protobuf.compile` | `common.proto`, `auth.proto`, `payloads.proto`をclean environmentでcompile | error/warning-as-contract-failureなしでcode generation可能 |
| `schema.protobuf.import-closure` | schema import graphをclean checkoutからresolve | repository外のprivate schema dependencyなし |
| `schema.protobuf.field-number-stability` | published field/enum number baselineと比較 | reuse/renumberなし |
| `schema.protobuf.roundtrip` | standard payload fixtureをencode/decode | semantic field value exact round-trip |
| `schema.protobuf.unknown-compatible` | same-major optional unknown fieldを含むpayload | protobuf compatibility ruleに従いdecode可能、required semanticはCapabilityでguard |
| `schema.protobuf.registry-complete` | message registryのpayload typeをschema descriptorsと照合 | 全standard messageがexactly one known payload typeへ解決 |
| `protocol.registry.message-schema-exact` | `message_type` / payload schema id / protobuf type照合 | mismatchを`protocol.payload-schema-mismatch`でreject |
| `protocol.registry.audit-pair` | Admin audit query/result | `audit.query -> AuditQueryV1`, `audit.page -> AuditPageV1` |

## 3. Contract source acceptance

| TestCaseId | Acceptance |
|---|---|
| `schema.codegen.local-only` | 各componentが同一`.proto`を入力にlocal generateでき、shared runtime DTO assemblyを要求しない |
| `schema.codegen.clean-reproducible` | clean checkout + pinned toolchainでsame schema descriptor digest |
| `determinism.protobuf-not-hash-source` | protobuf field ordering/unknown field差異がauthoritative MV-DCBOR/domain hash contractを置換しない |

Generated sourceだけを変更して`.proto`との差分を作る変更はCIでfailureとする。

## 4. Internal mTLS acceptance

| TestCaseId | Scenario | Acceptance |
|---|---|---|
| `security.internal-mtls.required` | client certificateなしでinternal production endpointへ接続 | READYへ遷移しない |
| `security.internal-mtls.untrusted` | untrusted CA certificate | connection reject |
| `security.internal-mtls.expired` | expired/not-yet-valid certificate | connection reject |
| `security.internal-mtls.core-role` | Gateway endpointへwrong Core/Gateway role identity | connection reject |
| `security.internal-mtls.gateway-id-match` | certificate GatewayLogicalIdと`GatewayRegisterV1`一致 | authentication/registration継続可能 |
| `security.internal-mtls.gateway-id-mismatch` | certificate GatewayLogicalIdとpayload claim不一致 | `auth.component-identity-mismatch`相当でREADY不可 |
| `security.internal-mtls.no-downgrade` | mTLS failure後のfallback attempt | plaintext/server-only TLSでnormal protocolを開始しない |
| `security.internal-mtls.rotation` | old/new certificate trust overlapでrotation | same GatewayLogicalId、same protocol identity semanticsを維持 |
| `security.internal-mtls.revocation-reconnect` | trust removal後のreconnect | revoked peer reject、accepted Operation double applyなし |

## 5. TLS/protocol ordering

`security.internal-mtls.before-protocol-hello`:

- TLS peer validation成功前に`ProtocolHelloV1`をtrusted normal peerとして処理しない。
- peer identity failureでworld Operation handler、Gateway registration、Master role handlerへ到達しない。

`security.internal-auth-not-master-authority`:

- valid Gateway certificateだけではMaster-only operationを許可しない。
- stale/wrong `MasterGeneration` は既存protocol ruleどおりrejectする。

## 6. Browser/internal credential separation

| TestCaseId | Acceptance |
|---|---|
| `security.credential-domain-separation` | browser SessionHandle/OIDC tokenをinternal component mTLS identityとして受理しない |
| `security.internal-cert-not-browser-session` | component certificate identityだけでGeneral/Admin user sessionを成立させない |
| `security.internal-secret-not-world-state` | private key/certificate secret referenceがWorldState/Snapshot/Operation digestへ混入しない |

## 7. Release gate integration

P4-08 L8 release acceptanceは本書の全testを含む。

Release candidateは次をすべて満たすまでPASSにしない。

- standard `.proto` compile/registry tests PASS。
- internal mTLS negative/positive tests PASS。
- P4-08 existing protocol/auth/security suite PASS。
- same schema sourceからCore/Gateway/View/Adminのrequired code generationが成立。
- schema descriptor digestとbuild provenanceを記録可能。

## 8. Golden artifact policy

QA-01で次をversion管理する。

- protobuf descriptor set digest/reference。
- representative `WireEnvelopeV1` fixture。
- handshake fixture。
- Operation/Batch fixture。
- state publication fixture。
- auth/session fixture。
- Admin audit query/page fixture。
- mTLS identity matching positive/negative metadata fixture。

Private key、production certificate、real user credentialをgolden artifactへ含めない。

Unresolved acceptance blocker: 0件。