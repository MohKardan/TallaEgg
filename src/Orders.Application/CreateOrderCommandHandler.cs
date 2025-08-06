using Orders.Core;
using Orders.Infrastructure;

namespace Orders.Application;

// تعریف اینترفیس مجوز داخلی پروژه (در صورت نیاز آن را در فایل جداگانه قرار دهید)
public interface IAuthorizationService
{
    Task<bool> CanCreateOrderAsync(Guid userId);
    Task<bool> CanManageUsersAsync(Guid userId); // اضافه شد
}

// نمونه ساده پیاده‌سازی سرویس مجوز (در پروژه خود باید این را کامل‌تر کنید)
public class AuthorizationService : IAuthorizationService
{
    public async Task<bool> CanCreateOrderAsync(Guid userId)
    {
        // منطق بررسی نقش کاربر (مثلاً فقط ادمین‌ها مجاز باشند)
        // اینجا فقط برای نمونه true برمی‌گردد
        return await Task.FromResult(true);
    }

    public async Task<bool> CanManageUsersAsync(Guid userId)
    {
        // منطق بررسی نقش کاربر برای مدیریت کاربران (مثلاً فقط ادمین‌ها)
        // اینجا فقط برای نمونه true برمی‌گردد
        return await Task.FromResult(true);
    }
}

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
        // اطمینان حاصل کنید که متد AddAsync مقدار Order را برمی‌گرداند و نه void
        var createdOrder = await _repo.AddAsync(order);
        return createdOrder;
    }
}