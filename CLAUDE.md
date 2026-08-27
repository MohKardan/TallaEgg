# TallaEgg

Persian-language gold trading platform: .NET 9 microservices behind a Telegram bot.

## Read this before writing code

Process and standards live in [`docs/process/INDEX.md`](docs/process/INDEX.md). Read
[`STANDARDS.md`](docs/process/STANDARDS.md) before writing code and
[`PR_TEMPLATE.md`](docs/process/PR_TEMPLATE.md) before opening a PR. They apply to human
developers and AI agents alike.

This file exists because it is loaded automatically at the start of every session, and
[`AGENT.md`](AGENT.md) is not. AGENT.md is the fuller guide — build commands, architecture,
conventions — and is worth reading; this file only guarantees that the rules are never missed
because nobody happened to open the right file.

The rules that get skipped most often:

- **Work on a branch.** `feat/`, `fix/`, or `hotfix/` followed by a description. Never commit
  directly to `main`.
- **Open a PR**, using `PR_TEMPLATE.md`. Peer review is thin on a two-person team, but the PR is
  still where the reasoning is recorded.
- **Comments, commit messages, documentation, and GitHub text in English.** Persian is for what
  users see in the bot, and for talking to the team.
- **Keep the change small.** Do what was asked and stop. No refactoring that nobody requested —
  see [`docs/process/STANDARDS.md`](docs/process/STANDARDS.md) on scope discipline.
- **Never commit `config/appsettings.global.json`.** It holds live credentials and this repo is
  public. It is untracked as of #33 — only `config/appsettings.global.example.json` belongs in
  git. Do not add it back.

## Things that are easy to get wrong

- **Build before you run.** `dotnet test` only builds the test project's dependency graph, so an
  API's `bin` can be stale. Always `dotnet build TallaEgg.sln` before `dotnet run --no-build`.
- **All configuration is in `config/appsettings.global.json`**, one shared file for every service,
  under `Services:{ApplicationName}`. Per-project `appsettings.json` files are not the source of
  truth.
- **The runnable bot project is `TallaEgg.TelegramBot.Infrastructure`.** Several projects under
  `TelegramBot/` look like candidates; that is the one with the host. `TallaEgg.TelegramBot`, the
  empty shell that used to sit beside it and start nothing, was deleted.
- **Dates shown to users are Jalali at a fixed +03:30 offset**, formatted through
  `PersianFormat`. Storage stays Gregorian UTC; the two are unrelated.
- **Credit is per-asset in storage, but cross-asset in effect.** Every tradable asset gets a
  `CREDIT_<ASSET>` ledger — see `CurrenciesConstant.CreditAssetFor`. `ValidateCreditAndBalanceAsync`
  then lets credit in one currency back a position in the other, so a customer holding only
  `CREDIT_MAUA` can legitimately drive their IRT balance negative. A balance check must constrain
  `balance + credit` across both sides, never `balance` alone, and never per-asset in isolation.
- **`Wallet.LockBalance` enforces no balance rule, and must not.** The ceiling lives in a separate
  wallet row, so a single-asset entity cannot see it; the check belongs in the caller. The
  commented-out guard there is wrong code, correctly disabled.
- **Two things that look like bugs and are not:** the market maker may go arbitrarily negative
  with no ceiling (that balance is the shop's book — alerting is #124), and commission is
  deliberately `0.00` on every trade because the revenue model is the spread. Fee code is dormant,
  not dead. Fuller list in [`AGENT.md`](AGENT.md) under "Business rules that look like bugs".
- **The bot tokens still visible in tracked files are dead. Do not report them as a leak.** Two
  literals remain in git — `TelegramBot/TestRunner/testsettings.json`, and the
  `TelegramLoggerService` registration in `TallaEgg.TelegramBot.Infrastructure/Program.cs` where
  the config lookup is commented out. Both values were rotated under #33 and confirmed dead by the
  product owner (2026-08-27); the same is true of every other credential that ever reached this
  repo. History is deliberately **not** rewritten — #105 records that decision and the reasoning,
  and the constraint still holds. Grep will keep finding these strings, and every fresh session
  rediscovers them and raises an incident; it is a false alarm each time. Deleting the literals
  while working in those files is welcome, but it is cleanup, not a security response.
