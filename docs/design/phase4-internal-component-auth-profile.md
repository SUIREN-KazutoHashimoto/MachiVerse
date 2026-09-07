# 詳細設計 Phase 4: Internal Component Authentication Profile

Status: Complete / P4-02 security addendum  
Tracking: Issue #17  
Parent: `phase4-protocol-schema.md`  
Related: `phase4-auth-session-protocol.md`, `phase4-protocol-payload-catalog.md`

## 1. 目的

`mv.core-gateway` と `mv.gateway-gateway` のproduction接続について、transport encryptionだけでなくpeer component identityを相互検証できるstandard authentication profileを固定する。

本書はbrowser user authenticationを定義しない。General View/Admin Viewのuser authentication、Gateway session、OIDC/BFFは `phase4-auth-session-protocol.md` を正本とする。

## 2. 適用境界

| ProtocolId | Production authentication |
|---|---|
| `mv.core-gateway` | mutual TLS required |
| `mv.gateway-gateway` | mutual TLS required |
| `mv.gateway-view` | HTTPS/TLS + Gateway browser session profile |
| `mv.gateway-admin-view` | HTTPS/TLS + Gateway browser session profile |

Internal protocolでproduction profileを使用する場合、server-only TLS、plaintext、unauthenticated private network assumptionをstandard fallbackとしてはならない。

## 3. TLS profile

Production internal connectionは次を満たす。

- HTTP/2 gRPC transport上でmutual TLSを使用する。
- client/server双方が相手certificate chain、validity、intended usage、configured trust rootを検証する。
- TLS 1.3をpreferredとし、TLS 1.2をminimumとする。
- TLS 1.0/1.1およびSSLへfallbackしない。
- certificate validation完了前に`ProtocolHelloV1`をnormal peerとして処理しない。
- hostname/service identity検証を無効化するdevelopment shortcutをproduction profileへ持ち込まない。

Cipher suiteの具体選択はplatform TLS providerへ委譲できるが、known-insecure/deprecated suiteを明示許可しない。

## 4. Trust root

Internal component certificateはdeploymentが明示的に構成するMachiVerse internal trust bundleへchainする。

- trust bundleはcomponent Configへsecret private keyとして埋め込まない。
- OS trust storeだけを暗黙に全許可することをstandard policyとしない。
- public CAを利用するdeploymentでも、許可対象service identityを別途検証する。
- trust root追加・削除はaudit対象とする。
- private keyはSecretStore/OS certificate store/HSM等のsecret-capable storageに保持し、通常Config、WorldState、Snapshot、protocol payload、structured logへ出力しない。

## 5. Service identity

Certificate subjectのdisplay name/CNだけをauthorization identityとして使用しない。

Standard identityはX.509 Subject Alternative NameのURI entryで表す。

Simulation Core:

```text
urn:machiverse:component:simulation-core
```

Gateway:

```text
urn:machiverse:component:gateway:<gateway-logical-id>
```

`<gateway-logical-id>` は `GatewayLogicalId` の32桁lowercase hexadecimal representationとする。

Gateway certificateにはexactly one active MachiVerse Gateway identityを含める。複数GatewayLogicalIdを1certificateで同時authorizeすることをstandard profileとしない。

`ComponentInstanceId`はprocess instanceのoperational identityでありcertificateへ固定しない。process restartでComponentInstanceIdが変わっても、同じauthorized GatewayLogicalIdとして新processを起動できる。

## 6. Core ↔ Gateway identity binding

GatewayがCoreへ接続した場合、CoreはTLS peer identityと`GatewayRegisterV1.gateway_logical_id`を一致検証する。

Validation order:

1. TLS mutual authentication success。
2. peer certificateがGateway service identityを持つこと。
3. certificateのGatewayLogicalIdと`GatewayRegisterV1.gateway_logical_id`がexact match。
4. `component_instance_id`がvalid non-zero Id128。
5. protocol handshake/version/capability validation。
6. registration/readiness/master semanticsへ進む。

Mismatchは次でrejectする。

```text
auth.component-identity-mismatch
```

Certificate identityが許可されていない場合:

```text
auth.component-untrusted
```

TLS validationそのものが失敗した場合はapplication-level protocol messageを返せないことを許可し、security auditにはconnection reject reasonを記録する。

CoreからGatewayへのcertificateは `urn:machiverse:component:simulation-core` と一致しなければならない。

## 7. Gateway ↔ Gateway identity binding

Gateway peer connectionでは双方がmutual TLS peer identityを検証する。

- peer certificateはGateway URI identity required。
- protocol payload中でpeerがclaimする`GatewayLogicalId`はcertificate identityとexact match required。
- `PeerHeartbeatV1.gateway_logical_id`、Gateway registration/role related identityをcertificateと矛盾させない。
- peerのcertificate identityだけでMaster authorityを決定しない。
- Master authorityはCoreが発行する`MasterGeneration` / role stateを正本とする。

