# 001 — The customer trades on a published quote and never enters a price

**Status:** accepted · **Settled by:** issue #48

## Decision

An operator publishes a two-sided quote. The customer picks a symbol, a side and a quantity, and
trades at the published price. The customer is never asked for a price of their own.

## Why

The shop is the counterparty to every trade. Its revenue is the spread between the two published
prices ([002](002-commission-is-zero-revenue-is-the-spread.md)), so the price has to be the shop's,
not something a customer proposes.

It also removes an ambiguity that had already caused real mispricing. Gold is quoted per mesghal
and stored per gram, and when customers entered prices there was no way to tell which unit they
meant — an earlier version showed prices off by the conversion factor. With the price coming from
the quote, the customer only ever gives a quantity, and the unit question never reaches them.

## What this rules out

- Asking the customer for a price anywhere in the ordinary flow. `BotConversationFlowTests`
  asserts this directly, and the test says why.
- Treating the order-book path as the normal one. It still exists in `BotHandler`, but it is a
  fallback taken only when no quote is published, and every symbol runs in dealer mode today, so an
  order placed there has no counterparty and rests unfilled.
- Reading "no orders are resting" as a bug. In this model an order exists only for the instant of a
  fill.

## What this does **not** rule out

Customer-to-customer order placement returning. It existed before the dealer model and may come
back — see [`../product/DIRECTION.md`](../product/DIRECTION.md). This record says what the product
does today, not what it will always do.

## Evidence

- `TelegramBot/TallaEgg.TelegramBot.Infrastructure/BotHandler.cs` — `HandleOrderAmountInputAsync`
  builds the confirmation from the active quote and returns before any price prompt.
- `docs/architecture/DEALER_QUOTE_MODEL.md` — how quoting and filling work.
- `tests/TallaEgg.AllServices.Tests/BotConversationFlowTests.cs` —
  `WithAPublishedQuote_TheCustomerIsNeverAskedForAPrice`.
