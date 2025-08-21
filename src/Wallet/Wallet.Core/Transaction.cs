using System.ComponentModel.DataAnnotations;
using TallaEgg.Core.Enums.Wallet;

namespace Wallet.Core;

public class Transaction
{
    public Guid Id { get; private set; }
    public Guid WalletId { get; private set; }
    public decimal Amount { get; private set; }
    public string Currency { get; private set; } = "";
    public TransactionType Type { get; private set; }
    public TransactionStatus Status { get; private set; }
    public string TrackingCode { get; private set; } = string.Empty;
    public decimal BallanceBefore { get; set; }
    public decimal BallanceAfter { get; set; }
    public string? ReferenceId { get; private set; } // Order ID, Trade ID, etc.
    public string? Description { get; private set; }
    public string? Detail { get; private set; } // JSON data for additional transaction information
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Navigation property
    public virtual WalletEntity Wallet { get; private set; } = null!;

    // Private constructor for EF Core
    private Transaction() { }

    public static Transaction Create(
        Guid walletId,
        decimal amount,
        string currency,
        TransactionType type,
        decimal ballanceBefore,
        decimal ballanceAfter,
        string? trackingCode,
        TransactionStatus transactionStatus,
        string? description = null,
        string? referenceId = null,
        string? detail = null)
    {
        if (walletId == Guid.Empty)
            throw new ArgumentException("WalletId cannot be empty", nameof(walletId));
        
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        
        if (string.IsNullOrWhiteSpace(currency))
            throw new ArgumentException("Currency cannot be empty", nameof(currency));

        if (string.IsNullOrWhiteSpace(trackingCode))
            trackingCode = GenerateTrackingCode();

        return new Transaction
        {
            Id = Guid.NewGuid(),
            WalletId = walletId,
            Amount = amount,
            Currency = currency.Trim().ToUpperInvariant(),
            Type = type,
            BallanceBefore = ballanceBefore,
            BallanceAfter = ballanceAfter,
            TrackingCode = trackingCode,
            Status = transactionStatus,
            ReferenceId = referenceId,
            Description = description,
            Detail = detail,
            CreatedAt = DateTime.UtcNow
        };
    }

    private static string GenerateTrackingCode()
    {
        // یک کد کوتاه یکتا (مثلاً ۱۲ کاراکتر)
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("=", "")
            .Replace("+", "")
            .Replace("/", "")
            .Substring(0, 12)
            .ToUpperInvariant();
    }

    public void Complete()
    {
        if (Status != TransactionStatus.Pending)
            throw new InvalidOperationException("Only pending transactions can be completed");
        
        Status = TransactionStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure reason cannot be empty", nameof(reason));
        
        Status = TransactionStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
        Description = reason;
    }

    public void Cancel(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Cancellation reason cannot be empty", nameof(reason));
        
        Status = TransactionStatus.Canceled;
        UpdatedAt = DateTime.UtcNow;
        Description = reason;
    }

    public void UpdateDescription(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));
        
        Description = description;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateDetail(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail))
            throw new ArgumentException("Detail cannot be empty", nameof(detail));
        
        Detail = detail;
        UpdatedAt = DateTime.UtcNow;
    }

    public bool IsPending() => Status == TransactionStatus.Pending;
    public bool IsCompleted() => Status == TransactionStatus.Completed;
    public bool IsFailed() => Status == TransactionStatus.Failed;
    public bool IsCancelled() => Status == TransactionStatus.Canceled;
    public bool CanBeModified() => Status == TransactionStatus.Pending;
}
