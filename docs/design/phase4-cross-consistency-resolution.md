# 詳細設計 Phase 4: Cross-Consistency Resolution

Status: Complete / Issue #17 cross-cutting resolution  
Tracking: Issue #17  
Predecessors: Phase 1〜4 completion/final review documents

## 1. 目的

本書は詳細設計 Phase 1〜4 完了後の横断整合性監査で検出した、正本優先順位、Protocol wire schema、internal component authentication、文書status、renderer表記、requirements traceability、旧handoff/TODO記述の不整合を解消する最終解決文書である。

本書は既存設計の意味契約を変更するための再設計ではなく、Phase 4完了時点で既に確定している設計を一意な実装契約へ収束させるためのamendmentである。

## 2. 正本と優先順位

設計解釈は次の規則に従う。

1. `docs/requirements` の確定要件 Q001〜Q279。
2. 各Phaseのcompletion/final review、および本書。
3. Phase 4のexact contract文書。
4. `docs/protocols/schema/*.proto` のwire declaration。
5. `docs/protocols/*.md` のcomponent境界・semantic overview。
6. Phase 1〜3個別文書および旧architecture/protocol文書の作業時点の未決定/TODO/handoff記述。

ただしProtocolについては単純な上下関係だけでなく、責務を次のように分離する。

- protocolの意味、validation、security、ordering、retry、dedup、authority、context requiredness: Phase 4 design文書が正本。
- protobuf field number、wire type、enum number、service signature、package/import: `docs/protocols/schema/*.proto` が正本。
- component境界の説明・利用上の意味: `docs/protocols/*.md`。

Markdown例と`.proto`でwire declarationが競合した場合、`.proto`を修正せず実装側で独自解釈してはならない。semantic intentとの矛盾としてdesign amendmentを起票し、両方を同時更新する。

Generated C#/JavaScript/TypeScript等は正本ではない。

## 3. 最終status matrix

| Scope | Final status |
|---|---|
| Phase 1 | Complete |
| Phase 2 | Complete |
| Phase 3 | Complete |
| Phase 4 | Complete |
| P4-01 Data structure | Complete |
| P4-02 Protocol / Auth | Complete |
| P4-03 Config | Complete |
| P4-04 Persistence | Complete |
| P4-05 Algorithm / domain schema | Complete |
| P4-06 Performance | Complete |
| P4-07 Observability / audit | Complete |
| P4-08 Test / acceptance | Complete |
| P4-09 Platform / implementation breakdown | Complete |
| Issue #17 cross-consistency resolution | Complete |

Phase 4の個別文書に残る `Status: In Progress` は、その文書を作成した時点のwork-log metadataであり、completion reviewおよび本status matrixと競合する場合は最終statusではない。

実装者は個別文書の旧statusだけを根拠に仕様を未確定扱いしてはならない。

## 4. Protocol source-of-truth解決

横断監査で、`docs/protocols`をcomponent contract正本とする記述と、Phase 4 protocol schemaをexact contract正本とする記述が併存していた。

最終的に次へ固定する。

- `docs/protocols/README.md`: protocol contractの入口とgovernance。
- `docs/protocols/phase4-resolution.md`: Standard Protocol v1の最終技術選択と旧未決定事項の解決表。
- `docs/protocols/schema/*.proto`: version-controlled protobuf wire schema。
- `docs/design/phase4-protocol-schema.md`: envelope/validation/compatibility/securityの意味契約。
- `docs/design/phase4-protocol-payload-catalog.md`: message/payload registryの意味契約。
- `docs/design/phase4-auth-session-protocol.md`: browser user authentication/session/permission契約。
- `docs/design/phase4-internal-component-auth-profile.md`: Core/Gateway間component authentication契約。
- `docs/design/phase4-protocol-completion-review.md`: P4-02 completion判定。

`docs/protocols/*` の旧「詳細設計へ残す事項」「component実装へ残す事項」は、`docs/protocols/phase4-resolution.md` のresolution tableで解決済みと明示された項目についてはhistorical handoff記録として扱う。

## 5. Version-controlled `.proto` contract

Protocol Buffersをcontract artifactとする設計に対し、version-controlled `.proto`実体が存在しなかったため、次をStandard Protocol v1のschema sourceとして追加する。

```text
docs/protocols/schema/common.proto
docs/protocols/schema/auth.proto
docs/protocols/schema/payloads.proto
docs/protocols/schema/README.md
docs/protocols/schema/message-registry-v1.md
```

