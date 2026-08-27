namespace Wallet.Core;

public interface IWalletRepository
{
    // Wallet operations
    Task<WalletEntity?> GetWalletAsync(Guid userId, string asset);
    Task<IEnumerable<WalletEntity>> GetUserWalletsAsync(Guid userId);
    Task<WalletEntity> CreateWalletAsync(WalletEntity wallet);
    Task<WalletEntity> UpdateWalletAsync(WalletEntity wallet, Transaction? transaction = null);
    Task<WalletEntity> LockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<WalletEntity> UnlockBalanceAsync(Guid userId, string asset, decimal amount);
    
    // Transaction operations
    Task<Transaction> CreateTransactionAsync(Transaction transaction);
    Task<WalletTransaction?> GetTransactionAsync(Guid transactionId);
    Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null);
    Task<IEnumerable<WalletTransaction>> GetTransactionsByReferenceAsync(string referenceId);
    Task<WalletTransaction> UpdateTransactionAsync(WalletTransaction transaction);

    /// <summary>
    /// Atomically settles a matched trade: consumes both sides' locked collateral,
    /// credits each side the asset they bought, and records a Transaction per leg —
    /// all in a single database transaction with one SaveChanges. Idempotent on
    /// <paramref name="tradeId"/>: a second call for the same trade is a no-op.
    /// </summary>
    Task<(bool Success, string Message)> SettleTradeAsync(
        Guid tradeId, Guid buyerUserId, Guid sellerUserId,
        string symbol, decimal quantity, decimal quoteQuantity,
        decimal feeBuyer, decimal feeSeller);
} 