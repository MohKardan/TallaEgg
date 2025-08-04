using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Application.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _orderRepository;

    public OrderService(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<Order> CreateOrderAsync(string asset, decimal amount, decimal price, Guid userId, string type)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Asset = asset,
            Amount = amount,
            Price = price,
            UserId = userId,
            Type = type,
            CreatedAt = DateTime.UtcNow,
            Status = OrderStatus.Pending
        };
        
        return await _orderRepository.AddAsync(order);
    }

    public async Task<IEnumerable<Order>> GetOrdersByAssetAsync(string asset)
    {
        return await _orderRepository.GetOrdersByAssetAsync(asset);
    }

    public async Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        return await _orderRepository.GetByIdAsync(orderId);
    }

    public async Task<IEnumerable<Order>> GetUserActiveOrdersAsync(Guid userId)
    {
        var allOrders = await _orderRepository.GetOrdersByAssetAsync("Gold"); // فعلاً فقط طلا
        return allOrders.Where(o => o.UserId == userId && o.Status == OrderStatus.Pending);
    }

    public async Task<Order> CancelOrderAsync(Guid orderId, Guid userId, string reason = "Cancelled by user")
    {
        var order = await _orderRepository.GetByIdAsync(orderId);
        if (order == null)
        {
            throw new InvalidOperationException("سفارش یافت نشد.");
        }

        if (order.UserId != userId)
        {
            throw new InvalidOperationException("شما مجاز به لغو این سفارش نیستید.");
        }

        if (order.Status != OrderStatus.Pending)
        {
            throw new InvalidOperationException("این سفارش قابل لغو نیست.");
        }

        order.Status = OrderStatus.Cancelled;
        order.CancelledAt = DateTime.UtcNow;
        order.CancelledBy = userId.ToString();
        order.CancellationReason = reason;

        return await _orderRepository.UpdateAsync(order);
    }

    public async Task<bool> CancelAllUserOrdersAsync(Guid userId, string reason = "Cancelled by user")
    {
        var activeOrders = await GetUserActiveOrdersAsync(userId);
        foreach (var order in activeOrders)
        {
            await CancelOrderAsync(order.Id, userId, reason);
        }
        return true;
    }

    public async Task<bool> HasActiveOrdersAsync(Guid userId)
    {
        var activeOrders = await GetUserActiveOrdersAsync(userId);
        return activeOrders.Any();
    }

    public async Task<bool> CanUserCreateOrderAsync(Guid userId)
    {
        // فعلاً فقط کاربران admin می‌توانند سفارش ایجاد کنند
        // این منطق باید بر اساس نقش کاربر باشد
        return true; // فعلاً همه می‌توانند
    }
} 