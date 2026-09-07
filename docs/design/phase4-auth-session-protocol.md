# 詳細設計 Phase 4: Auth / Session / Permission Protocol

Status: Complete / P4-02 auth sub-spec  
Tracking: Issue #16  
Parent: `phase4-protocol-schema.md`  
Applies to: `mv.gateway-view`, `mv.gateway-admin-view`, login proxy over `mv.gateway-gateway`

## 1. 目的

Web General View / Admin Viewのauthentication、Gateway session、Master Gateway login finalization、role/permission、WebSocket attach/reconnectを実装可能なschemaへ固定する。

本書のauth/session stateはGateway operational authorityであり、Simulation Core WorldStateへcredential/token/session secretを保存しない。

## 2. Standard authentication profile

Browser authenticationは次を標準profileとする。

- OpenID Connect Core 1.0
- OAuth 2.0 Authorization Code Grant
- PKCE `S256`
- OAuth 2.0 Security Best Current Practiceに従う
- browser applicationはGatewayをBackend-for-Frontendとして利用し、access token / refresh tokenをbrowser JavaScriptへ渡さない

OAuth 2.1 Internet-Draftをprotocolのnormative version dependencyにはしない。

IdP固有profileはdeployment Configで選択可能だが、standard browser session semanticsを変更しない。

## 3. BFF boundary

Gatewayがconfidential OAuth/OIDC clientとなる。

```text
Browser
  -> Gateway BFF
     -> Authorization Server / OpenID Provider
     <- authorization response
  <- opaque Gateway session
```

Browserへ禁止:

- OAuth access token
- refresh token
- client secret
- ID Token raw valueをlong-lived session credentialとして保存

Browserが保持するのはGateway発行opaque session cookieのみ。

## 4. HTTPS auth bootstrap endpoint

WebSocket normal message前のbrowser redirect flowにはHTTPS endpointを使用する。

Standard endpoint:

```text
GET  /auth/v1/login
GET  /auth/v1/callback
POST /auth/v1/logout
GET  /auth/v1/session
```

`/auth/v1/login`はlogin transactionを開始するだけでworld mutationを行わない。

`/auth/v1/session`はsecret/tokenを返さず、browserがWebSocket attachに必要なnon-secret session statusだけを返す。

## 5. Login transaction

```text
LoginTransactionId := 128-bit opaque random value
```

```text
LoginTransactionV1 {
  login_transaction_id: LoginTransactionId,
  connected_gateway_id: GatewayLogicalId,
  master_generation: MasterGeneration,
  oidc_issuer_id: StableToken,
  state_digest: Hash256,
  nonce_digest: Hash256,
  pkce_verifier_secret_ref: SecretStoreRef,
  requested_auth_domain: AuthDomainV1,
  created_at_monotonic_operational,
  expires_at_operational,
  status: LoginTransactionStatusV1
}
```

```text
AuthDomainV1 :=
  GENERAL_VIEW
  | ADMIN_VIEW
```

```text
LoginTransactionStatusV1 :=
  CREATED
  | AUTHORIZATION_PENDING
  | CALLBACK_RECEIVED
  | MASTER_FINALIZING
  | COMPLETED
  | REJECTED
  | EXPIRED
```

wall-clock expiryはauth operational safetyに使用できる。SimulationStepやworld outcomeへ利用しない。

## 6. Master finalization

Q241系のlogin authority ruleに従い、connected Gatewayはloginを独立finalizeしない。

Logical flow:

```text
Browser -> connected Gateway: login begin
connected Gateway -> Master Gateway: auth.login.proxy
Master: LoginTransactionId / authority generation発行
connected Gateway -> browser/IdP redirect
IdP -> connected Gateway callback
connected Gateway: OIDC response verification
connected Gateway -> Master: auth.login.assertion
Master: account/session/role final validation
Master -> connected Gateway: auth.login.result
connected Gateway -> Browser: session cookie
```

MasterGenerationがcallback途中で変化した場合、old generation finalizeをcurrent authorityとして使用しない。

new Masterへtransaction statusをquery/recoverできない場合はlogin transactionを再開始し、old transactionをsuccess扱いしない。

## 7. Verified identity assertion

connected GatewayからMasterへraw credentialを無制限forwardしない。

```proto
message VerifiedIdentityAssertionV1 {
  bytes login_transaction_id = 1;      // Id128
  string issuer = 2;                   // normalized HTTPS issuer URI
  string subject = 3;                  // OIDC subject, sensitive operational field
  optional string tenant = 4;
  repeated string authentication_methods = 5;
  optional uint64 authentication_time_unix_seconds = 6;
  bytes verification_digest = 7;       // Hash256 over normalized verified assertion
}
```

