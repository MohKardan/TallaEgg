using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Wallet;
using Wallet.Core;
using TallaEgg.Core.ErrorHandling;

namespace Wallet.Infrastructure;

public class WalletRepository : IWalletRepository
{
    private readonly WalletDbContext _context;
    private readonly ILogger<WalletRepository> _logger;

    public WalletRepository(ILogger<WalletRepository> logger, WalletDbContext context)
    {
        _context = context;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WalletEntity?> GetWalletAsync(Guid userId, string asset)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Asset.ToUpper() == asset.ToUpper());
    }

    public async Task<IEnumerable<WalletEntity>> GetUserWalletsAsync(Guid userId)
    {
        return await _context.Wallets
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.Asset)
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new wallet row.
    /// </summary>
    /// <param name="wallet">The wallet to create.</param>
    /// <returns>The created wallet, or the existing one if a concurrent caller won the race.</returns>
    public async Task<WalletEntity> CreateWalletAsync(WalletEntity wallet)
    {
        try
        {
            // Re-check for an existing wallet to narrow the race window.
            var existingWallet = await GetWalletAsync(wallet.UserId, wallet.Asset);
            if (existingWallet != null)
            {
                _logger.LogWarning("Wallet already exists during creation for user {UserId}, asset {Asset}",
                    wallet.UserId, wallet.Asset);
                return existingWallet;
            }

            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully created wallet {WalletId} for user {UserId}, asset {Asset}",
                wallet.Id, wallet.UserId, wallet.Asset);

            return wallet;
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("duplicate") == true ||
                                          ex.InnerException?.Message?.Contains("UNIQUE") == true)
        {
            _logger.LogWarning("Duplicate wallet creation attempted for user {UserId}, asset {Asset}. Returning existing wallet.",
                wallet.UserId, wallet.Asset);

            // Lost the race — return the wallet the other caller created.
            var existingWallet = await GetWalletAsync(wallet.UserId, wallet.Asset);
            if (existingWallet != null)
                return existingWallet;

            throw; // Still not found, so the duplicate was not the cause. Let it surface.
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating wallet for user {UserId}, asset {Asset}",
                wallet.UserId, wallet.Asset);
            throw;
        }
    }
    public async Task<WalletEntity> UpdateWalletAsync(WalletEntity wallet, Transaction? transaction = null)
    {
        if (wallet == null)
        {
            _logger.LogError("Attempted to update a null wallet entity");
            throw new ArgumentNullException(nameof(wallet));
        }

        try
        {
            if (transaction != null)
            {
                _context.Transactions.Add(transaction);
            }
            else
            {
                _logger.LogDebug("No transaction provided while updating wallet {WalletId}", wallet.Id);
            }

            wallet.UpdatedAt = DateTime.UtcNow;
            _context.Wallets.Update(wallet);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated wallet {WalletId} for user {UserId}", wallet.Id, wallet.UserId);

            return wallet;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while updating wallet {WalletId} for user {UserId}", wallet.Id, wallet.UserId);
            throw;
        }
        catch (DbUpdateException ex) when (IsDuplicateReference(ex))
        {
            // Rethrown for ApplyWithIdempotencyAsync to absorb, but not logged as an error on the
            // way past. A reference arriving twice is the normal outcome this design expects, and the
            // generic handler below would record it at Error with a stack trace — the same
            // false-alarm shape that made the bot's error log pure noise in #148.
            _logger.LogDebug(ex, "Duplicate reference on wallet {WalletId}; the caller will absorb it.", wallet.Id);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating wallet {WalletId} for user {UserId}", wallet.Id, wallet.UserId);
            throw;
        }
    }


    /// <summary>
    /// How long a wallet write keeps re-attempting after losing an optimistic-concurrency race.
    ///
    /// <para>
    /// This used to be a count — three attempts, 20/40ms apart, a 60ms budget — on the reasoning
    /// that a wallet contended by more than one other writer at once is a load problem rather than
    /// a retry problem. The market maker's wallet is that wallet by design: it is the counterparty
    /// to every quote fill, so the whole shop's writes land on one row (issue #174).
    /// </para>
    ///
    /// <para>
    /// A budget in time rather than tries is what matches the thing being waited out. The competing
    /// writer is the outbox draining settlements — a batch of twenty, back to back, roughly one to
    /// two seconds of near-continuous writes to that same row. Sixty milliseconds could not span
    /// that, so a fill arriving mid-batch exhausted its tries and was refused; the run that
    /// produced this value showed four collisions inside 136ms, three retried and the fourth with
    /// nothing left. Two seconds outlasts a full batch, and the caller is holding a customer's fill
    /// open, so what has to be bounded is the delay they see, not the number of tries behind it.
    /// </para>
    /// </summary>
    private static readonly TimeSpan ConcurrencyRetryBudget = TimeSpan.FromSeconds(2);

    /// <summary>
    /// First backoff after a lost race; each subsequent wait doubles it. Short enough that an
    /// uncontended collision costs almost nothing, since most are resolved on the first retry.
    /// </summary>
    private static readonly TimeSpan InitialConcurrencyDelay = TimeSpan.FromMilliseconds(25);

    /// <summary>
    /// Attempts granted regardless of the clock, matching the fixed cap this replaced.
    ///
    /// <para>
    /// The budget is measured from the start of the first attempt, so it covers the operation as
    /// well as the waiting — and the operation is a read-modify-write against a contended row,
    /// which is exactly what gets slow under the load this exists for. Without a floor, a wallet
    /// write that spent longer than the budget before its first collision would be refused having
    /// retried nothing at all: worse, precisely when it matters most, than the three attempts it
    /// used to get unconditionally. The floor makes the budget able only to extend the old
    /// behaviour, never to cut into it.
    /// </para>
    /// </summary>
    private const int MinimumConcurrencyAttempts = 3;

    /// <summary>
    /// Spreads the backoff over half to one and a half of its nominal length, so two writers that
    /// collided do not wake together and collide again for the same reason they collided the first
    /// time. Without it, doubling keeps a pair of losers in lockstep instead of separating them.
    /// </summary>
    private static TimeSpan WithJitter(TimeSpan delay) =>
        TimeSpan.FromMilliseconds(delay.TotalMilliseconds * (0.5 + Random.Shared.NextDouble()));

    /// <summary>
    /// Runs a read-modify-write against a wallet, re-running it from the start if a concurrent
    /// writer got there first.
    ///
    /// <para>
    /// Re-running the <b>whole</b> operation is the point, and the reason this wraps a delegate
    /// rather than retrying the save. The losing writer's arithmetic was performed on a balance
    /// that is now stale; saving it again would write exactly the number that was already wrong.
    /// The retry has to re-read the row and recompute from what it finds. The transaction record
    /// each caller builds also captures BallanceBefore from that read, so it has to be rebuilt too
    /// or the audit trail would record a balance the wallet never held.
    /// </para>
    ///
    /// <para>
    /// <c>ChangeTracker.Clear()</c> is what makes the re-read real. Without it EF's identity
    /// resolution hands back the same tracked instance it already has — the stale one — and every
    /// attempt recomputes from the same wrong number. That exact trap produced issue #74 in the
    /// Orders service, where a "re-fetch with lock" comment described code that did neither.
    /// </para>
    ///
    /// <para>
    /// Retrying rather than failing is deliberate for wallets, and differs from
    /// <c>OrderMatchingRepository</c>, which refuses. Two matchers racing for one order are
    /// competing: the loser has nothing left to do, because the winner did it. Two writers on one
    /// wallet are not competing — a deposit and a withdrawal must both land, they simply have to
    /// land one after the other, which is what a retry gives them.
    /// </para>
    /// </summary>
    private async Task<T> WithConcurrencyRetryAsync<T>(
        Func<Task<T>> operation, string operationName, Guid userId, string asset)
    {
        var spent = Stopwatch.StartNew();
        var backoff = InitialConcurrencyDelay;

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (DbUpdateConcurrencyException)
            {
                var delay = WithJitter(backoff);

                // Give up once the next wait would carry this write past the budget, rather than
                // after a set number of tries. Checking before sleeping is what keeps the ceiling
                // honest: waiting first and testing afterwards could overshoot by a whole backoff.
                // The floor is checked first so a slow operation cannot exhaust the budget before
                // the first collision and leave the write with no retries at all.
                if (attempt >= MinimumConcurrencyAttempts && spent.Elapsed + delay > ConcurrencyRetryBudget)
                    throw;

                _context.ChangeTracker.Clear();

                _logger.LogWarning(
                    "Concurrent write to the {Asset} wallet of user {UserId} during {Operation}; " +
                    "retrying from a fresh read (attempt {Attempt}, {Spent}ms of {Budget}ms used).",
                    asset, userId, operationName, attempt,
                    (long)spent.Elapsed.TotalMilliseconds, (long)ConcurrencyRetryBudget.TotalMilliseconds);

                await Task.Delay(delay);
                backoff += backoff;
            }
        }
    }

    public async Task<WalletEntity> LockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        return await WithConcurrencyRetryAsync(async () =>
        {
            var wallet = await GetWalletAsync(userId, asset);

            if (wallet == null)
            {
                // Hit live: selling BTC/SEKE_BAHAR failed for every customer and the market maker
                // alike with "wallet not found", because CreateDefaultWalletsAsync only ever seeds
                // Toman/MAUA/CREDIT_MAUA at registration — a newer trading symbol's wallet never
                // existed for anyone until now. Same fix as WalletService.IncreaseBalanceAsync
                // (issue: charge-command bugs earlier in this conversation), applied here since
                // trading locks collateral through this repository directly, not through that
                // service. A genuinely unknown asset still fails instead of creating a phantom
                // wallet.
                if (!CurrenciesConstant.IsValidCurrency(asset))
                    throw new BusinessRuleException("کیف پول پیدا نشد");

                wallet = await CreateWalletAsync(WalletEntity.Create(userId, asset));
            }

            var transaction = Transaction.Create(
              wallet.Id,
              amount,
              asset,
              TransactionType.Freeze,
              wallet.Balance,
              wallet.Balance - amount,
              null,
              TransactionStatus.Completed,
              "LockBalance transaction",
              null,
              null
          );
            wallet.LockBalance(amount);

            await UpdateWalletAsync(wallet, transaction);
            return wallet;
        }, "lock", userId, asset);
    }

    public async Task<WalletEntity> UnlockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        return await WithConcurrencyRetryAsync(async () =>
        {
            var wallet = await GetWalletAsync(userId, asset);

            if (wallet == null)
            {
                // Same reasoning as LockBalanceAsync — kept symmetric even though, in the normal
                // order-cancellation flow, a Lock always precedes the matching Unlock and so the
                // wallet already exists by then.
                if (!CurrenciesConstant.IsValidCurrency(asset))
                    throw new BusinessRuleException("کیف پول پیدا نشد");

                wallet = await CreateWalletAsync(WalletEntity.Create(userId, asset));
            }

            if (amount < 0)
                throw new ArgumentOutOfRangeException(nameof(amount), "مقدار آزادسازی نمی‌تواند منفی باشد.");

            // Releasing more than is locked drives LockedBalance negative while raising Balance —
            // money created from nothing. There used to be no guard here at all, and the
            // order-cancellation path computed the amount with a different formula than the lock did,
            // so this state was genuinely reachable (issue #52).
            if (amount > wallet.LockedBalance)
            {
                _logger.LogError(
                    "Refusing to unlock {Amount} {Asset} for user {UserId}: only {Locked} is locked.",
                    amount, asset, userId, wallet.LockedBalance);

                throw new BusinessRuleException(
                    $"مقدار آزادسازی ({amount}) از موجودی قفل‌شده ({wallet.LockedBalance}) بیشتر است.");
            }

            var transaction = Transaction.Create(
              wallet.Id,
              amount,
              asset,
              TransactionType.Unfreeze,
              wallet.Balance,
              wallet.Balance + amount,
              null,
              TransactionStatus.Completed,
              "UnLockBalance transaction",
              null,
              null
          );
            wallet.UnLockBalance(amount);

            await UpdateWalletAsync(wallet, transaction);
            return wallet;
        }, "unlock", userId, asset);
    }

    public async Task<WalletEntity> IncreaseBalanceForTradeAsync(Guid userId, string asset, decimal amount)
    {
        return await WithConcurrencyRetryAsync(async () =>
        {
            var wallet = await GetWalletAsync(userId, asset);

            if (wallet == null)
            {
                // The most serious of the three (Lock/Unlock/this): this is trade settlement
                // crediting the buyer's side. Without this fix, a buyer's first-ever purchase of a
                // newer symbol would have the seller's collateral already consumed while the buyer
                // receives nothing — the outbox settlement failing here, not merely a customer-facing
                // "wallet not found" message (issue #39 territory: a stuck settlement, not just a
                // refused request).
                if (!CurrenciesConstant.IsValidCurrency(asset))
                    throw new BusinessRuleException("کیف پول پیدا نشد");

                wallet = await CreateWalletAsync(WalletEntity.Create(userId, asset));
            }

            var transaction = Transaction.Create(
              wallet.Id,
              amount,
              asset,
              TransactionType.Trade,
              wallet.Balance,
              wallet.Balance + amount,
              null,
              TransactionStatus.Completed,
              "IncreaseBalanceAsync transaction",
              null,
              null
                                                );
            wallet.IncreaseBalance(amount);

            await UpdateWalletAsync(wallet, transaction);
            return wallet;
        }, "settlement credit", userId, asset);
    }


    public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task<Transaction?> FindTransactionByReferenceAsync(Guid walletId, string referenceId) =>
        await _context.Transactions
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.WalletId == walletId && t.ReferenceId == referenceId);

    public async Task<Transaction> ApplyWithIdempotencyAsync(WalletEntity wallet, Transaction transaction)
    {
        var referenceId = transaction.ReferenceId;

        if (string.IsNullOrWhiteSpace(referenceId))
        {
            await UpdateWalletAsync(wallet, transaction);
            return transaction;
        }

        // Fast path, the same optimisation SettleTradeAsync opens with: answer an already-seen
        // reference without paying for a failed write. It is not the guarantee — two callers can
        // both pass it — the unique index below is.
        var alreadyRecorded = await FindTransactionByReferenceAsync(wallet.Id, referenceId);
        if (alreadyRecorded is not null)
        {
            await DiscardUnsavedChangeAsync(wallet);

            _logger.LogInformation(
                "Reference {ReferenceId} was already applied to wallet {WalletId}; returning the original transaction and moving nothing (idempotent).",
                referenceId, wallet.Id);

            return alreadyRecorded;
        }

        try
        {
            await UpdateWalletAsync(wallet, transaction);
            return transaction;
        }
        catch (DbUpdateException ex) when (IsDuplicateReference(ex))
        {
            // Lost the race. The balance change and the transaction insert are one SaveChanges,
            // so the index rejecting the insert leaves the balance untouched — the duplicate
            // moved no money, which is the whole point.
            _context.ChangeTracker.Clear();

            var winner = await FindTransactionByReferenceAsync(wallet.Id, referenceId);

            if (winner is null) throw; // The duplicate was about something else. Let it surface.

            await DiscardUnsavedChangeAsync(wallet);

            _logger.LogInformation(
                "Reference {ReferenceId} was applied concurrently to wallet {WalletId}; this attempt was rejected by the database and moved nothing (idempotent).",
                referenceId, wallet.Id);

            return winner;
        }
    }

    /// <summary>
    /// Puts the caller's wallet instance back to what the database holds.
    ///
    /// <para>
    /// A caller applies its balance change before handing the entity over — <c>IncreaseBalance</c>
    /// adjusts <c>Balance</c> and stamps <c>UpdatedAt</c> — and on the idempotent path that change
    /// is never saved. Left alone, the entity returned to the caller would report a balance and a
    /// modification time the wallet never had, and the endpoint would answer with them. Reloading
    /// makes the response describe the wallet as it actually is: unchanged, because the duplicate
    /// changed nothing.
    /// </para>
    /// </summary>
    private async Task DiscardUnsavedChangeAsync(WalletEntity wallet)
    {
        var entry = _context.Entry(wallet);

        // Detached after a ChangeTracker.Clear() on the concurrent path; Entry() re-attaches it,
        // and Reload() then overwrites every property from the row that is actually stored.
        await entry.ReloadAsync();
    }

    /// <summary>
    /// Whether a save failure was the unique index over <c>(WalletId, ReferenceId)</c> rejecting a
    /// duplicate transaction.
    ///
    /// Asks EF what was being written rather than matching a provider error number, for the reason
    /// <see cref="IsDuplicateSettlement"/> gives: the tests run on SQLite, which reports a different
    /// code from SQL Server, and a check that only recognised the SQL Server one would leave this
    /// path untested. A save from this method adds exactly one entity, the Transaction.
    /// </summary>
    private static bool IsDuplicateReference(DbUpdateException ex) =>
        ex.Entries.Count > 0 && ex.Entries.All(e => e.Entity is Transaction);

    public async Task<(bool Success, string Message)> SettleTradeAsync(
        Guid tradeId, Guid buyerUserId, Guid sellerUserId,
        string symbol, decimal quantity, decimal quoteQuantity,
        decimal feeBuyer, decimal feeSeller)
    {
        var referenceId = tradeId.ToString();

        // Fast path: if the trade is already settled, return without opening a transaction.
        //
        // This check is an optimisation, not the guarantee. Outbox redelivery is normal — the
        // design explicitly allows a message that already succeeded to be sent again — so it is
        // worth short-circuiting the common case without paying for a transaction or raising an
        // exception.
        //
        // The actual guarantee is the TradeSettlements primary key, applied further down. This
        // SELECT used to be the only protection, and because it ran outside the transaction two
        // concurrent settlements could both pass it and move the money twice (issue #42).
        if (await _context.TradeSettlements.AnyAsync(s => s.TradeId == tradeId))
        {
            _logger.LogInformation("Trade {TradeId} already settled; skipping (idempotent).", tradeId);
            return (true, "Trade already settled.");
        }

        // Symbol is BASE/QUOTE, e.g. MAUA/IRT. Parse defensively (never symbol.Split('/')[1] blindly).
        var parts = symbol?.Split('/');
        if (parts is not { Length: 2 } || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return (false, $"Invalid symbol '{symbol}'. Expected BASE/QUOTE.");

        var baseAsset = parts[0].Trim().ToUpperInvariant();   // e.g. MAUA (gold)
        var quoteAsset = parts[1].Trim().ToUpperInvariant();  // e.g. IRT (toman)

        if (quantity <= 0 || quoteQuantity <= 0)
            return (false, "Quantity and quoteQuantity must be positive.");
        if (feeBuyer < 0 || feeSeller < 0)
            return (false, "Fees cannot be negative.");

        // Defence in depth: matching should never permit self-trading, but if some path bypasses
        // that, settlement would run against a single shared wallet and produce a false audit trail.
        if (buyerUserId == sellerUserId)
        {
            _logger.LogError("Refusing to settle trade {TradeId}: buyer and seller are the same user.", tradeId);
            return (false, "Buyer and seller must be different users.");
        }

        // Fail closed on fees. Settlement debits each payer the full amount but would
        // credit the receiver the amount minus the fee — and the difference is credited
        // to NO account, so a non-zero fee silently destroys money on every trade.
        // Fee rates are 0 for the MVP, so this guard is inert today; it exists so that
        // restoring a non-zero rate produces a loud, visible failure instead of a slow
        // leak. Remove it only together with fee crediting to the fee account (issue #35).
        if (feeBuyer != 0m || feeSeller != 0m)
        {
            _logger.LogError(
                "Refusing to settle trade {TradeId}: non-zero fees are not supported because collected " +
                "fees are not credited to any account (feeBuyer={FeeBuyer}, feeSeller={FeeSeller}). See issue #35.",
                tradeId, feeBuyer, feeSeller);
            return (false, "Fee crediting is not implemented; settlement refused to avoid losing the fee amount.");
        }

        var buyerReceivesBase = quantity;      // no fee is deducted while fees are disabled
        var sellerReceivesQuote = quoteQuantity;

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // Fetch all four wallets as tracked entities; every change below is persisted by a single SaveChanges.
            var buyerQuote = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == buyerUserId && w.Asset == quoteAsset);
            var buyerBase = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == buyerUserId && w.Asset == baseAsset);
            var sellerBase = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == sellerUserId && w.Asset == baseAsset);
            var sellerQuote = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == sellerUserId && w.Asset == quoteAsset);

            // buyerBase and sellerQuote are the *receiving* sides — for a newer symbol nobody
            // has ever bought before, the buyer's base-asset wallet (and, symmetrically, a
            // first-time seller's quote-asset wallet) may never have been created. Without this,
            // the entire settlement rolled back — collateral already locked at order time, buyer
            // credited nothing — the most serious of the wallet-creation gaps found in this
            // conversation, since it fails settlement itself rather than a customer-facing
            // request. buyerQuote/sellerBase are the *locked* sides and should already exist from
            // LockBalanceAsync's own fix, but are created here too for symmetry and safety.
            if (buyerQuote is null || buyerBase is null || sellerBase is null || sellerQuote is null)
            {
                if (!CurrenciesConstant.IsValidCurrency(baseAsset) || !CurrenciesConstant.IsValidCurrency(quoteAsset))
                {
                    await tx.RollbackAsync();
                    return (false, "One or more participant wallets were not found.");
                }

                buyerQuote ??= await CreateWalletAsync(WalletEntity.Create(buyerUserId, quoteAsset));
                buyerBase ??= await CreateWalletAsync(WalletEntity.Create(buyerUserId, baseAsset));
                sellerBase ??= await CreateWalletAsync(WalletEntity.Create(sellerUserId, baseAsset));
                sellerQuote ??= await CreateWalletAsync(WalletEntity.Create(sellerUserId, quoteAsset));
            }

            // The collateral must actually be locked. Guards against the lock-after-match ordering bug (audit C-5):
            // settling from funds that were never locked would drive a balance negative.
            if (buyerQuote.LockedBalance < quoteQuantity)
            {
                await tx.RollbackAsync();
                return (false, $"Buyer locked {quoteAsset} ({buyerQuote.LockedBalance}) is less than required ({quoteQuantity}).");
            }
            if (sellerBase.LockedBalance < quantity)
            {
                await tx.RollbackAsync();
                return (false, $"Seller locked {baseAsset} ({sellerBase.LockedBalance}) is less than required ({quantity}).");
            }

            // Buyer pays quote: consume the locked collateral. The funds were reserved at
            // order time (possibly credit-backed, so available Balance may be negative);
            // consuming them removes the lock and leaves the debt on Balance.
            var buyerQuoteBefore = buyerQuote.Balance;
            buyerQuote.ConsumeLockedBalance(quoteQuantity);
            AddTradeTransaction(buyerQuote, quoteQuantity, quoteAsset, buyerQuoteBefore, buyerQuote.Balance, referenceId, "Buyer paid quote asset (from locked funds)");

            // Buyer receives base.
            var buyerBaseBefore = buyerBase.Balance;
            buyerBase.IncreaseBalance(buyerReceivesBase);
            AddTradeTransaction(buyerBase, buyerReceivesBase, baseAsset, buyerBaseBefore, buyerBase.Balance, referenceId, "Buyer received base asset");

            // Seller pays base: consume the locked collateral (same credit-aware handling).
            var sellerBaseBefore = sellerBase.Balance;
            sellerBase.ConsumeLockedBalance(quantity);
            AddTradeTransaction(sellerBase, quantity, baseAsset, sellerBaseBefore, sellerBase.Balance, referenceId, "Seller paid base asset (from locked funds)");

            // Seller receives quote.
            var sellerQuoteBefore = sellerQuote.Balance;
            sellerQuote.IncreaseBalance(sellerReceivesQuote);
            AddTradeTransaction(sellerQuote, sellerReceivesQuote, quoteAsset, sellerQuoteBefore, sellerQuote.Balance, referenceId, "Seller received quote asset");

            var now = DateTime.UtcNow;
            buyerQuote.UpdatedAt = now; buyerBase.UpdatedAt = now; sellerBase.UpdatedAt = now; sellerQuote.UpdatedAt = now;
            _context.Wallets.UpdateRange(buyerQuote, buyerBase, sellerBase, sellerQuote);

            // The uniqueness barrier: this row is inserted inside the same transaction that moves
            // the money.
            //
            // Because TradeId is the primary key, if a concurrent settlement committed first this
            // insert fails on a duplicate key and the whole transaction — all four balance changes
            // included — is rolled back. "Exactly once" is therefore guaranteed by the database,
            // not by the order in which code happens to run.
            _context.TradeSettlements.Add(
                TradeSettlement.Create(tradeId, buyerUserId, sellerUserId, symbol!, quantity, quoteQuantity));

            await _context.SaveChangesAsync(); // One save: 4 balance changes + 4 transaction rows + 1 settlement row.
            await tx.CommitAsync();

            _logger.LogInformation(
                "Settled trade {TradeId}: buyer={Buyer} seller={Seller} {Qty} {Base} for {Quote} {QuoteAsset}",
                tradeId, buyerUserId, sellerUserId, quantity, baseAsset, quoteQuantity, quoteAsset);
            return (true, "Trade settled.");
        }
        catch (DbUpdateException ex) when (IsDuplicateSettlement(ex))
        {
            // We lost the race. Another settlement committed first and the primary key rejected
            // this second insert. The rollback undoes every balance change from this attempt, so
            // the net effect is exactly one settlement.
            //
            // This is reported as success rather than an error because from the caller's point of
            // view it genuinely is one: the trade is settled. Returning an error would make the
            // outbox processor treat the message as failed, retry it five times and finally mark
            // it Failed — raising a perfectly healthy trade to an operator as "stuck".
            await tx.RollbackAsync();

            _logger.LogInformation(
                "Trade {TradeId} was settled concurrently by another caller; this attempt was rolled back (idempotent).",
                tradeId);

            return (true, "Trade already settled.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error settling trade {TradeId}", tradeId);
            return (false, "Settlement error");
        }
    }

    /// <summary>
    /// Decides whether a save failure was caused by a duplicate insert into TradeSettlements.
    ///
    /// Deliberately does not key off SQL Server's specific error numbers (2627 for a constraint
    /// violation, 2601 for a unique index), because the tests run against SQLite, which raises a
    /// different code. Accepting only the SQL Server code would mean this path never executed
    /// under test — leaving precisely the guarantee that matters most unverified.
    ///
    /// Instead we ask EF what was being inserted: if the only Added entity is a TradeSettlement,
    /// a duplicate key is the only thing the uniqueness violation can be about.
    /// </summary>
    private static bool IsDuplicateSettlement(DbUpdateException ex) =>
        ex.Entries.Count > 0 && ex.Entries.All(e => e.Entity is TradeSettlement);

    /// <summary>Adds (but does not save) a completed Trade transaction record to the current unit of work.</summary>
    private void AddTradeTransaction(WalletEntity wallet, decimal amount, string asset,
        decimal balanceBefore, decimal balanceAfter, string referenceId, string description)
    {
        var transaction = Transaction.Create(
            wallet.Id, amount, asset, TransactionType.Trade,
            balanceBefore, balanceAfter, null, TransactionStatus.Completed,
            description, referenceId, null);
        _context.Transactions.Add(transaction);
    }

    public async Task<WalletTransaction?> GetTransactionAsync(Guid transactionId)
    {
        return await _context.WalletTransactions.FindAsync(transactionId);
    }

    public async Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null)
    {
        var query = _context.WalletTransactions.Where(wt => wt.UserId == userId);
        if (!string.IsNullOrEmpty(asset))
            query = query.Where(wt => wt.Asset == asset);

        return await query.OrderByDescending(wt => wt.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<WalletTransaction>> GetTransactionsByReferenceAsync(string referenceId)
    {
        return await _context.WalletTransactions
            .Where(wt => wt.ReferenceId == referenceId)
            .OrderByDescending(wt => wt.CreatedAt)
            .ToListAsync();
    }

    public async Task<WalletTransaction> UpdateTransactionAsync(WalletTransaction transaction)
    {
        _context.WalletTransactions.Update(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }


}




