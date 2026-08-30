# TallaEgg — MVP Risk Audit (v6, one-day, single-file)

> **Historical — never used to run an audit.** This is one of the drafts between v1 and
> v7; see [“Where these versions came from”](README.md#where-these-versions-came-from).
> The current methodology is [`METHODOLOGY_v8.md`](METHODOLOGY_v8.md).

## WHAT THIS IS

One working day, one archived deliverable: `docs/audit/AUDIT_YYYY-MM.md`. Nothing
else gets generated — no HTML, no second format. This file is the only thing meant
to be kept, linked, diffed, and compared across future audits.

**Success criterion:** can we identify the most important risks in this MVP within
one working day and hand the team a clear, evidence-based list of what must be
fixed before the next release? Optimize for signal-to-noise, not report length.

---

## PRIORITY ORDER — deeply inspect in this order

1. **Critical security vulnerabilities** — including authentication/authorization
   bypass on financial resources (IDOR, missing ownership checks on wallet/order
   endpoints). On a financial platform this is usually the single most damaging bug
   class, so it isn't split out separately — it's folded into #1, not ranked below
   business-logic concerns.
2. Financial / wallet / balance integrity
3. Transaction atomicity and concurrency
4. Data integrity and EF Core/database issues
5. Critical business-logic bugs
6. Reliability and error handling
7. Major architectural problems that will make near-term development difficult
8. Critical testability gaps
9. Obvious performance problems

## DEPRIORITIZE — skip unless there is concrete evidence of real risk

Minor naming issues, formatting, theoretical SOLID violations, exhaustive code
smells, stylistic preferences, unnecessary design-pattern recommendations,
hypothetical scalability concerns, enterprise-level architecture improvements,
exhaustive line-by-line review, low-impact refactoring. The absence of a popular
pattern (Repository, CQRS, DDD, microservices, extensive interfaces, etc.) is never
itself a finding — only concrete evidence that its absence causes a real problem is.

## RISK-BASED SAMPLING — apply in every session

Do not inspect every file with equal depth. Deeply inspect the critical execution
paths: **Wallet, Order, Transaction, Authentication/Authorization, API boundaries,
persistence, and any other financially sensitive workflow.** Inspect supporting code
only as far as needed to understand those paths — a utility class three calls away
from a wallet mutation gets read to understand the path, not audited line-by-line in
its own right.

---

## RULES THAT APPLY TO EVERY SESSION

- **Independence:** start from zero. Don't open `docs/audit/` during the working
  sessions. Don't assume a previously reported issue is still present or already
  fixed. Don't inherit a previous score. Comparison against history happens only in
  the final session, and current code evidence always wins if it conflicts with an
  old report.
- **Evidence-based, every significant finding:** `Evidence → Analysis → Risk →
  Recommendation`. No claim without something actually viewed in the repo this
  session.
- **No fabrication:** git branch/commit, package versions, vulnerability data, line
  numbers — only from a command actually run or a file actually viewed. Tool
  unavailable → say so, never guess.
- **Redaction:** never copy a real secret/key/connection string/token into any file
  — describe its shape and location only.
- **Don't pad.** Fewer, real findings beat a long list padded to look thorough.
- **Language: English only**, everywhere in the archived file.

Working notes live in `.audit-work/` (gitignored, disposable, never archived).

---

# SESSION 1 — Discovery & Architecture (~30–45 min)

Real commands only, record actual output:
- `git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`, `git log -1 --format=%cd`
  (or "unavailable")
- `.sln`/`.csproj` files, target frameworks, project references, package references
- Project types, Docker/CI config, migration folders

Then map the actual architecture from evidence and flag, in one line each, anything
out of scope for deep review (generated code, vendored dependencies, anything
already excluded by product decision).

## Output
`.audit-work/01-discovery-architecture.md`.

---

# SESSION 2 — Priorities 4–9: Data, Reliability, Architecture, Testability, Performance (~45–60 min)

Read Session 1's output. Apply risk-based sampling: full depth on the financially
sensitive paths, lighter pass everywhere else.

Cover: EF Core tracking/transactions/missing concurrency control, obvious N+1s;
critical business-logic bugs found while tracing the critical paths; error handling
around wallet/order/auth flows (swallowed exceptions, sensitive-data leakage,
whether a production failure could actually be diagnosed from logs); architectural
issues that will slow near-term feature work specifically (not enterprise-maturity
gaps); testability gaps in the critical paths; performance problems only with
plausible, observed evidence.

Every finding: `Evidence → Analysis → Risk → Recommendation`, plus ID/Severity/
Location/Confidence/Estimated Effort (see schema below). ID prefixes: `C-`
critical, `H-` high, `M-` medium, `L-` low, numbered as found.

## Output
`.audit-work/02-priorities-4-9.md`.

---

# SESSION 3 — Priorities 1–3: Security, Financial Integrity, Concurrency (~45–60 min)

**Switch to the strongest available model — this session doesn't get compressed.**

## Security (Priority 1)
AuthN/AuthZ with explicit resource-ownership checks on every wallet/order endpoint,
secrets handling (location only), CORS, CSRF, XSS, SQL injection, rate limiting.
Run `dotnet list package --vulnerable` if available; if not, say so rather than
asserting packages are safe from memory. Classify: theoretical concern / probable
vulnerability / confirmed vulnerability.

## Financial / Wallet / Transaction Integrity (Priorities 2–3)
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
Medium confidence at most, stated plainly as uncertain.

## Output
`.audit-work/03-priorities-1-3.md`.

---

# SESSION 4 — Synthesis & Archive (~30–45 min)

Read all three prior files.

## Finding limits — targets, not a hard ceiling
Critical: report all identified, no cap. High: aim for the ~10 that matter most —
if risk-based sampling on the priority paths genuinely surfaces more real, distinct
High findings, report all of them; never omit a real one or quietly downgrade its
severity just to fit a number. Medium: same logic, aim for ~10. Low: include only if
genuinely useful — skip entirely rather than pad.

## Finding schema (every finding in the final report)
```
ID           — C-/H-/M-/L- + sequential number
Severity     — Critical / High / Medium / Low
Location     — project / file / class / method (real line number only if directly viewed)
Evidence     — what was actually observed in the code
Analysis     — why it matters, reasoned from the evidence (fold into the prose)
Risk         — engineering + business consequence if unaddressed
Recommendation — concrete and actionable
Confidence   — High / Medium / Low
Estimated Effort — Low / Medium / High
```

## Detect audit mode & compare
Look in `docs/audit/` for any prior audit file (any name) — use
`docs/audit/README.md` if it exists, otherwise each file's `**Audit Date**` line.
No prior file → initial audit. Prior file exists → re-audit: check every open prior
finding against *current code*, not tracker/issue status; classify Resolved /
Partial / Contained / Open. If a prior finding turns out to have been wrong, mark it
**Corrected** with the real explanation rather than silently dropping it. Continue
the ID sequence for genuinely new findings.

## Write `docs/audit/AUDIT_YYYY-MM.md` — the only deliverable

Required sections, in this order:

1. **Audit Metadata & Disclosure** — who/what performed this audit, stated plainly
   and near the top, not buried:
   ```
   Performed By: <name/role of the person who ran this audit session>
   AI Provider: <actual provider available in this environment>
   AI Model: <exact model if available; otherwise "not available in this
              execution environment" — never fabricated>
   Audit Date: <real date>
   Commit / Branch: <real git output, or "unavailable">
   Audit ID: TALLAEGG-AUDIT-<YYYYMMDD>-<short id>
   Methodology Version: 6.0 (one-day, single-file)
   ```
   Plus, once:
   > This audit was performed with the assistance of Artificial Intelligence,
   > based on analysis of the source code, configuration, and other repository
   > artifacts within the stated scope. It should be validated by qualified human
   > engineers before critical production, security, financial, or architectural
   > decisions are made.
2. **Executive Summary**
3. **Audit Scope & Coverage** — what was deeply reviewed (the priority paths, by
   name), what was sampled lightly, what was explicitly excluded and why.
4. **Progress vs. Previous Audits** — if re-audit: a compact trend table pulling
   from every archived audit found via `docs/audit/README.md` (date, overall score,
   production readiness %), so improvement is visible across more than just the
   immediately previous run; then the prior-findings status table
   (Resolved/Partial/Contained/Open/Corrected). If initial audit: state plainly
   "Initial audit — no prior baseline."
5. **Critical Findings**
6. **High-Priority Findings**
7. **Key Positive Findings** — genuine strengths, not a courtesy section; skip if
   there's nothing real to say.
8. **Security Assessment**
9. **Financial & Data Integrity Assessment**
10. **Architecture Assessment**
11. **Production/MVP Readiness** — the % and, above it, the actual **fix-before-
    release list** — this is the section the team will act on first.
12. **Prioritized Fix Roadmap** — ordered by risk, following the priority order
    above, not by effort.
13. **Final Score** — overall + the handful of categories that matter (Security,
    Financial Integrity, Reliability/Concurrency, Architecture, Data Layer). Any
    category containing an unresolved Critical caps at 3/10; more than one
    unresolved High caps it at 5/10 — name the finding that triggered the cap.

## No false certification
Never say the system is "completely secure" or "guaranteed production-ready." Use
evidence-based phrasing: "no evidence of X was found within the audited scope,"
"further testing is recommended before relying on this."

Update `docs/audit/README.md` with a row for this run (create it if it doesn't
exist) — this is what makes the next audit's trend table and mode-detection work.

## Output
`docs/audit/AUDIT_YYYY-MM.md` (the only archived deliverable) +
updated `docs/audit/README.md`.
