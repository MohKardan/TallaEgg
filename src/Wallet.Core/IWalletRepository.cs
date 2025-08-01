namespace Wallet.Core;

public interface IWalletRepository
{
    // Wallet operations
    Task<Wallet?> GetWalletAsync(Guid userId, string asset);
    Task<IEnumerable<Wallet>> GetUserWalletsAsync(Guid userId);
    Task<Wallet> CreateWalletAsync(Wallet wallet);
    Task<Wallet> UpdateWalletAsync(Wallet wallet);
    Task<bool> LockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<bool> UnlockBalanceAsync(Guid userId, string asset, decimal amount);
    
    // Transaction operations
    Task<WalletTransaction> CreateTransactionAsync(WalletTransaction transaction);
    Task<WalletTransaction?> GetTransactionAsync(Guid transactionId);
    Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null);
    Task<IEnumerable<WalletTransaction>> GetTransactionsByReferenceAsync(string referenceId);
    Task<WalletTransaction> UpdateTransactionAsync(WalletTransaction transaction);
} 