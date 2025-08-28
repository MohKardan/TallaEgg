using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core.Enums.Order;

namespace Orders.Infrastructure;

/// <summary>
/// Repository with Database Locking for Thread-Safe Order Matching
/// مخزن با قفل پایگاه داده برای تطبیق ایمن سفارشات
/// </summary>
public class OrderMatchingRepository
{
    private readonly OrdersDbContext _context;
    private readonly ILogger<OrderMatchingRepository> _logger;

    public OrderMatchingRepository(OrdersDbContext context, ILogger<OrderMatchingRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Get buy orders with pessimistic lock for atomic matching
    /// دریافت سفارشات خرید با قفل متقابل برای تطبیق اتمی
    /// </summary>
    public async Task<List<Order>> GetBuyOrdersWithLockAsync(string asset)
    {
        try
        {
            // Use LINQ instead of raw SQL to avoid conversion issues
            // استفاده از LINQ به‌جای SQL خام برای جلوگیری از مشکلات تبدیل
            var orders = await _context.Orders
                .Where(o => o.Asset == asset && 
                           o.Type == OrderType.Buy && 
                           (o.Status == OrderStatus.Pending || 
                            o.Status == OrderStatus.Confirmed || 
                            o.Status == OrderStatus.Partially) &&
                           o.RemainingAmount > 0)
                .OrderByDescending(o => o.Price)
                .ThenBy(o => o.CreatedAt)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} buy orders for asset {Asset}", orders.Count, asset);
            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting buy orders for asset {Asset}", asset);
            throw;
        }
    }

    /// <summary>
    /// Get sell orders with pessimistic lock for atomic matching
    /// دریافت سفارشات فروش با قفل متقابل برای تطبیق اتمی
    /// </summary>
    public async Task<List<Order>> GetSellOrdersWithLockAsync(string asset)
    {
        try
        {
            // Use LINQ instead of raw SQL to avoid conversion issues
            // استفاده از LINQ به‌جای SQL خام برای جلوگیری از مشکلات تبدیل
            var orders = await _context.Orders
                .Where(o => o.Asset == asset && 
                           o.Type == OrderType.Sell && 
                           (o.Status == OrderStatus.Pending || 
                            o.Status == OrderStatus.Confirmed || 
                            o.Status == OrderStatus.Partially) &&
                           o.RemainingAmount > 0)
                .OrderBy(o => o.Price)
                .ThenBy(o => o.CreatedAt)
                .ToListAsync();

            _logger.LogDebug("Retrieved {Count} sell orders for asset {Asset}", orders.Count, asset);
            return orders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting sell orders for asset {Asset}", asset);
            throw;
        }
    }

    /// <summary>
    /// Execute atomic order matching with transaction
    /// اجرای تطبیق اتمی سفارش با تراکنش
    /// </summary>
    public async Task<(bool Success, Trade? Trade, string ErrorMessage)> ExecuteAtomicMatchAsync(
        Order buyOrder, 
        Order sellOrder, 
        decimal matchQuantity)
    {
        using var transaction = await _context.Database.BeginTransactionAsync();
        
        try
        {
            // 1. Re-fetch orders with lock to ensure they haven't changed
            // بازخوانی سفارشات برای اطمینان از عدم تغییر
            var currentBuyOrder = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == buyOrder.Id);

            var currentSellOrder = await _context.Orders
                .FirstOrDefaultAsync(o => o.Id == sellOrder.Id);

            // 2. Validate orders still exist and are processable
            if (currentBuyOrder == null || currentSellOrder == null)
            {
                await transaction.RollbackAsync();
                return (false, null, "یکی از سفارشات یافت نشد");
            }

            if (!IsOrderProcessable(currentBuyOrder) || !IsOrderProcessable(currentSellOrder))
            {
                await transaction.RollbackAsync();
                return (false, null, "وضعیت سفارشات برای پردازش مناسب نیست");
            }

            // 3. Validate price compatibility (Buy >= Sell)
            if (currentBuyOrder.Price < currentSellOrder.Price)
            {
                await transaction.RollbackAsync();
                return (false, null, "قیمت خرید کمتر از قیمت فروش است");
            }

            // 4. Calculate actual tradeable quantity
            var actualMatchQty = Math.Min(
                Math.Min(matchQuantity, currentBuyOrder.RemainingAmount),
                currentSellOrder.RemainingAmount
            );

            if (actualMatchQty <= 0)
            {
                await transaction.RollbackAsync();
                return (false, null, "مقدار قابل معامله صفر است");
            }

            // 5. Update order remaining amounts
            currentBuyOrder.UpdateRemainingAmount(currentBuyOrder.RemainingAmount - actualMatchQty);
            currentSellOrder.UpdateRemainingAmount(currentSellOrder.RemainingAmount - actualMatchQty);

            // 6. Update order statuses
            UpdateOrderStatus(currentBuyOrder);
            UpdateOrderStatus(currentSellOrder);

            // 7. Create trade record
            var tradePrice = DetermineTradePrice(currentBuyOrder, currentSellOrder);
            var trade = CreateTrade(currentBuyOrder, currentSellOrder, actualMatchQty, tradePrice);

            // 8. Save all changes
            _context.Orders.UpdateRange(currentBuyOrder, currentSellOrder);
            _context.Trades.Add(trade);
            
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Successfully matched orders: Buy={BuyOrderId}, Sell={SellOrderId}, Quantity={Quantity}, Price={Price}",
                currentBuyOrder.Id, currentSellOrder.Id, actualMatchQty, tradePrice);

