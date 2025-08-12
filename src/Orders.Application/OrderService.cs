using Orders.Core;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;

namespace Orders.Application;

//public interface IOrderService
//{
//    Task<Order> CreateMakerOrderAsync(CreateOrderCommand command);
//    Task<Order> CreateTakerOrderAsync(CreateTakerOrderCommand command);
//    Task<Order?> GetOrderByIdAsync(Guid orderId);
//    Task<List<Order>> GetOrdersByAssetAsync(string asset);
//    Task<List<Order>> GetOrdersByUserIdAsync(Guid userId);
//    Task<List<Order>> GetActiveOrdersAsync();
//    Task<List<Order>> GetAvailableMakerOrdersAsync(string asset, TradingType tradingType);
//    Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, string? notes = null);
//    Task<bool> CancelOrderAsync(Guid orderId, string? reason = null);
//    Task<bool> ConfirmOrderAsync(Guid orderId);
//    Task<bool> CompleteOrderAsync(Guid orderId);
//    Task<bool> FailOrderAsync(Guid orderId, string reason);
//    Task<bool> AcceptTakerOrderAsync(Guid makerOrderId, Guid takerOrderId);
//    Task<(List<Order> Orders, int TotalCount)> GetOrdersPaginatedAsync(
//        int pageNumber, 
//        int pageSize, 
//        string? asset = null, 
//        OrderType? type = null, 
//        OrderStatus? status = null,
//        TradingType? tradingType = null,
//        OrderRole? role = null);
//    Task<decimal> GetTotalValueByAssetAsync(string asset);
//    Task<int> GetOrderCountByAssetAsync(string asset);
//}

public class OrderService /*: IOrderService*/
{
    private readonly IOrderRepository _orderRepository;
    private readonly IAuthorizationService _authorizationService;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IAuthorizationService authorizationService,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Order> CreateMakerOrderAsync(CreateOrderCommand command)
    {
        try
        {
            _logger.LogInformation("Creating maker order for user {UserId} with asset {Asset} and trading type {TradingType}", 
                command.UserId, command.Asset, command.TradingType);

            // Check authorization
            var canCreateOrder = await _authorizationService.CanCreateOrderAsync(command.UserId);
            if (!canCreateOrder)
            {
                _logger.LogWarning("User {UserId} is not authorized to create orders", command.UserId);
                throw new UnauthorizedAccessException("شما مجوز ثبت سفارش ندارید. فقط مدیران می‌توانند سفارش ثبت کنند.");
            }

            // Create maker order using domain factory method
            var order = Order.CreateMakerOrder(
                command.Asset,
                command.Amount,
                command.Price,
                command.UserId,
                command.Type,
                command.TradingType,
                command.Notes
            );

            // Save to repository
            var createdOrder = await _orderRepository.AddAsync(order);
            
            _logger.LogInformation("Maker order created successfully with ID: {OrderId}", createdOrder.Id);
            return createdOrder;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error creating maker order for user {UserId}", command.UserId);
            throw new InvalidOperationException("خطا در ایجاد سفارش maker", ex);
        }
    }

    public async Task<Order> CreateTakerOrderAsync(CreateTakerOrderCommand command)
    {
        try
        {
            _logger.LogInformation("Creating taker order for user {UserId} with parent order {ParentOrderId}", 
                command.UserId, command.ParentOrderId);

            // Get parent order
            var parentOrder = await _orderRepository.GetByIdAsync(command.ParentOrderId);
            if (parentOrder == null)
            {
                throw new ArgumentException("Parent order not found", nameof(command.ParentOrderId));
            }

            if (!parentOrder.IsMaker())
            {
                throw new InvalidOperationException("Parent order must be a maker order");
            }

            if (parentOrder.Status != OrderStatus.Pending)
            {
                throw new InvalidOperationException("Parent order must be pending");
            }

            if (command.Amount > parentOrder.Amount)
            {
                throw new ArgumentException("Taker order amount cannot exceed maker order amount");
            }

            // Create taker order
            var takerOrder = Order.CreateTakerOrder(
                command.ParentOrderId,
                command.Amount,
                command.UserId,
                command.Notes
            );

            // Set properties from parent order
            takerOrder = await _orderRepository.AddAsync(takerOrder);
            
            _logger.LogInformation("Taker order created successfully with ID: {OrderId}", takerOrder.Id);
            return takerOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating taker order for user {UserId}", command.UserId);
            throw new InvalidOperationException("خطا در ایجاد سفارش taker", ex);
        }
    }

