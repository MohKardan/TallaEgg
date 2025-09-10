using System.ComponentModel;
using TallaEgg.Core.Enums.Wallet;

namespace Wallet.Core;

public class WalletEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public WalletType Type { get; set; }
    public string Asset { get; set; } = "";
    public decimal Balance { get; set; }
    /// <summary>
    /// FrozenBalance برای مبالغ بلوکه شده
    /// </summary>
    public decimal LockedBalance { get; set; } = 0; // For pending orders
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
            throw new ArgumentException("مقدار باید بزرگتر از صفر باشد", nameof(amount));

        Balance += amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void DecreaseBalance(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentException("مقدار باید بزرگتر از صفر باشد", nameof(amount));

        if (Balance - amount < 0)
            throw new ArgumentException("مقدار کسر از حساب بیشتر از حد مجاز است");

        Balance -= amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void LockBalance(decimal amount)
    {
        if (Balance < amount) throw new ArgumentNullException("موجودی کافی نیست", nameof(amount));


        LockedBalance += amount;
        Balance -= amount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UnLockBalance(decimal amount)
    {
        if(LockedBalance < amount) throw new ArgumentNullException("موجودی قفل شده کافی نیست", nameof(amount));

        LockedBalance -= amount;
        Balance += amount;
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

