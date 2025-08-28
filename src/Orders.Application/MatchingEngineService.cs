using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core.Enums.Order;

namespace Orders.Application;

public class MatchingEngineService : IMatchingEngine
{
    private readonly IOrderRepository _orderRepository;
    private readonly ITradeRepository _tradeRepository;
    private readonly ILogger<MatchingEngineService> _logger;

    public MatchingEngineService(
        IOrderRepository orderRepository,
        ITradeRepository tradeRepository,
        ILogger<MatchingEngineService> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _tradeRepository = tradeRepository ?? throw new ArgumentNullException(nameof(tradeRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing order {OrderId} for matching", order.Id);

            // Validate order
            if (!order.IsActive() || order.Status != OrderStatus.Pending)
            {
                _logger.LogWarning("Order {OrderId} is not eligible for matching. Status: {Status}", 
                    order.Id, order.Status);
                return;
            }

            // Find potential matching orders
            var potentialMatches = await FindMatchingOrdersAsync(order);
            if (!potentialMatches.Any())
            {
                _logger.LogInformation("No matching orders found for order {OrderId}. Order remains as Maker in order book.", 
                    order.Id);
                return;
            }

            // Process matches (this order becomes a Taker)
            await ProcessMatchesAsync(order, potentialMatches, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId}", order.Id);
            throw;
        }
    }

    public async Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Processing all pending orders for matching");

            var pendingOrders = await _orderRepository.GetOrdersByStatusAsync(OrderStatus.Pending);
            
            foreach (var order in pendingOrders.Where(o => o.IsActive()))
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                await ProcessOrderAsync(order, cancellationToken);
            }

            _logger.LogInformation("Completed processing {Count} pending orders", pendingOrders.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing all pending orders");
            throw;
        }
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MatchingEngine service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MatchingEngine service stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Find orders that can match with the given order
    /// یافتن سفارشاتی که می‌توانند با سفارش داده شده تطبیق یابند
    /// </summary>
    private async Task<List<Order>> FindMatchingOrdersAsync(Order incomingOrder)
    {
        try
        {
            // Get all orders for the same asset and trading type
            var allOrders = await _orderRepository.GetOrdersByAssetAsync(incomingOrder.Asset);
            
            // Filter for potential matches
            var potentialMatches = allOrders
                .Where(o => CanOrdersMatch(incomingOrder, o))
                .ToList();

            // Sort by price priority (best price first)
            if (incomingOrder.Type == OrderType.Buy)
            {
                // For buy orders, match with lowest sell prices first
                potentialMatches = potentialMatches
                    .Where(o => o.Type == OrderType.Sell)
                    .OrderBy(o => o.Price)
                    .ThenBy(o => o.CreatedAt) // Time priority for same price
                    .ToList();
            }
            else // Sell order
            {
                // For sell orders, match with highest buy prices first
                potentialMatches = potentialMatches
                    .Where(o => o.Type == OrderType.Buy)
                    .OrderByDescending(o => o.Price)
                    .ThenBy(o => o.CreatedAt) // Time priority for same price
                    .ToList();
            }

            _logger.LogInformation("Found {Count} potential matches for order {OrderId}", 
                potentialMatches.Count, incomingOrder.Id);

            return potentialMatches;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error finding matching orders for order {OrderId}", incomingOrder.Id);
            throw;
        }
    }

    /// <summary>
    /// Check if two orders can be matched
    /// بررسی اینکه آیا دو سفارش می‌توانند تطبیق یابند
    /// </summary>
    private static bool CanOrdersMatch(Order incomingOrder, Order existingOrder)
    {
        // Basic validation
        if (existingOrder.Id == incomingOrder.Id) return false;
        if (!existingOrder.IsActive()) return false;
        if (existingOrder.Status != OrderStatus.Pending) return false;
        if (existingOrder.RemainingAmount <= 0) return false;
        if (existingOrder.Asset != incomingOrder.Asset) return false;
        if (existingOrder.TradingType != incomingOrder.TradingType) return false;
        if (existingOrder.Type == incomingOrder.Type) return false; // Same side
        if (existingOrder.UserId == incomingOrder.UserId) return false; // Same user

        // Price matching logic
        if (incomingOrder.Type == OrderType.Buy && existingOrder.Type == OrderType.Sell)
        {
            // Buy order can match with sell order if buy price >= sell price
            return incomingOrder.Price >= existingOrder.Price;
        }
        else if (incomingOrder.Type == OrderType.Sell && existingOrder.Type == OrderType.Buy)
        {
            // Sell order can match with buy order if sell price <= buy price
            return incomingOrder.Price <= existingOrder.Price;
        }

        return false;
    }

