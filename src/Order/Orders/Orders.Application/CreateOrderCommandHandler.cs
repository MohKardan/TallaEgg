using Orders.Core;
using Orders.Infrastructure;

namespace Orders.Application;

public class CreateOrderCommandHandler
{
    private readonly IOrderRepository _repo;

    public CreateOrderCommandHandler(IOrderRepository repo)
    {
        _repo = repo;
    }

    public async Task<Order> Handle(CreateOrderCommand cmd)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Asset = cmd.Asset,
            Amount = cmd.Amount,
            Price = cmd.Price,
            UserId = cmd.UserId,
            Type = cmd.Type,
            CreatedAt = DateTime.UtcNow
        };
        return await _repo.AddAsync(order);
    }
}