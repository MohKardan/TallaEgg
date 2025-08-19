namespace Wallet.Core;

public class WalletEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Asset { get; set; } = "";
    public decimal Balance { get; set; }
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
    Deposit,        // واریز
    Withdrawal,     // برداشت
    Buy,           // خرید
    Sell,          // فروش
    Fee,           // کارمزد
    Transfer,      // انتقال
    Adjustment     // تعدیل
}

public enum TransactionStatus
{
    Pending,    // در انتظار
    Completed,  // تکمیل شده
    Failed,     // ناموفق
    Cancelled   // لغو شده
} 