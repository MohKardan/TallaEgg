# TallaEgg — Phased Architecture, Security & Production Readiness Audit (v3)

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
  `docs/audit/`, alongside your existing `AUDIT_FINDINGS.md` and
  `RE_AUDIT_2026-08.md`. That's the only thing meant to be kept, linked, and diffed
  over time.

If `.audit-work/` files already exist when you start a phase, treat them as ground
truth from a previous session — extend them, don't regenerate from scratch. If a
prior archived audit already exists in `docs/audit/`, Phase 5 runs in **re-audit
mode** (see below) instead of a fresh initial audit.

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

Do not open `docs/audit/*.md` or any prior findings during this phase — independence
first, comparison later (Phase 5 handles that explicitly).

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

### Working note format (use for every finding, all of Phase 2)

Write these as prose with a consistent anchor, the way a finding should read once it
lands in the final report — not a mechanical 12-field form. At minimum, every entry
needs an **ID**, a **severity**, **where it lives** (project/file/class/method — real
line number only if you actually viewed it), what you **observed**, why it **matters**
(technical + business consequence), and a **concrete recommendation**. Classify each
one mentally as: confirmed defect, confirmed security issue, architectural weakness,
maintainability concern, performance concern, best-practice deviation, or design
trade-off — only report the first six as problems; note a trade-off as context, not a
defect.

ID prefixes: `C-` critical, `H-` high, `M-` medium, `L-` low — numbered sequentially
within category as you find them (e.g. C-1, C-2, H-1...).

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
Repository/Unit of Work is needed here specifically, not by default.

## Phase 2D — Security Audit
AuthN/AuthZ (including per-endpoint resource ownership), secrets handling (location
only), CORS, CSRF, XSS, SQL injection, file uploads, rate limiting, headers, cookies/
tokens, mass assignment. For dependency vulnerabilities: actually run
`dotnet list package --vulnerable` (or equivalent) if available; if not available,
say so rather than asserting packages are safe from memory. Classify each: theoretical
concern / probable vulnerability / confirmed vulnerability.

## Phase 2E — Financial / Wallet / Trading Domain Audit
Highest-stakes section. For every operation that mutates wallet balances, order
state, or asset holdings, trace explicitly:

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
a deliberate feature like credit trading). A finding built on inferred intent rather
than confirmed intent belongs at Medium confidence at most, with the uncertainty
stated plainly — not asserted as fact.

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
anything, check: every finding has severity + evidence; scores trace to findings;
readiness % reflects open Critical/High items; no fabricated paths/hashes/line
numbers anywhere; no secret values anywhere.

## Step 1 — detect audit mode
Look in `docs/audit/` for **any** prior audit file, not just ones matching the
`AUDIT_YYYY-MM.md` pattern — older files may use different names (e.g.
`AUDIT_FINDINGS.md`, `RE_AUDIT_2026-08.md`) and are still valid history. If a
`docs/audit/README.md` index exists, use it to find the most recent one; otherwise
open each candidate file and use the `**Audit Date**` line inside it to determine
recency — never assume from filename alone. If no prior audit file exists at all,
this is an **initial audit**. Otherwise this is a **re-audit**, and the rest of this
phase changes shape accordingly. After writing the new archived file, also add a row
for it to `docs/audit/README.md` (create that index if it doesn't exist yet) so the
next run's detection stays reliable.

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
containment), or **Open**.

**If a prior finding turns out to have been wrong** (inferred from code shape rather
than confirmed intent, and it turns out intentional) — don't quietly drop it. Mark it
**Corrected**, dated, with the real explanation, and keep it in the file. This is the
single most valuable thing your last re-audit did: it kept its own track record
honest instead of erasing the mistake.

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

- **If re-audit:** a table of prior findings and their current state (Resolved /
  Partial / Contained / Open / Corrected), then prose detail for anything not simply
  Resolved — the interesting cases deserve explanation, closed-with-no-nuance ones
  don't need more than the table row.
- All findings from this audit (from `.audit-work/02-findings.md`), in prose with the
  ID/severity/location/evidence/recommendation content from Phase 2 — condense
  mechanical repetition, keep every piece of evidence.
- Strengths (carry forward prior strengths that still hold; add new ones).
- Score table (compare to prior audit if re-audit; explain what moved and why).
- Roadmap for what's next, prioritized by risk.
- **A note on this audit's own reliability** — if any findings were corrected in
  Step 2, or if you're materially uncertain about anything you're reporting as fact,
  say so here in one short section rather than burying the caveat inline. This
  doesn't need to be a template; if nothing was corrected, a one-line "no corrections
  this round" suffices.

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

- Start independent: don't let a prior archived audit bias Phase 0–4 — those phases
  build their own picture from the code. Phase 5 is where comparison happens,
  explicitly and only there.
- Don't recommend a pattern (Repository, CQRS, DDD, microservices, interfaces
  everywhere) unless there's a concrete problem it solves in *this* codebase.
  Absence of a pattern isn't automatically a defect.
- Don't claim a security or performance issue without evidence; don't fabricate line
  numbers, branch names, commits, or vulnerability data.
- Never include a real secret value in any file, scratch or archived, at any
  severity.
- A style preference is not an engineering defect — say so explicitly when a finding
  is a trade-off rather than a problem.
