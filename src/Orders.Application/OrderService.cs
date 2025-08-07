using Orders.Core;
using Microsoft.Extensions.Logging;

namespace Orders.Application;

public interface IOrderService
{
    Task<Order> CreateOrderAsync(CreateOrderCommand command);
    Task<Order?> GetOrderByIdAsync(Guid orderId);
    Task<List<Order>> GetOrdersByAssetAsync(string asset);
    Task<List<Order>> GetOrdersByUserIdAsync(Guid userId);
    Task<List<Order>> GetActiveOrdersAsync();
    Task<bool> UpdateOrderStatusAsync(Guid orderId, OrderStatus status, string? notes = null);
    Task<bool> CancelOrderAsync(Guid orderId, string? reason = null);
    Task<bool> ConfirmOrderAsync(Guid orderId);
    Task<bool> CompleteOrderAsync(Guid orderId);
    Task<bool> FailOrderAsync(Guid orderId, string reason);
    Task<(List<Order> Orders, int TotalCount)> GetOrdersPaginatedAsync(
        int pageNumber, 
        int pageSize, 
        string? asset = null, 
        OrderType? type = null, 
        OrderStatus? status = null);
    Task<decimal> GetTotalValueByAssetAsync(string asset);
    Task<int> GetOrderCountByAssetAsync(string asset);
}

public class OrderService : IOrderService
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

    public async Task<Order> CreateOrderAsync(CreateOrderCommand command)
    {
        try
        {
            _logger.LogInformation("Creating order for user {UserId} with asset {Asset}", command.UserId, command.Asset);

            // Check authorization
            var canCreateOrder = await _authorizationService.CanCreateOrderAsync(command.UserId);
            if (!canCreateOrder)
            {
                _logger.LogWarning("User {UserId} is not authorized to create orders", command.UserId);
                throw new UnauthorizedAccessException("شما مجوز ثبت سفارش ندارید. فقط مدیران می‌توانند سفارش ثبت کنند.");
            }

            // Create order using domain factory method
            var order = Order.Create(
                command.Asset,
                command.Amount,
                command.Price,
                command.UserId,
                command.Type,
                command.Notes
            );

            // Save to repository
            var createdOrder = await _orderRepository.AddAsync(order);
            
            _logger.LogInformation("Order created successfully with ID: {OrderId}", createdOrder.Id);
            return createdOrder;
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException)
        {
            _logger.LogError(ex, "Error creating order for user {UserId}", command.UserId);
            throw new InvalidOperationException("خطا در ایجاد سفارش", ex);
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

    public async Task<List<Order>> GetOrdersByUserIdAsync(Guid userId)
    {
        try
        {
            return await _orderRepository.GetOrdersByUserIdAsync(userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for user: {UserId}", userId);
            throw new InvalidOperationException("خطا در بازیابی سفارشات کاربر", ex);
        }
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

    public async Task<(List<Order> Orders, int TotalCount)> GetOrdersPaginatedAsync(
        int pageNumber, 
        int pageSize, 
        string? asset = null, 
        OrderType? type = null, 
        OrderStatus? status = null)
    {
        try
        {
            return await _orderRepository.GetOrdersPaginatedAsync(pageNumber, pageSize, asset, type, status);
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
