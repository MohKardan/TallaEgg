# TallaEgg Project Standards & Conventions

## Overview
This document defines all standards and conventions for the TallaEgg project to ensure consistency, maintainability, and quality across the codebase and documentation.

---

## 1. Language Standards

### Code Comments & Strings
- **All code comments MUST be in English**
- Exception: user-facing error messages can be localized per deployment configuration
- Exception: comments referencing external Farsi documentation may link to external sources

### Documentation Files
- **All process/standards documentation MUST be in English** (this shared framework must be
  readable by both the team and AI coding agents, so one common language is required)
- Use standard Markdown formatting
- Use American English spelling conventions
- **Exception**: source/reference artifacts written for a specific audience may be in Farsi
  (e.g. `docs/CODE_AUDIT_REPORT.html`). When they are, the authoritative English summary is the
  one linked from `INDEX.md` (for the audit: `operations/AUDIT_FINDINGS.md`).

---

## 2. Folder Structure & Naming Conventions

### Solution Structure

This reflects the repository **as it is today**, not an idealized target. Two things below are
known tech debt flagged by the audit (see `operations/AUDIT_FINDINGS.md` M-1) and are slated for
cleanup in TASK-010 — they are marked ⚠️:

```
TallaEgg/
├── src/                              # Each Bounded Context: {Name}.Api/.Application/.Core/.Infrastructure
│   ├── User/                         #   → Users.Api, Users.Application, Users.Core, Users.Infrastructure
│   ├── Wallet/                       #   → Wallet.Api, Wallet.Application, Wallet.Core, Wallet.Infrastructure
│   ├── Order/                        #   → Orders.Api, Orders.Application, Orders.Core, Orders.Infrastructure
│   │   └── Orders/                   #   ⚠️ duplicate/legacy folder, not in .sln — remove (TASK-010)
│   ├── Affiliate/                    #   → Affiliate.Api, Affiliate.Application, Affiliate.Core, Affiliate.Infrastructure
│   └── TallaEgg/                     # Shared kernel + API gateway (TallaEgg.Api/.Application/.Core/.Infrastructure)
├── TelegramBot/                      # ⚠️ at repo ROOT, not under src/ (TallaEgg.TelegramBot + .Application/.Core/.Infrastructure)
├── tests/                            # → TallaEgg.AllServices.Tests — the solution's only test project, covers every service
├── docs/
│   ├── CODE_AUDIT_REPORT.html        # Full audit report (see operations/AUDIT_FINDINGS.md for the summary)
│   ├── architecture/                 # Architecture documentation (ROADMAP.md)
│   ├── operations/                   # AUDIT_FINDINGS.md; runbooks/deployment (planned)
│   └── process/                      # Development process standards (this file, INDEX, WORKFLOW, SPRINT_PLAN, PR_TEMPLATE)
└── TallaEgg.sln
```

**Target for new bounded contexts**: place the four layers under `src/{Context}/`. The `TelegramBot`
placement at repo root is existing history, not a pattern to copy.

### Naming Conventions

#### C# Code
- **Namespaces**: PascalCase, hierarchical (e.g., `TallaEgg.Wallet.Application.Services`)
- **Classes**: PascalCase (e.g., `WalletService`, `OrderEntity`)
- **Interfaces**: PascalCase with leading `I` (e.g., `IWalletService`, `IOrderRepository`)
- **Methods**: PascalCase (e.g., `ApplyTradeAsync`, `GetOrderByIdAsync`)
- **Properties**: PascalCase (e.g., `UserId`, `Balance`)
- **Private fields**: camelCase with leading underscore (e.g., `_logger`, `_dbContext`)
- **Constants**: UPPER_SNAKE_CASE (e.g., `TRANSACTION_TIMEOUT_MS`)
- **Local variables**: camelCase (e.g., `walletId`, `totalAmount`)

#### File Names
- **Class files**: Match class name (e.g., `WalletService.cs`)
- **Interface files**: Match interface name (e.g., `IWalletService.cs`)
- **Test files**: `{ClassName}Tests.cs` (e.g., `WalletServiceTests.cs`)
- **Project folders**: PascalCase matching project name

