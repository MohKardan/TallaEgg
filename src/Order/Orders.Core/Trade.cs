namespace Orders.Core;

public class Trade
{
    public Guid Id { get; private set; }
    public Guid BuyOrderId { get; private set; }
    public Guid SellOrderId { get; private set; }
    public Guid MakerOrderId { get; private set; }
    public Guid TakerOrderId { get; private set; }
    public string Symbol { get; private set; } = "";
    public decimal Price { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal QuoteQuantity { get; private set; }
    public Guid BuyerUserId { get; private set; }
    public Guid SellerUserId { get; private set; }
    public Guid MakerUserId { get; private set; }
    public Guid TakerUserId { get; private set; }
    public decimal FeeBuyer { get; private set; }
    public decimal FeeSeller { get; private set; }
    public decimal MakerFee { get; private set; }
    public decimal TakerFee { get; private set; }
    public decimal MakerFeeRate { get; private set; } = 0.001m; // 0.1% default
    public decimal TakerFeeRate { get; private set; } = 0.002m; // 0.2% default
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    // Navigation properties
    public virtual Order BuyOrder { get; private set; } = null!;
    public virtual Order SellOrder { get; private set; } = null!;
    public virtual Order MakerOrder { get; private set; } = null!;
    public virtual Order TakerOrder { get; private set; } = null!;

    // Private constructor for EF Core
    private Trade() { }

    public static Trade Create(
        Guid buyOrderId,
        Guid sellOrderId,
        Guid makerOrderId,
        Guid takerOrderId,
        string symbol,
        decimal price,
        decimal quantity,
        decimal quoteQuantity,
        Guid buyerUserId,
        Guid sellerUserId,
        Guid makerUserId,
        Guid takerUserId,
        decimal makerFeeRate = 0.001m,  // 0.1%
        decimal takerFeeRate = 0.002m,  // 0.2%
        decimal feeBuyer = 0,
        decimal feeSeller = 0)
    {
        if (buyOrderId == Guid.Empty)
            throw new ArgumentException("BuyOrderId cannot be empty", nameof(buyOrderId));
        
        if (sellOrderId == Guid.Empty)
            throw new ArgumentException("SellOrderId cannot be empty", nameof(sellOrderId));
        
        if (makerOrderId == Guid.Empty)
            throw new ArgumentException("MakerOrderId cannot be empty", nameof(makerOrderId));
        
        if (takerOrderId == Guid.Empty)
            throw new ArgumentException("TakerOrderId cannot be empty", nameof(takerOrderId));
        
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
        
        if (makerUserId == Guid.Empty)
            throw new ArgumentException("MakerUserId cannot be empty", nameof(makerUserId));
        
        if (takerUserId == Guid.Empty)
            throw new ArgumentException("TakerUserId cannot be empty", nameof(takerUserId));
        
        if (makerFeeRate < 0)
            throw new ArgumentException("MakerFeeRate cannot be negative", nameof(makerFeeRate));
        
        if (takerFeeRate < 0)
            throw new ArgumentException("TakerFeeRate cannot be negative", nameof(takerFeeRate));
        
        if (feeBuyer < 0)
            throw new ArgumentException("FeeBuyer cannot be negative", nameof(feeBuyer));
        
        if (feeSeller < 0)
            throw new ArgumentException("FeeSeller cannot be negative", nameof(feeSeller));

        // Calculate fees based on roles
        var totalTradeValue = quantity * price;
        var makerFee = totalTradeValue * makerFeeRate;
        var takerFee = totalTradeValue * takerFeeRate;

        return new Trade
        {
            Id = Guid.NewGuid(),
            BuyOrderId = buyOrderId,
            SellOrderId = sellOrderId,
            MakerOrderId = makerOrderId,
            TakerOrderId = takerOrderId,
            Symbol = symbol.Trim().ToUpperInvariant(),
            Price = price,
            Quantity = quantity,
            QuoteQuantity = quoteQuantity,
            BuyerUserId = buyerUserId,
            SellerUserId = sellerUserId,
            MakerUserId = makerUserId,
            TakerUserId = takerUserId,
            MakerFeeRate = makerFeeRate,
            TakerFeeRate = takerFeeRate,
            MakerFee = makerFee,
            TakerFee = takerFee,
            FeeBuyer = feeBuyer > 0 ? feeBuyer : (buyerUserId == makerUserId ? makerFee : takerFee),
            FeeSeller = feeSeller > 0 ? feeSeller : (sellerUserId == makerUserId ? makerFee : takerFee),
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
