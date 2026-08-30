# TallaEgg — MVP Risk Audit (v8)

**Methodology Version:** 8.0
**Supersedes:** v7 (kept as `METHODOLOGY_v7.md`, since `AUDIT_2026-08b.md` was run under it)
**Status:** current — use this file, not a pasted copy of an older prompt.

## WHAT THIS IS

One working day, one archived deliverable: `docs/audit/AUDIT_YYYY-MM.md`. Nothing else
gets generated — no HTML, no second format. That file is the only thing meant to be
kept, linked, diffed, and compared across future audits.

**Success criterion:** can we identify the most important risks in this MVP within one
working day, and hand the team a clear, evidence-based list of what must be fixed
before the next release? Optimize for signal-to-noise, not report length.

**A finding that is wrong costs more than a finding that is missing.** A missed risk leaves
the product where it already was. A false one sends two developers to change working code.
This methodology is shaped by three such failures, each of a different kind, and every rule
below traces to one of them:

- **August 26** — two findings inferred from the *shape* of the code (a commented-out line, a
  directory layout) and never checked against what the product is for. Both were caught by the
  product owner. Section 0 and the reproduction requirement answer this.
- **August 29** — a prior-audit status row copied forward and labelled "(checked)", for a
  problem fixed three days earlier; and a verified sample ("these four entities are (28,8)")
  generalised into a verified universe ("all money columns"). Both were written in Session 4,
  the one session that works from notes rather than from code. The traceability rule answers
  this.
- **The same run** — the auditor's terminal silently lost the output of roughly a third of its
  commands, and one empty result was read as "no match" rather than "no answer". That is what
  produced the false status row. The unobserved-output rule answers this.

None of those three answers is optional.

---

## WHAT THIS AUDIT MAY AND MAY NOT DO

**May:** read anything in the repository; run builds, tests, and the simulator; query a
local database; call a locally running service; run `git`, `gh`, `dotnet` and similar
read-only tooling.

**May not:** change code, tests, configuration, or documentation. Fix nothing, however
tempting or small. The audit produces exactly two written artifacts —
`docs/audit/AUDIT_YYYY-MM.md` and an updated `docs/audit/README.md` — plus disposable
notes in `.audit-work/`. No commits beyond those two files, no branches of fixes, no
"while I was there" cleanups. A repository that changed during the audit cannot be
measured by it.

If the audit must run something that mutates state (the simulator writes to the
database, for instance), say so in the report's Scope section and prefer a disposable
or local database.

---

## SECTION 0 — THINGS THAT LOOK LIKE DEFECTS AND ARE NOT

Read before writing a single finding.

[`AGENT.md`](../../AGENT.md) has a section titled **"Business rules that look like
bugs"**. Every item in it has been reported as a defect at least once, including by a
previous audit, and each was confirmed as intended by the product owner.
[`CLAUDE.md`](../../CLAUDE.md) carries the same list in shorter form and is loaded
automatically, so it will be in context whether or not it is opened deliberately.

**The rule:** before reporting anything, check it against those two lists. If the
finding appears there, one of two things must happen:

1. Drop it, or
2. Report it anyway *and address the documented reasoning directly* — quote what the
   documentation claims and show, from code, why it is wrong.

A finding that contradicts documented product intent without engaging that
documentation is not a finding. It is the mistake the last audit made twice.

**This does not compromise independence.** Independence means not inheriting a previous
audit's *score, findings, or conclusions* — see the rule below. It does not mean
auditing while refusing to know what the product is for. Intent is evidence.

The list as of this writing — verify each against `AGENT.md`, which is authoritative:

- The market maker may go arbitrarily negative on any asset, with no ceiling. That
  balance *is* the shop's book. Absence of alerting is tracked as #124.
- Commission is deliberately `0.00` on every trade; revenue is the spread. Fee code is
  dormant, not dead.
- Credit is **cross-asset**: credit denominated in one asset legitimately backs a
  negative position in another, converted at price. Any per-asset balance invariant will
  reject trades the business intends to allow.
- `Wallet.LockBalance` enforces no balance rule and must not; the commented-out guard
  there is wrong code, correctly disabled.
