# Roadmap — Telegram Bot to Web App Migration

**Status**: Draft  
**Horizon**: Sprint 2–3 (see [`docs/process/SPRINT_PLAN.md`](../process/SPRINT_PLAN.md) for the day-to-day breakdown)

---

## Goal

Beyond the Sprint 1 security/financial-integrity hardening tracked in `SPRINT_PLAN.md`, the longer-term
objective is to expose the same trading capabilities currently only reachable through the Telegram bot
as a proper web application. This requires the backend to become client-agnostic before a web frontend
can be built on top of it.

## Why this is a separate document

`SPRINT_PLAN.md` tracks remediation of specific audit findings (C-1..C-9). The items below are follow-on
architecture work that enables the web app, not audit fixes — they're roadmap items, not sprint tasks with
owners/estimates yet. Once scheduled into a sprint, promote an item from here into `SPRINT_PLAN.md` with a
task number, owner, and acceptance criteria.

## Workstream: API Contract for the Web App

- Define an OpenAPI/Swagger contract per service, converting bot-oriented endpoints into RESTful resources.
- Audit existing GET endpoints for hidden side effects (state-mutating GETs) and rewrite them to be safe/idempotent.
- Replace manual `new HttpClient()` usage with typed clients via `IHttpClientFactory` (extends TASK-006's
  interface-extraction work in `SPRINT_PLAN.md`).

## Workstream: Cross-Service Transaction Reliability

- Introduce an Outbox pattern (or a Retry/Reconciliation job) for operations that span multiple services,
  so a partial failure between services is recoverable instead of silently inconsistent.
- This is additional to TASK-004's single-service transaction atomicity — it addresses failures *between*
  Wallet/Orders/Matching, not within one of them.

## Workstream: Deployment Hygiene

- Move EF Core migration execution out of application `Startup`/`Program.cs` into a separate deploy-time
  step, so migrations aren't silently re-run (or blocked) by application restarts.

## Out of Scope for Now

- Web frontend implementation itself (framework choice, UI/UX) — not scoped until the API contract above
  is stable.

---

**Maintenance**: Review at the start of Sprint 2 planning; promote items into `SPRINT_PLAN.md` as they're scheduled.
