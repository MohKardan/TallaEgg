using System.ComponentModel.DataAnnotations;
using Orders.Core;
using TallaEgg.Core.Enums.Order;

namespace Orders.Application;

public record CreateOrderCommand
{
    [Required]
    [StringLength(50, MinimumLength = 1)]
    public string Asset { get; init; } = string.Empty;
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; init; }
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than zero")]
    public decimal Price { get; init; }
    
    [Required]
    public Guid UserId { get; init; }
    
    /// <summary>
    /// The side — buy or sell. Named <c>Type</c> for historical reasons; the order <i>type</i> is
    /// <see cref="OrderType"/> below. The same misnomer sits on <c>OrderHistoryDto.Type</c>, where
    /// it reaches the wire and so costs more to correct; issue #250 records both and deliberately
    /// renames neither here.
    /// </summary>
    [Required]
    public OrderSide Type { get; init; }

    /// <summary>
    /// How the price was arrived at — named, or taken. See <see cref="Order.Type"/> for why the two
    /// sides of one quote fill hold different values.
    /// </summary>
    [Required]
    public OrderType OrderType { get; init; }

    [Required]
    public TradingType TradingType { get; init; }

    [StringLength(500)]
    public string? Notes { get; init; }

    public CreateOrderCommand(
        string asset,
        decimal amount,
        decimal price,
        Guid userId,
        OrderSide type,
        OrderType orderType,
        TradingType tradingType,
        string? notes = null)
    {
        Asset = asset?.Trim() ?? throw new ArgumentNullException(nameof(asset));
        Amount = amount;
        Price = price;
        UserId = userId;
        Type = type;
        OrderType = orderType;
        TradingType = tradingType;
        Notes = notes;
    }
}

public record CreateTakerOrderCommand
{
    [Required]
    public Guid ParentOrderId { get; init; }
    
    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero")]
    public decimal Amount { get; init; }
    
    [Required]
    public Guid UserId { get; init; }
    
    [StringLength(500)]
    public string? Notes { get; init; }

    public CreateTakerOrderCommand(
        Guid parentOrderId,
        decimal amount,
        Guid userId,
        string? notes = null)
    {
        ParentOrderId = parentOrderId;
        Amount = amount;
        UserId = userId;
        Notes = notes;
    }
}