- Bot tokens visible in the working tree and in git history are **dead** — rotated under
  #33 and confirmed by the product owner. History is deliberately not rewritten (#105).
  Reporting them as a live leak is a false positive that every fresh reader produces.

---

## PRIORITY ORDER — deeply inspect in this order

1. **Critical security vulnerabilities** — including authentication/authorization bypass
   on financial resources (IDOR, missing ownership checks on wallet/order endpoints). On
   a financial platform this is usually the single most damaging bug class, so it isn't
   split out separately — it's folded into #1, not ranked below business-logic concerns.
2. Financial / wallet / balance integrity
3. Transaction atomicity and concurrency
4. Data integrity and EF Core/database issues
5. Critical business-logic bugs
6. Reliability and error handling
7. Major architectural problems that will make near-term development difficult
8. Critical testability gaps
9. Obvious performance problems

## DEPRIORITIZE — skip unless there is concrete evidence of real risk

Minor naming issues, formatting, theoretical SOLID violations, exhaustive code smells,
stylistic preferences, unnecessary design-pattern recommendations, hypothetical
scalability concerns, enterprise-level architecture improvements, exhaustive
line-by-line review, low-impact refactoring. The absence of a popular pattern
(Repository, CQRS, DDD, microservices, extensive interfaces, etc.) is never itself a
finding — only concrete evidence that its absence causes a real problem is.

## RISK-BASED SAMPLING — apply in every session

Do not inspect every file with equal depth. Deeply inspect the critical execution paths:
**Wallet, Order, Transaction, Authentication/Authorization, API boundaries, persistence,
and any other financially sensitive workflow.** Inspect supporting code only as far as
needed to understand those paths — a utility class three calls away from a wallet
mutation gets read to understand the path, not audited line-by-line in its own right.

## SCOPE EXCLUSIONS — decided by the product owner, not by this audit

- **The Affiliate service** (`src/Affiliate/`) is out of scope. Excluded at the product
  owner's request; it was excluded from the August 2026 pass for the same reason. Do not
  audit it, and do not report its state — including its apparent dormancy — as a finding.
- Generated code, vendored dependencies, and migration scaffolding are read to
  understand a path, never audited as authored code.

If something else appears to be out of scope, say so in the Scope section and audit it
anyway at low depth; do not silently expand the exclusion list.

---

## RULES THAT APPLY TO EVERY SESSION

- **Independence:** start from zero on *conclusions*. Do not open `docs/audit/` during
  working sessions 1–3. Do not assume a previously reported issue is still present or
  already fixed. Do not inherit a previous score. Comparison against history happens
  only in Session 4, and current code evidence always wins if it conflicts with an old
  report. This rule governs prior *audits*; it does not apply to product documentation
  (Section 0), which is read first, deliberately.
- **Evidence-based, every significant finding:** Evidence, then Analysis, then Risk,
  then Recommendation. No claim without something actually viewed in the repo this
  session.
- **Reproduction before Critical** — see the next section.
- **An unobserved result is not a negative result.** If a command's output does not come back
  — an empty pane, a dropped stream, a tool that returns nothing — that is "unchecked", never
  "no match found". Re-run it. If it will not produce output, the claim it was meant to support
  is marked unverified in the report. An audit run in an unreliable terminal is still valid; an
  audit that reads silence as evidence is not.
- **No quantifier without an enumeration.** "All", "every", "uniform", "none", "no X exists",
  "nothing" may be written only where the notes contain the counted list behind them. If the
  list cannot be produced, write what was actually checked instead.
- **Record when each session starts and ends**, in the notes, as wall-clock time. It goes in
  the report (§3) so a reader can weigh depth without reconstructing it from file
  timestamps.
- **No fabrication:** git branch/commit, package versions, vulnerability data, line
  numbers — only from a command actually run or a file actually viewed. Tool
  unavailable: say so, never guess.
- **Redaction:** never copy a real secret/key/connection string/token into any file —
  describe its shape and location only.
