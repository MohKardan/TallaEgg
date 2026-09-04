using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.Core.Responses.Order;

namespace TallaEgg.Infrastructure.Clients;

/// <summary>
/// Client interface for communicating with Wallet service
/// Client interface for the Wallet service.
/// </summary>
public interface IWalletApiClient
{
    /// <summary>
    /// Get user balance for specific asset
    /// Returns a user's balance for one asset.
    /// </summary>
    Task<(bool Success, string Message, decimal? balance)> GetBalanceAsync(Guid userId, string asset);

    // Admin credit/debit and the balance screen, used by the bot (issue #65).
    Task<ApiResponse<IEnumerable<WalletDTO>>> GetUserWalletsBalanceAsync(Guid userId);
    Task<ApiResponse<WalletBallanceDTO>> DepositeAsync(WalletRequest request);
    Task<ApiResponse<WalletBallanceDTO>> WithdrawalAsync(WalletRequest request);
    
    /// <summary>
    /// Records a trade's transaction and updates the balances.
    /// </summary>
    Task<(bool Success, string Message)> TradeTransactionAndBalanceChangeAsync(TradeDto trade);
    
    /// <summary>
    /// Lock balance for order placement
    /// Locks balance when placing an order.
    /// </summary>
    Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(Guid userId, string asset, decimal amount);

    /// <summary>
    /// Unlock balance when order is cancelled
    /// Releases locked balance when an order is cancelled.
    /// </summary>
    Task<(bool Success, string Message)> UnlockBalanceAsync(Guid userId, string asset, decimal amount);

    /// <summary>
    /// Validate if user has sufficient balance for order
    /// Whether the user has enough balance for an order.
    /// </summary>
    Task<(bool Success, string Message, bool HasSufficientBalance)> ValidateBalanceAsync(
        Guid userId,
        string asset,
        decimal amount);
    /// <summary>
    /// Checks a user's credit and balance before an order is placed.
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="symbol">
    /// pair assets like BTC/USDT
    /// </param>
    /// <param name="amount">
    /// quantety
    /// </param>
    /// <param name="price">
    /// quote price
    /// </param>
    /// <returns></returns>
    Task<(bool Success, string Message, bool HasSufficientCreditAndBalanceBase, bool HasSufficientCreditAndBalanceQuote)> 
        ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price);
}
