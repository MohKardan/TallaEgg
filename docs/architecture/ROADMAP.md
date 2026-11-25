# Roadmap — Telegram Bot to Web App Migration

**Status**: Active — initial web portal shipped (#350–#360); backend API-contract workstreams remain open

---

## Goal

Expose the trading capabilities currently reachable only through the Telegram bot as a proper web
application. This requires the backend to become client-agnostic before a web frontend can be
built on top of it.

An initial Next.js 14 Persian RTL web client (`web/`) — covering the `ZARORBIT` landing page,
ecosystem vision page, Telegram Login BFF, and read-only wallet view — shipped in #350–#360.
The backend workstreams below remain tracked alongside `web/` so browser clients can eventually
consume stable, client-agnostic APIs without depending exclusively on `api.telegram.org`. For
product-owner sequencing of full web and mobile trading, see
[`../product/DIRECTION.md`](../product/DIRECTION.md).

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

## Remaining Web Scope

- Full browser-based trading flows in `web/` (quote execution and order placement) — the initial
  Next.js 14 portal and read-only wallet BFF shipped in #350–#360, while transactional web trading
  remains dependent on stabilizing the backend API contract above and product-owner prioritization
  (see [`../product/DIRECTION.md`](../product/DIRECTION.md)).

---

**Maintenance**: Revisit when the web app becomes scheduled work. Anything that starts moving gets
a GitHub issue and comes off this list.
