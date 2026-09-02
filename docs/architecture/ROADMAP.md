# Roadmap — Telegram Bot to Web App Migration

**Status**: Draft — direction, not scheduled work

---

## Goal

Expose the trading capabilities currently reachable only through the Telegram bot as a proper web
application. This requires the backend to become client-agnostic before a web frontend can be
built on top of it.

`docs/OKR.md` records that the web app is deliberately **out of scope** for the current engineering
cycle: it is a real strategic need — the bot depends on `api.telegram.org` and stops entirely
under an international-internet outage — but it has never been the thing blocking a demo.

## Why this is a separate document

The items below are architecture work that enables the web app, not fixes for a numbered audit
finding. They have no owner and no estimate. When one is scheduled, open a GitHub issue for it and
work from there — issues are where live priorities are, and this file is not.

## Workstream: API Contract for the Web App

- Define an OpenAPI/Swagger contract per service, converting bot-oriented endpoints into RESTful resources.
- Audit existing GET endpoints for hidden side effects (state-mutating GETs) and rewrite them to be safe/idempotent.
- Replace the remaining manual `new HttpClient()` usage with typed clients via `IHttpClientFactory`.
  The interface extraction is done — `IWalletApiClient` and `IOrderApiClient` exist — but several
  call sites still construct a client directly.

## ~~Workstream: Cross-Service Transaction Reliability~~ — done

Delivered in #21 / #41. Trade settlement crosses the Orders and Wallet services through a
transactional outbox: `OutboxMessage` in `Orders.Core`, drained by `OutboxProcessorService` with
exponential backoff, idempotent on the trade id, and with operator endpoints under `/api/outbox`
to inspect, redrive or abandon a stuck message. Kept here as a record of what the roadmap
predicted, not as pending work.

## Workstream: Deployment Hygiene

- Move EF Core migration execution out of application `Startup`/`Program.cs` into a separate deploy-time
  step, so migrations aren't silently re-run (or blocked) by application restarts.

## Out of Scope for Now

- Web frontend implementation itself (framework choice, UI/UX) — not scoped until the API contract above
  is stable.

---

**Maintenance**: Revisit when the web app becomes scheduled work. Anything that starts moving gets
a GitHub issue and comes off this list.
