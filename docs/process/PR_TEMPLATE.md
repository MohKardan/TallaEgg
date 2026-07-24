# Pull Request Template

> GitHub auto-loads a **lean** version of this from [`.github/pull_request_template.md`](../../.github/pull_request_template.md)
> into every new PR. This file is the **full** reference — copy the Security, Testing, and
> Deployment/Rollback sections below into the PR for critical, financial, or security changes.

**Branch Name**: (e.g., `hotfix/secrets-rotation`, `feat/wallet-atomicity`)  
**Linked Issue/Task**: TASK-### or Issue ###

---

## Description
Brief 2–3 sentence summary of the change and why it's needed.

**Example**:
> Wraps `ApplyTradeAsync` logic in a single `IDbContextTransaction` to prevent lost updates and ensure financial data consistency. Adds `RowVersion` to `WalletEntity` and implements `DbUpdateConcurrencyException` handling with exponential backoff retry.

---

## Type of Change
- [ ] Hotfix (urgent bug fix)
- [ ] Feature (new capability)
- [ ] Refactor (code organization, no behavior change)
- [ ] Performance (optimization)
- [ ] Documentation (docs/comments only)
- [ ] Security (auth, encryption, secrets rotation)

---

## Changes Made
List each significant change. Use bullet points.

**Example**:
- Modified `WalletService.ApplyTradeAsync` to use single `IDbContextTransaction`
- Added `[Timestamp] byte[] RowVersion` to `WalletEntity`
- Implemented `DbUpdateConcurrencyException` handler with 3-retry limit, 100ms base delay
- Added integration tests for concurrent wallet operations
- Updated `SPRINT_PLAN.md` with task completion notes

---

## Testing Completed

### Unit Tests
- [ ] New unit tests written
- [ ] All unit tests pass (`dotnet test`)
- [ ] Code coverage: ___ % (target: >80% for critical paths)

**Example**:
```
dotnet test TallaEgg.Wallet.Tests --filter Category=Unit
Tests: 25 passed, 0 failed
Coverage: Wallet.Service → 92%
```

### Integration Tests
- [ ] Integration tests added (if applicable)
- [ ] Tests run successfully on staging (`dotnet test --filter Category=Integration`)
- [ ] Manual smoke test on staging (date/time: _____)

**Example**:
```
Tested on staging at 2026-07-20 14:30 UTC:
- Single trade: ✓ balance updated correctly
- Concurrent trades (10 parallel): ✓ all succeeded, no data loss
- Retry mechanism: ✓ recovers from transient failures
```

### Environment
- [ ] Tested locally on Windows/Mac/Linux (circle one)
- [ ] Tested on staging environment
- [ ] Feature flag working as expected (if applicable)

---

## Security Checklist

**For all PRs**:
- [ ] No hardcoded secrets, passwords, API keys, or tokens
- [ ] No sensitive data in logs or error messages
- [ ] `.gitignore` blocks sensitive files (config, .env, secrets)
- [ ] Code compiles without warnings

**For security-specific PRs**:
- [ ] TLS validation enabled in Release builds
- [ ] Authentication/authorization checks in place
- [ ] Input validation on all external inputs
- [ ] SQL injection protections (parameterized queries)
- [ ] CORS properly configured (not `AllowAnyOrigin` in production)
- [ ] Second reviewer assigned
- [ ] Pair-programming session completed (for critical changes)

---

## Code Quality

- [ ] Code follows naming conventions (`STANDARDS.md`)
- [ ] Comments are in English and explain the "why", not the "what"
- [ ] No large blocks of commented-out code
- [ ] Consistent indentation and formatting
- [ ] No unnecessary dependencies added

---

## Documentation

- [ ] Updated relevant `.md` files in `docs/`
- [ ] Swagger/OpenAPI docs updated (if API changed)
- [ ] Code comments are clear and in English
- [ ] Runbook updated (if operational behavior changed)

---

## Breaking Changes?

- [ ] No breaking changes
- [ ] Breaking changes (see details below)

**If breaking**: List all services/clients affected and migration path.

---

## Deployment Notes

**Pre-Deployment**:
- Any database migrations needed?
- Any environment variables to set?
- Any feature flags to enable/disable?

**Post-Deployment Verification**:
- [ ] Application starts successfully
- [ ] Smoke tests pass
- [ ] Logs show no errors
- [ ] Metrics/dashboards show expected behavior

**Rollback Plan**:
Describe steps to revert this change if issues arise.

**Example**:
> If wallet transactions fail: revert to previous commit, restart services, run `dotnet ef database update` to previous migration version.

---

## Reviewer Checklist

**Primary Reviewer** (name): ___________  
**Second Reviewer** (for critical changes): ___________

Reviewers should verify:
- [ ] Code logic is sound
- [ ] No security issues
- [ ] Tests adequately cover changes
- [ ] Commit messages are clear
- [ ] No merge conflicts
- [ ] Definition of Done met

---

## Commit Message

**Format**:
```
<type>(<scope>): <subject> — <task-ref>

<body>

Closes <issue>
```

**Example**:
```
feat(wallet): implement optimistic concurrency control — TASK-004

Added [Timestamp] RowVersion to WalletEntity and Order to detect
concurrent modifications. Implemented DbUpdateConcurrencyException
handler with exponential backoff retry (max 3 attempts, 100ms base delay).

Integration tests verify no Lost Update anomalies under concurrent load.

Closes TASK-004
```

---

## Related Issues

Link to related issues, PRs, or documentation:
- Related to TASK-004 (Financial Integrity)
- Related to audit finding C-4 (Optimistic Concurrency)
- Depends on TASK-001 (Secrets Rotation)

---

## Screenshots / Logs (if applicable)

Paste relevant test output, deployment logs, or screenshots.

**Example** (test output):
```
Integration Test Results:
- ApplyTradeAsync_SingleTrade_UpdatesBalance: PASS
- ApplyTradeAsync_ConcurrentModification_RetriesSuccessfully: PASS
- ApplyTradeAsync_RetryExhausted_RollsBack: PASS
- ApplyTradeAsync_DuplicateReferenceId_Idempotent: PASS

All 4 tests passed in 2.34 seconds.
```

---

## Final Checklist

- [ ] PR title is descriptive and follows format
- [ ] Description clearly explains "why" and "what"
- [ ] All tests pass locally and on CI
- [ ] Code reviewed by at least one peer
- [ ] No secrets or sensitive data in code
- [ ] Commit messages are clear
- [ ] Documentation is updated
- [ ] Ready for merge

---

**Merge Instructions**:
After approval, squash and merge. Target `staging` once that branch and its CI/CD exist; until then, merge to `main` after review. (Auto-deploy via GitHub Actions is a planned target, not yet in place — see `WORKFLOW.md` → Deployment to Staging.)
