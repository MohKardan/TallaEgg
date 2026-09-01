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
# -NoClobber matters: the file is untracked (issue #33), so overwriting a real one destroys
# live connection strings, bot token and OwnerTelegramIds with no git copy to recover from.
Copy-Item config/appsettings.global.example.json config/appsettings.global.json -NoClobber
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

`start` refuses to run if any of the three ports is already listening (tell it `stop` first —
otherwise the new processes die on "address already in use" while the health poll answers 200
from the old ones), builds in Release and **throws if the build fails**, launches the three
APIs as background processes (stdout/stderr redirected to `run-logs/<service>.{out,err}.log`),
and polls each service's Swagger page (`/api-docs/index.html`) until all three answer 200 or 60
seconds pass. On timeout it prints the last 40 lines of *both* logs per service and throws.

`smoke` is the real interaction. With no arguments it runs a small simulation
(`--users 5 --quotes 5 --trades 10 --seed 1`): registers 5 fake Telegram users through the real
`/start` + contact-share flow, promotes one to admin, has admin approve/reject the rest, funds
wallets, publishes quotes through the real `18500000-18550000`-style text command, fills trades
by quote acceptance, and clicks through the help/history/balance menu buttons — end to end,
through the real handler and API code, against the real database.

**It trades every symbol in the pair catalogue, not just gold** (issue #147). Each symbol gets its
own quotes, its own wallet funding (base asset *and* `CREDIT_` ledger), and quantities at its own
precision — eight decimal places on `BTC/IRT`, two on `MAUA/IRT`. That difference is the point: the
simulator ran a thousand clean MAUA trades on top of #146, where `Orders.Amount` was
`decimal(18, 2)`, because MAUA's precision is exactly the two places the column held and every gold
quantity round-tripped unchanged. The symbol cycles per trade, so any run with at least as many
trades as there are symbols touches all of them, and the run prints a per-symbol breakdown:

```
Settled trades by symbol:
  BTC/IRT: 4 settled
  MAUA/IRT: 3 settled
  SEKE_BAHAR/IRT: 3 settled
```

A symbol showing `0 settled` means the run did not exercise it. Override any subset of the
knobs after `smoke` — the ones you don't name keep the small defaults above (the driver merges
them; passing them straight through would drop to the Simulator's own compiled defaults of 100
users / 120 quotes / **1000 trades**, so `smoke --seed 7` alone would be a hundred-fold bigger
run than it looks):

```powershell
& .claude/skills/run-tallaegg/driver.ps1 smoke --users 20 --quotes 20 --trades 50 --seed 7
& .claude/skills/run-tallaegg/driver.ps1 smoke --trades 50    # 5 users, 5 quotes, 50 trades, seed 1
```

`--users` must be at least 2: user #0 is promoted to admin and is the counterparty to every
fill, so it can never trade with itself, and a 1-user run ends `errors 1` with "No approved
users to trade with."

A clean run ends with a line like:

```
=== Done in 00:00:10.26. Registered 5 (4 approved), trades attempted 10, errors 0 ===
```

`errors 0` is the thing to check, and the driver checks it for you — the Simulator itself always
exits 0 no matter how much failed, so `driver.ps1 smoke` parses that summary line and throws if
the count isn't zero or the line never appeared.

