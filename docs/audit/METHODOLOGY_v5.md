# TallaEgg — MVP Audit (v5, one-day version)

> **Historical — never used to run an audit.** This is one of the drafts between v1 and
> v7; see [“Where these versions came from”](README.md#where-these-versions-came-from).
> The current methodology is [`METHODOLOGY_v8.md`](METHODOLOGY_v8.md).

## WHY THIS VERSION EXISTS

v4 was built for a mature codebase and spreads the work across 10 sessions over
several days — accurate, but too heavy for an MVP-stage project. This version keeps
everything that actually protects you from a bad finding (evidence discipline,
false-positive control, risk-weighted scoring, redaction, independence) and cuts
everything that was there for scale and long-term process discipline, not accuracy.
Four sessions, runnable in one working day.

**What's compressed and why it's still safe:**
- Discovery + architecture merge into one session — for an MVP-sized solution, this
  is genuinely one sitting of work, not two.
- Code quality + API/validation/logging + database/performance merge into one
  session — these are the lower-stakes categories; breadth in one pass beats three
  separate deep passes at MVP scale.
- Security + Financial stay their own session, on the stronger model, unmerged with
  anything else — this is the one place compression would actually be dangerous.
- Cross-cutting quality, scoring, and the final report merge into one closing
  session — an MVP doesn't need a separate testability/extensibility deep-dive; a
  short pass folded into synthesis is enough.
- The category list to score is cut from 23 down to 6 that actually matter for a
  pre-launch financial MVP.

If the project outgrows MVP scope later, go back to v4 for a deeper pass — this
version is deliberately not trying to be the permanent audit process.

---

## RULES THAT DON'T GET LIGHTER (apply to every session)

- **Evidence chain, mandatory for every significant finding:**
  `Evidence → Technical Analysis → Risk → Business Impact → Recommendation →
  Severity → Confidence`. No claim without something you actually viewed in the
  repo this session.
- **False Positive Control:** the absence of a pattern (Repository, CQRS, DDD,
  interfaces, microservices, etc.) is never itself a finding — only concrete
  evidence that its absence causes a real problem is.
- **Independence:** don't open `docs/audit/` during the working sessions (1–3).
  Comparison against a prior audit happens only in the final session, and current
  code evidence always wins if it conflicts with what an old report said.
- **No fabrication:** git branch/commit, package versions, vulnerability data, line
  numbers — only from a command you actually ran or a file you actually viewed.
  Unavailable tool → say "unavailable," never guess.
- **Redaction:** never copy a real secret/key/connection string/token into any file.
  Describe its shape and location only.
- **Risk over style:** Financial Integrity, concurrency, and security findings
  dominate every score and every ranking. Naming/formatting findings never outrank
  them regardless of count.
- **Don't pad.** A short, accurate report beats a long one with manufactured
  findings — this matters more, not less, for a one-day audit.
- **Language: English only**, everywhere.

Working notes live in `.audit-work/` (gitignored, disposable). The one thing that
gets archived is `docs/audit/AUDIT_YYYY-MM.md`.

---

# SESSION 1 — Discovery & Architecture

Run real commands, record actual output, never guess:
- `git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`, `git log -1 --format=%cd`
  (or "unavailable")
- `.sln`/`.csproj` files, target frameworks, project references, package references
  (`dotnet list package` if available)
- Project types (API/Application/Domain/Infrastructure/Tests/Workers), Docker/CI
  config, migration folders

Then, from that evidence (not folder names): what architecture is actually
implemented, the real dependency graph, any direction violations (e.g. Domain →
Infrastructure), circular dependencies, global/static state.

Flag anything to exclude from deep review (generated code, vendored dependencies,
anything already out of scope by product decision) — one line each, no elaboration
needed at MVP scale.

## Output
`.audit-work/01-discovery-architecture.md`.

---

# SESSION 2 — Code Quality, API & Data Layer

Read Session 1's output first. Cover, in one pass:

- **Code quality:** concrete SOLID violations with evidence, real duplication, dead
  code, naming inconsistency — only where it has real maintainability impact.
- **API & validation:** leaked domain/DB models in responses, missing/duplicated
  validation, swallowed exceptions, sensitive-data leakage in error responses,
  whether a production failure could actually be diagnosed from what's logged.
- **Database & performance:** tracking behavior, obvious N+1s, transaction
  boundaries, missing concurrency control, blocking async calls (`.Result`,
  `.Wait()`, `async void`) — only report with plausible evidence, not theoretical
  optimization.

Every finding uses the evidence chain from the rules above. ID prefixes: `C-`
critical, `H-` high, `M-` medium, `L-` low, numbered as found.

## Output
`.audit-work/02-code-api-data.md`.

---

# SESSION 3 — Security & Financial Risk

**Switch to the strongest available model for this session — do not compress this
one further.** This is where a missed or wrong finding costs the most.

## Security
AuthN/AuthZ (including per-endpoint resource ownership), secrets handling (location
only), CORS, CSRF, XSS, SQL injection, rate limiting, cookie/token handling. Run
`dotnet list package --vulnerable` if available; if not, say so rather than
asserting packages are safe from memory. Classify each finding: theoretical concern
/ probable vulnerability / confirmed vulnerability.

## Financial / Wallet / Trading
For every operation that mutates balances, order state, or asset holdings, trace:

```
Input → Validation → Authorization → Business Rules → State Transition →
Database Operations → External Operations → Commit → Failure Behavior →
Retry Behavior → Recovery Behavior
```

Check: decimal/money precision; race conditions on concurrent requests to the same
wallet/order; double-spend potential; atomicity of multi-step operations;
idempotency/replay protection; database constraints backing the invariants (or their
absence); whether a balance can be reconstructed independently of the live value.

**Before flagging something as a defect, check whether it might be intentional
product behavior.** A finding built on inferred rather than confirmed intent is
Medium confidence at most, and say so plainly.

## Output
`.audit-work/03-security-financial.md`.

---

# SESSION 4 — Cross-Cutting Pass, Synthesis & Report

Read all three prior `.audit-work/` files.

## Quick cross-cutting pass (folded in, not a separate deep dive)
One short pass: anything that's clearly untestable due to tight coupling, anything
that would make a production incident hard to diagnose, and genuine strengths worth
naming — 3–5 bullets each is enough at MVP scale, not an exhaustive category-by-
category review.

## Scoring (6 categories, not 23)
Score 0–10, each traceable to specific findings:
- **Financial Integrity**
- **Security**
- **Reliability & Concurrency**
- **Architecture & Code Quality** (merged — MVP doesn't need these split)
- **Data Layer & Performance**
- **Overall**

**Score cap, hard rule:** a category caps at **3/10** with any unresolved Critical
finding in it, and at **5/10** with more than one unresolved High finding — name the
finding(s) that triggered the cap next to the score.

**Production Readiness %**, derived from open Critical/High counts, never invented.
An unresolved Critical financial or security finding caps this low — say so
explicitly.

**Fix-before-launch list** — the small number of things that must be fixed before
real money moves through this system, ranked by risk, not effort. This is the
single most important output of an MVP audit; everything else is secondary.

## Archive: write the single file

Look in `docs/audit/` for a prior audit (any filename, e.g. `AUDIT_FINDINGS.md`,
`RE_AUDIT_2026-08.md`, or `AUDIT_YYYY-MM.md`) — use `docs/audit/README.md` if it
exists, otherwise check each file's `**Audit Date**` line. No prior file → initial
audit. Prior file exists → re-audit: check each of its open findings against
*current code*, not tracker/issue status, and classify Resolved / Partial /
Contained / Open. If a prior finding turns out to have been wrong, mark it
**Corrected** with the real explanation — don't silently drop it. Continue the ID
sequence for genuinely new findings rather than restarting.

Write `docs/audit/AUDIT_YYYY-MM.md`:

```markdown
# Audit — <Month YYYY> (MVP scope)

**Audit Date**:
**Overall Score**: X/10 (was Y/10 on <date>, if re-audit)
**Production Readiness**: XX% (was YY%, if re-audit)
**Scope**: <what was covered — name anything excluded>

> This is a stable reference of what this audit found — not a status tracker.
> Remediation status lives in issues/PRs, never here. Do not edit this file to
> mark work "done" — write a new dated audit instead.

**Verification performed**: <real commands run and their real output, if any>
```

Followed by: fix-before-launch list, findings (condensed evidence-chain prose, IDs
kept), strengths, score table with caps named, and — if re-audit — the prior-findings
status table plus a one-line note on this audit's own reliability (or "no
corrections this round").

Add a row to `docs/audit/README.md` (create it if it doesn't exist) so the next run
can find this one.

No HTML report by default — generate one later, on request, from this file if
you ever need something polished to hand to someone outside the team.

## No false certification
Never say the system is "completely secure" or "guaranteed production-ready." Use
evidence-based phrasing: "no evidence of X was found within the audited scope,"
"further testing is recommended before relying on this."

## Output
`docs/audit/AUDIT_YYYY-MM.md` + updated `docs/audit/README.md`.
