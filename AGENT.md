# TallaEgg - Agent Development Guide

## Process & Standards

Before writing code or opening a PR, read [`docs/process/INDEX.md`](docs/process/INDEX.md) — it is
the canonical source for coding standards and branch/commit/PR conventions
(`docs/process/STANDARDS.md`, `docs/process/WORKFLOW.md`, `docs/process/PR_TEMPLATE.md`,
`docs/process/CODE_REVIEW_GUIDE.md`). It applies equally to human developers and AI agents.
**Live priorities are GitHub issues** — `gh issue list` — not any document in this repository.

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

To run the stack somewhere other than a developer machine, use the `manual-test-run` workflow
(Actions → manual-test-run → Run workflow). It brings up Users, Wallet, Orders and the bot —
not `Affiliate.Api` — against a throwaway SQL Server and keeps them alive for a chosen number of
minutes, so a human can drive the bot over real Telegram. It asserts nothing, and it needs the
`TELEGRAM_BOT_TOKEN` and `OWNER_TELEGRAM_ID` repository secrets.

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
- **Migration runs *after* the host starts, not before it (issue #230).** Users, Wallet and Orders
  register it through `AddDatabaseMigrationAtStartup` in `TallaEgg.Core.Startup`, which retries a
  database that is not answering yet and gives up loudly after ten attempts. Until it succeeds
  every request is answered `503` by `UseDatabaseReadinessGate()`, `GET /version` excepted. Putting
  a migration back between `builder.Build()` and `app.Run()` reopens the outage in #228: under
  `UseWindowsService()` nothing is connected to the SCM until `app.Run()`, so time spent there is
  time the SCM counts against its 45-second start timeout. Wrapping the old call site in a retry
  loop makes it worse, not better.

## Business rules that look like bugs

Each of these has been mistaken for a defect at least once, including by an audit. None of them is
one. **The reasoning for each now lives in [`docs/decisions/`](docs/decisions/README.md)** — this
list is the index, so that a reader who only ever opens this file still knows the trap is there.

| It looks like | It is | Record |
|---|---|---|
| The market maker's account is far below zero with no ceiling | the shop's book — what customers are owed | [003](docs/decisions/003-market-maker-balance-is-the-shops-book.md) |
| Commission is `0.00` on every trade | deliberate; the revenue is the spread | [002](docs/decisions/002-commission-is-zero-revenue-is-the-spread.md) |
| A customer's balance goes negative while holding credit in another asset | credit is cross-asset — **never write a per-asset balance check** | [004](docs/decisions/004-credit-is-cross-asset.md) |
| `Wallet.LockBalance` validates nothing, with a guard commented out | correct; the entity cannot see the `CREDIT_` row the rule needs | [005](docs/decisions/005-balance-rules-live-where-both-rows-are-visible.md) |
| The `Admin` role skips the balance and credit check entirely | intended, and removing it would not break dealer trading | [006](docs/decisions/006-admin-role-bypasses-the-balance-check.md) |
| An in-flight order is lost when the bot restarts | deliberate; prices move | [007](docs/decisions/007-conversation-state-is-not-persisted.md) |
| No request schema declares a single constraint | deliberate; the endpoints enforce ~50 checks the document does not state | [009](docs/decisions/009-schemas-declare-nothing-endpoints-enforce.md) |
| A trading pair is spelled two different ways across endpoints | real and pre-existing; unifying it is a data migration, not a rename | [010](docs/decisions/010-two-spellings-for-a-trading-pair.md) |

**Before proposing to delete anything, or to "fix" something that looks unfinished, read
[`docs/decisions/`](docs/decisions/README.md) and [`docs/product/DIRECTION.md`](docs/product/DIRECTION.md).**
The second one matters as much as the first: code can look dead because a capability is paused
rather than gone, and the dealer model has paused one.

## Measurements that mislead

Three places where a reasonable-looking measurement gives a confident wrong answer. Each one has
produced a wrong conclusion at least once.

- **A simulator run is not over when it says it is.** The simulator prints `errors 0` when the
  *trading* phase ends; settlement is queued through the Orders outbox and lands tens of seconds
  later. Anything that reads that summary line has measured the first half of a run.

  `driver.ps1 smoke` handles this — since #185 (2026-09-01) `DataReset` waits for no `Status=0` row
  before deleting, and the driver drains before and after, then asserts that no new `Status=2`
  appeared **and** that `Status=1` grew. So trust the driver's closing line, not the simulator's.
  Read a run whose settlement wait never reaches zero as proving nothing at all.

- **The local `Orders` table is mostly simulator residue, so a count from it is not evidence about
  the product.** Measured 2026-09-08: 4,503 orders, of which **4,451 had no `Trade` at all** while
  sitting `Completed` with `RemainingAmount = 0`, against 26 real trades. 4,460 of them belonged to
  one account — the market maker.

  `TelegramBot/TallaEgg.TelegramBot.Simulator/DataReset.cs` deletes asymmetrically: trades where
  *either* side is a simulated user, but orders only `WHERE UserId IN (simulated)`. The dealer is a
  real account with a positive `TelegramId`, so it is never in that list, and **every simulated
  fill leaves the dealer's side of the pair behind forever**, with its trade deleted out from under
  it. Dev machines only; production never runs the simulator.

- **An XML doc comment on a shared DTO never reaches the published schema.** All three APIs call
  `IncludeXmlComments` with **only their own assembly's** file
  (`Assembly.GetExecutingAssembly().GetName().Name`), and `TallaEgg.Core` — where `OrderDto`,
  `TradeDto`, `WalletRequest` and the rest live — sets no `GenerateDocumentationFile` at all. So a
  `<summary>` there is visible to a reader of the C# and invisible in `swagger.json`. Endpoint
  descriptions written in an API project's own `Program.cs` do reach it.

  Measured 2026-09-07 while working #237. The general rule it belongs to: **for anything about the
  wire, measure the wire.** Casing, schema and request-shape defects are invisible in a C# diff,
  and seven issues in this repository hid behind exactly that.

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