各componentは同一schema sourceをbuild inputとしてlocal code generationする。

禁止:

- generated assembly/packageを唯一の正本にすること。
- component間でgenerated runtime DTO assemblyを共有してprotocol independenceを失うこと。
- protobuf wire bytesをauthoritative deterministic digestの正本にすること。
- `.proto`にないfield number/typeをcomponent独自に追加すること。

## 6. Internal component authentication解決

`mv.core-gateway` と `mv.gateway-gateway` のproduction profileは `phase4-internal-component-auth-profile.md` を正本とし、mutual TLSをrequiredとする。

重要な最終条件:

- client/server双方のX.509 certificate validationをprotocol handshake前に完了する。
- Gateway certificate identityとclaimed `GatewayLogicalId`を一致検証する。
- Simulation Core certificateはCore service roleとして検証する。
- `ComponentInstanceId`、`MessageId`、`CorrelationId`をcredentialとして扱わない。
- plaintext、server-only TLS、identity mismatch時のauth bypassへfallbackしない。
- certificate/trust rotationはworld semantics、Operation identity、MasterGenerationを変更しない。

Browser user authenticationのOIDC/BFF/session contractとは別境界であり、相互に代替しない。

## 7. General View renderer表記解決

General Viewのstandard rendererは `phase4-platform-runtime-profile.md` の定義を正本として次へ固定する。

```text
THREE.WebGPURenderer
import: three/webgpu
WebGPU preferred
WebGL2 backend fallback
forceWebGL = false by default
```

`THREE.WebGLRenderer`をstandard renderer classとして直接採用する記述は誤りであり、`docs/design/README.md`のsummaryを `WebGPURenderer` profileへ統一する。

## 8. Protocol registry補正

Admin audit queryのregistryは次を正本とする。

| Direction | MessageType | Payload |
|---|---|---|
| Admin View → Gateway | `audit.query` | `AuditQueryV1` |
| Gateway → Admin View | `audit.page` | `AuditPageV1` |

旧 `phase4-protocol-schema.md` の `audit.query -> protocol.audit-page` 表記は誤記としてsupersedeする。

その他のexact message/schema mappingは `docs/protocols/schema/message-registry-v1.md` を参照する。

## 9. Q001〜Q279 traceability解決

Phase 3 completion reviewのrange-level traceabilityはcoverage判定として有効だが、implementation change impactとacceptance追跡のため、`phase4-requirement-traceability-index.md` にQ001〜Q279を1 requirement 1 rowで展開する。

各rowは少なくとも次を持つ。

- requirement id
- semantic owner / primary design
- Phase 4 detailed contract family
- P4-08 verification family

Phase 3の「coverage gap 0件」という判定は維持する。

## 10. Test acceptance補足

本横断解決で追加した`.proto` contractとinternal mTLS profileは `phase4-test-acceptance-addendum.md` をP4-08のnormative addendumとする。

最低限、次をrelease gateへ追加する。

- `.proto` schema compile可能性。
- message registryとpayload typeの一致。
- protobuf round-trip/unknown-field compatibility。
- production internal boundaryでmutual TLS必須。
- untrusted/expired certificate reject。
- GatewayLogicalId/certificate identity mismatch reject。
- plaintext/server-only TLS downgrade禁止。
- certificate rotation時にprotocol/world identityが変化しないこと。

## 11. 実装Issueへの引き渡し

Phase 4 implementation work breakdownの38 work packageは維持する。

追加されたschema/auth resolutionは新たなarchitecture判断を実装Issueへ押し付けるものではなく、既存work packageのacceptance inputとして扱う。

特にQA-01は次を最初に固定する。

- `.proto` code-generation compile check。
- protocol schema/message registry golden fixture。
- deterministic/non-wire digest fixtureとの分離確認。
- internal mTLS negative test fixture。

## 12. 最終blocker判定

本書、`docs/protocols/phase4-resolution.md`、version-controlled `.proto`、internal component auth profile、traceability index、test acceptance addendumが同一branchへ反映された状態では、横断監査で検出した詳細設計blockerは解消済みと判定する。

Unresolved detailed-design blocker: 0件。

実装中に契約変更が必要になった場合は、implementation内でsilent変更せず、design amendment、`.proto` schema/version、compatibility/migration、P4-08 acceptanceを同時更新する。