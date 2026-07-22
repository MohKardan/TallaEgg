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
- Current sprint priorities (3-week cycle):
  - **Weeks 1-2**: Security & financial integrity (critical path)
  - **Week 3**: Architecture & API quality

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

### Critical Change Criteria
- Changes to financial transactions
- Security-related changes (secrets, TLS, authentication)
- Breaking API changes
- Database schema changes

**For Critical Changes**: Use pair-programming or require 2 reviewers

---

## Immediate Next Tasks (Sprint Start)

Execute in order; current sprint cycle: July 17 — July 31, 2026.

### Task 1: Rotate Secrets (1–2 days)
**Owner**: Dev B (execution), Dev A (Git cleanup)  
**Status**: BLOCKED until approval  
**Details**: See `SPRINT_PLAN.md` section "Immediate Tasks"

- [ ] Generate new API key and Telegram bot token
- [ ] Remove old credentials from Git history
- [ ] Update configuration template in `config/appsettings.template.json`
- [ ] Update deployment runbook

### Task 2: Gate TLS Validation & Restrict CORS (0.5–1 day)
**Owner**: Dev B  
**Status**: READY  
**Details**: 

- [ ] Wrap TLS bypass code in `#if DEBUG` directive
- [ ] Set CORS to whitelist only allowed origins (not `AllowAnyOrigin`)
- [ ] Test locally and on staging
- [ ] Create PR with security label

### Task 3: Quarantine Stub Endpoints (1–2 days)
**Owner**: Dev A  
**Status**: READY  
**Details**:

- [ ] Identify stub endpoints returning hardcoded/fake data
- [ ] Replace logic with `return StatusCode(501, "Not Implemented")` + feature flag
- [ ] Add unit test verifying quarantine behavior
- [ ] Create PR, link to audit finding C-8

### Task 4: Transaction Atomicity for Wallet Operations (3–5 days)
**Owner**: Dev A (primary)  
**Status**: IN PROGRESS  
**Details**: See `SPRINT_PLAN.md` section "Sprint 1 — Financial Integrity"

- [ ] Implement single `IDbContextTransaction` for `ApplyTradeAsync`
- [ ] Add `RowVersion` to `WalletEntity` and `Order`
- [ ] Handle `DbUpdateConcurrencyException` with retry
- [ ] Write integration tests for concurrent scenarios
- [ ] Deploy to staging with feature flag disabled

---

## Sprint Structure (3 Weeks)

### Week 1: Immediate Security Fixes
- Tasks 1–3 above
- Daily code review during standup
- Smoke test on staging Friday afternoon

### Week 2: Financial Integrity
- Task 4 + DI cleanup (Task 5)
- Integration tests for wallet/order flows
- Staging acceptance testing Wednesday–Friday

### Week 3: API & Architecture Quality
- Global error middleware
- API standardization (ProblemDetails, HTTP codes)
- Documentation updates

---

## When You're Blocked

1. **Log it**: Update task in Kanban ("Blocked" column)
2. **Communicate**: Mention blocker in next standup
3. **Escalate**: If blocked > 2 hours, escalate to Tech Lead
4. **Switch context**: Pull next task from "Ready" if available
5. **Document**: Leave detailed comment on Kanban card

---

## Deployment to Staging

### Manual Deployment Process
1. PR merged to `staging` branch
2. Run: `./publish-all.ps1` (from `publishes/` folder)
3. Execute `start-all-services.ps1` on staging server
4. Smoke test endpoints per `docs/operations/SMOKE_TEST.md`
5. Run integration tests: `dotnet test --filter Category=Integration`

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
