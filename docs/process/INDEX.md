# TallaEgg Documentation Index — Master Guide

**Last Updated**: July 17, 2026  
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
1. **Start here**: [`docs/process/WORKFLOW.md`](WORKFLOW.md) — Daily standup, task selection, PR process
2. **Sprint tasks**: [`docs/process/SPRINT_PLAN.md`](SPRINT_PLAN.md) — Detailed task breakdown, ownership, acceptance criteria
3. **Code standards**: [`docs/process/STANDARDS.md`](STANDARDS.md) — Naming conventions, comments, PR checklist

### For Problem-Solving
- **Why are we doing this?** → [`docs/operations/AUDIT_FINDINGS.md`](../operations/AUDIT_FINDINGS.md) — Critical findings and rationale
- **How should I write code?** → [`docs/process/STANDARDS.md`](STANDARDS.md) — Code style, naming, testing
- **What's the architecture?** → [`docs/architecture/ROADMAP.md`](../architecture/ROADMAP.md) for where things are headed; `SoftwareArchitecture/` (repo root) for current component/class/ER/sequence diagrams

### For Onboarding
- **New to the team?** → Read in this order:
  1. This file (you're reading it)
  2. [`docs/process/STANDARDS.md`](STANDARDS.md)
  3. [`docs/process/WORKFLOW.md`](WORKFLOW.md)
  4. [`docs/process/SPRINT_PLAN.md`](SPRINT_PLAN.md)

---

## Documentation Structure

### Existing today

```
docs/
├── CODE_AUDIT_REPORT.html        ← Full audit report (raw)
├── process/
│   ├── INDEX.md (this file)
│   ├── STANDARDS.md              ← Code, naming, folder structure conventions
│   ├── WORKFLOW.md               ← Daily process, standup, PR review, Kanban
│   ├── SPRINT_PLAN.md            ← Task breakdown, ownership, estimates, acceptance criteria
│   └── PR_TEMPLATE.md            ← Copy this for every PR
├── architecture/
│   └── ROADMAP.md                ← Post-Sprint-1 roadmap (bot → web app migration)
└── operations/
    └── AUDIT_FINDINGS.md         ← Critical/high/medium findings from code audit
```

### Planned (not yet created — do not link to these until they exist)

- `docs/process/METRICS.md` — weekly metrics tracking (lead time, deployment freq, etc.)
- `docs/architecture/ADR-###-*.md` — Architecture Decision Records
- `docs/architecture/DIAGRAMS.md` — component, data flow, sequence diagrams (note: `SoftwareArchitecture/` at repo root already has component/class/ER/sequence/activity/state diagrams — check there first before creating a duplicate)
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

### [`AUDIT_FINDINGS.md`](../operations/AUDIT_FINDINGS.md) — **Why We're Doing This**
**Purpose**: Reference critical findings from code audit (4.6/10 score, 30% prod-ready).

**Covers**:
- 9 CRITICAL findings (C-1 through C-9)
- 4 HIGH-priority findings
- Strengths/points of leverage
- Roadmap summary (Sprint 1–3 targets)
- Metrics tracking

**When to use**:
- To understand the "why" behind sprint priorities
- During task planning (link findings to tasks)
- In PR descriptions (reference finding codes like C-4, H-1)

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
A: See [`AUDIT_FINDINGS.md`](../operations/AUDIT_FINDINGS.md), findings C-1, C-2, C-7, C-9.

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

## Next Steps (Right Now)

1. **Dev A & Dev B**: Read this index + `STANDARDS.md` (30 min)
2. **Tech Lead**: Review `SPRINT_PLAN.md` for clarity (20 min)
3. **Team**: Hold 30-min kickoff: confirm sprint goal, task assignments, blockers
4. **Dev B**: Start TASK-001 (Rotate Secrets) — highest priority
5. **Dev A**: Start TASK-004 (Financial Integrity) — already in progress, continue with atomicity focus

---

**Repository**: [TallaEgg GitHub](https://github.com/YourOrg/TallaEgg)  
**Sprint Duration**: 2 weeks  
**Current Sprint**: Sprint 1 (July 17–31, 2026)  
**Target Score**: 4.6 → 6.5/10 (60%+ production-ready)

Good luck! 🚀