    public async Task<Order?> GetOrderByIdAsync(Guid orderId)
    {
        try
        {
            return await _orderRepository.GetByIdAsync(orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order with ID: {OrderId}", orderId);
            throw new InvalidOperationException("خطا در بازیابی سفارش", ex);
        }
    }

    public async Task<List<Order>> GetOrdersByAssetAsync(string asset)
    {
        try
        {
            return await _orderRepository.GetOrdersByAssetAsync(asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for asset: {Asset}", asset);
            throw new InvalidOperationException("خطا در بازیابی سفارشات", ex);
        }
    }

    public async Task<PagedResult<OrderHistoryDto>> GetOrdersByUserIdAsync(Guid userId, int pageNumber,int pageSize)
    {
       
            return await _orderRepository.GetOrdersByUserIdAsync(userId,pageNumber,pageSize);
       
    }

    public async Task<List<Order>> GetActiveOrdersAsync()
    {
        try
        {
            return await _orderRepository.GetActiveOrdersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active orders");
            throw new InvalidOperationException("خطا در بازیابی سفارشات فعال", ex);
        }
    }

    public async Task<List<Order>> GetAvailableMakerOrdersAsync(string asset, TradingType tradingType)
    {
        try
        {
            var orders = await _orderRepository.GetOrdersByAssetAsync(asset);
            return orders.Where(o => 
                o.IsMaker() && 
                o.IsActive() && 
                o.TradingType == tradingType)
                .OrderBy(o => o.Price)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available maker orders for asset: {Asset}", asset);
            throw new InvalidOperationException("خطا در بازیابی سفارشات maker موجود", ex);
        }
    }

    public async Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, string? notes = null)
    {
        try
        {
            _logger.LogInformation("Updating order {OrderId} status to {Status}", orderId, status);
            return await _orderRepository.UpdateStatusAsync(orderId, status, notes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for order {OrderId}", orderId);
            throw new InvalidOperationException("خطا در به‌روزرسانی وضعیت سفارش", ex);
        }
    }

    public async Task<bool> CancelOrderAsync(Guid orderId, string? reason = null)
    {
        try
        {
            _logger.LogInformation("Cancelling order {OrderId}", orderId);
            return await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Cancelled, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cancelling order {OrderId}", orderId);
            throw new InvalidOperationException("خطا در لغو سفارش", ex);
        }
    }

    public async Task<bool> ConfirmOrderAsync(Guid orderId)
    {
        try
        {
            _logger.LogInformation("Confirming order {OrderId}", orderId);
            return await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Confirmed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error confirming order {OrderId}", orderId);
            throw new InvalidOperationException("خطا در تایید سفارش", ex);
        }
    }

    public async Task<bool> CompleteOrderAsync(Guid orderId)
    {
        try
        {
            _logger.LogInformation("Completing order {OrderId}", orderId);
            return await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Completed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error completing order {OrderId}", orderId);
            throw new InvalidOperationException("خطا در تکمیل سفارش", ex);
        }
    }

    public async Task<bool> FailOrderAsync(Guid orderId, string reason)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Reason is required for failed orders", nameof(reason));

            _logger.LogInformation("Failing order {OrderId} with reason: {Reason}", orderId, reason);
            return await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Failed, reason);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error failing order {OrderId}", orderId);
            throw new InvalidOperationException("خطا در شکست سفارش", ex);
        }
    }

    public async Task<bool> AcceptTakerOrderAsync(Guid makerOrderId, Guid takerOrderId)
    {
        try
        {
            var makerOrder = await _orderRepository.GetByIdAsync(makerOrderId);
            var takerOrder = await _orderRepository.GetByIdAsync(takerOrderId);

            if (makerOrder == null || takerOrder == null)
                return false;

            makerOrder.AcceptTakerOrder(takerOrder);
            await _orderRepository.UpdateAsync(makerOrder);
            
            _logger.LogInformation("Taker order {TakerOrderId} accepted by maker order {MakerOrderId}", takerOrderId, makerOrderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error accepting taker order {TakerOrderId} by maker order {MakerOrderId}", takerOrderId, makerOrderId);
            throw new InvalidOperationException("خطا در پذیرش سفارش taker", ex);
        }
    }

    public async Task<(List<Order> Orders, int TotalCount)> GetOrdersPaginatedAsync(
        int pageNumber, 
        int pageSize, 
        string? asset = null, 
        OrderType? type = null, 
        OrderStatus? status = null,
        TradingType? tradingType = null,
        OrderRole? role = null)
    {
        try
        {
            return await _orderRepository.GetOrdersPaginatedAsync(pageNumber, pageSize, asset, type, status, tradingType, role);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated orders");
            throw new InvalidOperationException("خطا در بازیابی سفارشات", ex);
        }
    }

    public async Task<decimal> GetTotalValueByAssetAsync(string asset)
    {
        try
        {
            return await _orderRepository.GetTotalValueByAssetAsync(asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total value for asset: {Asset}", asset);
            throw new InvalidOperationException("خطا در محاسبه ارزش کل", ex);
        }
    }

    public async Task<int> GetOrderCountByAssetAsync(string asset)
    {
        try
        {
            return await _orderRepository.GetOrderCountByAssetAsync(asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting orders for asset: {Asset}", asset);
            throw new InvalidOperationException("خطا در شمارش سفارشات", ex);
        }
    }
}
