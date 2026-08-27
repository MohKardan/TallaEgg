# Re-Audit — August 2026

**Audit Date**: August 26, 2026
**Overall Score**: 6.6/10 (was 4.6/10 on July 8)
**Production Readiness**: ~55% (was 30%)
**Scope**: every service and project in the repository **except Affiliate**, which was
excluded from this pass at the product owner's request.

> This re-audit re-runs the July 8 pass in
> [`AUDIT_FINDINGS.md`](AUDIT_FINDINGS.md), which the OKR named as KR3's measurement
> criterion but which was never executed before the cycle ended.
>
> **Every finding below was verified against the code as it stands, not against issue
> state.** That distinction produced the single most consequential result here: C-5 is
> fixed in code while both the tracker and the OKR still call it open.
>
> Like the original, this file is a stable reference of what one audit found on one day.
> Do not edit it to mark work done — remediation status belongs in issues and PRs.

**Verification performed**: `dotnet build TallaEgg.sln` succeeds with 0 errors;
`dotnet test TallaEgg.sln` reports 483 passed / 0 failed.

> **Corrected**: this line first read "succeeds with no warnings". That was a measurement
> mistake — the build it was based on was incremental and up-to-date, so nothing
> recompiled and nothing was emitted. A clean build reports **169 warnings**, mostly
> `CS1587` (misplaced XML comments, concentrated in `Orders.Api/Program.cs`), `CS8981`
> and nullability `CS8620`. None are errors and none change behaviour, but they belong
> in the code-hygiene row below rather than being reported as absent.

---

## Original Critical Findings — Current State

| ID | Finding | State |
|---|---|---|
| C-1 | Hardcoded API key | ✅ Resolved |
| C-2 | Bot token in committed config | ✅ Resolved |
| C-3 | No transaction atomicity | ✅ Resolved on the live path |
| C-4 | No optimistic concurrency control | ⚠️ Partial — Orders only |
| C-5 | Lock-after-match race | ✅ **Resolved** (tracker is stale) |
| C-6 | Duplicate MatchingEngine registration | ✅ Resolved |
| C-7 | TLS validation disabled | ✅ Resolved |
| C-8 | Live stub endpoint | ⚠️ Contained, not fixed |
| C-9 | CORS fully open | ✅ Resolved |

### C-1 / C-2 — Resolved

`APIKeyConstant` reads `TALLAEGG_API_KEY` from the environment and
`RequireTallaEggApiKey()` throws in Production rather than falling back silently.
`config/appsettings.global.json` is no longer tracked; only
`appsettings.global.example.json` is. Credentials were rotated by the owner.

The pre-rotation values remain in git history by deliberate decision — rewriting
history was ruled out. Since those credentials are dead, the residual exposure is
nil. No action outstanding.

### C-3 — Resolved on the live path

Settlement runs inside a real transaction (`WalletRepository.cs:315`) driven by the
transactional outbox, with a unique index enforcing idempotency
(`OutboxMessageConfiguration.cs:32`). The specific method the audit named,
`ApplyTradeAsync`, no longer carries the six-`SaveChangesAsync` pattern.

**Residual, tracked below as N-3**: `Wallet.Application/WalletService.cs` still holds
older money-moving methods that were never brought up to this standard.

### C-4 — Partial: Orders is protected, Wallet is not

`Order.RemainingAmount` is configured `.IsConcurrencyToken()`
(`OrderConfigurations.cs:41`), and `OrderMatchingRepository` handles the resulting
`DbUpdateConcurrencyException`. That half is genuinely fixed.

The Wallet side has **no concurrency token of any kind** — no `RowVersion`, no
`IsConcurrencyToken`, on `WalletEntity` or its configuration. Yet
`WalletRepository.cs:107` catches `DbUpdateConcurrencyException` and logs
"Concurrency conflict". Without a concurrency token EF issues an `UPDATE` keyed on
the primary key alone, which matches one row and succeeds; the lost update happens
silently and that catch block never runs for it.

The cost is not the missing check — the OKR deliberately deferred C-4 as
over-engineering for the current scale, which was a defensible call. The cost is that
the code now *reads* as though wallets are concurrency-protected when they are not.

### C-5 — Resolved, and the tracker is wrong about it

`OrderService.CreateLockedAndConfirmedOrderAsync` now runs: create as `Pending`
(invisible to the matching engine) → lock collateral → confirm → match. If the lock
fails, the order is marked `Failed` and never becomes matchable, so no trade can exist
without reserved collateral. `PendingOrderNotMatchableTests` covers it, and the
quote-fill path validates balance and credit before creating either side.

This landed on **July 28** in `0b3df5b` — one day after the OKR status update recorded
it as still open, which is why the document never caught up. The correctness of the
system no longer depends on outbox timing.

