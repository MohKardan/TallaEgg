# TallaEgg - Agent Development Guide

## Process & Standards

Before writing code or opening a PR, read [`docs/process/INDEX.md`](docs/process/INDEX.md) — it is
the canonical source for coding standards, branch/commit/PR conventions, and current sprint priorities
(`docs/process/STANDARDS.md`, `docs/process/WORKFLOW.md`, `docs/process/SPRINT_PLAN.md`,
`docs/process/PR_TEMPLATE.md`). It applies equally to human developers and AI agents.

## Build Commands
- **Build entire solution:** `dotnet build TallaEgg.sln`
- **Run tests:** `dotnet test TallaEgg.sln` (xUnit only — no Moq, no FluentAssertions)
- **Run the one test project:** `dotnet test tests/TallaEgg.AllServices.Tests/TallaEgg.AllServices.Tests.csproj`

`tests/TallaEgg.AllServices.Tests` is the only test project in the solution, and it covers every
service: Wallet, Orders, the bot's handlers and message builders, and shared formatting. **New
tests go in it regardless of which service they cover.**

It was called `Wallet.Tests` and lived under `src/Wallet/` until #117, at which point eight of
its fifty-six files were about the wallet and the rest were not. The name was misleading enough
that an audit concluded the bot was untested because its tests were not in a folder named after
it. If this project is ever split, split it per service so the layout keeps predicting the
contents.

**Build before running.** `dotnet test` builds only the test project's dependency graph, so an
API's `bin` can still hold an older build. Always `dotnet build TallaEgg.sln` before
`dotnet run --no-build`, or you will run code you have already changed.

When a stale `bin`/`obj` needs clearing outright — after switching branches with different
project layouts, say — **stop every running service first** (a live `dotnet run` holds its own
DLLs open, and the delete fails partway leaving a half-emptied `bin`), then, **from the repo
root**:

```powershell
Get-ChildItem -Path . -Recurse -Directory |
    Where-Object { $_.Name -match '^(bin|obj)$' } |
    Remove-Item -Recurse -Force -ErrorAction Stop
```

`-Path .` is not optional: without it the command takes whatever the current directory happens to
be, and one level up that is every sibling repository on the machine.

### Starting the services

To bring the stack up in one command, use `.claude/skills/run-tallaegg/driver.ps1 start` — it
builds the solution, launches Users/Wallet/Orders in the background and waits for all three to
report healthy. On a server, `scripts/windows-services/install-services.ps1` installs and starts
all four as Windows services (see `docs/operations/WINDOWS_DEPLOYMENT.md`).

To run them in the foreground instead, one per terminal:

```
dotnet build TallaEgg.sln
dotnet run --no-build --project src/User/Users.Api/Users.Api.csproj
dotnet run --no-build --project src/Wallet/Wallet.Api/Wallet.Api.csproj
dotnet run --no-build --project src/Order/Orders.Api/Orders.Api.csproj
dotnet run --no-build --project TelegramBot/TallaEgg.TelegramBot.Infrastructure/TallaEgg.TelegramBot.Infrastructure.csproj
```

`Affiliate.Api` is not normally run — the affiliate feature is dormant. `TallaEgg.Api` registers a
DbContext and CORS but maps no endpoints, so running it achieves nothing today.

To run the whole stack somewhere other than a developer machine, use the `manual-test-run`
workflow (Actions → manual-test-run → Run workflow). It stands everything up against a throwaway
SQL Server and keeps it alive for a chosen number of minutes.

## Architecture
- **Clean Architecture** with Core/Application/Infrastructure layers
- **Services and their HTTP ports:** Users 5136, Orders 5140, Wallet 60933 are the three that are
  deployed and that the bot actually calls. `Affiliate.Api` 60812 and `TallaEgg.Api` 5135 are
  configured but not run (see above). All are plain HTTP on loopback — **no service binds an
  HTTPS address.** The authority is `config/appsettings.global.json`, not this list.
- **The bot has a `Urls` entry (57546) but nothing listens on it.** It is a plain generic host
  with no web server and no mapped endpoints; it reaches Telegram by long polling, dialling out.
  The key is inert configuration, not a port to open.
- **There is no Matching service.** Matching is a library inside the Orders service.
- **TelegramBot:** `Core` holds the shared models; `Infrastructure` is the runnable project and
  holds the handlers; `Simulator` drives the real handlers without Telegram. The empty
  `TallaEgg.TelegramBot` and `TallaEgg.TelegramBot.Application` shells were deleted — neither
  contained a single source file.