Certificateが正しくてもstale MasterGenerationのworld-affecting messageをcurrent authorityとして扱わない。

## 8. Authenticationとauthorizationの分離

mTLSは「どのcomponent/serviceが接続しているか」を認証する。

次をmTLS identityから自動導出しない。

- current Master authority。
- world Operationのpermission。
- user account/session permission。
- Admin View permission。
- SimulationStep/effective Step。

Protocol role、MasterGeneration、Gateway readiness、Gateway browser session/authz、Core invariantをそれぞれ既存契約に従い検証する。

## 9. Certificate rotation

Certificate rotationはnormal operational actionとし、world semanticsへ影響させない。

Standard rule:

- old/new certificateまたはold/new issuing trust rootを必要なrotation windowだけoverlap可能。
- new connectionは接続時点のcurrent trust policyで検証する。
- reconnect時は必ずcertificate validationを再実行する。
- certificate serial number、expiry、TLS session idをworld ordering、OperationId、BatchId、EntityId、random seedへ使用しない。
- certificate更新だけでGatewayLogicalIdを変更しない。
- GatewayLogicalId変更が必要な場合は別deployment identity changeとして扱う。

## 10. Revocation / trust removal

Compromised certificateまたはtrust rootを無効化する場合:

- new connectionは直ちにcurrent trust policyでrejectする。
- existing connectionはsecurity policy更新通知後、bounded operational deadline内にdrain/closeして再authenticationさせる。
- close/reconnectによりaccepted Operation identityを変更・破棄しない。
- Core durable acceptance不明のOperationは既存retry/status-query契約で収束させる。

CRL、OCSP、short-lived certificate、private PKI API等の具体mechanismはdeployment implementationが選択できる。ただし「revocation不能」をstandard assumptionにはしない。

## 11. Failure / downgrade behavior

次の場合、normal internal protocol connectionをREADYへ進めない。

- no client certificate。
- untrusted chain。
- expired/not-yet-valid certificate。
- wrong extended/key usage where enforced by issuer profile。
- required MachiVerse SAN URI absent。
- wrong component role。
- GatewayLogicalId mismatch。
- TLS version below minimum。

禁止:

- mTLS failure後にplaintextへ再接続すること。
- mTLS failure後にserver-only TLSへsilent downgradeすること。
- certificate errorをignoreしてprotocol MessageId/ComponentInstanceIdをcredential代替にすること。
- development self-signed trust-all modeをproduction defaultにすること。

## 12. Observability / audit

Security audit eventは少なくとも次を区別する。

```text
security.internal-auth.accepted
security.internal-auth.untrusted
security.internal-auth.expired
security.internal-auth.identity-mismatch
security.internal-auth.role-mismatch
security.internal-auth.tls-version-rejected
security.internal-auth.trust-reloaded
security.internal-auth.connection-revalidated
```

Private key、raw certificate private material、session secretをlogへ出さない。

Certificate fingerprint/serial等をdiagnostic correlationへ使う場合も、world semanticsには使用しない。

## 13. Development profile

Local developmentでephemeral local CA/self-signed certificateを利用してよいが、次を維持する。

- mutual authentication自体は有効にする。
- explicit development trust bundleへ限定する。
- production Configと区別する。
- trust-all callbackをstandard sampleにしない。
- plaintextをproductionと同一acceptance pathとして扱わない。

## 14. Acceptance criteria

- `mv.core-gateway` production connectionはclient/server双方certificateなしでREADYにならない。
- `mv.gateway-gateway` production connectionもmutual TLS必須。
- untrusted/expired certificateをrejectする。
- Core/Gateway service role mismatchをrejectする。
- Gateway certificate identityと`GatewayLogicalId` mismatchをrejectする。
- `ComponentInstanceId`をcredentialとして信頼しない。
- mTLS failureからplaintext/server-only TLSへfallbackしない。
- certificate rotation前後でsame GatewayLogicalIdとOperation semanticsを維持できる。
- certificate/trust revocationによるreconnectでもCore accepted Operationをdouble applyしない。
- browser OIDC/BFF session secretとinternal component credentialを混同しない。

## 15. Implementation-local choices

次は本詳細設計を変更しない範囲でimplementation/deploymentへ委譲できる。

- certificate issuer/automation product。
- private key storage provider。
- CRL/OCSP/short-lived certificate等のrevocation mechanism。
- concrete certificate lifetime/rotation lead time。
- deployment-specific trust bundle distribution mechanism。
- endpoint DNS/address/port。

これらを選択する際も、本書のmutual authentication、service identity binding、no-downgrade、world-semantics separationを変更してはならない。