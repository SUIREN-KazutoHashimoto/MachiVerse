# Gateway間Protocol設計書

## 1. 所有者

本protocolのownerはGateway。

```text
ProtocolId = mv.gateway-gateway
```

共通契約の正本:

- envelope / version / Capability / result: `docs/design/phase1-protocol-envelope.md`
- Operation lifecycle / retry / dedup / custody: `docs/design/phase1-operation-lifecycle-retry-dedup.md`

## 2. 目的

複数Gateway構成で、General View由来Operationをcurrent Master Gatewayへ安全に集約し、retry、ACK loss、Master切替、live migration、result routingを成立させる。

World Stateの正本はSimulation Coreであり、本protocolはauthoritative simulation ruleをGatewayへ複製しない。

## 3. 基本原則

- non-Master GatewayはGeneral View final batchをCoreへ直接送らない。
- source Gatewayはlocal authn/authz / external-request mediationを行う。
- Masterはcross-Gateway mergeを行う。
- stable OperationId / immutable digest / scheduling admission contextをretry/failoverで維持する。
- network arrival race / thread completion orderだけでmerge orderを決めない。
- hop ACKをCore durable acceptanceと同一視しない。

## 4. Common envelope / Capability

normal messageは `ProtocolEnvelopeV1` を使用する。

- `ProtocolId = mv.gateway-gateway`
- negotiated ProtocolVersion / NegotiationGenerationを明示する。
- Master authorityに依存するmessageはWorldContextV1.MasterGenerationを使用する。
- Operation / Batch messageはOperationContextV1を使用する。
- required Capability不足をsilent degradationしない。
- connection中のCapability changeはreconnectを基本とする。

## 5. Master identity / generation

Coreがcurrent MasterGenerationのauthority。

- old generation messageをcurrentとして扱わない。
- stale Master outputをblind acceptしない。
- Master不明時にnon-Masterが独断でCoreへfinal batchを送らない。
- ComponentInstanceIdをGateway logical identityの代替にしない。

## 6. Local Operation admission

source GatewayがOperationとして受理する際、confirmed Core basisとCore配布scheduling policyを使用して次を固定する。

```text
OperationSchedulingAdmissionV1 {
  admission_basis_step,
  scheduling_policy_generation,
  requested_not_before_step,
  requested_deadline_step
}
```

このcontextはimmutable Operation digestへ含める。

source Gatewayはresync中でconfirmed basisを持たない場合、新規world-affecting Operationをauthoritative admissionしない。

## 7. Local batch transfer

Non-Master Gatewayはlocal batchをMasterへ送る。

batch entryは少なくとも:

- OperationId
- immutable Operation digest
- immutable scheduling admission context
- advisory candidate Step
- operation type / target / content
- result routing context

を追跡可能にする。

candidate Stepはauthoritative `effective_step` ではない。

## 8. Batch identity

```text
BatchDigest = DomainHash(
  "mv.batch.v1",
  {
    batch_kind,
    ordered_entries: [
      { operation_id, operation_payload_digest }, ...
    ]
  }
)
```

MasterGeneration、routing、MessageId、retry metadataはBatchDigestへ含めない。

- exact same logical batchのretry/failoverではsame BatchId / BatchDigestを維持できる。
- same BatchId + different BatchDigestは `protocol.batch-payload-mismatch`。
- subset retry / entry追加削除 / semantic reorderはnew BatchId。
- contained OperationIdは維持する。

## 9. Batch ACK / processing state

標準BatchはPER_OPERATION processing。

```text
BatchStatus := RECEIVED | PARTIAL | COMPLETE | REJECTED
```

- RECEIVED: Master hop receipt。
- PARTIAL: contained Operationのlifecycleが混在。
- COMPLETE: 全entry terminalまたはknown duplicate terminal。
- REJECTED: wrapper不正でentry処理未開始。

Master receipt ACKはCore authoritative acceptanceを意味しない。

Batch PARTIALで既にterminalになったOperationをrollbackしない。

## 10. custody

source Gateway / Master間のdelivery responsibilityを次で扱う。

```text
SOURCE_HELD
 -> MASTER_RECEIVED
 -> CORE_ACCEPTED
 -> TERMINAL
```

### SOURCE_HELD

source Gatewayはdownstream Core custody確認前のOperationを保持する。

- disconnect / Master switchを跨いでretry可能にする。
- OperationId / digest / scheduling admission contextを保持する。

### MASTER_RECEIVED

Masterがlocal batchをreceipt ACKした状態。