**That line is not the end of the run.** It is printed when the trading phase ends, but
settlement is queued through the Orders outbox and drains tens of seconds later, so `errors 0`
only means the conversations and the matching worked. `driver.ps1 smoke` therefore drains the
queue before the run, takes its counts, and after the run polls until no outbox message is
`Pending` again before checking two things (issue #175):

- **no settlement failed** — the number of permanently `Failed` messages must not have gone up
- **settlements actually happened** — `Completed` must have gone up, or a regression that stops
  queueing settlements entirely would drain instantly and look perfect

So a green run now means the trades settled, not just that they were recorded. Expect an extra
5–15s per run for the drain:

```
Waiting for settlement: 43 outbox message(s) still pending...
Simulation completed with errors 0; 60 settlement(s) completed, no new failures.
```

Failures are compared as a **delta**, not against zero: these databases already carry failures
from earlier work that belong to nobody's current change. The pre-run drain is part of that —
a doomed message left over from an earlier run does not fail when it is abandoned but when it
exhausts its retries, minutes later, and without the drain that lands inside this run's window
and is charged to it. If either check fires, the message names the counts and points at
`/api/outbox/unsettled`, where a stuck settlement can be re-driven (once the cause is fixed) or
abandoned.

### What a run changes on the database

- **Rows with `TelegramId < 0`.** Every simulated user is in that range, and each run's first
  phase (`DataReset`) wipes only that range before starting, so repeated runs are self-cleaning
  and a real (positive-id) user's data is never touched.
- **`OutboxMessages` rows, which are never deleted.** Each trade queues one settlement message
  and it stays in the Orders database as `Completed` afterwards. `DataReset` does not remove
  them; it *waits* for them instead, blocking until nothing is `Pending` before it deletes
  anything (issues #184, #175). Deleting a wallet out from under a queued settlement is what
  used to strand it permanently, so on a queue that has not drained the reset now says so and
  waits rather than starting:

  ```
  Reset: 1 settlement(s) from the previous run are still queued; waiting for the outbox to drain before deleting anything.
  Reset: outbox drained.
  ```

  It gives up after 10 minutes rather than waiting forever, and says where to look when it does.
- **The auto-quote flag for every symbol the run trades.** The Simulator turns each one off in
  Phase 2 — a background publisher replacing the run's quotes breaks quote-fill trades — and never
  turns any back on. That is per-symbol Orders-DB state, not `TelegramId`-scoped, so `DataReset`
  does not restore it. **`driver.ps1 smoke` snapshots every symbol's flag from the
  `AutoQuoteSettings` table before the run and puts them back afterwards**, including when the run
  fails part-way. If a restore itself fails the driver says so; turn it back on from the bot with
  the admin command `اتومات روشن`, or:

  ```powershell
  Invoke-RestMethod -Method Post -ContentType 'application/json' `
    -Uri http://localhost:5140/api/autoquote-settings/MAUA/IRT/enabled `
    -Body '{"IsEnabled":true,"UpdatedByUserId":"00000000-0000-0000-0000-000000000000"}'
  ```

  Running the Simulator directly (not through `driver.ps1`) leaves auto-quote off.

Verified end-to-end on this machine against a real local SQL Server Express instance — the run
finished with `errors 0`, left queryable data behind, and left the auto-quote flag as it found
it. Confirmed via:

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
  it.** `driver.ps1 stop` therefore kills on two grounds: the *current* `OwningProcess` of each
  of the three ports via `Get-NetTCPConnection` (so it works even if the shell that ran `start`
  already exited), plus anything still alive from `run-logs/launcher.pids`. The second is what
  catches a service that died before Kestrel bound — unreachable SQL Server, say — which owns no
  port but is still running and still holding a lock on `bin/`. It reports only kills that
  actually succeeded, and warns about the ones it couldn't make.
- **All three APIs log to stdout, not stderr.** They configure Serilog `.WriteTo.Console()` with
  no `standardErrorFromLevel`, so `run-logs/<service>.err.log` is usually empty and the real
  error is in `<service>.out.log`. `driver.ps1 start` tails both on timeout for this reason.
- **The `/api-docs` health probe only exists in Development.** All three APIs map Swagger inside
  `if (app.Environment.IsDevelopment())`. `dotnet run` picks up `ASPNETCORE_ENVIRONMENT=Development`
  from each project's `launchSettings.json`, so this works by default — but on a box where the
  environment variable is set to Production (as `scripts/windows-services/install-services.ps1`
  does for installed services), the services come up fine and `start` still times out. The
  timeout message says so.
- **Orders.Api calling Wallet/Users before they're listening just logs noisy (harmless)
  connection-refused warnings, not a failure** — `driver.ps1 start` gives Users a 3-second head
  start before launching Orders for exactly this reason, but a slow machine can still race it;
  the readiness poll at the end catches that regardless.

## Troubleshooting

- **`config/appsettings.global.json is missing`** (thrown by `driver.ps1 start`): copy
  `config/appsettings.global.example.json` to that path first — see Setup above. It's
  git-ignored on purpose (`CLAUDE.md`: never commit it, it holds live credentials).
- **`Timed out waiting for Users/Wallet/Orders APIs to report healthy.`**: `driver.ps1` prints
  the last 40 lines of every `run-logs/*.log` when this happens — check the `.out.log` files
  first, that's where Serilog puts errors. In practice this has meant either SQL Server isn't
  reachable (check the `Get-Service` command under Prerequisites), a connection string in
  `config/appsettings.global.json` points at the wrong server/database, or
  `ASPNETCORE_ENVIRONMENT` isn't Development (see Gotchas). The launched processes are left
  running so their logs stay readable; `driver.ps1 stop` cleans them up.
- **`Already listening: ...`** (thrown by `driver.ps1 start`): a previous stack is still up.
  `driver.ps1 status` shows what owns the ports, `driver.ps1 stop` clears them. `start` refuses
  rather than launching processes that would die on "address already in use" while the health
  poll answered from the old ones.
- **`Build failed (exit N)`**: fix the build. `start` stops here on purpose — it launches with
  `--no-build`, so continuing would serve the *previous* build and quietly invalidate whatever
  you were trying to verify.
- **A previous `smoke` run's data is still there and you want a clean slate without running a
  new simulation**: there's no standalone reset command — `smoke`'s Phase 0 always resets
  first, so `smoke --users 5 --quotes 1 --trades 1` is the cheapest way to wipe
  previously-simulated (`TelegramId < 0`) rows on demand. Keep `--users` at 5 rather than the
  bare minimum of 2: each non-admin registration is only approved with probability 0.9, so a
  2-user run can land on the one rejection and end `errors 1` with nobody left to trade.
