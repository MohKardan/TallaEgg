# Development Workflow & Process Guide

## Purpose
This document defines the daily workflow for a two-person development team working on TallaEgg, ensuring clarity, minimal coordination overhead, and adherence to Lean Software Development principles.

---

## Source of Truth
**Primary Reference**: `docs/process/SPRINT_PLAN.md`
- Contains detailed task assignments, sprint goals, and dependencies
- Updated at sprint start and reviewed during retrospectives

**Supporting References**:
- `docs/operations/AUDIT_FINDINGS.md` — Why priorities are ordered as they are
- `docs/architecture/ROADMAP.md` — High-level feature roadmap
- `docs/CODE_AUDIT_REPORT.html` — Detailed technical findings

---

## Daily Workflow

### 1. Daily Standup (15 minutes)
**When**: Start of workday  
**Participants**: Dev A, Dev B

Agenda:
- What did I complete yesterday?
- What am I working on today?
- What blockers do I have?
- Any handoffs or dependencies?

**Output**: Update Kanban board (move cards to "Doing", note blockers in comments)

### 2. WIP (Work In Progress) Limits
- **Maximum per developer**: 2 tasks in "Doing" column
- **Rationale**: Minimizes context switching, ensures focus
- If blocked: move to "Blocked" column, update comment with reason

### 3. Kanban Board Columns
1. **Backlog**: Tasks ready to start (dependencies met)
2. **Ready**: Approved, clear acceptance criteria, ready for dev
3. **Doing**: Currently being worked on (max 2 per person)
4. **Review**: Awaiting code review from peer
5. **Testing**: On staging/QA for validation
6. **Done**: Merged, deployed, verified

### 4. Task Selection (Pull Model)
- Developers pull tasks from "Ready" column
- Prioritize based on order in `SPRINT_PLAN.md`
- Sprint priorities (3 sprints × 2 weeks; see `SPRINT_PLAN.md`):
  - **Sprint 1**: Security & financial integrity (critical path)
  - **Sprint 2**: Architecture & API quality
  - **Sprint 3**: Performance, cleanup & documentation

---

## Code Review & PR Process

### PR Checklist (Definition of Done)
Before opening PR, ensure:
- [ ] Code compiles without warnings
- [ ] All tests pass locally (`dotnet test`)
- [ ] No secrets/tokens in code
- [ ] Comments are in English
- [ ] Follows naming conventions (see STANDARDS.md)
- [ ] Feature flag added (if feature incomplete)
- [ ] Commit message follows format
- [ ] Updated relevant documentation

### PR Title & Description
Use template from `docs/process/STANDARDS.md`, section 3.

### Review Process
1. Developer creates PR with description
2. Assign peer as reviewer
3. For critical changes: second reviewer required
4. Address review comments
5. Approver merges after passing CI

**Reviewers**: see [`CODE_REVIEW_GUIDE.md`](CODE_REVIEW_GUIDE.md) for how deep to review, the
project-specific red flags to check, and when to approve vs request changes.

### Critical Change Criteria
- Changes to financial transactions
- Security-related changes (secrets, TLS, authentication)
- Breaking API changes
- Database schema changes

**For Critical Changes**: Use pair-programming or require 2 reviewers

---

## Immediate Next Tasks (Sprint Start)

The task breakdown — owners, estimates, checklists, acceptance criteria — lives in the single
source of truth, [`SPRINT_PLAN.md`](SPRINT_PLAN.md). It is **not** duplicated here, so the two
can't drift. Live status (TODO / in progress / done) lives in branches and the board, not in any doc.

**Sprint 1 order**: TASK-001 (Rotate Secrets) → TASK-002 (Gate TLS / Restrict CORS) →
TASK-003 (Quarantine Stubs) → TASK-004 (Wallet Atomicity) → TASK-005 (Matching DI / Lock order).
Start each by opening its section in `SPRINT_PLAN.md`.

---

