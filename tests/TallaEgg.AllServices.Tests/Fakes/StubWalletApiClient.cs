using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests.Fakes;

/// <summary>
/// A wallet client that refuses every call, for tests to override only what they exercise.
///
/// Each test used to spell out all of <see cref="IWalletApiClient"/>, so widening the
/// interface broke every fake at once and buried the one method that mattered under a
/// screenful of throws. Overriding one member states plainly what a test depends on.
///
/// The default is to throw rather than return a benign value on purpose: a test that
/// silently exercises a call it never configured is testing the stub, not the code.
/// </summary>
public class StubWalletApiClient : IWalletApiClient
{
    public virtual Task<(bool Success, string Message)> TradeTransactionAndBalanceChangeAsync(TradeDto trade) =>
        throw new NotSupportedException(nameof(TradeTransactionAndBalanceChangeAsync));

    public virtual Task<(bool Success, string Message, decimal? balance)> GetBalanceAsync(Guid userId, string asset) =>
        throw new NotSupportedException(nameof(GetBalanceAsync));

    public virtual Task<(bool Success, string Message, WalletDTO? Wallet)> LockBalanceAsync(Guid userId, string asset, decimal amount) =>
        throw new NotSupportedException(nameof(LockBalanceAsync));

    public virtual Task<(bool Success, string Message)> UnlockBalanceAsync(Guid userId, string asset, decimal amount) =>
        throw new NotSupportedException(nameof(UnlockBalanceAsync));

    public virtual Task<(bool Success, string Message)> IncreaseBalanceAsync(Guid userId, string asset, decimal amount) =>
        throw new NotSupportedException(nameof(IncreaseBalanceAsync));

    public virtual Task<(bool Success, string Message, bool HasSufficientBalance)> ValidateBalanceAsync(
        Guid userId, string asset, decimal amount) =>
        throw new NotSupportedException(nameof(ValidateBalanceAsync));

    public virtual Task<(bool Success, string Message, bool HasSufficientCreditAndBalanceBase, bool HasSufficientCreditAndBalanceQuote)>
        ValidateCreditAndBalanceAsync(Guid userId, string symbol, decimal amount, decimal price) =>
        throw new NotSupportedException(nameof(ValidateCreditAndBalanceAsync));

    public virtual Task<ApiResponse<IEnumerable<WalletDTO>>> GetUserWalletsBalanceAsync(Guid userId) =>
        throw new NotSupportedException(nameof(GetUserWalletsBalanceAsync));

    public virtual Task<ApiResponse<WalletBallanceDTO>> DepositeAsync(WalletRequest request) =>
        throw new NotSupportedException(nameof(DepositeAsync));

    public virtual Task<ApiResponse<WalletBallanceDTO>> WithdrawalAsync(WalletRequest request) =>
        throw new NotSupportedException(nameof(WithdrawalAsync));
}
