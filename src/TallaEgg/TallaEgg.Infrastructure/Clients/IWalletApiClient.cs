using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Wallet;

namespace TallaEgg.Infrastructure.Clients;

/// <summary>
/// Client interface for communicating with Wallet service
/// واسط کلاینت برای ارتباط با سرویس کیف پول
/// </summary>
public interface IWalletApiClient
{
    /// <summary>
    /// Get user balance for specific asset
    /// دریافت موجودی کاربر برای دارایی مشخص
    /// </summary>
    Task<(bool Success, string Message, decimal? balance)> GetBalanceAsync(Guid userId, string asset);

    /// <summary>
    /// Lock balance for order placement
    /// قفل کردن موجودی برای ثبت سفارش
    /// </summary>
    Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(Guid userId, string asset, decimal amount);

    /// <summary>
    /// Unlock balance when order is cancelled
    /// آزاد کردن موجودی هنگام لغو سفارش
    /// </summary>
    Task<(bool Success, string Message)> UnlockBalanceAsync(Guid userId, string asset, decimal amount);
    /// <summary>
    /// Increase balance for order placement
    /// افزایش موجودی برای ثبت سفارش
    /// </summary>
    Task<(bool Success, string Message, WalletDTO? Wallet)> IncreaseBalanceAsync(Guid userId, string asset, decimal amount);

    /// <summary>
    /// Validate if user has sufficient balance for order
    /// بررسی داشتن موجودی کافی برای سفارش
    /// </summary>
    Task<(bool Success, string Message, bool HasSufficientBalance)> ValidateBalanceAsync(
        Guid userId,
        string asset,
        decimal amount);
    /// <summary>
    /// بررسی اعتبار و موجودی کاربر برای ثبت سفارش
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
