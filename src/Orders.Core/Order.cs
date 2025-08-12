using System.ComponentModel.DataAnnotations;
using TallaEgg.Core.Enums.Order;

namespace Orders.Core;



public class Order
{
    public Guid Id { get; private set; }
    public string Asset { get; private set; }
    public decimal Amount { get; private set; }
    public decimal Price { get; private set; }
    public Guid UserId { get; private set; }
    public OrderType Type { get; private set; }
    public OrderStatus Status { get; private set; }
    public TradingType TradingType { get; private set; }
    public OrderRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ParentOrderId { get; private set; } // برای Taker orders که به Maker order متصل می‌شوند

    // Private constructor for EF Core
    private Order() { }

    public static Order CreateMakerOrder(
        string asset, 
        decimal amount, 
        decimal price, 
        Guid userId, 
        OrderType type,
        TradingType tradingType,
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
            TradingType = tradingType,
            Role = OrderRole.Maker,
            CreatedAt = DateTime.UtcNow,
            Notes = notes
        };
    }

    public static Order CreateTakerOrder(
        Guid parentOrderId,
        decimal amount,
        Guid userId,
        string? notes = null)
    {
        if (parentOrderId == Guid.Empty)
            throw new ArgumentException("ParentOrderId cannot be empty", nameof(parentOrderId));
        
        if (amount <= 0)
            throw new ArgumentException("Amount must be greater than zero", nameof(amount));
        
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        return new Order
        {
            Id = Guid.NewGuid(),
            Asset = string.Empty, // Will be set from parent order
            Amount = amount,
            Price = 0, // Will be set from parent order
            UserId = userId,
            Type = OrderType.Buy, // Will be opposite of parent order
            Status = OrderStatus.Pending,
            TradingType = TradingType.Spot, // Will be set from parent order
            Role = OrderRole.Taker,
            CreatedAt = DateTime.UtcNow,
            ParentOrderId = parentOrderId,
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

    public void AcceptTakerOrder(Order takerOrder)
    {
        if (Role != OrderRole.Maker)
            throw new InvalidOperationException("Only maker orders can accept taker orders");
        
        if (Status != OrderStatus.Pending)
            throw new InvalidOperationException("Only pending maker orders can accept taker orders");
        
        if (takerOrder.Role != OrderRole.Taker)
            throw new ArgumentException("Only taker orders can be accepted");
        
        if (takerOrder.Amount > Amount)
            throw new ArgumentException("Taker order amount cannot exceed maker order amount");
        
        // Update amounts
        Amount -= takerOrder.Amount;
        
        // If maker order is fully filled, complete it
        if (Amount <= 0)
        {
            Complete();
        }
        
        UpdatedAt = DateTime.UtcNow;
    }

    public decimal GetTotalValue() => Amount * Price;

    public bool IsActive() => Status == OrderStatus.Pending || Status == OrderStatus.Confirmed;

    public bool CanBeCancelled() => Status == OrderStatus.Pending || Status == OrderStatus.Confirmed;

    public bool IsMaker() => Role == OrderRole.Maker;
    
    public bool IsTaker() => Role == OrderRole.Taker;

    public bool IsSpot() => TradingType == TradingType.Spot;
    
    public bool IsFutures() => TradingType == TradingType.Futures;
}