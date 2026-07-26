using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.Enums.Wallet;
using Wallet.Core;

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
    /// ایجاد کیف پول جدید در دیتابیس
    /// </summary>
    /// <param name="wallet">کیف پول برای ایجاد</param>
    /// <returns>کیف پول ایجاد شده</returns>
    public async Task<WalletEntity> CreateWalletAsync(WalletEntity wallet)
    {
        try
        {
            // بررسی مجدد وجود کیف پول (Race Condition Prevention)
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

            // در صورت تکراری بودن، کیف پول موجود را برگردان
            var existingWallet = await GetWalletAsync(wallet.UserId, wallet.Asset);
            if (existingWallet != null)
                return existingWallet;

            throw; // اگر هنوز هم پیدا نشد، خطا را دوباره پرتاب کن
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating wallet for user {UserId}, asset {Asset}",
                wallet.UserId, wallet.Asset);
            throw;
        }
    }
    public async Task<WalletEntity> UpdateWalletAsync(WalletEntity wallet, Transaction transaction = null)
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
        if (wallet == null) throw new ArgumentNullException("کیف پول پیدا نشد", nameof(wallet));

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
        if (wallet == null) throw new ArgumentNullException("کیف پول پیدا نشد", nameof(wallet));

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
        if (wallet == null) throw new ArgumentNullException("کیف پول پیدا نشد", nameof(wallet));

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

        // Idempotency: a retry or a duplicate outbox delivery must not settle twice.
        // Check the NEW Transactions table (settlement writes here), not the legacy WalletTransactions.
        var alreadySettled = await _context.Transactions.AnyAsync(t => t.ReferenceId == referenceId);
        if (alreadySettled)
        {
            _logger.LogInformation("Trade {TradeId} already settled; skipping (idempotent).", tradeId);
            return (true, "Trade already settled.");
        }

        // Symbol is BASE/QUOTE, e.g. MAUA/IRR. Parse defensively (never symbol.Split('/')[1] blindly).
        var parts = symbol?.Split('/');
        if (parts is not { Length: 2 } || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return (false, $"Invalid symbol '{symbol}'. Expected BASE/QUOTE.");

        var baseAsset = parts[0].Trim().ToUpperInvariant();   // e.g. MAUA (gold)
        var quoteAsset = parts[1].Trim().ToUpperInvariant();  // e.g. IRR (rial)

        if (quantity <= 0 || quoteQuantity <= 0)
            return (false, "Quantity and quoteQuantity must be positive.");
        if (feeBuyer < 0 || feeSeller < 0)
            return (false, "Fees cannot be negative.");

        var buyerReceivesBase = quantity - feeBuyer;         // buyer gets base minus buyer fee
        var sellerReceivesQuote = quoteQuantity - feeSeller; // seller gets quote minus seller fee
        if (buyerReceivesBase <= 0 || sellerReceivesQuote <= 0)
            return (false, "Fee exceeds the trade amount.");

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // Fetch all four wallets as tracked entities; every change below is persisted by a single SaveChanges.
            var buyerQuote = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == buyerUserId && w.Asset == quoteAsset);
            var buyerBase = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == buyerUserId && w.Asset == baseAsset);
            var sellerBase = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == sellerUserId && w.Asset == baseAsset);
            var sellerQuote = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == sellerUserId && w.Asset == quoteAsset);

            if (buyerQuote is null || buyerBase is null || sellerBase is null || sellerQuote is null)
            {
                await tx.RollbackAsync();
                return (false, "One or more participant wallets were not found.");
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

            await _context.SaveChangesAsync(); // single save: 4 balance changes + 4 transaction records
            await tx.CommitAsync();

            _logger.LogInformation(
                "Settled trade {TradeId}: buyer={Buyer} seller={Seller} {Qty} {Base} for {Quote} {QuoteAsset}",
                tradeId, buyerUserId, sellerUserId, quantity, baseAsset, quoteQuantity, quoteAsset);
            return (true, "Trade settled.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error settling trade {TradeId}", tradeId);
            return (false, $"Settlement error: {ex.Message}");
        }
    }

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