- **Don't pad.** Fewer, real findings beat a long list padded to look thorough.
- **Language: English only**, everywhere in the archived file.
- **Change nothing** — see "What this audit may and may not do".

Working notes live in `.audit-work/` (gitignored, disposable, never archived).

---

## THE REPRODUCTION REQUIREMENT

The previous audit's failures were not findings without evidence. They were findings
with *partial* evidence: the code was read correctly and the conclusion was still wrong,
because reading a code path shows what it does, not what happens when it runs.

This repository can be executed. There is a solution that builds, a test project with
several hundred tests, a bot simulator, and local databases. An audit that only reads is
leaving its strongest instrument unused.

**Every Critical and High finding must carry two additional fields:**

- **Failure Scenario** — concrete inputs or state, and the specific wrong outcome. "A
  customer holding X does Y and ends up with Z, which is wrong because W." Not "may
  cause data inconsistency."
- **Reproduction** — what was actually run and what it actually output: a test written
  and executed, a SQL query, an HTTP call, a simulator run, a specific trace through
  logged output. If reproduction was not possible, say exactly why, and drop the
  Confidence accordingly.

**The severity consequence, applied without exception:**

- A finding that was reproduced may be **Critical**.
- A finding that was not reproduced is at most **High**, with Confidence **Medium** or
  lower, and the report says plainly that it is unverified.
- A finding resting on inferred rather than confirmed product intent is at most
  **Medium** Confidence, stated as uncertain — and must first survive Section 0.

Trying to reproduce a finding and failing is a *result*, not a wasted hour. Write it
down: "attempted to reproduce with X; the guard at Y refused it." That sentence is often
worth more than the finding would have been.

---

## THE TRACEABILITY RULE — Session 4 may not introduce a claim

Sessions 1–3 look at the code. Session 4 looks at the notes. Every factual claim in the
archived report must therefore trace to a line in `.audit-work/` written by a session that
actually looked — including, and especially, the prior-findings table, which is nineteen
claims written at the end of the day and the cheapest place in the whole report to guess.

**When Session 4 wants to state something the notes do not contain, it has exactly two
options.** Go and check it now, writing the command *and its output* into
`.audit-work/04-*.md` before citing it. Or write "this pass did not check" and leave it at
that. There is no third option, and in particular "(checked)" is not something that may be
written about a check with no note behind it.

**The word "verified" and its relatives are reserved.** "Verified", "confirmed", "checked",
"re-verified" may appear only where a note records what was run. Everything else says "read",
"inferred", or "carried over" — all three of which are honest, and none of which is a defect
in an audit.

A claim that cannot be traced is not dropped quietly. It is labelled.

---

# SESSION 1 — Discovery & Architecture (~45–60 min)

Read Section 0 first, then `AGENT.md` and `CLAUDE.md` in full.

Real commands only, record actual output:

- `git rev-parse --abbrev-ref HEAD`, `git rev-parse HEAD`, `git log -1 --format=%cd`
  (or "unavailable")
- `.sln` and `.csproj` files, target frameworks, project references, package references
- Project types, Docker/CI config, migration folders
- `dotnet build TallaEgg.sln` and `dotnet test TallaEgg.sln` — record the real counts.
  A **clean** build, not an incremental one: the August audit reported "no warnings"
  from an up-to-date build that compiled nothing, and had to correct itself.

Then map the actual architecture from evidence, and list in one line each anything out
of scope for deep review.

**Output:** `.audit-work/01-discovery-architecture.md`.

---

# SESSION 2 — Priorities 4–9: Data, Reliability, Architecture, Testability, Performance (~60–90 min)

Read Session 1's output. Apply risk-based sampling.

Cover: EF Core tracking, transactions, missing concurrency control, obvious N+1s; error
handling around wallet/order/auth flows (swallowed exceptions, sensitive-data leakage,
whether a production failure could actually be diagnosed from logs); architectural
issues that will slow near-term feature work specifically, not enterprise-maturity gaps;
testability gaps in the critical paths; performance problems only with plausible,
observed evidence.

