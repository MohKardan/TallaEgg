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
    public async Task<WalletEntity> UpdateWalletAsync(WalletEntity wallet,Transaction transaction = null)
    {
        _context.Transactions.Add(transaction);
        wallet.UpdatedAt = DateTime.UtcNow;
        _context.Wallets.Update(wallet);
        await _context.SaveChangesAsync();
        return wallet;
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

        await UpdateWalletAsync(wallet,transaction);
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

        await UpdateWalletAsync(wallet,transaction);
        return wallet;
    }


    public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
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