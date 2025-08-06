using Orders.Core;
using Orders.Infrastructure;
using Users.Application;

namespace Orders.Application;

public class CreateOrderCommandHandler
{
    private readonly IOrderRepository _repo;
    private readonly IAuthorizationService _authService;

    public CreateOrderCommandHandler(IOrderRepository repo, IAuthorizationService authService)
    {
        _repo = repo;
        _authService = authService;
    }

    public async Task<Order> Handle(CreateOrderCommand cmd)
    {
        // بررسی مجوز کاربر برای ثبت سفارش
        var canCreateOrder = await _authService.CanCreateOrderAsync(cmd.UserId);
        if (!canCreateOrder)
        {
            throw new UnauthorizedAccessException("شما مجوز ثبت سفارش ندارید. فقط مدیران می‌توانند سفارش ثبت کنند.");
        }

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