# Sprint Plan & Task Decomposition

**Cycle**: 2-week sprints, 3 sprints total (~6 weeks). This document covers all 3.  
**Sprint 1**: July 17 — July 31, 2026 · **Sprint 2**: ~July 31 — Aug 14 · **Sprint 3**: ~Aug 14 — Aug 28  
**Team**: Dev A (Backend/Transactions), Dev B (Matching Engine/DI/API). AI coding agents follow the same standards and PR process — see [`INDEX.md`](INDEX.md).

---

## Sprint Goal (Sprint 1)
Eliminate critical production blockers related to financial integrity, security, and dependency management to raise production-readiness from **30% → 60%** (see [`../audit/AUDIT_2026-07.md`](../audit/AUDIT_2026-07.md)).

---

## Sprint 1 · Immediate Tasks (Days 1–2)

### TASK-001: Rotate & Secure Secrets
**Priority**: CRITICAL  
**Owner**: Dev B (execution), Dev A (Git cleanup)  
**Estimated**: 1–2 days  
**Blocker**: Yes (until completed, staging deployment is unsafe)

**What**:
- Generate new Telegram bot token from BotFather
- Generate new API key (or use Secret Manager instead of hardcoded)
- Remove old credentials from Git history using `git filter-branch` or BFG Repo-Cleaner
- Update `config/appsettings.template.json` with placeholders
- Update environment-specific configs to use `appsettings.{Environment}.json`

**Definition of Done**:
- [ ] Old secrets completely removed from Git history (verified with `git log --all -S "oldtoken"`)
- [ ] New secrets NOT in repository (use .gitignore + env vars)
- [ ] Configuration template documents how to set env vars
- [ ] Staging environment updated with new secrets
- [ ] Verified no hardcoded secrets in code
- [ ] PR merged with security label; second review completed

**Acceptance Criteria**:
- Application starts without secrets in code
- All services authenticate successfully with new credentials
- No secrets appear in application logs

**Testing**:
- Unit test: Verify `IConfiguration` loads from env vars, not hardcoded values
- Manual: Deploy to staging, verify all services online

---

### TASK-002: Gate TLS Validation & Restrict CORS
**Priority**: CRITICAL  
**Owner**: Dev B  
**Estimated**: 0.5–1 day  
**Dependency**: TASK-001 (parallel-safe)

**What**:
- Locate all `ServerCertificateCustomValidationCallback = _ => true` statements
- Wrap in `#if DEBUG` directive
- Set CORS policy to whitelist specific origins (e.g., `https://staging.tallaegg.com`)
- Add configuration for CORS origins in `appsettings`

**Definition of Done**:
- [ ] TLS validation active in Release builds
- [ ] TLS bypass only in Debug builds
- [ ] CORS restricted to configured origins
- [ ] No `AllowAnyOrigin` or `AllowAnyMethod` in production config
- [ ] Unit test verifying CORS policy correctness
- [ ] PR merged; tested on staging

**Acceptance Criteria**:
- TLS validation enabled on staging (certificate chain verified)
- Cross-origin requests from unauthorized hosts return 403
- Authorized cross-origin requests succeed

---

### TASK-003: Quarantine Stub Endpoints
**Priority**: HIGH  
**Owner**: Dev A  
**Estimated**: 1–2 days  
**Dependency**: None

**What**:
- Identify all stub/incomplete endpoints (audit findings C-8)
- Replace business logic with `StatusCode(501, "Endpoint not implemented")`
- Add feature flag (`IsStubQuarantined`) to disable stub endpoints in production
- Log every call to quarantined endpoint with `ILogger.LogWarning`
- Add `[Obsolete]` attribute with guidance to consumers

**Stubs Identified**:
- `WalletService.MakeTradeAsync` → endpoint returns hardcoded `WalletBalanceDTO()`
- `ChargeWalletAsync` → incomplete implementation, always returns true
- Any endpoint returning `// TODO: implement`

**Definition of Done**:
- [ ] All stubs identified and documented in PR description
- [ ] Each stub replaced with 501 response + logging
- [ ] Feature flag in appsettings with proper default (quarantine = ON)
- [ ] Unit tests verifying 501 response behavior
- [ ] Integration test on staging validating no state changes from stubs
- [ ] Consumers of stubs notified (link to issue)
- [ ] PR merged

**Acceptance Criteria**:
- Stubs return consistent 501 error
- No side effects from stub calls
- Logs show which stubs were called (help with monitoring)

---

## Sprint 1 · Financial Integrity (Days 3–10)

### TASK-004: Implement Transaction Atomicity for Wallet Operations
**Priority**: CRITICAL  
**Owner**: Dev A (lead), Dev B (pair on complex areas)  
**Estimated**: 3–5 days  
**Dependency**: TASK-001 (secrets must be secure)

**What**:
Implement atomic transactions for financial operations to prevent "money disappearing" scenarios.