#### Branch Names (Git)
- **Feature**: `feat/{description}` (e.g., `feat/add-wallet-transaction`)
- **Bugfix**: `fix/{description}` (e.g., `fix/null-reference-wallet`)
- **Hotfix**: `hotfix/{description}` (e.g., `hotfix/secrets-rotation`)
- **Refactor**: `refactor/{description}` (e.g., `refactor/di-cleanup`)
- **Release**: `release/{version}` (e.g., `release/v1.0.0`)

---

## 3. Development Process Standards

### Scope Discipline
- Keep every change to the smallest scope that accomplishes the task at hand.
- **Never rewrite or refactor code that was not part of the request** — not adjacent code that
  looks untidy, not a naming convention that disagrees with this document, not tests that would
  read better written another way.
- If something nearby looks wrong, flag it and let the task owner decide whether it becomes
  separate work. Do not fix it inline as part of an unrelated change.
- New code follows this document; existing code that predates or contradicts it is left as-is
  until a task specifically asks for it to change.
- Applies equally to human developers and AI coding agents.

### Definition of Done (for each task)
- [ ] Code compiles without warnings
- [ ] All unit tests written and passing
- [ ] Code follows naming conventions and formatting standards
- [ ] Comments in English; no hardcoded secrets/tokens
- [ ] Feature flag added (if feature is incomplete/risky)
- [ ] Integration tests run on staging successfully
- [ ] PR created with descriptive title and linked issue
- [ ] At least one peer review completed
- [ ] For security/critical changes: second reviewer or pair-programming session
- [ ] Documentation updated (README, Runbook, API docs)
- [ ] Committed to version control with descriptive message

### PR (Pull Request) Standards

#### Branch Naming
Follow Git branch naming conventions above.

#### Commit Message Format
```
<type>(<scope>): <subject> — <ticket-ref>

<body>

<footer>
```

Example:
```
feat(wallet): implement optimistic concurrency control — TASK-004

Added RowVersion timestamp to WalletEntity to detect and handle
concurrent modifications. Implemented DbUpdateConcurrencyException
handler with exponential backoff retry logic.

Tested with concurrent transaction scenarios.
Closes TASK-004
```

**Types**: feat, fix, refactor, docs, test, chore, hotfix

#### PR Title Format
```
[Type][Priority] Subject — reference

Examples:
- [Hotfix][Critical] Rotate API keys and database secrets — TASK-001
- [Feat] Implement transaction atomicity for wallet operations — TASK-004
- [Refactor] Extract wallet clients to interfaces (DIP) — TASK-005
```

#### PR Description Template
```markdown
## Description
Brief summary of changes.

## Issue/Task Reference
Closes TASK-###

## Changes Made
- Bullet point for each significant change
- ...

## Testing
- [ ] Unit tests added/updated
- [ ] Integration tests run on staging
- [ ] No secrets/tokens in diff

## Security Checklist (if applicable)
- [ ] No hardcoded credentials
- [ ] No TLS bypass in production code
- [ ] No SQL injection vulnerabilities
- [ ] Input validation present

## Deployment Notes
Steps to deploy or rollback if needed.
```

#### WIP Limit & Kanban
- **Status columns**: Backlog → Ready → Doing → Review → Testing → Done
- **WIP Limit**: Maximum 2 tasks per developer in "Doing"
- **Daily Standup**: 15 minutes, covering blockers and handoffs

---

## 4. Documentation Standards

### Markdown Format
- Use American English spelling
- Use clear, concise language
- Include examples where helpful
- Number lists for procedures, bullets for properties

### Documentation Types & Locations

#### Architecture Documentation (`docs/architecture/`)
- Architecture Decision Records (ADRs): `ADR-###-{title}.md`
- Component diagrams (Mermaid or images)
- Data flow diagrams
- Example: `ADR-001-microservices-with-database-per-service.md`

#### Design Documentation (`docs/design/`)
- API contracts (OpenAPI/Swagger specs)
- Database schemas
- DDD domain models

