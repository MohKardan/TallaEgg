using Microsoft.EntityFrameworkCore;
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

    public async Task<WalletEntity> UpdateWalletAsync(WalletEntity wallet)
    {
        wallet.UpdatedAt = DateTime.UtcNow;
        _context.Wallets.Update(wallet);
        await _context.SaveChangesAsync();
        return wallet;
    }

    public async Task<bool> LockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        var wallet = await GetWalletAsync(userId, asset);
        if (wallet == null || wallet.Balance < amount)
            return false;

        wallet.LockedBalance += amount;
        wallet.Balance -= amount;
        wallet.UpdatedAt = DateTime.UtcNow;
        
        await UpdateWalletAsync(wallet);
        return true;
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