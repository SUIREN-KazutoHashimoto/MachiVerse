# 決定論的更新・時間進行設計

## 確定方針

第200〜204問およびQ276〜Q278の時間・適用Step・Pause意味論を、Phase 1詳細設計へ反映する。

詳細契約の正本:

- `docs/design/phase1-common-foundation-contracts.md`
- `docs/design/phase1-determinism-ordering-random.md`
- `docs/design/phase1-operation-lifecycle-retry-dedup.md`

## World Time

- authoritative World Timeは `SimulationStep := uint64`。
- world初期状態は `State(0)`。
- `effective_step = S` のinputは `State(S) -> State(S+1)` transitionへ参加する。
- Pause中はSimulationStepを進めない。
- processing overrunだけを理由にStep skipしない。
- wall clockは運用・表示補助であり、authoritative apply/order/randomの入力にしない。

## same-Step処理

基本モデル:

```text
State(S)
 -> read / parallel calculation
 -> deterministic merge / conflict resolution
 -> authoritative apply
 -> State(S+1)
```

- thread/task completion orderをapply orderにしない。
- system dependencyとconflictはP1-02のdeterministic ordering contractに従う。
- same-Step canonical orderは `SameStepOrderKey` を使用する。
- network arrival order、thread id、Master identity、retry countをordering keyにしない。

## External Operation scheduling

Gatewayはrequest admission時にconfirmed Core basisとCore配布scheduling policyを用いてimmutable admission contextを作る。

```text
OperationSchedulingAdmissionV1 {
  admission_basis_step,
  scheduling_policy_generation,
  requested_not_before_step,
  requested_deadline_step
}
```

Gateway/Masterが送る `candidate_step` はadvisoryでありauthoritativeではない。

Coreはhistorical scheduling policyからcanonical candidateを再計算する。

```text
canonical_candidate = max(
  admission_basis_step + policy.min_lead_steps,
  requested_not_before_step if present
)
```

Coreはtransition input freeze後の最小open Stepを `next_schedulable_step` とし、通常時は:

```text
target_step = max(canonical_candidate, next_schedulable_step)
```

からfinal `effective_step`を決定する。

final effective Stepはdurable scheduling factとして保存し、recovery後に別Stepへsilent reassignmentしない。

## deadline / grace / late

Core scheduling policyは少なくとも次を持つ。

```text
min_lead_steps
default_deadline_window_steps
grace_steps
late_policy = REJECT | DEFER_WITHIN_GRACE
```

これらはworld outcomeへ影響し得るためSIMULATION Configとして履歴化する。

- origin requested deadlineとpolicy deadlineの両方がある場合は厳しい方を採用する。
- targetがdeadline以内なら通常schedule。
- deadline超過かつ `REJECT` ならterminal `world.deadline-exceeded`。
- `DEFER_WITHIN_GRACE` かつgrace limit以内ならfuture valid Stepへdeferし `world.late-deferred` とする。
- grace超過後はreject。
- finalized past Stepをretroactive rewriteしない。

## Pause中Operation

worldが `State(P)` でPauseしている場合:

- Pause前に `effective_step = P` とschedule済みのOperationはtransition Pに残す。
- Pause中はそれらをapplyしない。
- Resume後の最初の `State(P) -> State(P+1)` transitionで処理する。

Pause active中に新規durable acceptanceしたsimulation-affecting Operationは停止中Step Pへ後付けしない。

```text
pause_floor_step = P + 1

target_step = max(canonical_candidate, P + 1)
```

- Pause中arrival orderをresume後orderにしない。
- P+1へ集まったOperationはsame-Step canonical orderで処理する。
- Pause wall-clock durationだけでSimulationStep deadlineを消費しない。
- `pause_floor_step` によりdeadlineを超える場合のみ通常late/grace規則を適用する。
- durable accepted Operationにwall-clock queue expiryを設けない。

## capacity / backpressure

Coreはdurable accepted Operationをqueue pressureでsilent evictionしない。

resource pressure時はdurable acceptance前にtemporary reject/backpressureできる。

一度Coreがauthoritative `ACCEPTED` を返したOperationは、Pause、disconnect、Master切替、timeoutを理由に失わない。

## Gateway不在

connected Gatewayが0台でも、それ自体を理由にSimulationStepを停止しない。

- Core internal eventは継続する。
- Core durably accepted Operationは予定された規則に従って処理する。
- 新規external Operationだけが入らない。
- Gateway復旧後にabsence期間へworldをrewindしない。

## 再現性

同一WorldSeed、simulation-affecting Config/history、immutable Operation集合、final effective Step、same-Step orderが同一ならworld outcomeを一致させる。

replayはoriginal network arrival timing、retry timing、thread scheduling、Master identityを再現条件にしない。

## component実装へ残す事項

- physical transport retry algorithm
- exact operational timeout/backoff数値
- lag detection / load reductionの具体Config key
- Core internal scheduling queue data structure

これらは本書のauthoritative Step / Pause / late意味論を変更してはならない。
