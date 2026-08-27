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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating wallet {WalletId} for user {UserId}", wallet.Id, wallet.UserId);
            throw;
        }
    }

    public async Task<WalletEntity> LockBalanceAsync(Guid userId, string asset, decimal amount)
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
    }

    public async Task<WalletEntity> UnlockBalanceAsync(Guid userId, string asset, decimal amount)
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
    }

    public async Task<WalletEntity> IncreaseBalanceForTradeAsync(Guid userId, string asset, decimal amount)
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
    }


    public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

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




