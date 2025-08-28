using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.Models
{
    public class Symbol
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BaseAsset { get; set; } = string.Empty;
        public string QuoteAsset { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public decimal MinOrderAmount { get; set; }
        public decimal MaxOrderAmount { get; set; }
        public int PricePrecision { get; set; }
        public int QuantityPrecision { get; set; }
        public bool IsSpotTradingEnabled { get; set; }
        public bool IsFuturesTradingEnabled { get; set; }
        public SymbolStatus Status { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
