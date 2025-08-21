namespace Orders.Core;

public class Trade
{
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid SymbolId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal Price { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Private constructor for EF Core
    private Trade() { }

    public static Trade Create(Guid orderId, Guid symbolId, decimal quantity, decimal price)
    {
        if (orderId == Guid.Empty)
            throw new ArgumentException("OrderId cannot be empty", nameof(orderId));
        
        if (symbolId == Guid.Empty)
            throw new ArgumentException("SymbolId cannot be empty", nameof(symbolId));
        
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));

        return new Trade
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            SymbolId = symbolId,
            Quantity = quantity,
            Price = price,
            CreatedAt = DateTime.UtcNow
        };
    }

    public decimal GetTotalValue() => Quantity * Price;
}
