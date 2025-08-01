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
} 