**Hand-off rule — do not duplicate Session 3.** Anything that moves money, changes a
balance, or transitions order state gets **recorded and passed forward, not judged**.
Write it under a heading "For Session 3" with the location and what looked wrong, and
stop there. Session 3 owns the analysis, the severity and the finding ID. Two sessions
independently analysing the same code path produce two findings with different IDs and
one shared error.

Every finding this session does own: Evidence, Analysis, Risk, Recommendation, plus
ID / Severity / Location / Confidence / Estimated Effort. ID prefixes: `C-` critical,
`H-` high, `M-` medium, `L-` low, numbered as found.

**Output:** `.audit-work/02-priorities-4-9.md`.

---

# SESSION 3 — Priorities 1–3: Security, Financial Integrity, Concurrency (~90–120 min)

**Switch to the strongest available model — this session doesn't get compressed.**
Start from Session 2's "For Session 3" list, plus your own tracing.

## Security (Priority 1)

AuthN/AuthZ with explicit resource-ownership checks on every wallet and order endpoint,
secrets handling (location only), CORS, CSRF, XSS, SQL injection, rate limiting. Run
`dotnet list package --vulnerable` if available; if not, say so rather than asserting
packages are safe from memory. Classify each: theoretical concern / probable
vulnerability / confirmed vulnerability.

Before reporting any credential as exposed, check it against Section 0. The tokens in
this repository are rotated and dead, and reporting them again spends the reader's trust
on a false positive instead of on the findings that are real.

## Financial / Wallet / Transaction Integrity (Priorities 2–3)

For every operation that mutates balances, order state, or asset holdings, trace the
whole path: Input, Validation, Authorization, Business Rules, State Transition, Database
Operations, External Operations, Commit, Failure Behavior, Retry Behavior, Recovery
Behavior.

Check: decimal and money precision — including the **column** precision actually
configured, not only the C# type; race conditions on concurrent requests to the same
wallet or order; double-spend potential; atomicity of multi-step operations; idempotency
and replay protection; database constraints backing the invariants, or their absence;
whether a balance can be reconstructed independently of the live value.

**Every balance invariant must be evaluated across assets, never per-asset.** Credit in
one asset backs positions in another. A query that groups by asset and flags negative
balances will produce false positives, and has.

**Before flagging something as a defect, check whether it might be intentional product
behavior** — Section 0 first, then the code, then ask what the feature is for. A finding
built on inferred rather than confirmed intent is Medium confidence at most, stated
plainly as uncertain.

**Reproduce what you can.** A wallet race is testable; a precision loss is provable with
one query; an authorization gap is one HTTP call. Record the command and its output.

**Output:** `.audit-work/03-priorities-1-3.md`.

---

# SESSION 4 — Synthesis & Archive (~45–60 min)

Read all three prior files. Only now open `docs/audit/`.

## Finding limits — targets, not a hard ceiling

Critical: report all identified, no cap. High: aim for the ~10 that matter most — if
risk-based sampling genuinely surfaces more real, distinct High findings, report all of
them; never omit a real one or quietly downgrade its severity to fit a number. Medium:
same logic, aim for ~10. Low: include only if genuinely useful — skip entirely rather
than pad.

## Finding schema (every finding in the final report)

| Field | Content |
|---|---|
| ID | `C-` / `H-` / `M-` / `L-` plus sequential number |
| Severity | Critical / High / Medium / Low |
| Location | project / file / class / method (real line number only if directly viewed) |
| Evidence | what was actually observed in the code |
| Failure Scenario | concrete inputs or state, and the specific wrong outcome — **required for Critical and High** |
| Reproduction | what was run and what it output, or why it could not be run — **required for Critical and High** |
| Analysis | why it matters, reasoned from the evidence (fold into the prose) |
| Risk | engineering and business consequence if unaddressed |
| Recommendation | concrete and actionable |
| Confidence | High / Medium / Low |
| Estimated Effort | Low / Medium / High |
| Already Tracked | GitHub issue number, or "no" |

## Severity first, score second — never the reverse

Assign every finding its severity from the evidence and the reproduction result. Then
compute the scores. **Do not revisit a severity after seeing what it does to a score.**
The caps below are deliberately harsh; the correct response to a bad score is to report
it, not to reclassify the finding that caused it. If a cap feels wrong, say so in prose
next to the score — that is a legitimate audit statement. Moving the severity is not.

