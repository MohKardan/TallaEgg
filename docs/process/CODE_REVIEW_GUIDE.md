# Code Review Guide — For Reviewers

Companion to [`PR_TEMPLATE.md`](PR_TEMPLATE.md) (which is for PR *authors*). This file is for
whoever is **reviewing**. It applies equally to human reviewers and AI coding agents asked to
review a PR — same criteria, same vocabulary.

Rules live in [`STANDARDS.md`](STANDARDS.md); they are not repeated here. This document is about
**where to look and what to look for**.

---

## 1. Before you read the diff

Answer these first — reviewing without them wastes your time:

- **What is this PR supposed to do?** If the description doesn't say, ask; don't guess.
- **Which task / audit finding does it close?** (TASK-###, C-#, H-#)
- **What's the blast radius?** Money, auth, or DB schema → treat as critical (see depth below).

---

## 2. Pick your depth

Review effort should match risk. Don't apply level 3 to a typo fix, and never apply level 1 to wallet code.

| Level | When | What you do |
|-------|------|-------------|
| **1 — Skim** | Docs, comments, formatting, dead-code removal | Read for correctness and clarity. Approve quickly. |
| **2 — Standard** | Refactors, new endpoints, non-financial features | Read every changed hunk. Check logic, tests, error handling, naming. |
| **3 — Line-by-line** | **Anything touching money, auth, secrets, TLS/CORS, or DB schema** | Read every line and its surroundings — not just the diff. Pull the branch and run it. Require a second reviewer. |

Level 3 is mandatory for changes under `src/Wallet/**`, `src/Order/**`, and anything altering
balances, locks, matching, or credentials.

---

## 3. TallaEgg red flags

These come from our own audit (`../audit/AUDIT_2026-07.md`). They are the bugs this codebase
actually produces — and they look like clean code in a diff. Check them explicitly.

### Financial correctness
- **Count the `SaveChangesAsync` calls** in any multi-step money operation. More than one, without a
  single wrapping `IDbContextTransaction`, is a defect → **C-3**. A crash between saves loses money.
- **Lock before match.** For order flows: is the balance locked *before* the order is created and sent
  to the matcher? Lock-after-match is a race condition → **C-5**.
- **`RowVersion` on modified entities.** Any read-modify-write on `WalletEntity` / `Order` without a
  concurrency token is a lost-update waiting to happen → **C-4**.
- **Idempotency.** Can this operation run twice (network retry, user double-tap) without double-charging?
  Look for a `ReferenceId` check → **H-4**.

### Security
- **`AllowAnyOrigin` / `AllowAnyMethod`** reintroduced in any `Program.cs` → **C-9**.
- **`ServerCertificateCustomValidationCallback`** not wrapped in `#if DEBUG` → **C-7**.
- **Any literal that looks like a key, token, or connection string.** Even in tests or comments → **C-1/C-2**.
- Internal exception text returned to the caller (`Fail(ex.Message)`) → leaks internals.

### Lifetime & DI
- A service registered **twice** (e.g. `AddScoped` + `AddHostedService`) creates two instances and
  silently breaks any semaphore or in-memory state → **C-6**.
- `Application` layer taking a **concrete** Infrastructure class instead of an interface → **H-1**.

### Error handling & logging
- Empty `catch { }` or `catch { continue; }` — swallowed exceptions disappear in production.
- Log placeholders with no matching argument (`"... {Amount}"` with nothing passed) → broken logs.
- Serializing whole collections into logs on a hot path → CPU and log-volume problem.

### Stubs
- An endpoint that returns a hardcoded success without doing the work → **C-8**. If it isn't
  implemented, it must return 501, not a fake result.

---

## 4. Writing comments

Label every comment so the author knows what's binding. This prevents review from turning into a
taste debate:

- **`[blocking]`** — must change before merge (bug, security issue, standards violation).
- **`[suggestion]`** — would improve it; author may decline.
- **`[question]`** — you don't understand yet; not a criticism.
- **`[nit]`** — cosmetic. Never block on a nit.

Comment on the **code**, not the person: "this loses the exception" — not "you forgot".
If you request a change, say *why* it matters, so the fix isn't cargo-culted.

---

## 5. Approve or request changes

**Approve** when:
- You understood what changed and believe it works.
- No `[blocking]` comments remain open.
- Tests exist for the risky paths (or the PR explains why not).

**Request changes** when:
- Any red flag in section 3 is present.
- Financial/security behavior changed without a test.
- You could not understand the change — unreviewable is a valid reason to block.

**Comment (neither)** when you only have questions or nits, and you're happy for someone else to
make the merge call.

> Never approve to be polite. CI (`build-and-test`) catches a broken build and a failing test, and
> nothing else — it cannot tell you the logic is wrong, the lock is in the wrong place, or the
> money does not balance. Review is the only gate for any of that, and there is no staging to
> catch what you miss. If you approve it, you own it too.

---

## 6. After approval

The author squash-merges (see `STANDARDS.md` → Git Workflow). Note that `main` requires **1 approval**
and pushing new commits **dismisses existing approvals** — so re-review is needed if the author
pushes after you approve.