**Key Changes**:
1. Wrap `ApplyTradeAsync` logic in single `IDbContextTransaction`
   ```csharp
   using var transaction = await _dbContext.Database.BeginTransactionAsync();
   try {
       // All wallet updates in single batch
       await _dbContext.SaveChangesAsync();
       await transaction.CommitAsync();
   } catch (DbUpdateConcurrencyException ex) {
       await transaction.RollbackAsync();
       // Retry logic
   }
   ```

2. Add `RowVersion` column to `WalletEntity` and `Order`
   ```csharp
   [Timestamp] 
   public byte[] RowVersion { get; set; }
   ```

3. Implement `DbUpdateConcurrencyException` handler with exponential backoff retry

4. Add idempotency token (`ReferenceId`) to prevent duplicate trades

**Definition of Done**:
- [ ] Single SaveChangesAsync call in `ApplyTradeAsync`
- [ ] RowVersion added, EF Core mapping configured
- [ ] ConcurrencyException handler with 3-retry limit, 100ms base delay
- [ ] Unit tests: happy path, concurrent update, retry exhaustion
- [ ] Integration tests: 10 concurrent trades on same wallet
- [ ] Idempotency token tracked in database
- [ ] Performance baseline: single trade completes in <500ms
- [ ] PR merged with pair-review

**Acceptance Criteria**:
- All concurrent trades succeed without data loss
- Idempotent: duplicate ReferenceId returns same result, doesn't double-debit
- Rollback occurs if any step fails
- Logging captures retries for monitoring

**Testing Strategy**:
```csharp
// Unit test: concurrent modification
[Test]
public async Task ApplyTradeAsync_ConcurrentModification_RetriesAndSucceeds()
{
    var wallet = new WalletEntity { ... };
    var trade1 = Task.Run(() => ApplyTradeAsync(wallet, ...));
    var trade2 = Task.Run(() => ApplyTradeAsync(wallet, ...));
    
    var results = await Task.WhenAll(trade1, trade2);
    Assert.That(results, Has.All.Property("IsSuccess").EqualTo(true));
    Assert.That(wallet.Balance, Is.EqualTo(startingBalance - 2*tradeAmount));
}

// Integration test: rapid-fire trades
[Test]
public async Task ApplyTradeAsync_100ConcurrentTrades_AllSucceed()
{
    var tasks = Enumerable.Range(0, 100)
        .Select(i => ApplyTradeAsync(wallet, smallAmount, $"REF-{i}"))
        .ToList();
    
    var results = await Task.WhenAll(tasks);
    Assert.That(results.Count(r => r.Success), Is.EqualTo(100));
}
```

---

### TASK-005: Fix MatchingEngine DI & FixOrder Lock Sequence
**Priority**: CRITICAL  
**Owner**: Dev B (lead), Dev A (pair on order flow)  
**Estimated**: 2–3 days  
**Dependency**: TASK-004 (coordinate transaction ordering)

**What**:

1. **Fix MatchingEngine Registration**
   - Currently registered twice: once as `Scoped<MatchingEngine>`, once as `HostedService<MatchingEngine>`
   - This creates two separate instances; only one is hooked to semaphore
   - **Solution**: Register as `Singleton<MatchingEngine>` + implement `IHostedService`
   ```csharp
   // Before (WRONG)
   builder.Services.AddScoped<IMatchingEngine, MatchingEngineService>();
   builder.Services.AddHostedService<MatchingEngineService>();
   
   // After (CORRECT)
   builder.Services.AddSingleton<MatchingEngineService>();
   builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<MatchingEngineService>());
   ```

2. **Fix Order Lock Sequence**
   - Current: Create Order → Confirm + Send to Matcher → (Later) Lock Balance
   - Problem: Order already matched before balance is locked; possible race condition
   - **Correct sequence**:
     1. Validate user & balance sufficiency
     2. **Lock balance** (reserve funds)
     3. Create order (mark as pending)
     4. Send to matcher
     5. On match: transfer balance atomically (within transaction from TASK-004)

   **Code changes**:
   ```csharp
   public async Task CreateOrderAsync(CreateOrderCommand cmd)
   {
       // Step 1: Validate
       var user = await ValidateUserAsync(cmd.UserId);
       
       // Step 2: LOCK FIRST
       var lockSuccess = await _walletApiClient.LockBalanceAsync(
           cmd.UserId, cmd.QuoteAsset, cmd.Quantity * cmd.Price);
       if (!lockSuccess) throw new InsufficientBalanceException();
       
       // Step 3: Create order
       var order = new Order(cmd.UserId, cmd.Symbol, cmd.Quantity, cmd.Price);
       await _orderRepository.AddAsync(order);
       
       // Step 4: Send to matcher
       await _matchingEngine.EnqueueAsync(order);
   }
   ```