## Sprint Structure (3 Sprints × 2 Weeks)

### Sprint 1 — Security & Financial Integrity
- Immediate tasks (TASK-001..003) then TASK-004 (atomicity) + TASK-005 (matching DI / lock order)
- Daily code review during standup
- Integration tests for wallet/order flows

### Sprint 2 — API & Architecture Quality
- DIP client interfaces (TASK-006)
- Global error middleware / ProblemDetails (TASK-007)
- Financial integration test suite (TASK-008)

### Sprint 3 — Performance, Cleanup & Documentation
- Query optimization (TASK-009), dead-code removal (TASK-010)
- Runbook & operational playbooks (TASK-011)

---

## When You're Blocked

1. **Log it**: Update task in Kanban ("Blocked" column)
2. **Communicate**: Mention blocker in next standup
3. **Escalate**: If blocked > 2 hours, escalate to Tech Lead
4. **Switch context**: Pull next task from "Ready" if available
5. **Document**: Leave detailed comment on Kanban card

---

## Deployment to Staging

> ⚠️ **Target state — not yet set up.** There is currently no `staging` branch and no
> CI/CD. Until these exist, treat this section as the intended process, not today's reality.
> The concrete first step to make it real: create a `staging` branch and add
> `.github/workflows/` for build + test.

### Manual Deployment Process (target)
1. PR merged to `staging` branch
2. Run: `scripts\windows-services\publish-all.ps1` and `install-services.ps1` (see
   [`docs/operations/WINDOWS_DEPLOYMENT.md`](../operations/WINDOWS_DEPLOYMENT.md) — this is the
   real, verified production tooling from #70; a `publish-all.ps1` at the repo root and a
   `publishes/` folder predated it and have been removed)
3. Smoke test endpoints per `docs/operations/SMOKE_TEST.md` (planned)
4. Run integration tests: `dotnet test --filter Category=Integration`

### Automated CI/CD (Future)
- Implement GitHub Actions to auto-deploy on merge to `staging`
- Run integration tests post-deploy
- Notify team on Slack/Teams

---

## Metrics & Visibility

Track and report weekly:
- **Lead Time**: How long from "Ready" to "Done"
- **Cycle Time**: How long from "Doing" to "Done"
- **Deployment Frequency**: How many times deployed to staging/production per week
- **Change Failure Rate**: % of deployments causing issues
- **MTTR**: Mean time to recover from production incident

**Visibility**: Add metrics to `docs/process/METRICS.md` (updated Fridays)

---

## End-of-Sprint Retrospective

**When**: Every Friday, 4 PM (30 minutes)

**Agenda**:
1. What went well?
2. What could be improved?
3. What blockers did we hit?
4. Suggestions for next sprint

**Output**: Update process documentation or adjust task estimates

---

## Key Principles (Lean Software Development)

1. **Eliminate waste**: Remove meetings, async communicate where possible
2. **Amplify learning**: Pair-program on complex tasks, review code thoroughly
3. **Deliver fast**: Small PRs (<200 LOC), frequent deployments
4. **Empower team**: Decisions made at standup, not escalated unnecessarily
5. **Build quality in**: Tests first, code review mandatory, runbooks kept current
6. **Respect people**: Flexible schedules, async-first communication, celebrate wins

---

## Tools & Access

- **Version Control**: GitHub (TallaEgg organization)
- **Issue Tracking**: GitHub Issues (linked to PRs)
- **Communication**: Slack/Teams for async, standup via Zoom/Teams
- **Documentation**: Markdown in `docs/` folder
- **CI/CD**: GitHub Actions (or Azure Pipelines)
- **Staging Environment**: Azure/AWS (details in `docs/operations/INFRASTRUCTURE.md`)

---

## Questions?

- Process questions → Refer to this document or ask Tech Lead
- Technical questions → Refer to `docs/architecture/` or code comments
- Task clarification → Refer to `SPRINT_PLAN.md` or card acceptance criteria