Issue #36 is still open and should stay open, but it is no longer about C-5. Its
remaining scope is the credit-model redesign (replacing the separate `CREDIT_` asset
with a signed ledger and an enforced credit limit) — a different, larger piece of work.

### C-8 — Contained by a flag, not fixed at the source

`POST /api/wallet/transaction/trade` returns 501 whenever
`FeatureFlags:QuarantineStubEndpoints` is on, and it defaults to `true`. That is
effective containment.

Behind the flag, however, `WalletService.MakeTradeAsync` is **still the original stub**:
it validates its arguments and then returns a bare `new WalletBallanceDTO()` with the
entire implementation commented out. The call site labels it
`// Production implementation (currently unreachable due to quarantine)`, which is
false and is the actual hazard: anyone who turns the flag off — reasonably believing
a real implementation waits behind it — restores exactly the silent fake-success
behaviour C-8 described.

Either implement the method or delete it and the endpoint. A comment that misdescribes
a stub is worse than the stub.

---

## Original High Findings — Current State

| ID | Finding | State |
|---|---|---|
| H-1 | DIP violation (concrete `WalletApiClient`) | ✅ Resolved — `IWalletApiClient` injected |
| H-2 | No global error middleware | ⚠️ Partial |
| H-3 | No `AsNoTracking` in read queries | ❌ Open — 1 use in the whole codebase |
| H-4 | No idempotency protection | ✅ Resolved for settlement |

**H-2** is half-done in a way worth naming. `GlobalExceptionHandler` and
`AddProblemDetails` are wired into every service, which was the hard part. But the
scattered handlers the audit was counting went from **98 at audit time to 169 today**
(non-test, excluding Affiliate). The middleware was added *alongside* the old pattern
rather than replacing it. Nothing is broken by this; it just means H-2's stated impact —
inconsistent responses, leaked internals — is only partly retired.

---

## New Findings

These were not in the July 8 audit.

### N-1 (HIGH): The balance guard inside the wallet primitive is commented out

`Wallet.Core/Wallet.cs:68` and `:78` — both `LockBalance` and `UnLockBalance` have
their guards commented out:

```csharp
public void LockBalance(decimal amount)
{
    //if (Balance < amount) throw new ArgumentNullException("موجودی کافی نیست", nameof(amount));
    // چون اعتبار هم داریم میتونیم حسابو منفی کنیم
    LockedBalance += amount;
    Balance -= amount;
}
```

The reason is legitimate: the market maker must be able to go negative, and credit
means ordinary customers may too. But the consequence is that **the domain primitive
responsible for reserving funds enforces nothing at all.** Every guarantee lives in
callers, in `WalletApiClient.ValidateCreditAndBalanceAsync`.

This is not hypothetical. It is precisely how #77 happened: the quote-fill path (#48)
did not route through `SubmitOrderAsync`, so it silently inherited no balance check,
and any new customer could trade with an empty wallet. The fix added the check to that
one new caller. The next new call path will inherit the same hole.

The invariant that actually holds is `Balance >= -CreditLimit`, and it should be
enforced where the balance changes, with the market maker as an explicit exception,
rather than re-implemented at each call site. This is the same conclusion #36 reaches
from the other direction, and is an argument for doing #36 sooner.

### N-2 (LOW): Two abandoned test directories are tracked but never compiled