source GatewayはこのACKだけで唯一の再送可能copyを破棄しない。

### CORE_ACCEPTED

Core durable ACCEPTEDが確認できた状態。

source / MasterはCore未達を理由とするdelivery retryを停止できる。

terminal不明時はOperationId status queryで確認できる。

### TERMINAL

Core terminal result確認済み。

world mutation用retryを停止する。

## 11. retry

same logical Operation retryは:

- same OperationId
- same immutable digest
- same scheduling admission context

を維持する。

exact same logical batch retryならsame BatchIdを維持する。

retry interval / timeout / backoff / jitterはOPERATIONAL Config。

retry timing / countをworld orderへ使用しない。

## 12. Master failover

old Master障害時に次をnew Masterへ安全に引き継ぐ。

- sent済み / ACK不明local batch
- Master receipt済み / Core acceptance不明Operation
- retrying Operation
- result未返却Operation

new Masterへの再送規則:

- same OperationId / digest / scheduling context。
- exact same batchならsame BatchId可。
- re-mergeでcontentsが変わる場合はnew BatchId。

old generation final batchがCoreで `master.stale-generation` になっても、contained Operationをterminal rejectとみなさない。

## 13. ACK unknown convergence

Master failoverやACK lossでCore acceptanceが不明なOperationはsame identityでretryする。

Core authoritative responseにより:

- UNKNOWN/UNSEEN: normal acceptanceへ進む。
- ACCEPTED/SCHEDULED: duplicate current stateとして収束する。
- TERMINAL: stored terminal semanticsを返す。

これによりexactly-once deliveryを要求せずeffectively-once world mutationを成立させる。

## 14. deterministic merge

Masterはsame effective Operation setに対し、Gateway数、arrival timing、network latency、thread completion、Master identityによらずsame logical merge resultを作る。

- physical arrival orderをauthorityにしない。
- OperationIdの大小自体をbusiness priorityにしない。
- Core authoritative same-Step orderはP1-02 `SameStepOrderKey`に従う。
- Gateway-level mediationはexternal-request levelに限定する。

Gateway local / cross-Gateway mergeのdomain-specific keyは個別message schemaで定義するが、P1-02 / P1-06のidentityとscheduling意味論を上書きしない。

## 15. candidate Step / deadline

Gateway/Masterはadvisory `candidate_step` を扱える。

Core final assignment前にWorldContext.effective_stepへcandidateを設定しない。

scheduling deadline / grace / late policyはCore-owned scheduling policyを参照し、Master switch時に勝手に延長・変更しない。

## 16. result routing

Core resultをMasterからsource Gatewayへroutingする。

- CorrelationIdを可能な範囲で維持する。
- OperationIdをauthoritative result identityとして使用する。
- stale generation resultをcurrent requestへ誤対応しない。
- hop ACKとOperation terminal resultを区別する。

source Gateway reconnect後はOperationId status queryでterminal/current stateを再取得できる。

## 17. login proxy

Q241に従いlogin requestはconnected GatewayからMasterへproxyし、Masterでfinalizeする。

- non-Masterが独立finalizeしない。
- old Master auth stateをnew generationのauthorityとしてsilent reuseしない。
- login request/resultはCorrelationIdで追跡可能にする。

Credential/token/session storageはauth詳細設計の責務。

## 18. Batch retention

Batch ACK / dedup historyは有限OPERATIONAL retentionとしてよい。

ただしBatch history expiry後も、contained OperationIdはCore End-to-End dedupを必ず通す。

BatchId expiryを新しいlogical batchとしてsame ID再利用する根拠にしない。

## 19. 禁止事項

- non-MasterのCore direct final submission
- stale MasterGenerationをcurrent authorityとして扱うこと
- retryでOperationIdを変更すること
- same OperationIdでimmutable scheduling contextを変更すること
- Master hop ACKをCore durable acceptanceと同一視すること
- BatchIdをOperation dedup keyにすること
- same BatchIdでcontentsを変更すること
- Batchを暗黙transactionとして扱うこと
- candidate Stepをauthoritative effective_stepにすること
- network arrival orderだけでmergeすること
- old Master batch rejectをOperation terminal rejectと同一視すること

## 20. component実装へ残す事項

- physical transport / serialization / compression
- Gateway logical identity representation
- local / cross-Gateway merge field schema
- heartbeat / election physical message
- exact retry timeout/backoff values
- Gateway durable queue storage
- login session handoff implementation

これらは本書のcustody / identity / retry / Batch semanticsを変更してはならない。