Rules:

- issuer exact allowlist required。
- ID Token signature / issuer / audience / nonce / expiry等をconnected Gatewayでvalidateする。
- Masterはtrusted Gateway assertion channelとtransaction/generationをvalidateする。
- `subject`をWorldStateのDiver identityとして直接保存しない。

## 8. Stable account / Diver reference

Gateway auth storeはidentity provider subjectとMachiVerse internal account identityを分離する。

```text
AccountId := 128-bit opaque persistent value
DiverRef  := 128-bit opaque persistent value
```

- AccountId/DiverRefはcredentialではない。
- issuer/subject renameやIdP移行でWorldState側DiverRefを無言変更しない。
- Participation domainへ渡すのはDiverRefだけ。
- raw issuer/subject/email/display name等をSimulation Core binding stateへ保存しない。

## 9. Session identity

```text
GatewaySessionId := 128-bit cryptographically random opaque value
SessionHandle    := 256-bit cryptographically random opaque value
```

```text
GatewaySessionV1 {
  session_id: GatewaySessionId,
  account_id: AccountId,
  diver_ref: DiverRef | NONE,
  auth_domain: AuthDomainV1,
  role_or_permission_set_id: StableToken,
  issued_master_generation: MasterGeneration,
  session_generation: uint64,
  created_at_operational,
  last_security_event_at_operational,
  status: SessionStatusV1
}
```

```text
SessionStatusV1 :=
  ACTIVE
  | REAUTH_REQUIRED
  | REVOKED
  | EXPIRED
```

SessionHandleはbrowser cookie valueとして使用し、database primary identityのSessionIdと分離する。

## 10. Browser cookie profile

Normal session cookie:

```text
name: __Host-mv_session
Secure: true
HttpOnly: true
Path: /
Domain: absent
SameSite: Strict
```

Login redirect transaction cookieが必要な場合:

```text
name: __Host-mv_login
Secure: true
HttpOnly: true
Path: /
Domain: absent
SameSite: Lax
short operational lifetime
```

Session secretをlocalStorage / sessionStorage / IndexedDBへ保存しない。

## 11. WebSocket attach security

`/ws/v1/view`と`/ws/v1/admin`のHTTP Upgrade時:

1. TLS required production profile。
2. exact allowed `Origin` validation。
3. `__Host-mv_session` validation。
4. expected auth domain validation。
5. revoked/expired session reject。
6. protocol handshake実施。
7. session attach messageでsession generation確認。

cross-site WebSocketをcookieだけで許可しない。

## 12. Session wire schema

```proto
message AuthSessionAttachV1 {
  bytes session_id = 1;                 // Id128; non-secret reference
  uint64 expected_session_generation = 2;
}

message AuthSessionStateV1 {
  bytes session_id = 1;
  AuthDomainWireV1 auth_domain = 2;
  string effective_role_set = 3;
  repeated string effective_permissions = 4;
  uint64 session_generation = 5;
  SessionWireStatusV1 status = 6;
}
```

`effective_permissions`はStableToken ASCII ascending、duplicate禁止、最大1024。

cookie SessionHandleをprotobuf payloadへ載せない。

## 13. General View role registry

```text
view.spectator
view.diver
view.moderator
view.administrator
```

Roleはmutually exclusive primary roleとするが、permission setは明示的に計算する。

### 13.1 General View base permission registry

```text
view.world.read.public
view.world.read.participant
view.world.subscribe
view.operation.diver
view.operation.moderation
view.operation.administration
view.participation.bind
view.participation.policy.write
view.session.read.self
```

### 13.2 Role matrix

| permission | spectator | diver | moderator | administrator |
|---|---:|---:|---:|---:|
| `view.world.read.public` | yes | yes | yes | yes |
| `view.world.read.participant` | no | yes | yes | yes |
| `view.world.subscribe` | yes | yes | yes | yes |
| `view.operation.diver` | no | yes | yes | yes |
| `view.operation.moderation` | no | no | yes | yes |
| `view.operation.administration` | no | no | no | yes |
| `view.participation.bind` | no | yes | no | yes |
| `view.participation.policy.write` | no | yes | no | yes |
| `view.session.read.self` | yes | yes | yes | yes |

OperationKindごとのrequired permissionはOperation registryに固定し、role名だけでhandler分岐しない。

## 14. Admin View permission registry

Admin ViewはGeneral View roleとは別domain。

Standard permissions:

