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
- **The runnable bot project is `TallaEgg.TelegramBot.Infrastructure`**, not
  `TallaEgg.TelegramBot` — that one is not in the solution and will not start.
- **Dates shown to users are Jalali at a fixed +03:30 offset**, formatted through
  `PersianFormat`. Storage stays Gregorian UTC; the two are unrelated.
- **Credit is per-asset.** Every tradable asset gets a `CREDIT_<ASSET>` ledger — see
  `CurrenciesConstant.CreditAssetFor`. It is a ceiling the spot balance may go negative against,
  so a balance check must constrain `balance + credit`, never `balance` alone. Note that
  `Wallet.LockBalance` itself enforces nothing; the guard lives in the caller
  (`ValidateCreditAndBalanceAsync`), so a new call path inherits no protection by default.