- **Database:** SQL Server, one database per service. **EF Core migrations are the schema** —
  each service applies its own at startup, except `Affiliate.Api`, which calls `MigrateAsync()`
  while shipping zero migration files and so fails every request with `Invalid object name
  'Invitations'`. A hand-written `.sql` that *creates tables* is therefore a second source of
  truth and always wrong; a `.sql` under `scripts/` that *migrates data* — like
  `migrate-irr-to-irt.sql`, which relabels an asset without touching amounts — is a different
  thing and belongs there.

## Business rules that look like bugs

Each of these has been mistaken for a defect at least once, including by an audit. None of them
is one. Confirmed with the product owner on 6 Shahrivar 1405.

- **The market maker may go arbitrarily negative, on any asset, with no ceiling.** Its account
  currently sits far below zero in IRT and holds no `CREDIT_IRT` ledger. That negative balance
  *is* the shop's book — what customers are owed — and the market maker manages the exposure
  themselves. A balance check that treats it as an overdraft is wrong. There is no alerting on
  it yet; that gap is tracked in #124.
- **Commission is deliberately zero.** `FeeBuyer`, `FeeSeller`, `MakerFee` and `TakerFee` are
  `0.00` on every trade by design. The market maker's revenue is the spread between the published
  buy and sell prices, not commission. Fee code is therefore dormant, not dead — do not delete it
  as unused.
- **Credit is cross-asset, not per-asset.** `ValidateCreditAndBalanceAsync` lets credit
  denominated in the quote currency back a base-asset position (`creditQuote / price`) and vice
  versa. A customer holding only `CREDIT_MAUA` can legitimately drive their IRT balance negative.
  Any per-asset invariant would reject trades the business intends to allow — see the retracted
  N-1 finding in `docs/audit/AUDIT_2026-08.md`.
- **`Wallet.LockBalance` enforces no balance rule, and must not.** The credit ceiling for asset
  `A` lives in a separate wallet row keyed `CREDIT_A`, so a single-asset entity cannot evaluate
  the invariant. The check belongs where both rows are visible. The commented-out guard in that
  method is wrong code, correctly disabled.

## Configuration

All services and the bot read one shared file, `config/appsettings.global.json`, found by walking
up from the content root. Each reads its own section under `Services:{ApplicationName}`, which is
flattened into the root of its configuration. Per-project `appsettings.json` files are not the
source of truth.

**This file must never be committed** — it holds live credentials and the repo is public. It has
been untracked since #33; only `config/appsettings.global.example.json` belongs in git.

Missing configuration fails at startup rather than falling back to a default, so a service that
will not start is usually telling you exactly which key is missing.

## Bot Configuration
- **Location:** `config/appsettings.global.json`, under
  `Services:TallaEgg.TelegramBot.Infrastructure:BotSettings`
- **RequireReferralCode:** true/false to make referral codes mandatory
- **DefaultReferralCode:** used when referral is not required — `admin`, matching the invitation
  code of the seeded root administrator. They have to match, or nobody can register on a fresh
  database.
- **OwnerTelegramIds:** Telegram ids that are approved and made Admin automatically on first
  contact. Without at least one, a fresh deployment has no way to appoint its first administrator.

`RequireReferralCode` and `DefaultReferralCode` are bound once into `BotHandler` at startup, so
changing either needs a restart. **There is no runtime command to toggle them** — an
`/admin_referral_on` / `_off` / `_status` family was documented here for a year and has never
existed in the code.

## Code Style & Conventions
- **Framework:** .NET 9.0 with C# nullable enabled
- **Imports:** Microsoft.Extensions.* for DI/logging, explicit usings for business logic
- **Naming:** PascalCase for classes/methods, camelCase for fields, DTOs suffixed with "Dto"
- **Architecture:** Interfaces in Core, Services in Application, Handlers in Infrastructure
- **Error Handling:** Use `ILogger<T>` for logging, return Result<T> patterns where applicable
- **Testing:** xUnit, AAA pattern. No mocking library is used — test doubles are written by hand,
  and in-memory SQLite stands in for the database where one is needed. Name new tests
  `Method_Scenario_ExpectedResult` per `STANDARDS.md`; existing tests use sentence-style names and
  are deliberately left alone.
- **Comments explain why, not what**, and are written in English. Persian is for text users see.
