namespace TallaEgg.TelegramBot.Core.Models;

public class Price
{
    public Guid Id { get; set; }
    public string Asset { get; set; } = "";
    public decimal BuyPrice { get; set; }
    public decimal SellPrice { get; set; }
    public DateTime UpdatedAt { get; set; }
    public string Source { get; set; } = ""; // Source of the price data
} 