using TallaEgg.Core.DTOs.Wallet;

namespace Wallet.Core;

public interface IWalletService
{
    Task<WalletDTO> GetBalanceAsync(Guid userId, string asset);
    Task<(WalletEntity walletEntity, Transaction transactionEntity)> IncreaseBalanceAsync(Guid userId, string asset, decimal amount, string? refId = null);
    Task<(WalletEntity walletEntity, Transaction transactionEntity)> DecreaseBalanceAsync(Guid userId, string asset, decimal amount, string? refId = null);
    Task<bool> DebitAsync(Guid userId, string asset, decimal amount);
    Task<WalletDTO> LockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<WalletDTO> UnlockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<IEnumerable<WalletDTO>> GetUserWalletsAsync(Guid userId);
    Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null);
    Task<WalletBallanceDTO> DepositAsync(Guid userId, string asset, decimal amount, string? referenceId = null);
    Task<WalletBallanceDTO> WithdrawalAsync(Guid userId, string asset, decimal amount, string? referenceId = null);
    Task<WalletBallanceDTO> MakeTradeAsync(Guid fromUserId, Guid toUserId, string asset, decimal amount, string? referenceId = null);
    Task<(WalletDTO buyerBase, WalletDTO sellerQuote)> ApplyTradeAsync(Guid buyerUserId, Guid sellerUserId, string symbol, decimal price, decimal quantity, Guid tradeId);
    Task<(bool success, string message)> OldWithdrawAsync(Guid userId, string asset, decimal amount, string? referenceId = null);
    Task<(bool success, string message)> TransferAsync(Guid fromUserId, Guid toUserId, string asset, decimal amount);
    Task<(bool success, string message)> ChargeWalletAsync(Guid userId, string asset, decimal amount, string? paymentMethod = null);
    Task<IEnumerable<WalletDTO>> CreateDefaultWalletsAsync(Guid userId);
    
} 
