# 004 — Credit is stored per asset but backs a position in either currency

**Status:** accepted · **Confirmed with the product owner:** 6 Shahrivar 1405 · **Related:** issue
#36 (a different storage model, still open)

## Decision

Every tradable asset gets its own credit ledger, `CREDIT_<ASSET>` (see
`CurrenciesConstant.CreditAssetFor`). But `ValidateCreditAndBalanceAsync` lets credit denominated in
the quote currency back a base-asset position, and the reverse. A customer holding only
`CREDIT_MAUA` can legitimately drive their toman balance negative.

## Why

Credit is a ceiling the shop extends to a customer, not a per-currency allowance. A customer given
credit against gold is trusted for that amount; requiring them to hold it in the currency of each
trade would refuse trades the business intends to allow.

## What this rules out

**Never write a per-asset balance check.** A check must constrain `balance + credit` across both
sides of the pair, never `balance` alone and never one asset in isolation. An invariant that looks
obviously correct per asset will reject legitimate trades.

This has already happened once: an audit raised it as finding N-1, and the finding was retracted.

## Worth knowing

`ValidateCreditAndBalanceAsync` reads `CREDIT_<quote>` and would use it if it existed, but credit
ledgers are minted per tradable **base** asset, so `CREDIT_IRT` is not a currency and depositing
into it fails with "wallet does not exist". Half of the cross-asset design is therefore unreachable
rather than absent. Whether it should exist is the open question in #36.

## Evidence

- `CurrenciesConstant.CreditAssetFor`, `HasCreditLedger` — and the doc comment on the latter, which
  explains the reachability point above.
- `ValidateCreditAndBalanceAsync` in the Wallet service.
- `docs/audit/AUDIT_2026-08.md` — the retracted N-1 finding.
- Issue #36 — replacing this with a single signed ledger; open, and a decision the owner has partly
  answered.
