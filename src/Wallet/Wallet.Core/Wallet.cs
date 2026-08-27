using System.ComponentModel;
using TallaEgg.Core.Enums.Wallet;
using TallaEgg.Core.ErrorHandling;

namespace Wallet.Core;

public class WalletEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Asset { get; set; } = "";
    public decimal Balance { get; set; }
    /// <summary>
    /// Funds reserved against open orders. Held out of <see cref="Balance"/> until the order
    /// settles or is cancelled.
    /// </summary>
    public decimal LockedBalance { get; set; } = 0;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Private constructor for EF Core
    private WalletEntity() { }

    public static WalletEntity Create(
        Guid userId,
        string asset
        )
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        if (string.IsNullOrWhiteSpace(asset))
            throw new ArgumentException("Asset cannot be empty", nameof(asset));

        return new WalletEntity
        {
            Id = Guid.NewGuid(),
            Asset = asset,
            Balance = 0,
            LockedBalance = 0,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public void IncreaseBalance(decimal amount)
    {
        if (amount <= 0)
            throw new BusinessRuleException("مقدار باید بزرگتر از صفر باشد");

        Balance += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DecreaseBalance(decimal amount)
    {
        if (amount <= 0)
            throw new BusinessRuleException("مقدار باید بزرگتر از صفر باشد");

        if (Balance - amount < 0)
            throw new BusinessRuleException("مقدار کسر از حساب بیشتر از حد مجاز است");

        Balance -= amount;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Reserves funds against an open order. Deliberately enforces no sufficiency rule: customers
    /// trade on credit and are expected to go negative down to their ceiling, and the market maker
    /// may go negative without one. The ceiling for asset <c>A</c> lives in a separate wallet row
    /// keyed <c>CREDIT_A</c>, which this single-asset entity cannot see, so the check belongs in
    /// the caller that can read both — <c>WalletApiClient.ValidateCreditAndBalanceAsync</c>.
    /// </summary>
    public void LockBalance(decimal amount)
    {
        LockedBalance += amount;
        Balance -= amount;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Releases previously-reserved funds back into <see cref="Balance"/>. Like
    /// <see cref="LockBalance"/> this enforces nothing here; releasing more than is locked would
    /// create money, and that guard lives in <c>WalletRepository.UnlockBalanceAsync</c> where the
    /// stored <see cref="LockedBalance"/> is known (issue #52).
    /// </summary>
    public void UnLockBalance(decimal amount)
    {
        LockedBalance -= amount;
        Balance += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Consumes previously-locked collateral during trade settlement: the reserved funds
    /// are spent, so they leave LockedBalance WITHOUT returning to the available Balance.
    /// Unlike UnLockBalance + DecreaseBalance, this does not trip the non-negative Balance
    /// guard, which is required for credit-backed positions whose available Balance is
    /// already negative. The debt (negative Balance) legitimately remains.
    /// </summary>
    public void ConsumeLockedBalance(decimal amount)
    {
        if (amount <= 0)
            throw new BusinessRuleException("مقدار باید بزرگتر از صفر باشد");

        LockedBalance -= amount;
        UpdatedAt = DateTime.UtcNow;
    }

}

public class WalletTransaction
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Asset { get; set; } = "";
    public decimal Amount { get; set; }
    public TransactionType Type { get; set; }
    public TransactionStatus Status { get; set; }
    public string? ReferenceId { get; set; } // Order ID, Trade ID, etc.
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