> **Corrected 27 Shahrivar.** This finding was first filed at HIGH severity on the claim
> that the bot had no tests running against it. **That claim was wrong.** The bot is
> well covered — 17 test files in `Wallet.Tests` exercise the handlers, conversation
> flow, registration, admin commands and message builders, and all of them run. There
> is also a simulator project (#101) in the solution. The original finding was reached
> by looking at directory names rather than at content, and the corrected severity and
> remedy are below. See #117.

- `TelegramBot/TallaEgg.TelegramBot.Tests/` — a project with 9 files and 22 tests, not
  referenced in `TallaEgg.sln`. **Last modified August–September 2025**, roughly a year
  ago.
- `src/Order/Orders.Tests/` — two tracked `.cs` files with no `.csproj` in the
  directory. **Last modified August 2025.**

Both predate the current test suite and have been superseded by it. Nothing they cover
is uncovered today, so no coverage is being lost — these are leftovers, not orphaned
work. The correct remedy is to **delete them**, not to wire them into the build.

The durable part of this finding is narrower: `build-and-test.yml:59` ran `dotnet test`
against `Wallet.Tests.csproj` by name rather than against the solution. That is what
allowed a stale project to sit outside the build unnoticed, and it is what would let a
genuinely new test project be silently excluded in future.

Related: `Wallet.Tests` was the solution's only test project and its name no longer
described it. It referenced seven projects across Wallet, Orders and the bot, and its
56 files broke down roughly as 23 Orders, 14 shared, 11 bot, 8 Wallet. The misleading
name is what produced the incorrect version of this finding in the first place.

**Resolved.** Both abandoned directories were deleted; the live suite moved to
`tests/TallaEgg.AllServices.Tests`, a name that cannot be read as belonging to one
service; and CI now runs `dotnet test TallaEgg.sln`, so a new test project is picked up
by adding it to the solution and nothing else has to be remembered. 483/483 still pass
from the new location.

### N-3 (MEDIUM): Non-atomic legacy money paths remain in `WalletService`

`TransferAsync` (`WalletService.cs:303`) debits the source, then credits the
destination, with no transaction wrapping the pair and the compensating rollback
commented out (`:318-323`). Both audit-trail writes are commented out too (`:338`,
`:352`), so a transfer that "succeeds" leaves no `WalletTransaction` record at all.

`ChargeWalletAsync`, `DebitAsync`, `OldWithdrawAsync` and `MakeTradeAsync` are in
similar shape. **All are currently unreachable** — every endpoint that called them is
commented out in `Wallet.Api/Program.cs` — so this is not a live defect today. It is
M-1 (dead code) with financial consequences attached if anything ever re-exposes them.

Deleting them is the cheaper fix. They are not a partial implementation of anything
that is still wanted.

### N-4 (MEDIUM): The Production-only security path has never executed

API-key authentication and the CORS whitelist are both registered inside
`if (builder.Environment.IsProduction())` in all four services. Production has never
successfully started — #105 is open, and `RequireTallaEggApiKey()` throws at startup
without the key. So the authentication path, the CORS whitelist and the startup guard
are all code that has never once run.

Nothing here is wrong. The point is that the first production boot will be the first
execution of the security-critical branch, and it should be treated as such rather than
as a deployment formality.

### N-5 (LOW): Fallback logging writes an ungitignored file into the working directory

When `TelegramLoggerService.ErrorAsync` fails to deliver, it falls back to
`File.AppendAllTextAsync("SendExceptions.txt", ...)` (`TelegramLoggerService.cs:189`) —
a relative path, so the file lands in whatever directory the process was started from,
which during development is the repository root. It never rotates, and it is not in
`.gitignore`. One such file is sitting untracked in the working tree right now.

A broad `git add .` would commit serialized runtime exceptions into a public repo.
Adding the pattern to `.gitignore` costs nothing.

---

## Strengths

The July audit listed clean layering, database-per-service and a rich domain model.
Those hold. Added since:

1. **A real test suite** — 483 passing tests, up from effectively none, including
   regression tests that pin the specific defects behind C-5, C-6, #72, #77 and the
   dealer-mode wiring. Coverage spans all three services in scope, the bot included:
   17 files exercise the bot's handlers, conversation flow, registration and admin
   commands, alongside a simulator that seeds 100 users and 1000+ trades (#101).
2. **CI on every pull request** (#71) — build plus tests, so regressions surface before
   merge rather than in review.
3. **Transactional outbox with a uniqueness constraint** — settlement is atomic and
   idempotent by construction, not by convention.
4. **Deployment groundwork** — SQL Server Express, Windows service supervision and
   Production URLs all landed (#68, #69, #70).
5. **Structured error handling and on-disk logging** across all services (#88, #99).

---

## Score

| Area | July 8 | Aug 26 | Note |
|---|---|---|---|
| Secrets & transport security | 1/10 | 9/10 | C-1, C-2, C-7, C-9 all closed with tests |
| Financial correctness (core path) | 3/10 | 8/10 | Atomic, idempotent, correctly ordered |
| Financial correctness (edges) | 3/10 | 5/10 | N-1, N-3, C-4 wallet half |
| Test coverage & CI | 1/10 | 8/10 | 483 tests across Wallet, Orders and the bot, on every PR |
| Operational readiness | 2/10 | 4/10 | Groundwork done, never booted, #105 open |
| Code hygiene | 3/10 | 4/10 | Dead code and 169 scattered handlers remain |

**Overall: 6.6/10.** This clears the ≥ 6.0 target the OKR set for KR3.

> Revised from 6.4 when N-2 was corrected: testing was scored 7/10 on the incorrect
> belief that the bot was untested. The overall figure is a judgement weighted toward
> the critical-path rows, not an average of the column.

The improvement is real and concentrated where it mattered most: nothing on the
critical list is still an open hole, the money path is correct by construction rather
than by timing, and there is now a test suite that would catch a regression.

What holds the score down is not any single defect but a pattern: **the fixes were
applied at the call site rather than at the invariant.** N-1 is the clearest case — the
balance guard was restored for the one path that broke instead of for the primitive
that all paths share. C-4's wallet half, C-8's stub, and N-3's legacy methods are the
same shape. Each is contained today and each will be re-opened by the next new caller.

The highest-value next step is not another fix from this list; it is moving the balance
invariant into the wallet, which is what #36 already proposes.
