using System.ComponentModel.DataAnnotations;
using Orders.Core;

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
    
    [StringLength(500)]
    public string? Notes { get; init; }

    public CreateOrderCommand(
        string asset, 
        decimal amount, 
        decimal price, 
        Guid userId, 
        OrderType type,
        string? notes = null)
    {
        Asset = asset?.Trim() ?? throw new ArgumentNullException(nameof(asset));
        Amount = amount;
        Price = price;
        UserId = userId;
        Type = type;
        Notes = notes;
    }
}