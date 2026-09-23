# 002 — Commission is zero on every trade; the revenue is the spread

**Status:** accepted · **Confirmed with the product owner:** 6 Shahrivar 1405

## Decision

`FeeBuyer`, `FeeSeller`, `MakerFee` and `TakerFee` are `0.00` on every trade, by design. The shop
earns the difference between the buy and sell prices it publishes, not a commission.

## Why

The shop is the counterparty to every fill ([001](001-dealer-model-customer-never-enters-a-price.md)),
so it is already paid on each trade through the spread. Charging a commission on top would be
charging twice for the same service, and the published prices are what a customer compares against
other gold shops.

## What this rules out

- Deleting the fee fields, or the code that computes and stores them, as dead. They are dormant,
  not dead: the fields are written on every trade and a future pricing model may use them.
- Reading `0.00` in a fee column as a defect, a missing migration, or an unfinished feature.
- "Fixing" a fee calculation that produces zero.

## Evidence

- Fee fields on the trade rows in the Orders service, written as `0.00` on every settlement.
- `AGENT.md` → "Business rules that look like bugs", where this was first written down after being
  reported as a defect.
