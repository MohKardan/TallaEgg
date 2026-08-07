# TallaEgg - Agent Development Guide

## Process & Standards

Before writing code or opening a PR, read [`docs/process/INDEX.md`](docs/process/INDEX.md) — it is
the canonical source for coding standards, branch/commit/PR conventions, and current sprint priorities
(`docs/process/STANDARDS.md`, `docs/process/WORKFLOW.md`, `docs/process/SPRINT_PLAN.md`,
`docs/process/PR_TEMPLATE.md`). It applies equally to human developers and AI agents.

## Build Commands
- **Build entire solution:** `dotnet build TallaEgg.sln`
- **Run tests:** `dotnet test TallaEgg.sln` (xUnit only — no Moq, no FluentAssertions)
- **Run the one test project:** `dotnet test src/Wallet/Wallet.Tests/Wallet.Tests.csproj`

`Wallet.Tests` is the only test project in the solution, and it covers more than the wallet:
Orders, the bot's message builders, and shared formatting all have tests there. New tests go in it
until a service earns its own project (issue #46).

**Build before running.** `dotnet test` builds only the test project's dependency graph, so an
API's `bin` can still hold an older build. Always `dotnet build TallaEgg.sln` before
`dotnet run --no-build`, or you will run code you have already changed.

### Starting the services

There is no working script — `run.bat` is stale and its paths do not exist. Start each service in
its own terminal:

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
- **Services and their HTTP ports:** Users 5136, Orders 5140, Wallet 60933, Affiliate 60812,
  TallaEgg.Api 5135, Telegram bot 57546. These are what the services actually call each other on.
  All except Users also listen on an HTTPS port, which nothing in the system uses. The authority
  is `config/appsettings.global.json`, not this list.
- **There is no Matching service.** Matching is a library inside the Orders service.
- **TelegramBot:** Core/Application/Infrastructure layers. The runnable project is
  `TallaEgg.TelegramBot.Infrastructure`; `TallaEgg.TelegramBot` is not in the solution and will not
  start.
- **Database:** SQL Server, one database per service. The schema comes from EF Core migrations,
  which each service applies at startup. `create_table.sql` at the repo root is an early artifact
  describing a single table that no longer matches the model — it is not the schema.

## Configuration

All services and the bot read one shared file, `config/appsettings.global.json`, found by walking
up from the content root. Each reads its own section under `Services:{ApplicationName}`, which is
flattened into the root of its configuration. Per-project `appsettings.json` files are not the
source of truth.

**This file must never be committed** — it holds live credentials and the repo is public. It is
tracked today, which is a known defect (issue #33).

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
- **Admin Commands:** `/admin_referral_on`, `/admin_referral_off`, `/admin_referral_status`

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
