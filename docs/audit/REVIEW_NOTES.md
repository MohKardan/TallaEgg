# Review Notes on Archived Audits

What was found wrong with an archived audit **after** it was published, by reading it against the code,
the decision records and the tracker.

An archived audit is never edited (see [`README.md`](README.md)), so its errors cannot be corrected in
place without destroying the measurement. They are recorded here instead, one section per audit, newest
first. A reader of an audit file should read its section here before acting on any finding.

**Rules for this file**

- Every entry says **how it was checked**: a file and line, a command, or a decision record. An entry
  that was not checked is not written.
- An entry records a defect in the *audit*, not in the code. Defects in the code are issues.
- Severity disagreements are recorded as such, with the reason; they are not errors unless the
  severity rests on a false claim.
- Add to this file when a review finds something. Do not remove entries.

Each entry is classed as one of:

- **False claim**: something the audit states as fact that the code or the record contradicts.
- **Overstated**: real, but with a severity, scope or exploit path the evidence does not support.
- **Unsafe recommendation**: following it as written would break something, usually a recorded
  decision.
- **Method breach**: a rule of the methodology the audit ran under was not followed.
- **Process**: how the audit reached the repository.

---

## `AUDIT_2026-10c.md`: Gemini 3.1 Pro High / Antigravity, v10, 2026-10-06

Reviewed 2026-10-07 against `main` at `ec78993`. No code had changed since the audited commit.

**What it got right, recorded so the balance is visible:** H-9, the callback-data IDOR, is a real
defect that every earlier audit missed. It was correctly identified as new under v10's tracker rule,
with the searches recorded, and given H-9, the next number above the archive's highest (H-8 in
`AUDIT_2026-09b.md`). Without a reproduction, its severity was held at High rather than Critical. Now
#334.

| Class | Entry | How it was checked |
|---|---|---|
| Overstated | H-9's failure scenario says an attacker can "intercept or enumerate" an order GUID. GUIDs are random and cannot be enumerated, and no channel that leaks another user's GUID was identified. Under the dealer model ([decision 001](../decisions/001-dealer-model-customer-never-enters-a-price.md)) no order rests, so `cancel_order_` has nothing to hit today. The missing ownership check is real; its practical reach is smaller than stated | `BotHandler.cs:667-768`; `CreateLockedAndConfirmedOrderAsync` and the quote path; decision 001 |
| Method breach | Nine of the eleven prior-findings rows are provenance **(c)**, carried over, yet their status column reads "Open" rather than **unverified**. v9 and v10 both say a (c) row is promoted to (b) or printed as unverified | §4 of the audit; v10 "Detect audit mode and compare" |
| False claim | §3 says `git log 70df6a5..HEAD --no-merges` returned 0 commits. There were four, all documentation (`8f007ea`, `0cc737b`, `99dc2c4`, `754d932`). The conclusion that no code changed is right; the measurement as stated is not | `git log --oneline` on `main` |
| Method breach | The overall 5.0 is described as capped "by multiple unresolved High findings". The caps apply per **category**, and H-6 (Financial Integrity) and H-9 (Security) sit in different categories, so neither category holds more than one. The published category scores average **6.6**, and the audit says so itself. The overall is not derivable from its own table, which v9 rule I forbids | §14 of the audit; v10 report template item 14 |
| Overstated | §10 calls H-6 a "TOCTOU problem during order confirmation", which repeats `AUDIT_2026-10b.md`'s C-2 framing. The race is between concurrent API calls (#36); bot confirmation is not a path to it (see C-2 below) | `InMemoryConversationStore`; `QuoteFillService.cs:132` |
| Unsafe recommendation | Roadmap item 4 proposes a background cleanup for M-6. #331 records why the direct fix (cancel through `CancelOrderAsync` on the failure branch) is preferred, and that a sweep is only a fallback | #331 |
| Method breach | M-5 is marked (a) "re-verified via task execution" with no command, output or note behind it | §4; v8 traceability rule A |
| Method breach | Session timings total about 23 minutes for a methodology that budgets a working day, and §3 lists no deeply reviewed paths. The timings are disclosed honestly; the depth they imply should weigh on how the audit is read | §3 |
| Process | Committed and pushed directly to `main` (`a58d6e1`), not through a branch and PR. Its attribution was then edited in place on `main` (`43cf102`) | `git log --first-parent main` |

## `AUDIT_2026-10b.md`: Gemini 3.1 Pro High / Antigravity, v9, 2026-10-06

Reviewed 2026-10-07. **Of its four findings, one is real.** Read this audit as superseded by
`AUDIT_2026-10c.md`, run by the same model on the same day, which dropped three of these four.

