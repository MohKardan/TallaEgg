# TallaEgg — Phased Architecture, Security & Production Readiness Audit (v4)

> **Historical — never used to run an audit.** This is one of the drafts between v1 and
> v7; see [“Where these versions came from”](README.md#where-these-versions-came-from).
> The current methodology is [`METHODOLOGY_v8.md`](METHODOLOGY_v8.md).

## HOW THIS PROMPT WORKS (READ FIRST)

This audit is too large to complete in a single pass — no model can hold an entire
ASP.NET Core solution in context at once. This version splits the *work* into six
phases, run as separate sessions inside an agentic coding tool with real filesystem
access to the repository (e.g. Claude Code operating directly on the repo, not a chat
with pasted snippets).

**Two different things are kept deliberately separate:**

- **Scratch work (Phases 0–4):** notes each phase needs to hand off to the next.
  These live under `.audit-work/` and are disposable — add that folder to
  `.gitignore`. Nobody archives these; they exist only to get you through a multi-
  session run without re-reading the whole repo every time.
- **The archive (Phase 5):** exactly **one** dated Markdown file, written to
  `docs/audit/`. That's the only thing meant to be kept, linked, and diffed over
  time.

If `.audit-work/` files already exist when you start a phase, treat them as ground
truth from a previous session — extend them, don't regenerate from scratch. If a
prior archived audit already exists in `docs/audit/`, Phase 5 runs in **re-audit
mode** (see below) instead of a fresh initial audit.

**The goal is not the longest possible report.** The goal is an accurate,
evidence-based Engineering & Production Risk Audit. A shorter report with ten
well-evidenced findings is worth more than a long one padded with restated style
preferences. Do not manufacture findings to make a section look thorough.

**Redaction rule (every phase, no exceptions):** never copy a real secret, API key,
connection string, password, or token into any file — scratch or archived. Describe
its *shape and location* only, e.g. "hardcoded connection string with embedded
credentials, `appsettings.json:14`." A finding about a leaked secret must not itself
leak the secret.

**No fabrication rule (every phase):** every environmental fact — git branch, commit
hash, framework/package versions, vulnerability data, line numbers — must come from a
command you actually ran or a file you actually viewed in this session. If a command
fails or a tool is unavailable, write "unavailable in audit environment." Never guess.

**Language: English only**, in every phase and in the final archived file — including
code comments you quote and UI text if HTML is ever generated. Don't translate
identifiers, file paths, namespaces, or code snippets.

---

## INDEPENDENCE — applies to Phases 0 through 4 without exception

Phases 0–4 are where you actually look at the code and form conclusions. During
these phases:

- Do not open any file under `docs/audit/` (previous audits, findings, scores, TODOs,
  or prior AI-generated recommendations of any kind).
- Do not assume a previously reported issue is still present, and do not assume a
  previously reported issue has been fixed. Every claim in Phases 0–4 comes from code
  you looked at *this session*, not from memory of a prior report.
- Do not preserve, anchor to, or discount against a previous score. Score what you
  find, not what would be a plausible-looking delta from last time.

Comparison against history is real and useful — it happens in **Phase 5 only**, after
the independent analysis is already locked in. If Phase 5 finds a contradiction
between what Phases 0–4 concluded and what a prior archived audit said (e.g. a
finding the prior audit called "open" that current code shows is fixed, or vice
versa), **current code evidence always wins**. Do not soften or discard your
independent finding to match the old one. Instead, report the contradiction itself as
part of the re-audit narrative (see Phase 5) — that contradiction is often the most
useful thing a re-audit surfaces.

---

# PHASE 0 — Repository Discovery (scratch)

## Task
Build a factual inventory. No conclusions yet.

Run real commands and record actual output — never a plausible-sounding guess:
- `git rev-parse --abbrev-ref HEAD` → branch (or "unavailable")
- `git rev-parse HEAD` → commit hash (or "unavailable")
- `git log -1 --format=%cd` → last commit date (or "unavailable")
- `.sln`/`.csproj` files and target frameworks
- Project references between projects; package references per project
  (`dotnet list package` if available)
- Project types: API, Application, Domain, Infrastructure, Tests, Workers, shared libs
- Docker files, CI/CD config, migration folders, config file names (never contents if
  they may hold secrets)
- Approximate source file counts per project

Also flag, without judgment, anything that's a candidate for exclusion from deep
review: generated code (EF migrations unless specifically inspected, `obj/`, `bin/`,
scaffolded files), vendored third-party code, and any area a product owner has
already asked to be out of scope. This list feeds the **Audit Coverage** section in
Phase 5 — it's not a decision yet, just a flag.

Do not open `docs/audit/*.md` or any prior findings during this phase — see
INDEPENDENCE above.

## Output
`.audit-work/00-inventory.md`.

---

# PHASE 1 — Architecture Reconstruction (scratch)

Read Phase 0's output first.

Determine the *actual* architecture from evidence (project references, namespaces, DI
registrations) — not folder names. Cover: intended vs. actual architecture; the
dependency graph and any violations (e.g. Domain → Infrastructure); circular
dependencies, service locator usage, global/static state; SOLID violation candidates
with concrete file/class evidence (deep pass happens in Phase 2A).

## Output
`.audit-work/01-architecture.md`.

---

# PHASE 2 — Domain-by-Domain Deep Audit (scratch)

Five sub-phases, each its own session. Each reads Phases 0–1 and appends to
`.audit-work/02-findings.md` — never overwrite an earlier sub-phase's entries.

### Finding format — mandatory for every significant finding

Every significant finding must follow this exact evidentiary chain, in this order.
Never make a claim without concrete repository evidence — if you can't fill
"Evidence" with something you actually viewed, it isn't a finding yet.

```
Evidence           → what was actually observed in the repo: file, class, method,
                      real line number only if you directly viewed it, and what the
                      code actually does (not a paraphrase of what it should do)
Technical Analysis  → what this means technically — why it's a defect, weakness, or
                      risk, reasoned from the evidence above
Risk                → the engineering consequence if left unaddressed
Business Impact     → the product/financial/user consequence if left unaddressed
Recommendation      → concrete and actionable — not "improve the architecture"
Severity            → Critical / High / Medium / Low
Confidence          → High / Medium / Low — High only when the evidence is complete
                      and the intent behind the code (see False Positive Control
                      below) isn't in question
```

Prefix each finding's ID by severity: `C-` critical, `H-` high, `M-` medium, `L-` low
— numbered sequentially as found (C-1, C-2, H-1...).

Classify each one mentally as: confirmed defect, confirmed security issue,
architectural weakness, maintainability concern, performance concern, best-practice
deviation, or design trade-off — only the first six go in as problems; note a
trade-off as context, not a defect.

### False Positive Control (read before flagging anything architectural)

The absence of a pattern is not evidence of a problem. Do **not** report the absence
of Repository, Unit of Work, CQRS, MediatR, DDD tactical patterns, extensive
interface layers, Minimal APIs, microservices, or any other named pattern as a
finding **unless** you have concrete evidence, from this codebase, that its absence
is actually causing a real engineering problem — a test that can't be written, a
change that requires touching N unrelated files, a bug traceable to the missing
abstraction. "This is a common pattern and it's missing" is not evidence. "Method X
can't be unit tested because Y is instantiated directly instead of injected, and
here's the test I tried to write" is evidence.

## Phase 2A — Code Quality & Design
SOLID (concrete violations, with evidence), DRY/KISS/YAGNI, naming/consistency,
folder/project structure, dead code, code smells — only where there's real
maintainability evidence.

## Phase 2B — API, Validation, Error Handling, Logging
Controllers/Minimal APIs, status codes, DTO leakage, versioning, idempotency.
Validation: where it lives (syntactic/business/authorization/domain invariant),
duplication. Error handling: swallowed exceptions, broad catches, centralized
handling, sensitive-data leakage. Logging: structured logging, correlation IDs,
whether a production wallet/order failure could actually be diagnosed from what's
logged.

## Phase 2C — Database, EF Core, Async & Performance
Tracking behavior, N+1s, missing indexes, transaction boundaries, concurrency control,
migrations. Async correctness: `.Result`/`.Wait()`, async void, missing
CancellationToken. Performance: only with plausible evidence. Evaluate whether
Repository/Unit of Work is needed here specifically, not by default (False Positive
Control applies).

## Phase 2D — Security Audit
AuthN/AuthZ (including per-endpoint resource ownership), secrets handling (location
only), CORS, CSRF, XSS, SQL injection, file uploads, rate limiting, headers, cookies/
tokens, mass assignment. For dependency vulnerabilities: actually run
`dotnet list package --vulnerable` (or equivalent) if available; if not available,
say so rather than asserting packages are safe from memory. Classify each: theoretical
concern / probable vulnerability / confirmed vulnerability.

## Phase 2E — Financial / Wallet / Trading Domain Audit
Highest-stakes section. This is the domain where risk should dominate every other
consideration in this audit — see the risk-weighting rule in Phase 4. For every
operation that mutates wallet balances, order state, or asset holdings, trace
explicitly:

```
Input → Validation → Authorization → Business Rules → State Transition →
Database Operations → External Operations → Commit → Failure Behavior →
Retry Behavior → Recovery Behavior
```

Check specifically: decimal/money precision; race conditions on concurrent requests
to the same wallet/order; double-spend potential; atomicity (can it partially
succeed?); idempotency/replay protection; database constraints backing the invariants
(or their absence); whether a balance can be reconstructed independently of the live
value; behavior when an external call fails mid-operation.

**Before flagging something as a defect, check whether it might be intentional
product behavior** (e.g. a commented-out guard that exists because it conflicts with
a deliberate feature like credit trading) — this is the False Positive Control rule
applied to the financial domain specifically, where a wrong finding is most costly. A
finding built on inferred intent rather than confirmed intent belongs at Medium
confidence at most, with the uncertainty stated plainly — not asserted as fact.

Identify every operation that can leave the system in an inconsistent financial
state, with concrete evidence.

---

# PHASE 3 — Cross-Cutting Quality (scratch)

Read all prior `.audit-work/` files. Cover: testability (what's unit-testable vs.
needs integration/E2E, and why); debuggability (can a wallet/order/auth failure be
diagnosed quickly from what's logged?); maintainability (size, complexity, coupling);
extensibility (cost of adding a new asset/order type/integration); modern C#/.NET
practices, only where adoption genuinely helps.

Also capture **Positive Findings** — strong decisions already in the codebase, why
they're good, and whether they should become a project-wide standard. This is not a
criticism-only exercise.

## Output
`.audit-work/03-cross-cutting.md`.

---

# PHASE 4 — Synthesis (scratch)

Read every `.audit-work/` file, including the full findings list.

### Risk-weighted scoring — apply before assigning any score

TallaEgg is a financial platform; weight accordingly. When scoring categories and
computing Production Readiness, **Financial Integrity, wallet/balance operations,
transaction atomicity, concurrency correctness, security/authorization, data
consistency, and reliability weigh significantly more than naming, formatting, or
other purely stylistic findings.** A codebase with clean naming and inconsistent
money-handling is in worse shape than the reverse, and the scores must reflect that
— don't let a high count of minor style findings pull down a category that's
otherwise financially sound, and don't let clean style prop up a category that has
real financial or security risk in it.

**Score calibration (hard constraint):** a category score must not exceed **3/10** if
it contains any unresolved Critical finding, and must not exceed **5/10** if it
contains more than one unresolved High-severity finding — regardless of other
strengths counted in that category. State explicitly, next to each such score, which
finding(s) triggered the cap.

- Score each category 0–10 (Architecture, Design Quality, Maintainability,
  Scalability, Extensibility, Testability, Debuggability, Readability, Consistency,
  Performance, Security, Reliability, Financial Domain Safety, SOLID, DI, Error
  Handling, Logging, Validation, Configuration, API Design, Database/EF Core,
  Concurrency, Observability) — each traceable to specific findings, no false
  precision.
- Technical debt by category, with impact/urgency/effort/benefit.
- Production Readiness %, derived from the count and severity of open Critical/High
  findings — never invented. An unresolved Critical financial or security finding
  caps this low; say so explicitly.
- Refactoring roadmap: Phase 0 (blockers) → Phase 1 (high priority) → Phase 2
  (structural) → Phase 3 (optimization), plus a separate Quick Wins list.
- Top strengths, top weaknesses (by risk), top improvements (by Impact × Risk
  Reduction ÷ Effort — a real security/financial fix outranks a stylistic one
  regardless of effort).
- Architecture maturity (1–10) with justification.
- Overall risk across Security / Financial / Data Integrity / Reliability /
  Performance / Scalability / Maintainability / Architecture / Operations.

## Output
`.audit-work/04-synthesis.md`.

---

# PHASE 5 — Archive: Write the Final Report (single file)

## Precondition
All `.audit-work/00…04` files exist and are internally consistent. Before writing
anything, check: every finding follows the Evidence → Technical Analysis → Risk →
Business Impact → Recommendation → Severity → Confidence chain; score caps are
applied and their triggering findings named; readiness % reflects open Critical/High
items; no fabricated paths/hashes/line numbers anywhere; no secret values anywhere.

## Step 1 — detect audit mode
Look in `docs/audit/` for **any** prior audit file, not just ones matching the
`AUDIT_YYYY-MM.md` pattern — older files may use different names and are still valid
history. If a `docs/audit/README.md` index exists, use it to find the most recent
one; otherwise open each candidate file and use the `**Audit Date**` line inside it
to determine recency — never assume from filename alone. If no prior audit file
exists at all, this is an **initial audit**. Otherwise this is a **re-audit**, and
the rest of this phase changes shape accordingly. After writing the new archived
file, also add a row for it to `docs/audit/README.md` (create that index if it
doesn't exist yet) so the next run's detection stays reliable.

## Step 2 (re-audit only) — verify against the code, not the tracker
Read the most recent prior archived file. For every open finding in it, check the
*current code*, not issue/PR status — a task board can say "done" while the code
says otherwise, or vice versa. Also run real verification where possible, e.g.
`dotnet build <solution>` and `dotnet test <solution>`, and record actual output. If
a build was incremental rather than clean, say so rather than reporting "no warnings"
from a build that didn't actually recompile anything — verify with a clean build if
the distinction matters to what you're reporting.

For each prior finding, classify its current state as one of: **Resolved**,
**Partial** (say exactly what portion isn't), **Contained** (mitigated at the call
site or behind a flag, not fixed at the source — say what would undo the
containment), or **Open**. Per INDEPENDENCE above, this classification is made by
comparing your Phase 0–4 findings (already concluded independently) against the
prior file — never the reverse.

**If a prior finding turns out to have been wrong** (inferred from code shape rather
than confirmed intent, and it turns out intentional) — don't quietly drop it. Mark it
**Corrected**, dated, with the real explanation, and keep it in the file.

Continue the ID sequence from the prior file for genuinely new findings (`N-1`,
`N-2`, ...) rather than restarting numbering.

## Step 3 — write `docs/audit/AUDIT_YYYY-MM.md`

One file. Match the header conventions your team has already proven work — adapt as
needed, but keep this shape:

```markdown
# Audit — <Month YYYY>

**Audit Date**:
**Overall Score**: X/10 (was Y/10 on <prior date>, if re-audit)
**Production Readiness**: XX% (was YY%, if re-audit)
**Scope**: <what was actually covered — name anything excluded and why>

> This is a stable reference of what this audit found — not a status tracker.
> Remediation status lives in issues/PRs and the task board, never here. Each
> finding maps to a task; check that task's branch/PR for current status. Do not
> edit this file to mark work "done" — write a new dated audit instead.

**Verification performed**: <real commands run and their real output>
```

Then, in whatever order best serves a reader of *this specific audit* (an initial
audit and a re-audit will naturally read differently — don't force a rigid section
list onto both):

- **Audit Coverage** — what was actually deeply reviewed (name the projects/files/
  areas), what was only inventoried at a structural level, and what was explicitly
  excluded and why (generated code, vendored dependencies, product-owner-excluded
  areas from Phase 0's flags). Be specific enough that a reader knows what this audit
  does *not* claim to have checked.
- **If re-audit:** a table of prior findings and their current state (Resolved /
  Partial / Contained / Open / Corrected), then prose detail for anything not simply
  Resolved — the interesting cases deserve explanation, closed-with-no-nuance ones
  don't need more than the table row.
- All findings from this audit (from `.audit-work/02-findings.md`), condensing the
  Evidence → Technical Analysis → Risk → Business Impact → Recommendation →
  Severity → Confidence chain into readable prose — condense mechanical repetition,
  keep every piece of evidence and the severity/confidence tags.
- Strengths (carry forward prior strengths that still hold; add new ones).
- Score table (compare to prior audit if re-audit; explain what moved and why, and
  name the finding(s) behind any capped score).
- Roadmap for what's next, prioritized by risk — financial/security/concurrency
  findings first, regardless of effort, per the risk-weighting rule in Phase 4.
- **A note on this audit's own reliability** — if any findings were corrected in
  Step 2, or if you're materially uncertain about anything you're reporting as fact,
  say so here in one short section rather than burying the caveat inline. If nothing
  was corrected, a one-line "no corrections this round" suffices.

Keep the required governance information, but light — a few lines, not a heavy table:
AI provider and model used (never fabricated — if unavailable, say so explicitly),
audit date, methodology version, and this line, included once near the top:

> This audit was performed with the assistance of Artificial Intelligence, based on
> analysis of the source code, configuration, and other repository artifacts within
> the stated scope. It should be validated by qualified human engineers before
> critical production, security, financial, or architectural decisions are made.

## No standalone HTML by default
Don't generate an HTML report as part of this workflow. If a polished, shareable
version is needed for a specific audience later, generate it on request from the
archived Markdown at that point — a one-off render, not a second file maintained in
parallel on every audit run.

## No false certification
Never state the system is "completely secure," "guaranteed production-ready," or
"perfect." Use evidence-based phrasing: "The reviewed code does not show evidence
of...", "no implementation of X was identified within the audited scope,"
"additional runtime or penetration testing is recommended for confirmation."

---

# GLOBAL RULES (every phase)

- Independence is non-negotiable — see INDEPENDENCE above. It applies to Phases 0–4
  without exception; Phase 5 is the only place comparison against history happens,
  and current evidence always wins over a prior report when they disagree.
- False Positive Control — see Phase 2. The absence of a pattern (Repository, CQRS,
  MediatR, DDD, interfaces, Minimal APIs, microservices, or any other) is never
  itself a finding; only concrete evidence of a real problem caused by its absence
  is.
- Every significant finding follows Evidence → Technical Analysis → Risk → Business
  Impact → Recommendation → Severity → Confidence. Never claim without concrete
  repository evidence.
- Risk outweighs style — see the scoring rule in Phase 4. Financial integrity,
  concurrency, security, and reliability findings dominate naming/formatting
  findings in every score and every ranking.
- Don't claim a security or performance issue without evidence; don't fabricate line
  numbers, branch names, commits, or vulnerability data.
- Never include a real secret value in any file, scratch or archived, at any
  severity.
- Don't pad the report. A shorter, accurate audit beats a long one with manufactured
  findings.
