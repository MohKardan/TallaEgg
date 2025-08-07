using System.ComponentModel.DataAnnotations;

namespace Orders.Core;

public enum OrderType
{
    Buy,
    Sell
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed,
    Failed
}

public class Order
{
    public Guid Id { get; private set; }
    public string Asset { get; private set; }
    public decimal Amount { get; private set; }
    public decimal Price { get; private set; }
    public Guid UserId { get; private set; }
    public OrderType Type { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? Notes { get; private set; }

    // Private constructor for EF Core
    private Order() { }

    public static Order Create(
        string asset, 
        decimal amount, 
        decimal price, 
        Guid userId, 
        OrderType type,
        string? notes = null)
    {
        if (string.IsNullOrWhiteSpace(asset))
            throw new ArgumentException("Asset cannot be empty", nameof(asset));
        
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));
        
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        return new Order
        {
            Id = Guid.NewGuid(),
            Asset = asset.Trim().ToUpperInvariant(),
            Amount = amount,
            Price = price,
            UserId = userId,
            Type = type,
            Status = OrderStatus.Pending,
            CreatedAt = DateTime.UtcNow,
            Notes = notes
        };
    }

    public void Confirm()
    {
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending orders can be confirmed");
        
        Status = OrderStatus.Confirmed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Cancel(string? reason = null)
    {
        if (Status == OrderStatus.Completed)
            throw new InvalidOperationException("Completed orders cannot be cancelled");
        
        Status = OrderStatus.Cancelled;
        UpdatedAt = DateTime.UtcNow;
        Notes = reason ?? "Order cancelled";
    }

    public void Complete()
    {
        if (Status != OrderStatus.Confirmed)
            throw new InvalidOperationException("Only confirmed orders can be completed");
        
        Status = OrderStatus.Completed;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Fail(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Failure reason cannot be empty", nameof(reason));
        
        Status = OrderStatus.Failed;
        UpdatedAt = DateTime.UtcNow;
        Notes = reason;
    }

    public decimal GetTotalValue() => Amount * Price;

    public bool IsActive() => Status == OrderStatus.Pending || Status == OrderStatus.Confirmed;

    public bool CanBeCancelled() => Status == OrderStatus.Pending || Status == OrderStatus.Confirmed;
}