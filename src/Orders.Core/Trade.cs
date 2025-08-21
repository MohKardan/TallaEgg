namespace Orders.Core;

public class Trade
{
    public Guid Id { get; private set; }
    public Guid BuyOrderId { get; private set; }
    public Guid SellOrderId { get; private set; }
    public string Symbol { get; private set; } = "";
    public decimal Price { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal QuoteQuantity { get; private set; }
    public Guid BuyerUserId { get; private set; }
    public Guid SellerUserId { get; private set; }
    public decimal FeeBuyer { get; private set; }
    public decimal FeeSeller { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Navigation properties
    public virtual Order BuyOrder { get; private set; } = null!;
    public virtual Order SellOrder { get; private set; } = null!;

    // Private constructor for EF Core
    private Trade() { }

    public static Trade Create(
        Guid buyOrderId,
        Guid sellOrderId,
        string symbol,
        decimal price,
        decimal quantity,
        decimal quoteQuantity,
        Guid buyerUserId,
        Guid sellerUserId,
        decimal feeBuyer = 0,
        decimal feeSeller = 0)
    {
        if (buyOrderId == Guid.Empty)
            throw new ArgumentException("BuyOrderId cannot be empty", nameof(buyOrderId));
        
        if (sellOrderId == Guid.Empty)
            throw new ArgumentException("SellOrderId cannot be empty", nameof(sellOrderId));
        
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        
        if (price <= 0)
            throw new ArgumentException("Price must be greater than zero", nameof(price));
        
        if (quantity <= 0)
            throw new ArgumentException("Quantity must be greater than zero", nameof(quantity));
        
        if (quoteQuantity <= 0)
            throw new ArgumentException("QuoteQuantity must be greater than zero", nameof(quoteQuantity));
        
        if (buyerUserId == Guid.Empty)
            throw new ArgumentException("BuyerUserId cannot be empty", nameof(buyerUserId));
        
        if (sellerUserId == Guid.Empty)
            throw new ArgumentException("SellerUserId cannot be empty", nameof(sellerUserId));
        
        if (feeBuyer < 0)
            throw new ArgumentException("FeeBuyer cannot be negative", nameof(feeBuyer));
        
        if (feeSeller < 0)
            throw new ArgumentException("FeeSeller cannot be negative", nameof(feeSeller));

        return new Trade
        {
            Id = Guid.NewGuid(),
            BuyOrderId = buyOrderId,
            SellOrderId = sellOrderId,
            Symbol = symbol.Trim().ToUpperInvariant(),
            Price = price,
            Quantity = quantity,
            QuoteQuantity = quoteQuantity,
            BuyerUserId = buyerUserId,
            SellerUserId = sellerUserId,
            FeeBuyer = feeBuyer,
            FeeSeller = feeSeller,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void UpdateFees(decimal feeBuyer, decimal feeSeller)
    {
        if (feeBuyer < 0)
            throw new ArgumentException("FeeBuyer cannot be negative", nameof(feeBuyer));
        
        if (feeSeller < 0)
            throw new ArgumentException("FeeSeller cannot be negative", nameof(feeSeller));

        FeeBuyer = feeBuyer;
        FeeSeller = feeSeller;
        UpdatedAt = DateTime.UtcNow;
    }

    public decimal GetTotalValue() => Quantity * Price;
    
    public decimal GetTotalQuoteValue() => QuoteQuantity;
    
    public decimal GetTotalFees() => FeeBuyer + FeeSeller;
}
