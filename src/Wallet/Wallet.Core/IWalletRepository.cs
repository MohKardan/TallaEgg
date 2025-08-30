namespace Wallet.Core;

public interface IWalletRepository
{
    // Wallet operations
    Task<WalletEntity?> GetWalletAsync(Guid userId, string asset);
    Task<IEnumerable<WalletEntity>> GetUserWalletsAsync(Guid userId);
    Task<WalletEntity> CreateWalletAsync(WalletEntity wallet);
    Task<WalletEntity> UpdateWalletAsync(WalletEntity wallet,Transaction transaction= null);
    Task<WalletEntity> LockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<WalletEntity> UnlockBalanceAsync(Guid userId, string asset, decimal amount);
    
    // Transaction operations
    Task<Transaction> CreateTransactionAsync(Transaction transaction);
    Task<WalletTransaction?> GetTransactionAsync(Guid transactionId);
    Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null);
    Task<IEnumerable<WalletTransaction>> GetTransactionsByReferenceAsync(string referenceId);
    Task<WalletTransaction> UpdateTransactionAsync(WalletTransaction transaction);
} 