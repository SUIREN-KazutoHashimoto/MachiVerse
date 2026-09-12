# Alpha 1.1 Society FinanceAccount authority proposal

Status: **Review only / approval pending**

Tracking: #240
Implementation after approval: #265

## Purpose

This document proposes an exact `perf.reference.v1` authority for the remaining `society.finance_account` 80,000-record slice.

Nothing in this document is normative until explicit project-owner approval is recorded in #240. In particular, the owner mapping, currency mapping, zero balances/credit, optional institution choice, status Token, and empty-ledger digest are all explicit decision points rather than inferred semantics.

## Existing authority available

The proposed package can close its reference/token dependencies using already-proven production authority:

- canonical Resident identity: at least ordinals `0..79,999` are already production-proven;
- approved CurrencyMoney: 100 records with exact Tokens `perf.currency-000..perf.currency-099`;
- existing QA-04 `society.finance_account` descriptor slice at global Society/Governance ordinals `320,100..400,099`;
- standard FinanceAccount payload/schema and Snapshot codecs;
- runtime `FinanceAccountBalanceV1` balance/credit-limit validation and balanced-ledger mechanics already exist;
- required derived indexes `society.account-by-owner` and `society.account-by-currency` already exist in the standard index registry.

There is **no existing normative persistent `ledger_head_digest` generation rule**. That missing semantic is therefore surfaced explicitly below rather than inferred.

## Recommended benchmark mapping

For local ordinal `a = 0..79,999`:

```text
owner_ref              = Resident[a]
institution_ref        = NONE
currency_token         = perf.currency-{(a mod 100):000}
balance_microunit      = 0
credit_limit_microunit = 0
status                  = active
ledger_head_digest      = SHA-256(empty byte sequence)
```

The exact proposed empty-ledger digest is:

```text
e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855
```

### Proposed meaning

- one benchmark account is assigned to each canonical Resident ordinal `0..79,999`;
- `institution_ref = NONE` avoids inventing a bank, government, central bank, employer, or other custody institution;
- currencies are assigned round-robin over the **already-approved 100 CurrencyMoney Tokens**, yielding exactly 800 accounts per currency;
- `balance_microunit = 0` creates no initial holdings;
- `credit_limit_microunit = 0` creates no synthetic credit authority;
- `status = active` means the benchmark account definition is available, not that it contains funds or has transacted;
- `ledger_head_digest = SHA-256(empty byte sequence)` is proposed as an Alpha 1.1 **benchmark genesis sentinel for an account with zero postings**. It is not proposed as a general ledger-chain format, transaction hash recipe, Merkle scheme, or universal account-history representation;
- because there are zero genesis postings, the double-entry invariant is vacuously satisfied at genesis. Any later posting still remains subject to the existing balanced-ledger runtime mechanics.

## Why Resident owners are proposed

The schema allows a generic `owner_ref`, but no normative mixed Resident/Organization allocation exists for the QA-04 fixture. Using Resident ordinal `a`:

1. consumes an already-proven 80,000-element authority pool with exact one-to-one cardinality;
2. avoids inventing organization-account multiplicity or institutional ownership;
3. does not imply employment, membership, wealth, income, or banking relationships;
4. gives an exact deterministic owner mapping that can fail closed.

This is a benchmark fixture choice only and does not constrain general MachiVerse account ownership.

## Why institution_ref is NONE

Although Governance Institution records exist, mapping them to financial institutions would add semantics not established by current authority. `NONE` is therefore the least-assumptive benchmark genesis choice.

Approval of this package must not be read as saying accounts never have institutions. It says only that this reference-world genesis fixture does not fabricate one.

## Why the empty-byte SHA-256 is proposed

The persistent schema requires a 32-byte `ledger_head_digest`, but the repository currently provides no normative encoder/hash chain for a FinanceAccount ledger head. A fabricated account-specific hash recipe would create a new hidden protocol.

The proposed SHA-256 digest of the empty byte sequence is instead an explicit, deterministic sentinel for **no ledger postings at genesis**. The same digest on all 80,000 accounts is intentional because the proposed digest represents the same empty posting sequence, not account identity.

This choice must be explicitly approved before implementation. It must not be reused as the digest of a non-empty ledger.

## Canonical envelope

For every account use only the existing QA-04 descriptor identity:

```text
record_id    = existing descriptor RecordId for FinanceAccount local ordinal a
revision     = 1
created_step = 0
retired_step = NONE
detail_level = D2
lineage_ref  = NONE
```

No account RecordId derivation or specialized identity is introduced.

## Required derived indexes

Rebuild from production records:

```text
society.account-by-owner
society.account-by-currency
```

Required cardinality:

- account-by-owner: 80,000 keys, exactly one account per owner;
- account-by-currency: 100 keys, exactly 800 accounts per approved currency Token.

## Required production proof after approval

Before accepted accounting moves, #265 must prove on its current head:

1. exactly 80,000 FinanceAccount records materialized through the production payload validator;
2. exact existing descriptor/envelope binding for every record;
3. actual canonical Resident `a` closure for all 80,000 owner Refs;
4. exactly 80,000 distinct owners and one account per owner;
5. exact round-robin closure to the approved 100 CurrencyMoney Tokens, exactly 800 accounts per token;
6. `institution_ref = NONE` on all records;
7. exact zero balance, zero credit limit, `active`, and approved empty-ledger digest on all records;
8. runtime `FinanceAccountBalanceV1` validation for all proposed zero/zero account states;
9. both required secondary indexes with exact cardinalities above;
10. full 80,000-record Snapshot encode / recovery / semantic rehash;
11. fail-closed negative proof for missing/wrong owner, duplicate owner, wrong/non-approved currency Token, institution injection, balance drift, credit drift, status drift, digest drift, duplicate RecordId, and population/index cardinality drift;
12. current-head full CI.

The proof must not fabricate ledger entries merely to satisfy the digest. Genesis authority is explicitly zero postings if this proposal is approved.

## Acceptance accounting if approved and fully proven

Only after production proof and full current-head CI succeed:

```text
Society/Governance accepted = 1,601,200 / 2,000,000
Society/Governance remaining =   398,800
Society remaining            =   139,800
Governance remaining         =   259,000
```

Other parent blockers and release flags remain unchanged.

## Explicit non-decisions

This proposal does not decide:

- bank or financial-institution identity;
- Organization-owned accounts;
- nonzero initial balances or wealth distribution;
- credit issuance;
- interest, fees, overdraft policy, deposits, lending, reserves, or capital requirements;
- exchange rates or currency convertibility;
- ledger entry identity or a persistent ledger-chain encoding;
- digest rules for any non-empty ledger;
- payment/settlement authority;
- Governance TaxFiscal or monetary policy;
- PropertyRight, BusinessProduction, LogisticsObligation, or HistoryLineage authority.
