using System.ComponentModel.DataAnnotations;
using TallaEgg.Core.Enums.Order;

namespace Orders.Core;

public class Order
{
    public Guid Id { get; private set; }
    public string Asset { get; private set; } = string.Empty;
    public decimal Amount { get; private set; } // مقدار اولیه سفارش - تغییر نمی‌کند
    public decimal RemainingAmount { get; private set; } // مقدار باقی‌مانده سفارش
    public decimal Price { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>
    /// Order side: buy or sell.
    /// </summary>
    public OrderSide Side { get; private set; }

    /// <summary>
    /// How the order was placed: the price was named (<see cref="OrderType.Limit"/>) or taken
    /// (<see cref="OrderType.Market"/>).
    ///
    /// <para>
    /// The two sides of one quote fill disagree on this, and that is the model rather than a
    /// rounding of it: the dealer published the price, so their side is a limit order, while the
    /// customer took the price that was already there, so theirs is a market order. One trade, one
    /// price, two order types.
    /// </para>
    ///
    /// <para>
    /// Assigned by the factory, like every other value here. It used to carry a public setter and
    /// no caller, so every order ever written held the enum default — <c>Market</c> — including the
    /// dealer's half of every fill (issue #250). Rows written before this fix still hold it.
    /// </para>
    /// </summary>
    public OrderType Type { get; private set; }
    public OrderStatus Status { get; private set; }
    public TradingType TradingType { get; private set; }
    public OrderRole Role { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }
    public string? Notes { get; private set; }
    // Set on a taker order to point at the maker order it filled against.
    public Guid? ParentOrderId { get; private set; }

    /// <param name="orderType">
    /// How the price was arrived at — see <see cref="Type"/>. Required rather than defaulted: a
    /// default is what let every order in the database claim to be a market order (issue #250), and
    /// the answer differs between the two sides of a single fill, so no default can be right for
    /// both.
    /// </param>
    public static Order CreateMakerOrder(
        string asset,
        decimal amount,
        decimal price,
        Guid userId,
        OrderSide type,
        OrderType orderType,
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
            Type = orderType,
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
            // Set here rather than left at the enum default, which would have this factory produce
            // an order recording Market despite its name. It has no callers — issue #250 files it
            // with the other unreferenced declarations rather than deleting it here — but the
            // setter is private now, so a future caller would have no way to correct it.
            Type = OrderType.Limit,
            Status = OrderStatus.Pending,
            TradingType = TradingType.Spot, // Default to Spot for now
            Role = OrderRole.Maker,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    // CreateMarketOrder and CreateTakerOrder were removed.
    //
    // Neither was called from production code — CreateMakerOrder is the only real factory. Both
    // set Role to Taker, a state no order actually reaches, which is why the maker/taker model
    // looked like it worked at first glance while investigating #35.
    //
    // CreateTakerOrder was also incomplete: it left symbol, price and side blank to be "filled in
    // from the parent order" — code that was never written. It also carried a broken check,
    // userId == Guid.NewGuid(), which can never be true.
    //
    // The maker/taker role is a property of a fill, not of an order, and Trade records it
    // correctly. Details in issue #35.

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

    // AcceptTakerOrder was removed as unreachable: it required takerOrder.Role == Taker, a state
    // no production order reaches. Real matching happens in ExecuteAtomicMatchAsync.
    //
    // IsMaker/IsTaker went too: Role is always Maker, so IsMaker() was always true and IsTaker()
    // always false. Their only use was an always-true filter in the best-price calculation, which
    // looked meaningful but filtered nothing.

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