| Class | Entry | How it was checked |
|---|---|---|
| Overstated | **C-1** (callback IDOR) is real, and is #334. "Iterate through GUIDs" is not possible, and Critical was assigned without a reproduction, which v7 onwards forbids. See the H-9 entry above | `BotHandler.cs:667-768` |
| False claim | **C-2** (TOCTOU: open two confirmation prompts, approve both, exceed the ceiling). The bot keeps **one** order conversation per user (`InMemoryConversationStore`, a `ConcurrentDictionary<long, OrderState>`), so a second order replaces the first, and confirmation clears it. The server re-checks funds at fill time (`QuoteFillService.cs:132`). The bot processes one update at a time across all users (`TelegramBotHostedService.cs:203`, which awaits the handler). The real race is #36, between concurrent direct API calls | Code read as cited |
| False claim | C-2 says locking twice pushes the balance negative and "creates money out of thin air". Locking moves funds from available to locked; it creates nothing | `Wallet.LockBalance` |
| Unsafe recommendation | C-2's fix moves the `Balance + Credit` check into `WalletEntity` / `LockBalanceAsync`. That is what [decision 005](../decisions/005-balance-rules-live-where-both-rows-are-visible.md) forbids, and what methodology Section 0 lists. Done literally, it rejects legitimate cross-asset trades ([decision 004](../decisions/004-credit-is-cross-asset.md)) | Decisions 004 and 005; v9 Section 0 |
| False claim | **H-1** (price check commented out in `ExecuteAtomicMatchAsync`). `AUDIT_2026-10.md` §15 had already examined and dropped it: `FindBestMatch` and `GetMatchingOrdersAsync` both enforce price compatibility. "Uncomment it" is a recommendation to change code without stating what it does, which v9 rule K forbids | `AUDIT_2026-10.md` §15 |
| False claim | **H-2** says the services lack API-level authorization. In Production every API requires `X-API-Key` behind a fallback authorization policy. `AUDIT_2026-10.md` §3 executed this (401 with no key, 401 with a wrong key, 200 with the right one). The real gap is #332, `dotnet run` forcing Development | `Program.cs` `IsProduction()` gates; `AUDIT_2026-10.md` §3 |
| Method breach | A re-audit with no prior-findings table ("not the focus"), no session timings, no AI Provider line, no Medium/Low section ("abbreviated focused session"), and no list of deeply reviewed paths. All are required by v9 | The audit's §1, §3, §4, §7 |
| Method breach | Finding IDs C-1, C-2, H-1 and H-2 restart the sequence. All four already mean other findings in `AUDIT_2026-07.md` (C-1 is the hardcoded API key) | `AUDIT_2026-09b.md` §4 lists C-1/C-2 with their July meanings |
| Process | Committed and pushed directly to `main` (`a58d6e1`), together with 10c | `git log --first-parent main` |

## `AUDIT_2026-10.md`: DeepSeek V4.1 Flash / Cline, v9, 2026-10-01

Reviewed 2026-10-06 and 2026-10-07.

| Class | Entry | How it was checked |
|---|---|---|
| False claim | §8 says "No command lets a customer name another account. That is the boundary that actually protects customer money, and it holds." The `orders_`, `trades_` and `cancel_order_` callbacks take a user or order id from client-supplied callback data and check no ownership (#334). A universal stated without the enumeration behind it, which v8 rule C forbids | `BotHandler.cs:667-768` |
| Unsafe recommendation | M-6 recommends unlocking whenever confirmation returns `false`. `false` also means "the order already left Pending" (confirmed or cancelled concurrently), and a blind unlock then releases collateral still needed, or releases it twice. #331 records the safer route through `CancelOrderAsync` | `OrderService.cs:342, 381-415` |
| Overstated | M-5 rated Medium. The sanctioned server path sets Production, `README.md` already warns about `dotnet run`, and every service binds loopback. Assessed Low until something binds beyond loopback (#332). A severity disagreement, not an error | `install-services.ps1`; `README.md` Configuration |

## `AUDIT_2026-09b.md`: Gemini 3.8 Flash / Cline, v9, 2026-09-26

Reviewed 2026-10-06. It reported no finding that was not already an open issue.

| Class | Entry | How it was checked |
|---|---|---|
| False claim | The C-2 row says an "inactive dummy literal" remains in the bot's `Program.cs:156`. No token literal is there; that line registers a hosted service. The last literal was removed together with the Telegram error logger | `TelegramBot/TallaEgg.TelegramBot.Infrastructure/Program.cs:150-160` |
| False claim | "29 non-merge commits" since the baseline; there were 30. Already recorded by `AUDIT_2026-10.md` §4 | `git rev-list --no-merges --count 5e796dc..70df6a5` |
| Method breach | Gave new IDs (H-8, M-5, M-6, L-2) to four findings already tracked as #36, #279, #280 and #282, colliding with `AUDIT_2026-10.md`'s IDs. This is what v10's tracker rule was written for | `METHODOLOGY_v10.md`, "Changes from v9" |
| Unsafe recommendation | H-8 recommends one transaction that checks "the primary balance and corresponding `CREDIT_<ASSET>` rows". Read literally, that is a per-asset check, which [decision 004](../decisions/004-credit-is-cross-asset.md) forbids. Recorded on #36 | Decision 004; comment on #36 |
| Overstated | Says the credit race is contained because bot updates run "sequentially per user session". They run one at a time across **all** users, which is a stronger guarantee | `TelegramBotHostedService.cs:203` |
| Method breach | "Balances reconcile exactly to transaction history logs" is stated with no query or count behind it. `AUDIT_2026-10.md` later measured it properly | v8 traceability rule A |
| Method breach | Overall called a "weighted" average, with no weights given. Already recorded by `AUDIT_2026-10.md` §4 | §9 of the audit |
| Process | First archived with the model given as "Claude 3.7 Sonnet"; corrected to Gemini 3.8 Flash in #329 | `0cc737b` |

---

## Patterns across these four

1. **Findings drawn from code shape rather than execution remain the main error source.** Every false
   claim about the code above came from reading, not running. No reproduction stood behind any finding that turned out
   wrong.
2. **Decision records were not consulted before recommending.** Two of the three unsafe
   recommendations contradict a decision indexed in the repository's agent guide (`AGENT.md` at the
   time), which the agents may not have read.
3. **Audits by an agent that never saw the repository's rules.** Both process entries come from a tool
   (Antigravity) that reads `AGENTS.md` and `GEMINI.md`. The rule "work on a branch, open a PR" lived
   in `CLAUDE.md`, which only Claude Code loads, and the GitHub ruleset let the account's Admin role
   bypass the PR requirement. The fix belongs to the repository, not to the agent.
