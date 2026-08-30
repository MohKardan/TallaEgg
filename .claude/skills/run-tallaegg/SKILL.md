---
name: run-tallaegg
description: Build, run, and drive the TallaEgg backend (Users/Wallet/Orders APIs plus the Telegram bot's own handlers) end to end without a live Telegram connection. Use when asked to start TallaEgg, run the services, smoke-test the bot, generate sample trading data, or verify a change actually works against the running app rather than just its unit tests.
---

TallaEgg is a Telegram bot (Persian-language gold trading) backed by three ASP.NET Core
microservices (Users.Api, Wallet.Api, Orders.Api) and SQL Server, one database per service.
There is no GUI to screenshot and no live Telegram connection available outside a real bot
token — the agent path here is `.claude/skills/run-tallaegg/driver.ps1`, a thin wrapper that
builds the solution, launches the three APIs, and drives them through
**`TelegramBot.TallaEgg.TelegramBot.Simulator`** — a project the repo already ships, which
replays real bot conversations (registration, admin approval, quote publishing, quote-fill
trades, menu navigation) through the real `IBotHandler` and the real APIs/database, faking
only the Telegram transport itself. That is the interaction surface: not a browser, not a
window, a conversation replayed through the actual handler code.

All paths below are relative to the repo root (`TallaEgg.sln`'s directory), not to this skill
directory. The driver is Windows PowerShell (`powershell.exe`, not `pwsh`) — this project's
native dev environment is Windows with a local SQL Server Express instance, per `AGENT.md`.

## Prerequisites

- .NET 9 SDK (`dotnet --version` -> `9.x`)
- A reachable SQL Server instance. On a normal dev box this is SQL Server Express
  (`Server=localhost\SQLEXPRESS`, Windows integrated auth) — confirm it's running:

  ```powershell
  Get-Service | Where-Object { $_.Name -like '*SQL*' } | Select-Object Name, Status
  # -> MSSQL$SQLEXPRESS  Running
  ```

  Any reachable SQL Server works; only the connection strings in the config below need to match.

## Setup

`config/appsettings.global.json` is git-ignored (it holds live credentials — never commit it)
and every service reads its own section from it. If it doesn't exist yet, create it from the
template and point the four connection strings at your SQL Server:

```powershell
Copy-Item config/appsettings.global.example.json config/appsettings.global.json
# then edit ConnectionStrings:{UsersDb,WalletDb,OrdersDb,AffiliateDb} to a real server
```

Each API runs its own EF Core migrations at startup (`context.Database.MigrateAsync()`), so
there is no separate migrate step — an empty/nonexistent database is created and schema'd the
first time `driver.ps1 start` launches it.

The Simulator's config section (`Services:TallaEgg.TelegramBot.Infrastructure`) needs a
syntactically-present `TelegramBotToken`, but it is never dialed for this path — the Simulator
only constructs a `TelegramBotClient` and never calls `StartReceiving`/`GetMe`. The example
file's placeholder (`REPLACE_WITH_TEST_BOT_TOKEN_FROM_BOTFATHER`) is fine as-is; verified by
running the smoke test with exactly that value in place.

## Build

```powershell
dotnet build TallaEgg.sln --configuration Release
# -> Build succeeded. 0 Warning(s) 0 Error(s)
```

(`driver.ps1 start`, below, does this for you — this is only if you want the build on its own.)

## Run (agent path)

Everything goes through `driver.ps1`:

```powershell
& .claude/skills/run-tallaegg/driver.ps1 start    # build + launch Users/Wallet/Orders, wait for health
& .claude/skills/run-tallaegg/driver.ps1 status    # confirm what's listening
& .claude/skills/run-tallaegg/driver.ps1 smoke     # the actual interaction — see below
& .claude/skills/run-tallaegg/driver.ps1 stop      # stop whatever is listening on the 3 ports
```

`start` builds in Release, launches the three APIs as background processes (stdout/stderr
redirected to `run-logs/<service>.{out,err}.log`), and polls each service's Swagger page
(`/api-docs/index.html`) until all three answer 200 or 60 seconds pass. On timeout it prints
the last 40 lines of each service's stderr log and throws.

`smoke` is the real interaction. With no arguments it runs a small simulation
(`--users 5 --quotes 5 --trades 10 --seed 1`): registers 5 fake Telegram users through the real
`/start` + contact-share flow, promotes one to admin, has admin approve/reject the rest, funds
wallets, publishes quotes through the real `18500000-18550000`-style text command, fills trades
by quote acceptance, and clicks through the help/history/balance menu buttons — end to end,
through the real handler and API code, against the real database. Pass your own args through
after `smoke`:

```powershell
& .claude/skills/run-tallaegg/driver.ps1 smoke --users 20 --quotes 20 --trades 50 --seed 7
```

A clean run ends with a line like:

```
=== Done in 00:00:10.26. Registered 5 (4 approved), trades attempted 10, errors 0 ===
```

`errors 0` is the thing to check. Every simulated user has `TelegramId < 0`, and each run's
first phase (`DataReset`) wipes only rows with `TelegramId < 0` before it starts, so re-running
`smoke` repeatedly against the same database is safe and self-cleaning — it can never touch a
real (positive-id) user's data.

Verified end-to-end on this machine, twice (bash-launched and PowerShell-launched), against a
real local SQL Server Express instance — both runs finished with `errors 0` and left queryable
data behind, confirmed via:

```powershell
Invoke-RestMethod http://localhost:5140/api/orders/MAUA/IRT/best-prices
# -> {"Success":true,"Message":"...","Data":{"Symbol":"MAUA/IRT","BestBidPrice":...,"BestAskPrice":...}}
```

## Direct invocation (no bot layer)

Most PRs in this repo touch one service's API or business logic, not the bot conversation. For
that, skip the Simulator and hit the running API directly — the three Swagger UIs
(`http://localhost:5136/api-docs`, `:60933/api-docs`, `:5140/api-docs`) list every route. Example:

```powershell
Invoke-RestMethod http://localhost:5140/api/symbols/active
# -> {"Success":true,"Message":null,"Data":["MAUA/IRT","SEKE_BAHAR/IRT","BTC/IRT"]}
```

## Run (human path)

`dotnet run --project src/User/Users.Api/Users.Api.csproj` (etc., one per terminal) — the same
thing `driver.ps1 start` does, but blocking and in the foreground. See `AGENT.md` for the full
per-service list, including the bot itself (`TelegramBot/TallaEgg.TelegramBot.Infrastructure`),
which `driver.ps1` deliberately does not launch — it needs a real, valid Telegram bot token and
outbound access to `api.telegram.org`, neither of which this driver path requires.

## Test

```powershell
dotnet test tests/TallaEgg.AllServices.Tests/TallaEgg.AllServices.Tests.csproj --no-build
# -> Passed! Failed: 0, Passed: 501, Skipped: 0, Total: 501
```

Unit tests are a sanity check; `smoke` above is what actually proves the running system works.

---

## Gotchas

- **`pwsh` doesn't exist here — use `powershell.exe`/`& driver.ps1`.** This box only has Windows
  PowerShell 5.1, not PowerShell Core. `pwsh -File driver.ps1 ...` fails with "term not
  recognized"; invoke the script directly (`& .claude/skills/run-tallaegg/driver.ps1 start`).
- **`dotnet test` alone can run against a stale `bin`.** It only builds the test project's own
  dependency graph — per `AGENT.md`, always `dotnet build TallaEgg.sln` (which `driver.ps1
  start` does) before relying on `--no-build` anywhere.
- **The "User not found" 400 warnings during `smoke`'s registration phase are expected, not
  errors.** `UsersApiClient` logs a warning every time it looks up a Telegram id that doesn't
  exist yet — which is every new simulated user, by construction, right before it's created.
  The simulation's own error counter (`errors 0` in the final summary line) is what indicates
  real failure; the interleaved warnings are noise from a normal existence check.
- **`Start-Process dotnet -ArgumentList 'run',...` outlives the parent shell if you don't track
  it.** `driver.ps1 stop` doesn't rely on remembered PIDs for this reason — it resolves the
  *current* `OwningProcess` for each of the three ports via `Get-NetTCPConnection` and kills
  that, so it works even if the shell that ran `start` already exited.
- **Orders.Api calling Wallet/Users before they're listening just logs noisy (harmless)
  connection-refused warnings, not a failure** — `driver.ps1 start` gives Users a 3-second head
  start before launching Orders for exactly this reason, but a slow machine can still race it;
  the readiness poll at the end catches that regardless.

## Troubleshooting

- **`config/appsettings.global.json is missing`** (thrown by `driver.ps1 start`): copy
  `config/appsettings.global.example.json` to that path first — see Setup above. It's
  git-ignored on purpose (`CLAUDE.md`: never commit it, it holds live credentials).
- **`Timed out waiting for Users/Wallet/Orders APIs to report healthy.`**: `driver.ps1` prints
  the last 40 lines of each `run-logs/*.err.log` when this happens. In practice this has meant
  either SQL Server isn't reachable (check the `Get-Service` command under Prerequisites) or a
  connection string in `config/appsettings.global.json` points at the wrong server/database.
- **A previous `smoke` run's data is still there and you want a clean slate without running a
  new simulation**: there's no standalone reset command — `smoke`'s Phase 0 always resets
  first, so running `smoke --users 1 --quotes 1 --trades 1` is the cheapest way to wipe
  previously-simulated (`TelegramId < 0`) rows on demand.
