# 010 — A trading pair is `asset`/`amount` in some schemas and `symbol`/`quantity` in others

**Status:** accepted · **Settled by:** issue #237

## Decision

Both spellings stay. `POST /api/orders` takes `asset`/`amount`; `POST /api/quotes/accept` takes
`symbol`/`quantity`. A client that places orders has to know both and pick per endpoint.

## Why unifying them is not a rename

`TradeDto` is also the outbox payload. `OrderMatchingRepository` serializes it into
`OutboxMessages.Payload` with a bare serializer, and `OutboxProcessorService` reads it back
**case-sensitively** — there is no schema between that pair and no tolerance either. Renaming its
properties turns every payload not yet settled into a settlement for an empty symbol and zero
quantity.

So unification means a data migration over stored payloads plus a lockstep deployment of Orders and
Wallet. `asset` versus `symbol` is a different name, not a different case, so the case-insensitive
binding that carries ordinary DTO renames through a mixed deployment does not bridge it.

Worth doing only before a browser client (#97) ships against the current shape, and only as its own
piece of work. **That window has an end now:** the owner sequenced the web and mobile clients after
the first pilot contracts ([`../product/DIRECTION.md`](../product/DIRECTION.md), 2026-09-23), so
this is cheap until then and expensive afterwards.

## What this rules out

- Tidying one spelling into the other as part of an unrelated change.
- Changing `TradeDto`'s property names at all without the migration above.
- Relaxing the strict read in `OutboxProcessorService`. The strictness is what makes a change to
  the write side fail loudly instead of settling trades for an empty symbol.

## What a new endpoint does

Take the spelling **its own response already uses**. Where nothing constrains it, use
`symbol`/`quantity`, the larger group. That order matters: #235 kept `OrderDto` on `asset`/`amount`
precisely because `OrderHistoryDto` is the response of that very endpoint, and a client works one
endpoint at a time. Applying "follow the majority" first would have reintroduced the mismatch that
issue refused to create.

## Evidence

- `docs/process/STANDARDS.md` §2 — the full table of which DTO uses which spelling.
- `OrderMatchingRepository` (write) and `OutboxProcessorService` (read) — the strict pair.
- Issues #235, #237, #97.
