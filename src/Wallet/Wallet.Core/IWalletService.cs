using TallaEgg.Core.DTOs.Wallet;

namespace Wallet.Core;

public interface IWalletService
{
    Task<WalletDTO> GetBalanceAsync(Guid userId, string asset);
    Task<(WalletEntity walletEntity, Transaction transactionEntity)> CreditAsync(Guid userId, string asset, decimal amount, string? refId = null);
    Task<bool> DebitAsync(Guid userId, string asset, decimal amount);
    Task<WalletDTO> LockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<bool> UnlockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<IEnumerable<WalletDTO>> GetUserWalletsAsync(Guid userId);
    Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null);
    Task<WalletDepositDTO> DepositAsync(Guid userId, string asset, decimal amount, string? referenceId = null);
    Task<(bool success, string message)> WithdrawAsync(Guid userId, string asset, decimal amount, string? referenceId = null);
    Task<(bool success, string message)> TransferAsync(Guid fromUserId, Guid toUserId, string asset, decimal amount);
    Task<(bool success, string message)> ChargeWalletAsync(Guid userId, string asset, decimal amount, string? paymentMethod = null);
    
    // Market order balance validation and update methods
    Task<(bool success, string message, bool hasSufficientBalance)> ValidateBalanceForMarketOrderAsync(Guid userId, string asset, decimal amount, int orderType);
    Task<(bool success, string message)> UpdateBalanceForMarketOrderAsync(Guid userId, string asset, decimal amount, int orderType, Guid orderId);
} 