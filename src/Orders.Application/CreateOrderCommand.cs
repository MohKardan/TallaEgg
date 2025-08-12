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
    
    [Required]
    public OrderType Type { get; init; }
    
    [Required]
    public TradingType TradingType { get; init; }
    
    [StringLength(500)]
    public string? Notes { get; init; }

    public CreateOrderCommand(
        string asset, 
        decimal amount, 
        decimal price, 
        Guid userId, 
        OrderType type,
        TradingType tradingType,
        string? notes = null)
    {
        Asset = asset?.Trim() ?? throw new ArgumentNullException(nameof(asset));
        Amount = amount;
        Price = price;
        UserId = userId;
        Type = type;
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