## Cross-reference open issues

After the findings are final and their severities fixed, run:

```
gh issue list --state open --limit 100
gh issue list --label audit-finding --state all
```

Mark every finding that already has an issue with its number. This changes the
**roadmap**, not the finding: something already filed is still a real risk, but the team
needs to know it is not new work. Note that a finding's *status* is never assessed from
the tracker — status always comes from current code.

## Detect audit mode and compare

Look in `docs/audit/` for prior audit files — use `docs/audit/README.md` if it exists,
otherwise each file's `**Audit Date**` line. No prior file: initial audit. Prior file
exists: re-audit — check every open prior finding against *current code*, not tracker or
issue status, and classify Resolved / Partial / Contained / Open. If a prior finding
turns out to have been wrong, mark it **Corrected** with the real explanation rather than
silently dropping it. Continue the ID sequence for genuinely new findings.

**Before writing a single row, run this and read the output:**

```
git log --oneline --since=<date of the previous audit> --no-merges
```

That is every change made since the previous measurement, usually a page of subject lines
that name the fixes directly. A prior finding's status was true on the day it was written;
the whole question a re-audit answers is which ones stopped being true in the interval, and
this command is the interval. In the August 29 run it would have taken ten seconds and
prevented the one false row that pass produced — the fix was sitting in the list by name.

**Every row carries its provenance.** Against each prior finding, record which applies:

- **(a)** re-verified in code during Sessions 1–3 — name the file, method, or reproduction
- **(b)** verified now, during synthesis — name the command, with its output in the notes
- **(c)** carried over from the previous audit without independent verification

Rows may be (a) or (b). A row that would be (c) is either promoted to (b) by checking it, or
printed as **unverified** in the report. Do not publish a (c) row dressed as either of the
others: that is precisely the failure this rule exists for, and it is invisible to every
reader who was not there.

## Write `docs/audit/AUDIT_YYYY-MM.md` — the only deliverable

**Naming, and the one way to lose an audit:** `YYYY-MM` is the month the audit runs. If a
file with that name already exists, **never overwrite it** — an archived audit is
evidence, and a second run in the same month is a second measurement, not a correction.
Append a letter instead: `AUDIT_2026-08b.md`, then `c`, and so on. Check before writing,
not after.

Required sections, in this order:

1. **Audit Metadata & Disclosure** — who and what performed this audit, stated plainly
   near the top, not buried:
   ```
   Performed By: <name/role of the person who ran this audit session>
   AI Provider: <actual provider available in this environment>
   AI Model: <exact model if available; otherwise "not available in this
              execution environment" — never fabricated>
   Audit Date: <real date>
   Commit / Branch: <real git output, or "unavailable">
   Audit ID: TALLAEGG-AUDIT-<YYYYMMDD>-<short id>
   Methodology Version: 8.0
   ```
   Plus, once:
   > This audit was performed with the assistance of Artificial Intelligence, based on
   > analysis of the source code, configuration, and other repository artifacts within
   > the stated scope. It should be validated by qualified human engineers before
   > critical production, security, financial, or architectural decisions are made.
2. **Executive Summary**
3. **Audit Scope & Coverage** — what was deeply reviewed (the priority paths, by name),
   what was sampled lightly, what was excluded and why, **what was executed** (build, tests,
   simulator, queries) as opposed to only read, and the **wall-clock time of each session**.
   Depth is a fact about an audit and belongs on its face, not in its file timestamps.
4. **Progress vs. Previous Audits** — if re-audit: the trend table from
   `docs/audit/README.md` (date, overall score, production readiness %, methodology
   version, model), then the prior-findings status table
   (Resolved / Partial / Contained / Open / Corrected). If initial audit: state plainly
   "Initial audit — no prior baseline."
5. **Critical Findings**
6. **High-Priority Findings**
7. **Key Positive Findings** — genuine strengths, not a courtesy section; skip if there
   is nothing real to say.
