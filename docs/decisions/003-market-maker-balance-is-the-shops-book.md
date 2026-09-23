# 003 — The market maker's negative balance is the shop's book, not an overdraft

**Status:** accepted · **Confirmed with the product owner:** 6 Shahrivar 1405

## Decision

The market maker's account may go arbitrarily negative, on any asset, with no ceiling. That
negative balance is what customers are owed — the shop's book — and the market maker manages the
exposure themselves.

## Why

The shop takes the other side of every customer trade ([001](001-dealer-model-customer-never-enters-a-price.md)).
Selling gold to customers necessarily drives its gold balance down and its toman balance up, and
the reverse when it buys. A balance that never went negative would mean a shop that could not
trade.

The account currently sits far below zero in toman and holds no `CREDIT_IRT` ledger, which is
correct and not an oversight.

## What this rules out

- Any balance or credit check that treats the market maker's negative balance as an overdraft to be
  refused.
- Adding a floor to that account, or a migration that "corrects" it to zero.
- Reporting the figure as a data-integrity problem.

## What is genuinely missing

There is no alerting on the size of that exposure — it can grow without anyone being told. That gap
is real and is tracked in **#124**. It is a monitoring gap, not a reason to add a ceiling.

## Evidence

- The market maker's wallet rows in the Wallet service.
- `AGENT.md` → "Business rules that look like bugs", written down after an audit reported this as a
  defect.
- Issue #124 — the alerting that is missing.
