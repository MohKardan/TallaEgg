using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;

namespace Orders.Infrastructure;

public class OrderRepository : IOrderRepository
{
    private readonly OrdersDbContext _dbContext;
    private readonly ILogger<OrderRepository> _logger;

    public OrderRepository(OrdersDbContext dbContext, ILogger<OrderRepository> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Order> AddAsync(Order order)
    {
        try
        {
            _dbContext.Orders.Add(order);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Order created with ID: {OrderId}", order.Id);
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating order");
            throw new InvalidOperationException("خطا در ذخیره سفارش", ex);
        }
    }

    public async Task<Order?> GetByIdAsync(Guid id)
    {
        try
        {
            return await _dbContext.Orders
                .FirstOrDefaultAsync(o => o.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving order with ID: {OrderId}", id);
            throw new InvalidOperationException("خطا در بازیابی سفارش", ex);
        }
    }

    public async Task<List<Order>> GetOrdersByAssetAsync(string asset)
    {
        try
        {
            return await _dbContext.Orders
                .Where(o => o.Asset == asset)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders for asset: {Asset}", asset);
            throw new InvalidOperationException("خطا در بازیابی سفارشات", ex);
        }
    }

    public async Task<PagedResult<OrderHistoryDto>> GetOrdersByUserIdAsync(
     Guid userId,
     int pageNumber,
     int pageSize)
    {
        var query = _dbContext.Orders
            .Where(o => o.UserId == userId)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderHistoryDto
            {
                Id = o.Id,
                Asset = o.Asset,
                Amount = o.Amount,
                Price = o.Price,
                Type = o.Side,
                Status = o.Status,
                TradingType = o.TradingType,
                Role = o.Role,
                CreatedAt = o.CreatedAt,
                UpdatedAt = o.UpdatedAt,
                Notes = o.Notes
            });

        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return new PagedResult<OrderHistoryDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }

    public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatus status)
    {
        try
        {
            return await _dbContext.Orders
                .Where(o => o.Status == status)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders with status: {Status}", status);
            throw new InvalidOperationException("خطا در بازیابی سفارشات", ex);
        }
    }

    public async Task<List<Order>> GetOrdersByTypeAsync(OrderSide type)
    {
        try
        {
            return await _dbContext.Orders
                .Where(o => o.Side == type)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders with type: {Type}", type);
            throw new InvalidOperationException("خطا در بازیابی سفارشات", ex);
        }
    }

    public async Task<List<Order>> GetOrdersByTradingTypeAsync(TradingType tradingType)
    {
        try
        {
            return await _dbContext.Orders
                .Where(o => o.TradingType == tradingType)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders with trading type: {TradingType}", tradingType);
            throw new InvalidOperationException("خطا در بازیابی سفارشات", ex);
        }
    }

    public async Task<List<Order>> GetOrdersByRoleAsync(OrderRole role)
    {
        try
        {
            return await _dbContext.Orders
                .Where(o => o.Role == role)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders with role: {Role}", role);
            throw new InvalidOperationException("خطا در بازیابی سفارشات", ex);
        }
    }

    public async Task<List<Order>> GetActiveOrdersAsync()
    {
        try
        {
            return await _dbContext.Orders
                .Where(o => (o.Status == OrderStatus.Pending || 
                            o.Status == OrderStatus.Confirmed || 
                            o.Status == OrderStatus.Partially) && 
                           o.RemainingAmount > 0)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active orders");
            throw new InvalidOperationException("خطا در بازیابی سفارشات فعال", ex);
        }
    }

    /// <summary>
    /// دریافت تمام سفارشات فعال یک کاربر خاص
    /// </summary>
    /// <param name="userId">شناسه کاربر</param>
    /// <returns>لیست سفارشات فعال کاربر (وضعیت Pending، Confirmed یا Partially و مقدار باقی‌مانده بیشتر از صفر)</returns>
    /// <remarks>
    /// این تابع سفارشاتی را برمی‌گرداند که:
    /// 1. متعلق به کاربر مشخص شده باشند
    /// 2. وضعیت آنها Pending، Confirmed یا Partially باشد
    /// 3. مقدار باقی‌مانده آنها بیشتر از صفر باشد
    /// 4. به ترتیب تاریخ ایجاد نزولی مرتب شده باشند
    /// </remarks>
    public async Task<List<Order>> GetActiveOrdersByUserIdAsync(Guid userId)
    {
        try
        {
            return await _dbContext.Orders
                .Where(o => o.UserId == userId && 
                           (o.Status == OrderStatus.Pending || 
                            o.Status == OrderStatus.Confirmed || 
                            o.Status == OrderStatus.Partially) && 
                           o.RemainingAmount > 0)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active orders for user {UserId}", userId);
            throw new InvalidOperationException($"خطا در بازیابی سفارشات فعال کاربر {userId}", ex);
        }
    }

