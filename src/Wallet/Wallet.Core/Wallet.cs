using System.ComponentModel;

namespace Wallet.Core;

public class WalletEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Asset { get; set; } = "";
    public decimal Balance { get; set; }
    /// <summary>
    /// FrozenBalance برای مبالغ بلوکه شده
    /// </summary>
    public decimal LockedBalance { get; set; } = 0; // For pending orders
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
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

public enum TransactionType
{
    [Description("واریز")]
    Deposit,
    
    [Description("برداشت")]
    Withdraw,
    
    [Description("معامله")]
    Trade,
    
    [Description("فریز کردن موجودی")]
    Freeze,
    
    [Description("آزادسازی موجودی")]
    Unfreeze,
    
    [Description("کارمزد")]
    Fee,
    
    [Description("انتقال")]
    Transfer,
    
    [Description("تعدیل")]
    Adjustment
}

public enum TransactionStatus
{
    [Description("در انتظار")]
    Pending,
    
    [Description("تکمیل شده")]
    Completed,
    
    [Description("ناموفق")]
    Failed,
    
    [Description("لغو شده")]
    Canceled
} 