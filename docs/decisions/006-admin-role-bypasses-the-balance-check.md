# 006 — The `Admin` role places orders with no funds and no credit

**Status:** accepted · **Confirmed with the product owner:** 17 Shahrivar 1405 (2026-09-08)

## Decision

`OrderService.CreateOrderAsync` wraps both refusals — the balance-and-credit check failing, and the
balance being insufficient — in `if (!isadmin)`, where `isadmin` is `user?.Role == UserRole.Admin`.
An administrator can therefore place an order of any size with no funds and no credit. That is
intended.

## Why

An operator needs to be able to act on the book without their own account being funded first. The
account that matters for solvency is the shop's, and that is not constrained either
([003](003-market-maker-balance-is-the-shops-book.md)).

## What this rules out

Removing the bypass to "close a hole". Two things to know before touching it:

- **The role is `Admin` specifically, not `SuperAdmin`.** This is not the shop-ledger exemption it
  resembles.
- **It is not what lets the market maker take the other side of a quote fill.** That path runs
  through `CreateLockedAndConfirmedOrderForQuoteAsync` and never reaches this check at all. So
  removing the bypass would not break dealer trading — and it is still not to be removed.

## Evidence

- `src/Order/Orders.Application/OrderService.cs` — `CreateOrderAsync`, the two `if (!isadmin)`
  guards.
- `CreateLockedAndConfirmedOrderForQuoteAsync` in the same file — the dealer path, which is
  separate.