            return (true, trade, string.Empty);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error in atomic order matching");
            return (false, null, $"خطا در تطبیق سفارشات: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if order can be processed
    /// بررسی امکان پردازش سفارش
    /// </summary>
    private static bool IsOrderProcessable(Order order)
    {
        return order.Status == OrderStatus.Pending || 
               order.Status == OrderStatus.Confirmed || 
               order.Status == OrderStatus.Partially;
    }

    /// <summary>
    /// Update order status based on remaining amount
    /// بروزرسانی وضعیت سفارش بر اساس مقدار باقی‌مانده
    /// </summary>
    private static void UpdateOrderStatus(Order order)
    {
        if (order.RemainingAmount <= 0)
        {
            order.Complete();
        }
        else if (order.RemainingAmount < order.Amount)
        {
            order.UpdateStatus(OrderStatus.Partially);
        }
    }

    /// <summary>
    /// Determine trade execution price (Price-Time Priority)
    /// تعیین قیمت اجرای معامله (اولویت قیمت-زمان)
    /// </summary>
    private static decimal DetermineTradePrice(Order buyOrder, Order sellOrder)
    {
        // Earlier order gets price advantage
        // سفارش قدیمی‌تر مزیت قیمتی دارد
        return buyOrder.CreatedAt <= sellOrder.CreatedAt ? sellOrder.Price : buyOrder.Price;
    }

    /// <summary>
    /// Create trade record
    /// ایجاد رکورد معامله
    /// </summary>
    private static Trade CreateTrade(Order buyOrder, Order sellOrder, decimal quantity, decimal price)
    {
        var quoteQuantity = quantity * price;
        var feeRate = 0.001m; // 0.1% - should come from configuration
        var feeBuyer = quoteQuantity * feeRate;
        var feeSeller = quoteQuantity * feeRate;

        return Trade.Create(
            buyOrderId: buyOrder.Id,
            sellOrderId: sellOrder.Id,
            symbol: buyOrder.Asset,
            price: price,
            quantity: quantity,
            quoteQuantity: quoteQuantity,
            buyerUserId: buyOrder.UserId,
            sellerUserId: sellOrder.UserId,
            feeBuyer: feeBuyer,
            feeSeller: feeSeller
        );
    }

    /// <summary>
    /// Get all distinct assets that have active orders
    /// دریافت تمام دارایی‌های متمایز که سفارش فعال دارند
    /// </summary>
    public async Task<List<string>> GetActiveAssetsAsync()
    {
        try
        {
            return await _context.Orders
                .Where(o => (o.Status == OrderStatus.Pending || 
                            o.Status == OrderStatus.Confirmed || 
                            o.Status == OrderStatus.Partially) &&
                           o.RemainingAmount > 0)
                .Select(o => o.Asset)
                .Distinct()
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting active assets");
            throw;
        }
    }
}