8. **Security Assessment**
9. **Financial & Data Integrity Assessment**
10. **Architecture Assessment**
11. **Production/MVP Readiness** — the percentage and, above it, the actual
    **fix-before-release list**. This is the section the team acts on first.
12. **Prioritized Fix Roadmap** — ordered by risk, following the priority order above,
    not by effort. Mark items already tracked with their issue number.
13. **Final Score** — overall, plus the handful of categories that matter (Security,
    Financial Integrity, Reliability/Concurrency, Architecture, Data Layer). Any category
    containing an unresolved Critical caps at 3/10; more than one unresolved High caps it
    at 5/10 — name the finding that triggered the cap.
14. **A note on this audit's own reliability** — which findings were reproduced and which
    were only reasoned about; anything the audit could not check; where it is most likely to
    be wrong; and how many prior-findings rows were (a), (b) and (c). Both audits preceding
    this methodology had their errors found through this section rather than through their
    findings. It is required, not optional.

## No false certification

Never say the system is "completely secure" or "guaranteed production-ready." Use
evidence-based phrasing: "no evidence of X was found within the audited scope," "further
testing is recommended before relying on this."

Update `docs/audit/README.md` with a row for this run, including methodology version and
model — without those the trend column is not comparable across runs.

**Output:** `docs/audit/AUDIT_YYYY-MM.md` plus an updated `docs/audit/README.md`.
Nothing else.

---

## CHANGES FROM v7

Recorded so a future reader can tell whether a score moved because the code improved or
because the method changed. Every one of these comes from a specific failure in the
`AUDIT_2026-08b.md` run, which was conducted under v7 and reviewed afterwards.

| # | Change | Why |
|---|---|---|
| A | The traceability rule: Session 4 may not state what no session checked; "verified" and "(checked)" reserved for claims with a recorded command | A status row was labelled "(checked)" with no note behind it, and was false |
| B | An unobserved command result is "unchecked", never "no match" | The auditor's terminal dropped ~1/3 of its output; one empty pane became a false negative |
| C | No quantifier without an enumeration in the notes | "All money columns are (28,8)" turned four verified entities into a false universal |
| D | `git log --since=<previous audit date>` mandatory before the prior-findings table | The commit fixing the false row was in that list, by name |
| E | Every prior-findings row labelled (a) re-verified / (b) checked now / (c) carried over; no (c) may be published as anything else | Three of nineteen rows were carried over silently; two of the three were wrong |
| F | Per-session wall-clock times reported in §3 | A four-session run took 67 minutes and nothing in the report said so |
| G | §14 reports the (a)/(b)/(c) counts | Makes the weakest part of a re-audit visible to its reader |

Rules A, C, E and F were proposed by the reviewer; B and G by the auditor whose run produced
the failures, in its own account of how they happened.

v7's own changes from v6 are listed in `METHODOLOGY_v7.md`, and all of them remain in force:

| # | Change | Why |
|---|---|---|
| 1 | Reproduction required for Critical and High; severity capped without it | Both of the August audit's wrong findings were read, not run |
| 2 | Section 0: documented product intent read *first*; independence narrowed to prior audits | "Start from zero" was being applied to product intent, which is evidence |
| 3 | Paths corrected to `docs/audit/`; `.audit-work/` actually gitignored | v6 pointed at a directory that did not exist, so re-audit mode would never have triggered |
| 4 | Affiliate exclusion named explicitly | The auditor had no way to know |
| 5 | "Change nothing" stated | v6 never said the audit must not fix what it finds |
| 6 | Session times raised to a real day (~4–5.5h) | v6 totalled ~3h under a "one working day" heading |
| 7 | Session 2 records money-path observations; Session 3 judges them | The two sessions overlapped and would double-report |
| 8 | Open GitHub issues cross-referenced at synthesis | Prevents re-reporting already-tracked findings as new work |
| 9 | Severity fixed before scores are computed | The score caps created pressure to downgrade real findings |
| 10 | Trend table carries methodology version and model | Scores from different methods and models are not comparable |
| 11 | Section 14, the audit's self-assessment, made mandatory | It is what made the last audit's errors visible |