```text
admin.health.read
admin.metrics.read
admin.log.read
admin.config.read
admin.config.write.operational
admin.config.write.presentation
admin.config.write.simulation
admin.command.execute.low-impact
admin.command.execute.high-impact
admin.operation.submit
admin.audit.read
admin.session.read
admin.security.revoke-session
```

Permissionはexplicit setで保持する。

`view.administrator`から上記permissionを自動付与しない。

## 15. Authorization decision record

Gatewayでworld/system-affecting requestをadmitする際:

```text
AuthorizationDecisionV1 {
  decision_id: OpaqueId128,
  session_id: GatewaySessionId,
  session_generation: uint64,
  permission: StableToken,
  operation_id: OperationId | NONE,
  target_kind: StableToken,
  outcome: ALLOW | DENY,
  reason_code: StableToken
}
```

ALLOW decisionはCore world invariantをoverrideしない。

## 16. Role / permission change

Role/permission changeごとに:

```text
session_generation += 1
```

- old generationでnew Operation admissionしない。
- already Gateway-admitted immutable OperationはCore lifecycleへ従う。
- privilege revoke後にretryする場合も、retry deliveryはsame OperationIdを維持するが、new admissionとして権限を再解釈しない。
- severe revokeではWebSocket close + cookie invalidation可能。

## 17. Login protocol payload

```proto
message AuthLoginBeginV1 {
  AuthDomainWireV1 auth_domain = 1;
  optional string return_path = 2;
}

message AuthLoginBeginResultV1 {
  ResultV1 result = 1;
  optional bytes login_transaction_id = 2;
  optional string authorization_url = 3;
}

message AuthLoginResultV1 {
  ResultV1 result = 1;
  optional bytes session_id = 2;
  optional uint64 session_generation = 3;
}
```

`return_path`はsame-origin relative pathのみ。absolute URLやscheme-relative URLをrejectする。

## 18. Gateway-Gateway auth messages

P4-02 message registryへ次を固定追加する。

```text
auth.login.proxy
auth.login.proxy-result
auth.login.assertion
auth.login.result
auth.session.query
auth.session.result
auth.session.revoke
```

Master authorityに依存するmessageはMasterGeneration required。

## 19. Session reconnect

Reconnect時:

- same SessionHandleがactiveならsame GatewaySessionIdを使用可能。
- new WebSocket connectionのNegotiationGenerationは1から開始。
- session_generationを再取得する。
- world publication continuityは別途resyncする。
- reconnect自体でDiverRefやParticipation bindingを変更しない。

## 20. Logout / revoke

Logout:

```text
ACTIVE -> REVOKED
```

- cookie invalidation。
- WebSocket close。
- refresh/access token server-side revokeが可能なら実施。
- Participation bindingを自動解除しない。

Session revokeとworld binding releaseは別Operation。

## 21. Auth error codes

```text
auth.unauthenticated
auth.unauthorized
auth.session-expired
auth.session-revoked
auth.session-stale
auth.login-expired
auth.login-state-mismatch
auth.login-nonce-mismatch
auth.issuer-untrusted
auth.identity-verification-failed
auth.master-changed
auth.origin-rejected
```

Retry guidance:

- session expired/revoked: DO_NOT_RETRY until reauthentication。
- master changed during login: reconnect/restart login transaction。
- origin rejected: DO_NOT_RETRY。

## 22. Sensitive-field handling

次をstructured normal logへ直接出さない。

- authorization code
- access token
- refresh token
- ID Token raw value
- PKCE verifier
- SessionHandle cookie value
- client secret

issuer/subject等identity-provider identifiersもsecurity/audit storeで必要最小限とし、WorldState diagnosticへ含めない。

## 23. Acceptance criteria

- browser JavaScriptへOAuth access/refresh tokenが露出しない。
- PKCE S256 / state / nonce verificationをcontract testできる。
- connected Gatewayだけでlogin finalizationできない。
- MasterGeneration切替中にold login authorityをcurrent化しない。
- General View/Admin View auth domainが分離される。
- General View AdministratorがAdmin permissionを自動取得しない。
- role revoke後にold session generationでnew requestをadmitしない。
- WebSocket cross-origin attachをrejectできる。
- logout/session revokeでParticipation bindingを暗黙解除しない。
- credential/token/SessionHandleがWorldStateへ混入しない。

## 24. P4-02 handoff

- exact session storage backend / encryption key managementはimplementation security Issueへ分解可能。
- IdP deployment choiceはConfig対象。
- session/audit retention値はP4-03/P4-07で確定する。
- permissionごとのstandard OperationKind mappingはP4-02 payload catalogで確定する。