    /// <summary>
    /// Process matches between incoming order (Taker) and existing orders (Makers)
    /// پردازش تطبیق بین سفارش ورودی (Taker) و سفارشات موجود (Maker)
    /// </summary>
    private async Task ProcessMatchesAsync(Order takerOrder, List<Order> makerOrders, CancellationToken cancellationToken)
    {
        try
        {
            decimal remainingAmount = takerOrder.RemainingAmount;

            foreach (var makerOrder in makerOrders)
            {
                if (remainingAmount <= 0 || cancellationToken.IsCancellationRequested)
                    break;

                // Calculate match quantity
                var matchQuantity = Math.Min(remainingAmount, makerOrder.RemainingAmount);
                var matchPrice = makerOrder.Price; // Use maker's price (price improvement for taker)

                // Create trade record with proper Maker/Taker identification
                var trade = await CreateTradeAsync(takerOrder, makerOrder, matchQuantity, matchPrice);

                // Update order quantities
                await UpdateOrderAfterTradeAsync(takerOrder, matchQuantity);
                await UpdateOrderAfterTradeAsync(makerOrder, matchQuantity);

                // Update remaining amount for next iteration
                remainingAmount -= matchQuantity;

                _logger.LogInformation("Trade created: {TradeId}, Maker: {MakerId}, Taker: {TakerId}, Quantity: {Quantity}, Price: {Price}", 
                    trade.Id, makerOrder.Id, takerOrder.Id, matchQuantity, matchPrice);

                // If maker order is fully filled, mark it as completed
                if (makerOrder.RemainingAmount <= 0)
                {
                    await _orderRepository.UpdateStatusAsync(makerOrder.Id, OrderStatus.Completed);
                    _logger.LogInformation("Maker order {OrderId} fully filled and marked as completed", makerOrder.Id);
                }
            }

            // Update taker order status
            if (takerOrder.RemainingAmount <= 0)
            {
                await _orderRepository.UpdateStatusAsync(takerOrder.Id, OrderStatus.Completed);
                _logger.LogInformation("Taker order {OrderId} fully filled and marked as completed", takerOrder.Id);
            }
            else if (takerOrder.RemainingAmount < takerOrder.Amount)
            {
                await _orderRepository.UpdateStatusAsync(takerOrder.Id, OrderStatus.Partially);
                _logger.LogInformation("Taker order {OrderId} partially filled. Remaining: {Remaining}", 
                    takerOrder.Id, takerOrder.RemainingAmount);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing matches for taker order {OrderId}", takerOrder.Id);
            throw;
        }
    }

    /// <summary>
    /// Create a trade record with proper Maker/Taker identification and fee calculation
    /// ایجاد رکورد معامله با شناسایی صحیح Maker/Taker و محاسبه کارمزد
    /// </summary>
    private async Task<Trade> CreateTradeAsync(Order takerOrder, Order makerOrder, decimal quantity, decimal price)
    {
        try
        {
            // Determine buy/sell roles
            var (buyOrder, sellOrder, buyerUserId, sellerUserId) = 
                takerOrder.Type == OrderType.Buy 
                    ? (takerOrder, makerOrder, takerOrder.UserId, makerOrder.UserId)
                    : (makerOrder, takerOrder, makerOrder.UserId, takerOrder.UserId);

            // Calculate quote quantity
            var quoteQuantity = quantity * price;

            // Create trade with Maker/Taker identification
            var trade = Trade.Create(
                buyOrderId: buyOrder.Id,
                sellOrderId: sellOrder.Id,
                makerOrderId: makerOrder.Id,    // Maker provides liquidity
                takerOrderId: takerOrder.Id,    // Taker removes liquidity
                symbol: takerOrder.Asset,
                price: price,
                quantity: quantity,
                quoteQuantity: quoteQuantity,
                buyerUserId: buyerUserId,
                sellerUserId: sellerUserId,
                makerUserId: makerOrder.UserId, // Maker user ID
                takerUserId: takerOrder.UserId, // Taker user ID
                makerFeeRate: 0.001m,           // 0.1% for makers (liquidity providers)
                takerFeeRate: 0.002m            // 0.2% for takers (liquidity consumers)
            );

            // Save trade to repository
            var savedTrade = await _tradeRepository.AddAsync(trade);
            
            _logger.LogInformation("Trade created: {TradeId} - Maker: {MakerUserId} ({MakerFee}), Taker: {TakerUserId} ({TakerFee})", 
                savedTrade.Id, makerOrder.UserId, savedTrade.MakerFee, takerOrder.UserId, savedTrade.TakerFee);

            return savedTrade;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating trade for maker order {MakerOrderId} and taker order {TakerOrderId}", 
                makerOrder.Id, takerOrder.Id);
            throw;
        }
    }

    /// <summary>
    /// Update order after a trade is executed
    /// به‌روزرسانی سفارش پس از اجرای معامله
    /// </summary>
    private async Task UpdateOrderAfterTradeAsync(Order order, decimal tradedQuantity)
    {
        try
        {
            var newRemainingAmount = order.RemainingAmount - tradedQuantity;

            // Ensure remaining amount doesn't go below zero
            if (newRemainingAmount < 0)
            {
                newRemainingAmount = 0;
            }

            // Update the order remaining amount
            order.UpdateRemainingAmount(newRemainingAmount);

            // Determine new status based on fill level
            if (newRemainingAmount == 0)
            {
                order.UpdateStatus(OrderStatus.Completed);
            }
            else if (newRemainingAmount < order.Amount)
            {
                order.UpdateStatus(OrderStatus.Partially);
            }

            // Update the order in repository
            await _orderRepository.UpdateAsync(order);

            var filledAmount = order.Amount - order.RemainingAmount;
            _logger.LogDebug("Order {OrderId} updated: Filled={Filled}, Remaining={Remaining}", 
                order.Id, filledAmount, order.RemainingAmount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating order {OrderId} after trade", order.Id);
            throw;
        }
    }
}
