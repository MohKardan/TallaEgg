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
  (e.g. `docs/audit/AUDIT_2026-07.html`). When they are, the authoritative English summary is the
  one linked from `INDEX.md` (for the audit: `audit/AUDIT_2026-07.md`).

---

## 2. Folder Structure & Naming Conventions

### Solution Structure

This reflects the repository **as it is today**, not an idealized target. The one item marked ⚠️
is known tech debt.

```
TallaEgg/
├── src/                              # Each Bounded Context: {Name}.Api/.Application/.Core/.Infrastructure
│   ├── User/                         #   → Users.Api, Users.Application, Users.Core, Users.Infrastructure
│   ├── Wallet/                       #   → Wallet.Api, Wallet.Application, Wallet.Core, Wallet.Infrastructure
│   ├── Order/                        #   → Orders.Api, Orders.Application, Orders.Core, Orders.Infrastructure
│   ├── Affiliate/                    #   → Affiliate.Api, Affiliate.Application, Affiliate.Core, Affiliate.Infrastructure
│   │                                 #     Affiliate.Api is not deployed (no migrations); Affiliate.Core
│   │                                 #     is a ProjectReference of Users.Core and cannot be removed
│   └── TallaEgg/                     # Shared kernel + TallaEgg.Api, which maps no endpoints
├── TelegramBot/                      # ⚠️ at repo ROOT, not under src/
│                                     #   → .Core (models), .Infrastructure (the runnable bot), .Simulator
├── tests/                            # → TallaEgg.AllServices.Tests — the solution's only test project, covers every service
├── config/                           # appsettings.global.json — shared by every service, git-ignored
├── scripts/                          # windows-services/ publish, install, uninstall; one data migration
├── docs/
│   ├── audit/                        # Audit archive + current methodology (see audit/README.md)
│   ├── architecture/                 # DEALER_QUOTE_MODEL.md (how trading works), ROADMAP.md
│   ├── operations/                   # Runbooks/deployment (WINDOWS_DEPLOYMENT.md)
│   ├── process/                      # This file, INDEX, WORKFLOW, PR_TEMPLATE, CODE_REVIEW_GUIDE
│   ├── pull-requests/                # Archived PR records — never edited
│   ├── business/                     # Business proposal
│   └── OKR.md                        # The July–August 2026 cycle and its closing scores
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
- **Docs**: `docs/{description}` (e.g., `docs/correct-port-table`)
- **Chore**: `chore/{description}` (e.g., `chore/remove-root-residue`)
- **Release**: `release/{version}` (e.g., `release/v1.0.0`)

This is the whole list — a prefix not on it is a mistake, not a judgement call.

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
- [ ] All unit tests written and passing (`dotnet test TallaEgg.sln`)
- [ ] Code follows naming conventions and formatting standards
- [ ] Comments in English; no hardcoded secrets/tokens
- [ ] Feature flag added (if feature is incomplete/risky)
- [ ] Exercised against the running stack where behaviour changed
      (`driver.ps1 start` then `driver.ps1 smoke`)
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
feat(wallet): implement optimistic concurrency control — issue #143

Added RowVersion timestamp to WalletEntity to detect and handle
concurrent modifications. Implemented DbUpdateConcurrencyException
handler with exponential backoff retry logic.

Tested with concurrent transaction scenarios.
Closes #143
```

**Types**: feat, fix, refactor, docs, test, chore, hotfix

#### PR Title Format
```
[Type][Priority] Subject — reference

Examples:
- [Hotfix][Critical] Rotate API keys and database secrets — issue #33
- [Feat] Implement transaction atomicity for wallet operations — issue #41
- [Refactor] Extract wallet clients to interfaces (DIP) — issue #36
```

#### PR Description Template
```markdown
## Description
Brief summary of changes.

## Issue/Task Reference
Closes #___  (`gh issue list` — issues are the only tracker)

## Changes Made
- Bullet point for each significant change
- ...

## Testing
- [ ] Unit tests added/updated
- [ ] Exercised against the running stack where behaviour changed
- [ ] No secrets/tokens in diff

## Security Checklist (if applicable)
- [ ] No hardcoded credentials
- [ ] No TLS bypass in production code
- [ ] No SQL injection vulnerabilities
- [ ] Input validation present

## Deployment Notes
Steps to deploy or rollback if needed.
```

#### Work in progress
- Finish or hand off one piece of work before starting the next.
- Blocked for more than an hour? Say so on the issue and pick up something else.

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
- Integration tests: in-memory SQLite stands in for the database; test doubles are hand-written (no mocking library)
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
- **There is no `staging` branch.** Branches are cut from `main` and squash-merged back to it, and
  `main` is what ships. Deployment is manual — see `../operations/WINDOWS_DEPLOYMENT.md`.
- Feature/fix/hotfix branches: cut from `main`, merged back after CI is green and review is done

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

### CI
- `.github/workflows/build-and-test.yml` runs `dotnet build` and `dotnet test TallaEgg.sln` on
  every pull request, and on pushes to `main`. It is the `test` check the `main` ruleset requires.
  The admin role bypasses every rule, so treat it as a gate you keep, not one that holds you.
  It also runs `scripts/check-doc-paths.sh`, which fails the build when a tracked text file
  contains a Markdown link to a repository path that does not exist. Run it locally the same way
  — it needs no SDK — and `scripts/check-doc-paths.sh --self-test` to exercise its parser.
  Exempt as frozen archives: `docs/audit/AUDIT_*`, `docs/audit/METHODOLOGY_v*`,
  `docs/pull-requests/*` and `governance/*`. Note `docs/audit/README.md` is *not* exempt — it is
  a living index. If you add another frozen archive, add its path to `EXCLUDED_GLOBS` in the
  script.
- `.github/workflows/manual-test-run.yml` stands Users/Wallet/Orders and the bot up against a
  throwaway SQL Server so a human can drive it over real Telegram. It asserts nothing and needs
  the `TELEGRAM_BOT_TOKEN` and `OWNER_TELEGRAM_ID` secrets.
- **Not configured**: automated security scanning, and any deploy step. Deployment is manual —
  see [`../operations/WINDOWS_DEPLOYMENT.md`](../operations/WINDOWS_DEPLOYMENT.md).

---

## 9. Documentation Maintenance

This standards document should be reviewed and updated:
- When a claim here turns out to be false — fix it in the PR that discovered it
- When adding a tool, or changing how the project is built, run or deployed

---

## 10. Onboarding Checklist

New developers should:
- [ ] Read this entire standards document
- [ ] Read `docs/architecture/` to understand system design
- [ ] Read `docs/process/WORKFLOW.md` for development flow
- [ ] Clone repository and run `dotnet build`
- [ ] Run all tests locally
- [ ] Set up the local environment per [`README.md`](../../README.md) → Prerequisites and
      Configuration: .NET 9 SDK, SQL Server Express, and a `config/appsettings.global.json`
      copied from `config/appsettings.global.example.json`
- [ ] Attend code review session to see standards in practice
