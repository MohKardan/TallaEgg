namespace TallaEgg.TelegramBot.Core.Models;

public class Order
{
    public Guid Id { get; set; }
    public string Asset { get; set; } = "Gold";
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "BUY"; // BUY یا SELL
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Pending;
}

public enum OrderStatus
{
    Pending,
    Completed,
    Cancelled,
    Failed
} 