**Definition of Done**:
- [ ] MatchingEngine registered once (Singleton + HostedService)
- [ ] Only one instance active at any time (verify via logs)
- [ ] Lock-balance-first sequence implemented
- [ ] Order creation tests updated to reflect new sequence
- [ ] Integration test: Order + concurrent trades, verify lock prevents overdraft
- [ ] Logging added: `ILogger.LogInformation("Order locked balance: UserId={UserId}, Asset={Asset}, Amount={Amount}", ...)`
- [ ] PR merged with pair-review

**Acceptance Criteria**:
- Only one matcher instance running
- Concurrent lock requests serialize correctly
- No race condition: balance locked before order sent to matcher
- Orders cannot execute without balance lock

---

## Sprint 2: API & Architecture Quality (Weeks 3–4)

### TASK-006: Extract Wallet & Order API Clients to Interfaces (DIP)
**Priority**: HIGH  
**Owner**: Dev A  
**Estimated**: 2–3 days

**Changes**:
- Create interfaces in `{Service}.Core` (or `{Service}.Application`)
  - `IWalletApiClient` → move from Infrastructure
  - `IOrderApiClient` → if exists
- Implement concrete classes in Infrastructure
- Update DI registrations
- Remove direct dependencies on concrete classes

**Definition of Done**:
- [ ] All API clients accessed via interfaces
- [ ] Concrete implementations in Infrastructure
- [ ] Unit tests use mock clients
- [ ] Compile without warnings

---

### TASK-007: Implement Global Error Middleware
**Priority**: HIGH  
**Owner**: Dev B  
**Estimated**: 2–3 days

**What**:
- Replace 98 scattered `catch (Exception ex)` blocks with centralized middleware
- Implement `IExceptionHandler` + `AddProblemDetails`
- Return standard `ProblemDetails` (RFC 7807)
- Don't expose internal exception messages to clients
- Log all errors with context

**Definition of Done**:
- [ ] ProblemDetails middleware registered in all APIs
- [ ] All error responses conform to RFC 7807
- [ ] No internal messages leaked to clients
- [ ] Errors logged with correlation ID
- [ ] HTTP status codes standardized (400 for bad request, 404 for not found, etc.)

---

### TASK-008: Write Comprehensive Financial Integration Tests
**Priority**: HIGH  
**Owner**: Dev B (lead)  
**Estimated**: 2–3 days

**Scenarios**:
- Single trade: buyer, seller, amounts correct
- Concurrent trades on same wallet: no overdraft
- Insufficient balance: trade rejected, no state change
- Trade reversal (if order cancelled): balance restored
- Network failure during trade: compensating transaction
- Duplicate trade (same ReferenceId): idempotent result

**Definition of Done**:
- [ ] 15+ integration tests covering happy path + error paths
- [ ] Tests use in-memory database or Docker
- [ ] All tests pass locally and on staging
- [ ] Coverage report: >90% for financial paths

---

## Sprint 3: Polish & Documentation (Weeks 5–6)

### TASK-009: Performance Optimization (AsNoTracking, Query Optimization)
**Owner**: Dev A  
**Estimated**: 2 days

- Add `AsNoTracking` to read-only queries
- Move filtering to database (LINQ-to-Entities), not memory
- Index analysis & optimization

### TASK-010: Clean Dead Code & Remove Duplicate Folders
**Owner**: Dev B  
**Estimated**: 1–2 days

- Delete legacy `src/Order/*` folders not in solution
- Remove `ChargeWalletAsync`, `DebitAsync` (superseded by ApplyTradeAsync)
- Remove large blocks of commented code

### TASK-011: Prepare Runbook & Operational Playbooks
**Owner**: Dev A & B (pair)  
**Estimated**: 1–2 days

- Deployment runbook
- Incident response playbook (financial anomaly)
- Monitoring & alerting setup

---

## Sprint Metrics & Success Criteria

**Sprint 1 Success Metrics**:
- 100% of TASK-001 through TASK-005 completed
- All code reviewed, zero critical findings in production check
- Staging deployment successful; smoke tests pass
- Financial integrity tests: 100% pass rate
- No secrets in Git history; no TLS bypass in Release builds

**Quality Gates**:
- Code coverage: >80% for wallet/order logic
- PR review time: <24 hours
- Deployment time: <15 minutes

---

## Risk Mitigation

### Risk: Concurrent modifications cause data loss
**Mitigation**: TASK-004 (RowVersion + transactions + tests)

### Risk: Secrets leak during rotation
**Mitigation**: TASK-001 (Git history cleanup, env vars, no hardcoding)

### Risk: MatchingEngine still double-matches during TASK-005
**Mitigation**: Pair-programming on MatchingEngine DI fix; add logging to verify single instance

---

## End-of-Sprint Review (July 31)
- [ ] All planned tasks completed or rescheduled with justification
- [ ] Staging deployment successful
- [ ] Team retrospective: what went well, what to improve
- [ ] Sprint 2 tasks finalized and assigned
