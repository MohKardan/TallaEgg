namespace Wallet.Core;

public interface IWalletService
{
    Task<decimal> GetBalanceAsync(Guid userId, string asset);
    Task<bool> CreditAsync(Guid userId, string asset, decimal amount);
    Task<bool> DebitAsync(Guid userId, string asset, decimal amount);
    Task<bool> LockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<bool> UnlockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<IEnumerable<WalletEntity>> GetUserWalletsAsync(Guid userId);
    Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null);
    Task<(bool success, string message)> DepositAsync(Guid userId, string asset, decimal amount, string? referenceId = null);
    Task<(bool success, string message)> WithdrawAsync(Guid userId, string asset, decimal amount, string? referenceId = null);
    Task<(bool success, string message)> TransferAsync(Guid fromUserId, Guid toUserId, string asset, decimal amount);
} 