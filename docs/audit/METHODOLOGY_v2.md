# TallaEgg — Phased Independent Architecture, Security & Production Readiness Audit (v2)

> **Historical — never used to run an audit.** This is one of the drafts between v1 and
> v7; see [“Where these versions came from”](README.md#where-these-versions-came-from).
> The current methodology is [`METHODOLOGY_v8.md`](METHODOLOGY_v8.md).

## HOW THIS PROMPT WORKS (READ FIRST)

This audit is too large to complete in a single pass — no model can hold an entire
ASP.NET Core solution in context at once. This version splits the work into **six
phases**, run as **separate sessions** inside an agentic coding tool with real
filesystem access to the repository (e.g. Claude Code operating directly on the repo,
not a chat with pasted snippets).

Each phase:
- Reads the output files from all prior phases before starting.
- Does its own work using real tools (`view`/`grep`/`git`/`dotnet` commands, not
  memory or assumption).
- Writes its findings to a dedicated file under `audit/` in the repo before ending.
- Does NOT try to produce the final HTML/Markdown report itself (that's Phase 5 only).

If a phase's output file already exists when you start, treat it as ground truth from
a previous session — do not regenerate it from scratch, only extend it.

Working files this audit produces (create the `audit/` folder if absent):

```
audit/00-inventory.md
audit/01-architecture.md
audit/02-findings-register.md      (append-only across phases 2A–2E)
audit/03-cross-cutting.md
audit/04-synthesis.md
TallaEgg-Audit-Report.html         (Phase 5 output)
TallaEgg-Audit-Report.md           (Phase 5 output)
```

**Redaction rule (applies to every phase, no exceptions):** never copy a real secret,
API key, connection string, password, or token into any audit file. If evidence
requires referencing one, describe its *shape and location* only — e.g. "hardcoded
connection string with embedded credentials, `appsettings.json:14`" — never the value
itself. A finding about a leaked secret is itself a document that must not leak the
secret.

**No fabrication rule (applies to every phase):** every fact that comes from the
environment — git branch, commit hash, framework version, package versions,
vulnerability data, line numbers — must come from an actual command you ran or file
you viewed in this session. If a command fails or a tool is unavailable, write
"unavailable in audit environment" for that field. Never guess or infer a plausible-
sounding value.

---

# PHASE 0 — Repository Discovery & Inventory

## Role
Principal Software Architect performing initial repository reconnaissance.

## Task
Build a factual inventory of the repository. Do not draw architectural or quality
conclusions yet — that's Phase 1 onward.

Run real commands and record actual output:

- `git rev-parse --abbrev-ref HEAD` → branch (or "unavailable")
- `git rev-parse HEAD` → commit hash (or "unavailable")
- `git log -1 --format=%cd` → last commit date (or "unavailable")
- List all `.sln`, `.csproj` files and their target frameworks
- List NuGet package references per project (`dotnet list package` if available)
- Identify project types: API, Application, Domain, Infrastructure, Tests, Workers,
  shared/common libraries
- Identify Docker files, CI/CD config, migration folders, config files
  (`appsettings*.json` — note their existence, never their contents if they contain
  secrets)
- Count approximate source files per project

Do NOT open previous audit reports, TODOs, or prior AI-generated conclusions during
this phase if they exist in the repo — this is an independent audit.

## Output
Write `audit/00-inventory.md` with: project list, references between projects,
package list per project, framework versions, real git metadata, file/folder counts,
and a short "scope of this audit" note (what was and wasn't reachable).

---

# PHASE 1 — Architecture Reconstruction & Dependency Graph

## Precondition
Read `audit/00-inventory.md` first.

## Task
Determine the *actual* architecture from evidence (project references, namespaces,
DI registrations, controller/service/repository implementations) — not from folder
names alone.

Cover:
- Intended vs. actual architecture (layered, clean, vertical slice, modular monolith,
  etc. — whichever the evidence supports; do not force a label)
- Dependency graph between projects/layers; flag violations of intended direction
  (e.g. Domain → Infrastructure, API → DB direct access)
- Circular dependencies, service locator usage, global/static state
- SOLID violations with concrete file/class evidence (log candidates here; deep
  SOLID/DRY analysis happens in Phase 2A)

## Output
Write `audit/01-architecture.md`.

---

# PHASE 2 — Domain-by-Domain Deep Audit

Run as five sub-phases, each its own session. Each sub-phase reads
`audit/00-inventory.md`, `audit/01-architecture.md`, and appends to
`audit/02-findings-register.md` using the finding schema below — never overwrite
earlier sub-phases' entries.

### Finding schema (use for every entry across all of Phase 2)

```
### [ID]  e.g. SEC-003, FIN-007, DB-012

- Severity: Critical / High / Medium / Low
- Category: Architecture / Security / Database / Performance / Reliability /
  Financial Integrity / Maintainability / ...
- Location: Project / File path / Class / Method (line number ONLY if you directly
  viewed it — omit rather than guess)
- Evidence: what was actually observed, referencing the real implementation
- Problem: what's wrong
- Technical Risk: engineering consequence
- Business Impact: product/business consequence
- Recommendation: concrete, actionable — not "improve the architecture"
- Effort: Low / Medium / High
- Confidence: High / Medium / Low (High only when evidence is complete)
- Production Decision: Must Fix Before Production / Should Fix Before Production /
  Can Be Deferred / Optional Improvement
```

Classify every conclusion mentally as: confirmed defect, confirmed security issue,
architectural weakness, maintainability concern, performance concern, best-practice
deviation, design trade-off, or personal/style preference. Only the first six belong
in the findings register as problems — a design trade-off is noted as context, not a
defect.

## Phase 2A — Code Quality & Design
SOLID (concrete violations only, with evidence), DRY/KISS/YAGNI, naming and
consistency across the codebase, folder/project structure, dead code, code smells
(God Object, Long Method, Feature Envy, Primitive Obsession, Data Clumps, etc. — only
where there's real evidence of maintainability impact).

## Phase 2B — API, Validation, Error Handling, Logging
Controllers/Minimal APIs, routing, status codes, request/response DTOs vs. leaked
domain/DB models, versioning, pagination, idempotency. Validation: where it lives
(syntactic vs. business vs. authorization vs. domain invariant), duplication. Error
handling: swallowed exceptions, overly broad catches, centralized handling,
ProblemDetails usage, sensitive-data leakage in error responses. Logging: structured
logging, log levels, correlation IDs, whether a production incident in wallet/order
flows could actually be diagnosed from what's logged.

## Phase 2C — Database, EF Core, Async & Performance
DbContext design, tracking behavior, N+1 queries, missing indexes, transaction
boundaries, concurrency control (optimistic/pessimistic), migrations. Async
correctness: `.Result`/`.Wait()`, async void, missing CancellationToken, fire-and-
forget risks. Performance: only report issues with plausible evidence, not
theoretical optimizations. Explicitly evaluate whether Repository/Unit of Work
patterns are used, and if not, whether their absence causes a real problem — don't
recommend them by default.

## Phase 2D — Security Audit
Authentication, authorization (including resource ownership / IDOR checks per
endpoint), secrets handling (existence and location only — see redaction rule), CORS,
CSRF, XSS, SQL injection, file upload handling, rate limiting, security headers,
cookie/token handling, mass assignment. For dependency vulnerabilities: actually run
`dotnet list package --vulnerable` (or equivalent) if available in the environment;
if the tool isn't available, state that explicitly rather than asserting packages are
safe or unsafe from memory. For every security finding, classify as: theoretical
concern / probable vulnerability / confirmed vulnerability — do not claim a
vulnerability without evidence.

## Phase 2E — Financial / Wallet / Trading Domain Audit (dedicated deep pass)
This is the highest-stakes section — give it the most rigor. For every operation that
mutates wallet balances, order state, or asset holdings, trace it explicitly:

```
Input → Validation → Authorization → Business Rules → State Transition →
Database Operations → External Operations → Commit → Failure Behavior →
Retry Behavior → Recovery Behavior
```

For each such operation, check specifically:
- Decimal/money precision and currency handling
- Race conditions on concurrent requests to the same wallet/order
- Double-spend potential
- Atomicity — can the operation partially succeed and leave inconsistent state?
- Idempotency / duplicate-request handling (replay protection)
- Database constraints backing the invariants (unique constraints, check constraints)
  — or their absence
- Ledger/audit trail: can a balance be reconstructed independently of the live value?
- Behavior when an external service call fails mid-operation

Identify every operation that can leave the system in an inconsistent financial
state, with concrete evidence.

---

# PHASE 3 — Cross-Cutting Quality

## Precondition
Read all prior `audit/` files, including the full findings register.

## Task
Testability (what's unit-testable vs. requires integration/E2E, and why), debug-
ability (can a wallet/order/auth failure be diagnosed quickly in production from
what's logged and how errors surface?), maintainability (class/method size,
cyclomatic complexity, coupling/cohesion), extensibility (cost of adding a new asset,
order type, payment method, or integration), modern C#/.NET practices (only where
adoption would genuinely improve clarity/correctness/maintainability — not novelty
for its own sake).

Also produce **Positive Findings**: identify strong engineering decisions already in
the codebase, explain why they're good, what risk they prevent, and whether they
should become a project-wide standard. This audit is not criticism-only.

## Output
Write `audit/03-cross-cutting.md`.

---

# PHASE 4 — Synthesis & Scoring

## Precondition
Read every file under `audit/`, including the complete findings register.

## Task
- Score each category 0–10 (Architecture, Design Quality, Maintainability,
  Scalability, Extensibility, Testability, Debuggability, Readability, Consistency,
  Performance, Security, Reliability, Financial Domain Safety, SOLID, DI, Error
  Handling, Logging, Validation, Configuration, API Design, Database/EF Core,
  Concurrency, Observability). Each score must trace back to specific findings in the
  register — state which findings drove the score. Avoid false precision (no "8.7"
  without a stated reason).
- Technical debt estimate by category (architectural, code, testing, security,
  operational, documentation), each with impact/urgency/effort/expected benefit.
- Production Readiness %, derived explicitly from the count and severity of open
  Critical/High findings — never an invented number. Any unresolved Critical
  financial or security finding caps this at a low percentage; state the cap
  explicitly.
- Refactoring roadmap: Phase 0 (production blockers) → Phase 1 (high priority) →
  Phase 2 (structural) → Phase 3 (optimization) → Phase 4 (long-term), plus a
  separate Quick Wins list.
- Top 10 Strengths, Top 10 Weaknesses (ranked by risk), Top 20 Improvements (ranked
  by Impact × Risk Reduction ÷ Effort — a serious security or financial-integrity fix
  outranks a stylistic one regardless of effort).
- Architecture maturity rating (1–10 scale, defined in the original methodology)
  with justification.
- Final risk assessment across Security / Financial / Data Integrity / Reliability /
  Performance / Scalability / Maintainability / Architecture / Operations.

## Output
Write `audit/04-synthesis.md`.

---

# PHASE 5 — Final Report Generation

## Precondition
All of `audit/00…04` must exist and be internally consistent. Before generating
anything, run the consistency check below and fix any gaps by returning to the
relevant earlier phase rather than papering over them in the final report.

## Consistency check (do this before writing the final files)
- Every finding in the register has severity, confidence, and evidence
- Scores in Phase 4 trace to findings, not vibes
- Production readiness % reflects open Critical/High findings
- No fabricated file paths, line numbers, branch names, or commit hashes anywhere
- No secret values appear anywhere in any audit file
- Markdown and HTML will contain the same substantive conclusions

## Output requirements

Generate two deliverables. **Because the full report is long, write it incrementally
to disk section-by-section rather than composing it entirely in one response** — draft
each major section as its own file write, so nothing is lost if generation is
interrupted.

1. **Markdown** (`TallaEgg-Audit-Report.md`) — full report in English, all sections
   below, no artificial shortening.
2. **Standalone HTML** (`TallaEgg-Audit-Report.html`) — single file, no external CSS/
   JS dependencies, works offline. Dark mode, sticky sidebar/TOC, severity badges,
   score cards, finding cards, roadmap visualization, print-friendly, responsive.
   Same substantive content as the Markdown, not a summary of it.

### Required sections (both formats)
Executive Summary; Audit Metadata; Audit Methodology; Repository Overview;
Architecture Reconstruction; Dependency Analysis; SOLID Analysis; Design Quality;
Domain Model; Financial Domain Audit; Transaction Integrity; API Audit; Security
Audit; Database/EF Core Audit; Async/Concurrency Audit; Performance Audit; Error
Handling; Logging/Observability; Validation; Testability; Debuggability;
Maintainability; Extensibility; Code Smells; Consistency; Modern C#/ASP.NET Core
Practices; Positive Findings; Finding Register (master table, every finding);
Technical Debt; Production Readiness; Refactoring Roadmap; Top 10 Strengths; Top 10
Weaknesses; Top 20 Improvements; Final Scores; Final Risk Assessment; Architecture
Maturity; Final Recommendation.

### Audit Metadata table (populate with real values only)

| Audit Metadata | Value |
|---|---|
| Audit Type | AI-Assisted Architecture & Code Audit |
| Audit Date | *(actual date this phase ran)* |
| AI Provider | *(actual provider available in this environment)* |
| AI Model | *(actual model identity if available; otherwise: "Exact AI model identity was not available in the audit execution environment.")* |
| Audit Methodology | Independent Evidence-Based Repository Audit (Phased) |
| Repository | *(from Phase 0)* |
| Branch | *(from Phase 0, real git output or "unavailable")* |
| Commit | *(from Phase 0, real git output or "unavailable")* |
| Framework | *(from Phase 0)* |
| Audit Scope | *(what was actually covered — note any parts of the repo not reachable)* |
| Human Review Status | Not yet reviewed by a human engineer |
| Audit ID | TALLAEGG-AUDIT-[YYYYMMDD]-[SHORT-ID], real date |
| Methodology Version | 2.0 (phased) |

### Mandatory disclosure statement (include verbatim, both formats)

> This audit was performed with the assistance of Artificial Intelligence and is
> based on analysis of the source code, project structure, dependencies,
> configuration, database-related artifacts, tests, and other repository artifacts
> available within the defined audit scope. The findings represent an evidence-based
> engineering assessment and should be validated by qualified human engineers before
> making critical production, security, financial, or architectural decisions.

### AI Limitations section (include)
Note plainly that AI analysis may miss issues that depend on runtime-only behavior,
production traffic patterns, infrastructure/environment configuration, undocumented
business requirements, external service behavior, active-exploitation-only
vulnerabilities, or production-only race conditions — and that this doesn't excuse
shallow analysis in the areas that were reachable.

### No false certification
Never state the system is "completely secure," "guaranteed production-ready," or
"perfect." Use evidence-based phrasing: "The reviewed code does not show evidence
of...", "Within the audited scope, no implementation of X was identified,"
"Additional runtime or penetration testing is recommended for confirmation."

---

# GLOBAL RULES (apply across every phase)

- Language: English only, everywhere in every output file — including HTML UI text.
  Do not translate identifiers, file paths, namespaces, or code snippets.
- Start from zero: ignore any pre-existing audit docs, TODOs, or prior AI conclusions
  in the repo unless explicitly doing the optional post-hoc comparison after the
  independent audit is complete.
- Don't recommend a pattern (Repository, CQRS, DDD, microservices, interfaces
  everywhere) unless there's a concrete problem it solves in *this* codebase.
  Absence of a pattern is not automatically a defect.
- Don't claim a security or performance issue without evidence; don't fabricate line
  numbers, branch names, commits, or vulnerability data.
- Never include a real secret value in any audit file, at any severity.
- A style preference is not an engineering defect — say so explicitly when a finding
  is a trade-off rather than a problem.