    public async Task<List<Order>> GetAvailableMakerOrdersAsync(string asset, TradingType tradingType)
    {
        try
        {
            return await _dbContext.Orders
                .Where(o => o.Asset == asset && 
                           o.TradingType == tradingType && 
                           o.Role == OrderRole.Maker && 
                           (o.Status == OrderStatus.Pending || o.Status == OrderStatus.Confirmed))
                .OrderBy(o => o.Price)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available maker orders for asset: {Asset}", asset);
            throw new InvalidOperationException("خطا در بازیابی سفارشات maker موجود", ex);
        }
    }

    public async Task<List<Order>> GetOrdersByDateRangeAsync(DateTime from, DateTime to)
    {
        try
        {
            return await _dbContext.Orders
                .Where(o => o.CreatedAt >= from && o.CreatedAt <= to)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders from {From} to {To}", from, to);
            throw new InvalidOperationException("خطا در بازیابی سفارشات", ex);
        }
    }

    public async Task<int> GetOrderCountByAssetAsync(string asset)
    {
        try
        {
            return await _dbContext.Orders
                .CountAsync(o => o.Asset == asset);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting orders for asset: {Asset}", asset);
            throw new InvalidOperationException("خطا در شمارش سفارشات", ex);
        }
    }

    public async Task<decimal> GetTotalValueByAssetAsync(string asset)
    {
        try
        {
            return await _dbContext.Orders
                .Where(o => o.Asset == asset)
                .SumAsync(o => o.Amount * o.Price);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calculating total value for asset: {Asset}", asset);
            throw new InvalidOperationException("خطا در محاسبه ارزش کل", ex);
        }
    }

    public async Task<Order> UpdateAsync(Order order)
    {
        try
        {
            _dbContext.Orders.Update(order);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Order updated with ID: {OrderId}", order.Id);
            return order;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order with ID: {OrderId}", order.Id);
            throw new InvalidOperationException("خطا در به‌روزرسانی سفارش", ex);
        }
    }

    public async Task<bool> UpdateStatusAsync(Guid orderId, OrderStatus status, string? notes = null)
    {
        try
        {
            var order = await GetByIdAsync(orderId);
            if (order == null)
                return false;

            switch (status)
            {
                case OrderStatus.Confirmed:
                    order.Confirm();
                    break;
                case OrderStatus.Partially:
                    order.UpdateStatus(OrderStatus.Partially);
                    break;
                case OrderStatus.Cancelled:
                    order.Cancel(notes);
                    break;
                case OrderStatus.Completed:
                    order.Complete();
                    break;
                case OrderStatus.Failed:
                    if (string.IsNullOrWhiteSpace(notes))
                        throw new ArgumentException("Notes are required for failed orders");
                    order.Fail(notes);
                    break;
                default:
                    throw new ArgumentException($"Invalid status: {status}");
            }

            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Order status updated to {Status} for ID: {OrderId}", status, orderId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating status for order ID: {OrderId}", orderId);
            throw new InvalidOperationException("خطا در به‌روزرسانی وضعیت سفارش", ex);
        }
    }

    public async Task<bool> DeleteAsync(Guid id)
    {
        try
        {
            var order = await GetByIdAsync(id);
            if (order == null)
                return false;

            _dbContext.Orders.Remove(order);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Order deleted with ID: {OrderId}", id);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting order with ID: {OrderId}", id);
            throw new InvalidOperationException("خطا در حذف سفارش", ex);
        }
    }

    public async Task<bool> ExistsAsync(Guid id)
    {
        try
        {
            return await _dbContext.Orders.AnyAsync(o => o.Id == id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking existence of order with ID: {OrderId}", id);
            throw new InvalidOperationException("خطا در بررسی وجود سفارش", ex);
        }
    }

    public async Task<(List<Order> Orders, int TotalCount)> GetOrdersPaginatedAsync(
        int pageNumber, 
        int pageSize, 
        string? asset = null, 
        OrderSide? type = null, 
        OrderStatus? status = null,
        TradingType? tradingType = null,
        OrderRole? role = null)
    {
        try
        {
            var query = _dbContext.Orders.AsQueryable();

            if (!string.IsNullOrWhiteSpace(asset))
                query = query.Where(o => o.Asset == asset);

            if (type.HasValue)
                query = query.Where(o => o.Side == type.Value);

            if (status.HasValue)
                query = query.Where(o => o.Status == status.Value);

            if (tradingType.HasValue)
                query = query.Where(o => o.TradingType == tradingType.Value);

            if (role.HasValue)
                query = query.Where(o => o.Role == role.Value);

            var totalCount = await query.CountAsync();
            var orders = await query
                .OrderByDescending(o => o.CreatedAt)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (orders, totalCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving paginated orders");
            throw new InvalidOperationException("خطا در بازیابی سفارشات", ex);
        }
    }
}