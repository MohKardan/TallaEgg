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

## How to add to this file

Ask the owner, write what they said, date it, and name what it changes about work today. If it does
not change how a reader should judge existing code, it probably belongs in an issue instead.
