# TallaEgg Documentation Index — Master Guide

**Last Updated**: September 2, 2026  
**Language**: English (all documentation)  
**Standards**: Software Engineering Best Practices + Lean Development

This is the **single canonical source** for how work gets planned, built, and reviewed in this
repository — for human developers and AI coding agents alike. If a document elsewhere in the repo
conflicts with something here, this index (and the files it links to) wins; fix the conflicting
document instead of trusting it. AI agents: the root [`AGENT.md`](../../AGENT.md) points here for
process/standards context — read `STANDARDS.md` before writing code and `PR_TEMPLATE.md` before
opening a PR.

---

## Quick Navigation

### For Daily Work
1. **What to work on**: GitHub issues — `gh issue list`. That is where live priorities are; no document here tracks them.
2. **How work flows**: [`docs/process/WORKFLOW.md`](WORKFLOW.md) — task selection, PR process, review
3. **Code standards**: [`docs/process/STANDARDS.md`](STANDARDS.md) — Naming conventions, comments, PR checklist

> [`SPRINT_PLAN.md`](SPRINT_PLAN.md) is **history**, not a task list. Its three sprints ran
> July 17 – August 28, 2026 and are scored in [`docs/OKR.md`](../OKR.md).

### For Reviewing Someone Else's PR
- **How do I review this?** → [`docs/process/CODE_REVIEW_GUIDE.md`](CODE_REVIEW_GUIDE.md) — review depth, TallaEgg-specific red flags, when to approve vs request changes

### For Problem-Solving
- **Why are we doing this?** → [`docs/audit/README.md`](../audit/README.md) — The audit archive: what each audit found, the score trend, and how the next one is run
- **How should I write code?** → [`docs/process/STANDARDS.md`](STANDARDS.md) — Code style, naming, testing
- **What's the architecture?** → [`AGENT.md`](../../AGENT.md) for services, ports and layout; [`docs/architecture/DEALER_QUOTE_MODEL.md`](../architecture/DEALER_QUOTE_MODEL.md) for how trading actually works today — *that one is in Persian, under the §1 exception in [`STANDARDS.md`](STANDARDS.md); it has no English summary yet*; [`docs/architecture/ROADMAP.md`](../architecture/ROADMAP.md) for where things are headed

