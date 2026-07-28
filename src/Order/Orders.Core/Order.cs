using System.ComponentModel.DataAnnotations;
using TallaEgg.Core.Enums.Order;

namespace Orders.Core;

public class Order
{
    public Guid Id { get; private set; }
    public string Asset { get; private set; }
    public decimal Amount { get; private set; } // مقدار اولیه سفارش - تغییر نمی‌کند
    public decimal RemainingAmount { get; private set; } // مقدار باقی‌مانده سفارش
    public decimal Price { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>
    /// جهت سفارش (خرید یا فروش)
    /// </summary>
    public OrderSide Side { get; private set; }
    public OrderType Type { get; set; }
    public OrderStatus Status { get; private set; }
    public TradingType TradingType { get; private set; }
    public OrderRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? Notes { get; private set; }
    public Guid? ParentOrderId { get; private set; } // برای Taker orders که به Maker order متصل می‌شوند

    // Private constructor for EF Core
    //private Order() { }

    public static Order CreateMakerOrder(
        string asset, 
        decimal amount, 
        decimal price, 
        Guid userId, 
        OrderSide type,
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
            RemainingAmount = amount, // مقدار اولیه برابر با مقدار باقی‌مانده
            Price = price,
            UserId = userId,
            Side = type,
            Status = OrderStatus.Pending,
            TradingType = tradingType,
            Role = OrderRole.Maker,
            CreatedAt = DateTime.UtcNow,
            Notes = notes
        };
    }

    public static Order CreateLimitOrder(
        string symbol, 
        decimal quantity, 
        decimal price, 
        Guid userId)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));
        
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));

        return new Order
        {
            Id = Guid.NewGuid(),
            Asset = symbol.Trim().ToUpperInvariant(),
            Amount = quantity,
            RemainingAmount = quantity, // مقدار اولیه برابر با مقدار باقی‌مانده
            Price = price,
            UserId = userId,
            Side = OrderSide.Buy, // Default to Buy for now
            Status = OrderStatus.Pending,
            TradingType = TradingType.Spot, // Default to Spot for now
            Role = OrderRole.Maker,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // CreateMarketOrder و CreateTakerOrder حذف شدند.
    //
    // هیچ‌کدام از کد تولیدی صدا زده نمی‌شدند — CreateMakerOrder تنها کارخانهٔ واقعی است.
    // هر دو Role را روی Taker می‌گذاشتند، وضعیتی که هیچ سفارشی در عمل به آن نمی‌رسید و
    // همین باعث شد هنگام بررسی #35 اول به نظر برسد مدل maker/taker کار می‌کند.
    //
    // CreateTakerOrder علاوه بر آن ناقص هم بود: نماد، قیمت و سمت را خالی می‌گذاشت تا
    // «از سفارش والد پر شود» — کدی که هرگز نوشته نشد. یک بررسی اشتباه هم داشت
    // (userId == Guid.NewGuid()) که هرگز درست نمی‌شود.
    //
    // نقش maker/taker خاصیت یک fill است نه یک سفارش، و Trade آن را درست ثبت می‌کند.
    // جزئیات در issue #35.

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
        // Allow completing an order that is either Confirmed or Partially filled
        if (Status != OrderStatus.Confirmed && Status != OrderStatus.Partially)
            throw new InvalidOperationException("Only confirmed or partially filled orders can be completed");
        
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

    // AcceptTakerOrder حذف شد: غیرقابل‌دسترس بود. شرط takerOrder.Role == Taker داشت،
    // وضعیتی که هیچ سفارش تولیدی به آن نمی‌رسد. تطبیق واقعی در ExecuteAtomicMatchAsync
    // انجام می‌شود.
    //
    // IsMaker/IsTaker هم حذف شدند: Role همیشه Maker است، پس IsMaker() همیشه true و
    // IsTaker() همیشه false بود. تنها استفاده‌شان یک فیلترِ همیشه‌درست در محاسبهٔ
    // «بهترین قیمت» بود که معنادار به نظر می‌رسید ولی چیزی را فیلتر نمی‌کرد.

    public decimal GetTotalValue() => RemainingAmount * Price;

    public bool IsActive() => Status == OrderStatus.Pending || Status == OrderStatus.Confirmed || Status == OrderStatus.Partially;

    public bool CanBeCancelled() => Status == OrderStatus.Pending || Status == OrderStatus.Confirmed;

    public bool IsSpot() => TradingType == TradingType.Spot;

    public void UpdateRemainingAmount(decimal newRemainingAmount)
    {
        if (newRemainingAmount < 0)
            throw new ArgumentException("Remaining amount cannot be negative", nameof(newRemainingAmount));
        
        if (newRemainingAmount > Amount)
            throw new ArgumentException("Remaining amount cannot exceed original amount", nameof(newRemainingAmount));
        
        if (Status == OrderStatus.Completed)
            throw new InvalidOperationException("Cannot update remaining amount of completed order");
        
        RemainingAmount = newRemainingAmount;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateStatus(OrderStatus newStatus)
    {
        if (Status == OrderStatus.Completed)
            throw new InvalidOperationException("Cannot update status of completed order");
        
        Status = newStatus;
        UpdatedAt = DateTime.UtcNow;
    }
}