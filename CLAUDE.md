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

- **Read [`docs/decisions/`](docs/decisions/README.md) and
  [`docs/product/DIRECTION.md`](docs/product/DIRECTION.md) before proposing to delete anything, or
  to finish something that looks unfinished.** Both exist because code here can look dead when a
  capability is paused rather than gone, and can look broken when it is doing what was asked. In
  #293 the change was right and the justification written beside it was not — "removing it beats
  implementing a list the dealer model keeps empty" is only true while the dealer model is the
  whole product. DIRECTION.md is what would have caught that sentence.
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
  not dead. Fuller list in [`AGENT.md`](AGENT.md) under "Business rules that look like bugs",
  and the reasoning behind each in [`docs/decisions/`](docs/decisions/README.md).
- **Bot tokens in git history are not dead because a document says so.** No token is left in the
  working tree — the last literal, the Telegram error-logger registration in the bot's
  `Program.cs`, was removed together with the logger. Older tokens remain in git history,
  including the `testsettings.json` of the deleted `TelegramBot/TestRunner`. A 2026-09 check found
  that the earlier statement here, that every one had been rotated, did not hold. So measure before
  concluding anything: a read-only `getMe` (never printing the token) answers 401 for a revoked
  token. Keep token values and bot details out of issues, PRs and commits. History is deliberately
  **not** rewritten — #105 records that decision and it still holds; revoking in BotFather is the
  remedy.
- **Diagnostics go to `ILogger` (Serilog) and nowhere else.** Every host writes to the console and a
  rolling file under `logs/`. Do not add a second reporting path that posts to Telegram or any
  outside endpoint, and do not log message content: a customer's message can be a contact card
  carrying their phone number. `NoOutOfProcessLoggingTests` enforces the first half.
