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

### N-1 (INFO — retracted as a defect): the balance check is not local to the wallet

> **Corrected 6 Shahrivar.** This was filed at HIGH severity claiming the commented-out
> guard in `LockBalance` was missing protection. **That framing was wrong**, and the
> remedy it proposed — restoring the guard at the entity — would have broken credit
> trading, which is a deliberate and required product behaviour. Corrected below.
> See #116.

`Wallet.Core/Wallet.cs:68` carries a commented-out guard:

```csharp
public void LockBalance(decimal amount)
{
    //if (Balance < amount) throw new ArgumentNullException("موجودی کافی نیست", nameof(amount));
    // چون اعتبار هم داریم میتونیم حسابو منفی کنیم
    LockedBalance += amount;
    Balance -= amount;
}
```

**That guard is wrong code and commenting it out was correct.** Customers trade on
credit and are *supposed* to go negative down to their ceiling; `Balance < amount` would
forbid exactly the behaviour the product is built on.

More fundamentally, this method **cannot** enforce the invariant even in principle.
`WalletEntity` holds `Balance` and `LockedBalance` for one asset and nothing else. The
credit ceiling for asset `A` lives in a **different row entirely** — a separate wallet
keyed `CREDIT_A` (`CurrenciesConstant.CreditAssetFor`). An entity method cannot see it.
The real invariant, `Balance >= -CreditLimit`, spans two entities, so the only layer
that can evaluate it is one that reads both — which is what
`WalletApiClient.ValidateCreditAndBalanceAsync` does today.

So the check living outside the entity is a **consequence of the credit model, not a
defect in the wallet**.

**What survives, at much lower weight.** The check being re-implemented per call site is
still a genuine fragility, and #77 is the evidence: the quote-fill path (#48) did not
route through `SubmitOrderAsync`, silently inherited no balance check, and let customers
trade with an empty wallet. The fix added the check to that one caller.

But the remedy is not a guard in the entity. It is either to make credit part of the
wallet so the invariant becomes local — which is precisely what **#36** proposes — or to
ensure every path reaches one shared check. Both are #36's subject. There is no separate
piece of work here, which is why #116 was closed rather than fixed.

Note also that the credit model is deliberately **cross-asset**: `ValidateCreditAndBalanceAsync`
lets credit denominated in the quote currency back a base-asset position (`creditQuote / price`)
and vice versa. Any future per-asset invariant must preserve that or it will reject trades
the business intends to allow.

### N-6 (MEDIUM): `LockBalance` accepts a non-positive amount, which mints money

Found while re-examining N-1, and unrelated to the credit model — this one does not
touch business logic at all.

`WalletEntity.LockBalance` validates nothing:

```csharp
public void LockBalance(decimal amount)   // amount = -1000
{
    LockedBalance += amount;   // -1000
    Balance -= amount;         // +1000  ← created from nothing
}
```

`IncreaseBalance`, `DecreaseBalance` and `ConsumeLockedBalance` all reject `amount <= 0`.
`LockBalance` and `UnLockBalance` are the only two that do not, and **no layer above them
compensates on the lock side**: not `POST /api/wallet/lockBalance`, not
`WalletService.LockBalanceAsync`, not `WalletRepository.LockBalanceAsync`. A negative
amount reaches the entity and inflates the caller's spendable balance.

The unlock side is already covered — `WalletRepository.UnlockBalanceAsync:173` rejects
negative amounts and `:180` rejects unlocking more than is locked, both added for #52.
The lock side never got the same treatment.

Not currently exploitable from the bot, which always computes a positive collateral
amount, and the endpoint sits behind the API key in Production. It is reachable by any
caller of the internal API, and no legitimate caller ever locks a non-positive amount,
so rejecting it changes no intended behaviour.

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
| Financial correctness (edges) | 3/10 | 5/10 | N-6, N-3, C-4 wallet half, C-8 stub |
| Test coverage & CI | 1/10 | 8/10 | 483 tests across Wallet, Orders and the bot, on every PR |
| Operational readiness | 2/10 | 4/10 | Groundwork done, never booted, #105 open |
| Code hygiene | 3/10 | 4/10 | Dead code and 169 scattered handlers remain |

**Overall: 6.6/10.** This clears the ≥ 6.0 target the OKR set for KR3.

> Revised from 6.4 when N-2 was corrected: testing was scored 7/10 on the incorrect
> belief that the bot was untested. The overall figure is a judgement weighted toward
> the critical-path rows, not an average of the column.
>
> **Unchanged when N-1 was retracted.** Removing a wrong finding would have raised the
> edges row; adding N-6, a real money-minting hole found in its place, lowers it by
> about as much. The composition of that 5/10 changed; the number did not.

The improvement is real and concentrated where it mattered most: nothing on the
critical list is still an open hole, the money path is correct by construction rather
than by timing, and there is now a test suite that would catch a regression.

What holds the score down is a pattern, though a narrower one than this audit first
claimed: **several fixes were applied at the call site rather than at the invariant.**
C-4's wallet half, C-8's stub and N-3's legacy methods are all that shape — contained
today, re-opened by the next new caller.

The wallet's balance check is *not* an example of this, and saying so was this audit's
main error. It sits outside the entity because the credit ceiling lives in a separate
`CREDIT_` wallet row, so no entity method can evaluate it. Moving it inward requires
changing the credit model first, which is **#36**, and #36 remains the highest-value
next step — not because a guard is missing, but because the current model makes the
invariant impossible to state in one place.

---

## A note on this audit's own reliability

Two of the six new findings were wrong on first pass, and both were wrong the same way:
**inferred from shape rather than verified against intent.** N-2 read a directory layout
and concluded the bot was untested. N-1 read a commented-out line and concluded a guard
was missing, when the guard was wrong code deliberately disabled to permit credit
trading — the product's central feature.

Both were caught by the product owner, not by the audit. The findings that held up are
the ones traced through actual execution paths: C-5's ordering, C-8's stub behind the
flag, N-3's unreachable methods, N-6's missing amount check. Weight the two classes
accordingly when reading anything above.