### For Onboarding
- **New to the team?** → Read in this order:
  1. This file (you're reading it)
  2. [`docs/process/STANDARDS.md`](STANDARDS.md) — how to write code here
  3. [`AGENT.md`](../../AGENT.md) — build commands, services, and the business rules that look like bugs
  4. [`docs/architecture/DEALER_QUOTE_MODEL.md`](../architecture/DEALER_QUOTE_MODEL.md) — how trading actually works *(Persian)*
  5. [`docs/process/WORKFLOW.md`](WORKFLOW.md) — how work flows
  6. `gh issue list` — what to pick up

---

## Documentation Structure

### Existing today

```
docs/
├── audit/
│   ├── README.md                  ← Archive index and score trend across all audits
│   ├── METHODOLOGY_v8.md          ← How the next audit is run (current methodology)
│   ├── METHODOLOGY_v7.md          ← Retired; kept because AUDIT_2026-08b.md was run under it
│   ├── METHODOLOGY_v1.md          ← The original July prompt, archived for comparability
│   ├── METHODOLOGY_v2–6.md         ← Drafts between v1 and v7; none was ever run
│   ├── AUDIT_2026-07.md           ← July 2026 audit — English summary
│   ├── AUDIT_2026-07.html         ← July 2026 audit — full report (Farsi)
│   ├── AUDIT_2026-08.md           ← August 2026 re-audit
│   └── AUDIT_2026-08b.md          ← August 2026 second pass (first independent audit)
├── process/
│   ├── INDEX.md (this file)
│   ├── STANDARDS.md              ← Code, naming, folder structure conventions
│   ├── WORKFLOW.md               ← Daily process, standup, PR review, Kanban
│   ├── SPRINT_PLAN.md            ← Task breakdown, ownership, estimates, acceptance criteria
│   ├── PR_TEMPLATE.md            ← Copy this for every PR (author side)
│   └── CODE_REVIEW_GUIDE.md      ← How to review a PR (reviewer side)
├── architecture/
│   ├── DEALER_QUOTE_MODEL.md     ← How trading works today: quotes, fills, market modes (Persian)
│   └── ROADMAP.md                ← Post-Sprint-1 roadmap (bot → web app migration)
├── operations/
│   └── WINDOWS_DEPLOYMENT.md     ← Windows deployment notes
└── OKR.md                        ← The July–August 2026 cycle and its closing scores (Persian)
```

### Planned (not yet created — do not link to these until they exist)

- `docs/process/METRICS.md` — weekly metrics tracking (lead time, deployment freq, etc.)
- `docs/architecture/ADR-###-*.md` — Architecture Decision Records
- `docs/architecture/DIAGRAMS.md` — component, data flow, sequence diagrams. Write these as Mermaid inside the Markdown, the way `DEALER_QUOTE_MODEL.md` does: a root `SoftwareArchitecture/` folder of PNGs was deleted for being unmaintainable and a year out of date. Should also cover the `OrderStatus` lifecycle, which that folder documented and nothing replaced.
- `docs/operations/DEPLOYMENT.md`, `docs/operations/RUNBOOK.md`, `docs/operations/INFRASTRUCTURE.md`
- `docs/design/API_CONTRACT.md`, `docs/design/DATABASE_SCHEMA.md`

When one of these is created, move its line into "Existing today" in the same PR.

---

## Key Documents & Purpose

### [`STANDARDS.md`](STANDARDS.md) — **Engineering Standards** 
**Purpose**: Define how code, documentation, and processes should look.

**Covers**:
- Language: English for code comments, documentation
- Folder structure: organized by Bounded Context
- Naming conventions: C# classes, interfaces, files, branches, commits
- Code quality: testing, security checklist, code review standards
- PR format: title, commit message, description template
- Tools & configuration: required software, CI/CD

**When to use**: 
- Before writing any code
- During code review (to check compliance)
- When onboarding (mandatory read)

---

### [`WORKFLOW.md`](WORKFLOW.md) — **Development Process**
**Purpose**: Day-to-day workflow for a two-person team using Lean & Kanban.

**Covers**:
- Daily standup (15 min, 3 questions)
- WIP limits (max 2 per developer)
- Kanban board columns (Backlog → Ready → Doing → Review → Testing → Done)
- How to select tasks (pull from "Ready", prioritize by sprint plan)
- PR review & merge process
- What to do when blocked
- Deployment to staging (manual process, then CI/CD)
- Metrics tracking (lead time, cycle time, deployment frequency)

**When to use**:
- Every morning (review what you're working on)
- Before pulling a task from backlog
- During standup or when blocked
- End of sprint for retrospective

---

### [`SPRINT_PLAN.md`](SPRINT_PLAN.md) — **Sprint Breakdown & Task Details**
**Purpose**: Detailed task decomposition for Sprint 1 (July 17–31).

**Covers**:
- Sprint goal (eliminate critical production blockers)
- Immediate tasks (3 security/stability tasks, days 1–2)
- Sprint 1 tasks (5 core tasks addressing critical findings)
- Sprint 2 tasks (6 quality/architecture tasks)
- Sprint 3 tasks (5 optimization/cleanup tasks)
- For each task:
  - Owner (Dev A or Dev B)
  - Estimated effort (in days)
  - What needs to be done
  - Definition of Done checklist
  - Acceptance Criteria
  - Testing strategy
  - Risk mitigation

**When to use**:
- Sprint kickoff (Monday morning of sprint start)
- When selecting next task
- For effort estimation
- For code review (verify acceptance criteria met)
- End of sprint (retrospective, compare estimates to actual)

---

### [`PR_TEMPLATE.md`](PR_TEMPLATE.md) — **Pull Request Standard**
**Purpose**: Consistent, high-quality PR submissions.

**Covers**:
- PR title format (`[Type][Priority] Subject — reference`)
- Description template
- Type of change (hotfix, feature, refactor, etc.)
- Testing checklist (unit, integration, staging)
- Security checklist
- Code quality verification
- Documentation updates
- Breaking changes statement
- Deployment notes & rollback plan
- Reviewer assignments
- Commit message format
- Final merge instructions

**When to use**:
- Copy the entire template into every PR description
- Fill out all sections before requesting review
- Use as checklist before clicking "Merge"

---

### [`docs/audit/`](../audit/README.md) — **Why We're Doing This**
**Purpose**: Every risk audit the project has run, one file per run, plus the methodology
for the next one. Start at [`README.md`](../audit/README.md) — it carries the score trend
and says which audit is most recent.

**Covers**:
- [`AUDIT_2026-07.md`](../audit/AUDIT_2026-07.md) — 9 CRITICAL findings (C-1–C-9), 4 HIGH; 4.6/10, 30% prod-ready. Full Farsi report alongside it as `.html`
- [`AUDIT_2026-08.md`](../audit/AUDIT_2026-08.md) — re-audit: 6.6/10, ~55%; also records which of its own findings were wrong
- [`AUDIT_2026-08b.md`](../audit/AUDIT_2026-08b.md) — 7.8/10, ~65%; the first audit run by a model that did not write the code
- [`METHODOLOGY_v8.md`](../audit/METHODOLOGY_v8.md) — how to run the next audit

**When to use**:
- To understand the "why" behind sprint priorities
- During task planning (link findings to tasks)
- In PR descriptions (reference finding codes like C-4, H-1)

**Never** edit an archived audit to mark work done — remediation status lives in issues
(`gh issue list --label audit-finding`).

---

## Task Flow: From Audit → Sprint → PR

```
1. Audit Finding (e.g., C-4: No Optimistic Concurrency)
   ↓
2. Sprint Plan Task (e.g., TASK-004: Implement Transaction Atomicity)
   - Owner: Dev A
   - Acceptance Criteria: RowVersion added, tests pass, concurrency handled
   ↓
3. Daily Work
   - Pull task into "Doing"
   - Write code following STANDARDS.md
   - Write tests following STANDARDS.md
   - Update WORKFLOW.md metrics
   ↓
4. PR Submission
   - Use PR_TEMPLATE.md
   - Link to TASK-004 and finding C-4
   - Checklist all items
   ↓
5. Review
   - Reviewer checks STANDARDS.md + PR_TEMPLATE.md
   - Verify acceptance criteria met
   - Second review for critical changes
   ↓
6. Merge & Verify
   - Squash-merge to staging
   - Run smoke tests per DEPLOYMENT.md (coming)
   - Close task in sprint plan
   ↓
7. Retrospective (Friday)
   - Compare estimate vs actual
   - Update METRICS.md
   - Plan next sprint
```

---

## Critical Standards to Know Immediately

### 1. Language
✅ **DO**: Write code comments in English  
✅ **DO**: Write all documentation in English  
✅ **DO**: Translate Farsi requirements to English before responding  
❌ **DON'T**: Mix Persian and English in code/docs

### 2. Naming Conventions
✅ **Classes**: `PascalCase` (e.g., `WalletService`)  
✅ **Methods**: `PascalCase` (e.g., `ApplyTradeAsync`)  
✅ **Private fields**: `_camelCase` (e.g., `_logger`)  
✅ **Constants**: `UPPER_SNAKE_CASE` (e.g., `TRANSACTION_TIMEOUT_MS`)  
❌ **DON'T**: Mix Hungarian notation or unclear abbreviations

### 3. Branching & Commits
✅ **Branch**: `feat/wallet-atomicity`, `hotfix/secrets-rotate`, `fix/null-reference`  
✅ **Commit**: `feat(wallet): implement optimistic concurrency — TASK-004`  
✅ **PR Title**: `[Feat][Critical] Implement transaction atomicity — TASK-004`  
❌ **DON'T**: Commit secrets, use unclear branch names, skip PR description

### 4. PR Checklist (Must-Do)
- [ ] No secrets, tokens, or passwords in code
- [ ] Comments in English explaining "why"
- [ ] Tests written (unit + integration)
- [ ] Code follows STANDARDS.md
- [ ] PR filled using PR_TEMPLATE.md
- [ ] Linked to audit finding or task
- [ ] Pair-reviewed (if critical)

### 5. WIP Limits (Daily Discipline)
- Never work on more than 2 tasks simultaneously
- If blocked: move to "Blocked", pull next task
- If done early: pull next task, don't multitask

### 6. Scope Discipline
✅ **DO**: Keep changes to the smallest scope that accomplishes the task
✅ **DO**: Flag unrelated issues you notice and let the task owner decide if they become work
❌ **DON'T**: Refactor, rename, or "clean up" code that wasn't part of the request

---

## Start-of-Sprint Checklist (for Tech Lead / Sprint Planner)

- [ ] Review SPRINT_PLAN.md tasks with team
- [ ] Assign owners (Dev A / Dev B)
- [ ] Verify acceptance criteria are clear
- [ ] Identify blockers & dependencies
- [ ] Set WIP limits and standup time
- [ ] Create GitHub Issues linked to tasks
- [ ] Set sprint end date & review meeting

---

## End-of-Sprint Checklist (Retrospective)

- [ ] How many tasks completed? (target: 80%+)
- [ ] Any critical blockers?
- [ ] Were estimates accurate?
- [ ] What went well? (do more)
- [ ] What was hard? (improve process)
- [ ] Update METRICS.md with results
- [ ] Plan next sprint
- [ ] Celebrate wins! 🎉

---

## FAQ

**Q: I have a question about code standards. Where do I look?**  
A: [`STANDARDS.md`](STANDARDS.md), section on code style, naming, or testing.

**Q: My task is blocked. What do I do?**  
A: See [`WORKFLOW.md`](WORKFLOW.md), section "When You're Blocked".

**Q: I'm about to open a PR. What should I check?**  
A: Copy [`PR_TEMPLATE.md`](PR_TEMPLATE.md), fill out all sections, verify checklist items.

**Q: Why is security so important in TASK-001?**  
A: See [`AUDIT_2026-07.md`](../audit/AUDIT_2026-07.md), findings C-1, C-2, C-7, C-9.

**Q: How do I estimate effort for a new task?**  
A: Compare to similar tasks in [`SPRINT_PLAN.md`](SPRINT_PLAN.md); use past METRICS.md data.

**Q: What's the rollback plan if a deployment fails?**  
A: See [`PR_TEMPLATE.md`](PR_TEMPLATE.md), section "Rollback Plan" (fill in per task).

---

## Document Maintenance

This index & all standards documents should be reviewed:
- **Quarterly** or when major process changes occur
- **During Sprint Retros** if standards inhibit velocity
- **When Adding New Tools** (technologies, libraries, frameworks)

---

## Onboarding (start here)

1. Read this index + [`STANDARDS.md`](STANDARDS.md).
2. Read [`AGENT.md`](../../AGENT.md) for build commands, services and the business rules that
   look like bugs, and [`docs/architecture/DEALER_QUOTE_MODEL.md`](../architecture/DEALER_QUOTE_MODEL.md)
   for how trading works.
3. Pick up work from GitHub issues — `gh issue list`. That, not any document here, is where
   current priorities live.

---

**Repository**: [MohKardan/TallaEgg](https://github.com/MohKardan/TallaEgg)

> The three-sprint cycle this document was written around ran July 17 – August 28, 2026 and is
> closed. Its scoring and retrospective are in [`docs/OKR.md`](../OKR.md); `SPRINT_PLAN.md` is
> kept as the record of what was planned, not as current work. **Live priorities are GitHub
> issues** — `gh issue list`. Treat any status, sprint or date claim in the process documents as
> history unless an issue confirms it.
