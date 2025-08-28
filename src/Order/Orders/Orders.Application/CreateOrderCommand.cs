namespace Orders.Application;

public record CreateOrderCommand(string Asset, decimal Amount, decimal Price, Guid UserId, string Type);