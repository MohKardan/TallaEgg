using Microsoft.EntityFrameworkCore;
using TallaEgg.Core.Enums.Wallet;
using Wallet.Core;

namespace Wallet.Infrastructure;

public class WalletRepository : IWalletRepository
{
    private readonly WalletDbContext _context;

    public WalletRepository(WalletDbContext context)
    {
        _context = context;
    }

    public async Task<WalletEntity?> GetWalletAsync(Guid userId, string asset)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Asset == asset);
    }

    public async Task<IEnumerable<WalletEntity>> GetUserWalletsAsync(Guid userId)
    {
        return await _context.Wallets
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.Asset)
            .ToListAsync();
    }

    public async Task<WalletEntity> CreateWalletAsync(WalletEntity wallet)
    {
        _context.Wallets.Add(wallet);
        await _context.SaveChangesAsync();
        return wallet;
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
          wallet.Balance - amount,
          wallet.Balance,
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

    public async Task<bool> UnlockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        var wallet = await GetWalletAsync(userId, asset);
        if (wallet == null || wallet.LockedBalance < amount)
            return false;

        wallet.LockedBalance -= amount;
        wallet.Balance += amount;
        wallet.UpdatedAt = DateTime.UtcNow;
        
        await UpdateWalletAsync(wallet);
        return true;
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