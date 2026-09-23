# Product direction

Where the product is going, and what is deliberately not built yet.

This file exists because its absence cost something. In #293 an admin command was removed and
another relabelled — both correct, and the removal was the owner's own call. What was wrong was the
sentence written beside them in the code: *"removing it beats implementing a list the dealer model
keeps empty"*. That is only true while the dealer model is the whole product. Nothing in the
repository said it might not be, so nothing could have corrected it, and the comment would have
outlived everyone who knew better.

**Owned by the product owner.** An agent may propose an entry and must not write one from
inference. Every entry below says who confirmed it and when; an entry without that is a guess and
should be treated as one.

**What belongs here:** intent that changes how today's code should be judged — a capability that
may return, a direction that makes a current limitation temporary, a thing deliberately left
unbuilt. **What does not:** a feature request with an owner and a date. That is a GitHub issue.

For decisions already taken and in force, see [`../decisions/`](../decisions/README.md). This file
is about what has *not* been decided yet, or has been decided for later.

**Do not add or drop a product feature on your own initiative.** The owner said this while
answering about the accountant role, and it applies to all of it: intent recorded here is a
direction, never a licence to start building. Wait for a real need from a real customer. An entry
saying something will happen later is not an invitation to do it now.

---

## Customer-to-customer order placement may return

**Confirmed by the product owner, 2026-09-22.**

Before the dealer model, customers placed orders against each other. A customer could name a price
and a quantity and wait — possibly for a while — until someone else accepted it. Open orders were a
real thing a customer had, and that is what the admin command `س` (سفارش) was built to inspect.

Today every symbol runs in dealer mode ([decision 001](../decisions/001-dealer-model-customer-never-enters-a-price.md)),
the shop is the counterparty to every trade, and an order exists only for the instant of a fill. So
an open-order list is empty and the code around it looks like dead weight.

It may not stay that way. The owner may bring back customer-chosen price and quantity.

**What this means for work today:**

- **Do not treat order-book code as dead** merely because the dealer model does not exercise it.
  Removing something because "orders never rest" is reasoning from a state that is intended to
  change. Report it and ask.
- **The command letter `س` is reserved for orders**, so that it means the same thing if open orders
  come back.

  **Agreed 2026-09-22, done 2026-09-23.** A customer's trades are `معامله <phone>`; `س` is handled
  nowhere and is reserved, not free. Do not reuse the letter for a new command — that is the whole
  point of having moved it.
- No timeline, no commitment, and no design has been agreed. This is intent, not a plan.

---

## Commission will not stay zero

**Confirmed by the product owner, 2026-09-23.**

Commission is `0.00` on every trade today and the revenue is the spread
([decision 002](../decisions/002-commission-is-zero-revenue-is-the-spread.md)). That is a decision
about now, not forever. The owner expects to take up commission **after product-market fit**.

**What this means for work today:**

- **The fee code is not dormant-and-forgotten, it is dormant-and-waiting.** `FeeBuyer`,
  `FeeSeller`, `MakerFee` and `TakerFee` are written on every trade and will one day carry real
  numbers. Deleting them as unused would have to be undone.
- No timing, no rates, no model. "After product-market fit" is not a date.

---

## The accountant role will probably be a read-only administrator

**Confirmed by the product owner, 2026-09-23.** · Related: issue #294

`UserRole.Accountant` can be assigned today and grants nothing — an accountant sees the customer
menu. The likely answer is that they get everything an administrator can see, in **read-only**
form: balances, trades, quotes, the user list, and none of the commands that change anything.

**Likely, not decided.** The owner wants the real requirement from the first customers before it is
built, and explicitly does not want features added or removed on a guess.

**What this means for work today:**

- Do not implement #294 from this paragraph. It records where the answer will probably land, so
  that nobody designs something incompatible in the meantime.
- Do not remove the role either, on the grounds that it does nothing.

---

## A web and mobile client come after the first pilot contracts

**Confirmed by the product owner, 2026-09-23.** · Related: issue #97

The Telegram bot is the whole product today. A web client and a mobile client are expected, and
they are sequenced: **after the first pilot contracts are signed**, not before.

**What this means for work today:**

- Several decisions are written as "worth doing only before a browser client ships against the
  current shape" — most importantly unifying the two spellings of a trading pair
  ([decision 010](../decisions/010-two-spellings-for-a-trading-pair.md)). That window is real and
  it is still open. It closes at the pilot contracts, not at some vague future.
- The same goes for anything that would become a public contract. Today the only consumer is the
  bot, which this repository controls. That is a temporary luxury.
- Rate limiting (#279) is bounded today only because every service is on loopback. The web client
  is what changes that.

---

## Fifty tradable symbols is enough for now

**Confirmed by the product owner, 2026-09-23.**

`BotHandler.MaxSymbolButtons` is 50, set in #296 as a safety rail rather than a product limit. The
owner confirms 50 is comfortably above what the product needs today, and named the trigger for
changing it: **a large customer who trades more than fifty symbols.**

**What this means for work today:**

- The number has a reason to move now, and it is a customer, not a tidy-up. Do not raise it
  speculatively, and do not lower it.
- The comment on that constant says the figure is a conservative guard and not a measurement of
  what Telegram accepts. That is still true: if the ceiling ever needs raising, measure what
  Telegram actually refuses first.

---

## How to add to this file

Ask the owner, write what they said, date it, and name what it changes about work today. If it does
not change how a reader should judge existing code, it probably belongs in an issue instead.
