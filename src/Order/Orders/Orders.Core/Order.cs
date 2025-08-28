namespace Orders.Core;

public class Order
{
    public Guid Id { get; set; }
    public string Asset { get; set; } = "Gold";
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "BUY"; // BUY یا SELL
    public DateTime CreatedAt { get; set; }
}