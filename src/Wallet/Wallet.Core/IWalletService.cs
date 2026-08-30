using TallaEgg.Core.DTOs.Wallet;

namespace Wallet.Core;

public interface IWalletService
{
    Task<WalletDTO> GetBalanceAsync(Guid userId, string asset);
    Task<(WalletEntity walletEntity, Transaction transactionEntity, bool wasAlreadyApplied)> IncreaseBalanceAsync(Guid userId, string asset, decimal amount, string? refId = null);
    Task<(WalletEntity walletEntity, Transaction transactionEntity, bool wasAlreadyApplied)> DecreaseBalanceAsync(Guid userId, string asset, decimal amount, string? refId = null);
    Task<WalletDTO> LockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<WalletDTO> UnlockBalanceAsync(Guid userId, string asset, decimal amount);
    Task<IEnumerable<WalletDTO>> GetUserWalletsAsync(Guid userId);
    Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null);
    Task<WalletBallanceDTO> DepositAsync(Guid userId, string asset, decimal amount, string? referenceId = null);
    Task<WalletBallanceDTO> WithdrawalAsync(Guid userId, string asset, decimal amount, string? referenceId = null);
    Task<IEnumerable<WalletDTO>> CreateDefaultWalletsAsync(Guid userId);

    /// <summary>
    /// Settles a matched trade atomically and idempotently (keyed on <paramref name="tradeId"/>).
    /// Backs POST /api/wallet/changeBalance, called by the Orders outbox processor.
    /// </summary>
    Task<(bool Success, string Message)> SettleTradeAsync(
        Guid tradeId, Guid buyerUserId, Guid sellerUserId,
        string symbol, decimal quantity, decimal quoteQuantity,
        decimal feeBuyer, decimal feeSeller);
} 