#### Operations Documentation (`docs/operations/`)
- Deployment guides
- Runbooks for common operations
- Incident response playbooks
- Monitoring & alerting setup

#### Process Documentation (`docs/process/`)
- Development workflow (WORKFLOW.md)
- Sprint planning guide
- Development standards (this file)

### README.md Template
```markdown
# [Service Name]

## Overview
Brief description of the service.

## Architecture
Link to architecture docs.

## Quick Start
```bash
# Build
dotnet build

# Test
dotnet test

# Run
dotnet run --project [Project]
```

## API Endpoints
Link to API documentation.

## Development
- See `docs/process/WORKFLOW.md`
- Code standards: `docs/process/STANDARDS.md`

## Deployment
- See `docs/operations/DEPLOYMENT.md`
```

---

## 5. Code Quality Standards

### Comments
- Write comments in English only
- Avoid obvious comments; comment the "why", not the "what"
- Use XML documentation for public APIs

Example (GOOD):
```csharp
// Retry up to 3 times if wallet is locked by another transaction
// to avoid Lost Update anomalies in high-concurrency scenarios
await RetryWithBackoffAsync(async () => {
    // ...
}, maxRetries: 3);
```

Example (BAD):
```csharp
// این متد برای خواندن موجودی است
// Loop through all transactions
```

### Code Formatting
- Use EditorConfig (`.editorconfig` at repository root)
- C# formatting: follow Microsoft guidelines or Roslyn analyzers (StyleCop)
- Indentation: 4 spaces
- Line length: prefer < 120 characters

### Testing Standards
- Unit tests: fast, isolated, mock external dependencies
- Integration tests: use in-memory database or Docker for staging
- Test naming: `MethodName_Scenario_ExpectedResult` (e.g., `ApplyTradeAsync_InsufficientBalance_ThrowsException`)
- Code coverage target: >80% for critical paths (financial operations)

---

## 6. Security Standards

### Secrets Management
- **NEVER** commit secrets to Git
- Use environment variables or Secret Manager
- Rotate secrets quarterly
- Audit secret access in production

### Code Review Checklist for Security
- [ ] No hardcoded passwords, API keys, or tokens
- [ ] No TLS verification bypass in production
- [ ] Input validation on all external inputs
- [ ] SQL injection protections (parameterized queries)
- [ ] CORS properly configured (not AllowAnyOrigin in production)
- [ ] Authentication/authorization checks in place

---

## 7. Git Workflow

### Branching Strategy
- Main branch: `main` (production-ready, deployed)
- Staging branch: `staging` (pre-production testing) — ⚠️ **not yet created**; today branches are cut from and merged to `main`. Create `staging` when the deployment pipeline is set up.
- Feature/fix branches: created from `staging` once it exists (from `main` until then), merged back after review
- Hotfixes: created from `main`, merged to both `main` and `staging`

### Commit Guidelines
- Atomic commits: each commit should be logically independent
- Frequent commits for checkpoints, but squash before merge
- Descriptive messages (follow format in section 3)
- Never force-push to shared branches

---

## 8. Tools & Configuration

### Required Tools
- Visual Studio 2022+ or VS Code + OmniSharp
- .NET 9 SDK
- SQL Server 2019+ or Docker
- Git 2.30+

### Code Analysis
- Enable nullable reference types in all projects
- StyleCop analyzers (via NuGet package)
- SonarAnalyzer for C#

### CI/CD *(target — not yet configured)*
- GitHub Actions or Azure Pipelines
- Automated: build, test, security scan
- Manual approval before production deployment

---

## 9. Documentation Maintenance

This standards document should be reviewed and updated:
- Quarterly or when major process changes occur
- When adding new tools or technologies
- During sprint retrospectives if standards inhibit productivity

---

## 10. Onboarding Checklist

New developers should:
- [ ] Read this entire standards document
- [ ] Read `docs/architecture/` to understand system design
- [ ] Read `docs/process/WORKFLOW.md` for development flow
- [ ] Clone repository and run `dotnet build`
- [ ] Run all tests locally
- [ ] Set up local development environment per `docs/operations/DEV_SETUP.md`
- [ ] Attend code review session to see standards in practice
