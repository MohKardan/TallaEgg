using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.TelegramBot.Infrastructure.Repositories;

public class OrderRepository : IOrderRepository
{
    private readonly IOrderApiClient _orderApiClient;

    public OrderRepository(IOrderApiClient orderApiClient)
    {
        _orderApiClient = orderApiClient;
    }

    public async Task<Order> AddAsync(Order order)
    {
        var createdOrder = await _orderApiClient.CreateOrderAsync(
            order.Asset, 
            order.Amount, 
            order.Price, 
            order.UserId, 
            order.Type);
        
        if (createdOrder == null)
            throw new InvalidOperationException("Failed to create order");
            
        return createdOrder;
    }

    public async Task<List<Order>> GetOrdersByAssetAsync(string asset)
    {
        var orders = await _orderApiClient.GetOrdersByAssetAsync(asset);
        return orders.ToList();
    }

    public async Task<Order?> GetByIdAsync(Guid orderId)
    {
        // This would need to be implemented in the API
        throw new NotImplementedException();
    }

    public async Task<Order> UpdateAsync(Order order)
    {
        // This would need to be implemented in the API
        throw new NotImplementedException();
    }
} 