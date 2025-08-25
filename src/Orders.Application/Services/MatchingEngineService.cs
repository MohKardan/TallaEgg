using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orders.Application;
using Orders.Core;
using TallaEgg.Core.Enums.Order;

namespace Orders.Application.Services;

public class MatchingEngineService : BackgroundService, IMatchingEngine
{
    private readonly ILogger<MatchingEngineService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(1);
    private bool _isRunning = false;

    public MatchingEngineService(
        ILogger<MatchingEngineService> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Matching Engine Service is starting...");
        _isRunning = true;

        try
        {
            while (!stoppingToken.IsCancellationRequested && _isRunning)
            {
                await ProcessAllPendingOrdersAsync(stoppingToken);
                await Task.Delay(_processingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Matching Engine Service is stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred in Matching Engine Service");
        }
        finally
        {
            _isRunning = false;
            _logger.LogInformation("Matching Engine Service has stopped");
        }
    }

    public new Task StartAsync(CancellationToken cancellationToken)
    {
        _isRunning = true;
        _logger.LogInformation("Matching Engine started manually");
        return Task.CompletedTask;
    }

    public new Task StopAsync(CancellationToken cancellationToken)
    {
        _isRunning = false;
        _logger.LogInformation("Matching Engine stopped manually");
        return Task.CompletedTask;
    }

    public async Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var tradeRepository = scope.ServiceProvider.GetRequiredService<ITradeRepository>();

            _logger.LogInformation("Processing order {OrderId} of type {OrderType} for {Asset}", 
                order.Id, order.Type, order.Asset);

            // Get matching orders from the opposite side
            var matchingOrders = await GetMatchingOrdersAsync(order, orderRepository, cancellationToken);
            
            if (!matchingOrders.Any())
            {
                _logger.LogDebug("No matching orders found for order {OrderId}", order.Id);
                return;
            }

            // Match orders and create trades
            await MatchOrdersAsync(order, matchingOrders, orderRepository, tradeRepository, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing order {OrderId}", order.Id);
        }
    }

    public async Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();

            // Get all active orders
            var activeOrders = await orderRepository.GetActiveOrdersAsync();
            
            if (!activeOrders.Any())
            {
                return;
            }

            _logger.LogDebug("Processing {Count} active orders", activeOrders.Count);

            // Group orders by asset and type for efficient matching
            var buyOrders = activeOrders.Where(o => o.Type == OrderType.Buy)
                                      .GroupBy(o => o.Asset)
                                      .ToDictionary(g => g.Key, g => g.OrderByDescending(o => o.Price).ToList());

            var sellOrders = activeOrders.Where(o => o.Type == OrderType.Sell)
                                       .GroupBy(o => o.Asset)
                                       .ToDictionary(g => g.Key, g => g.OrderBy(o => o.Price).ToList());

            // Process each asset
            foreach (var asset in buyOrders.Keys.Union(sellOrders.Keys))
            {
                if (cancellationToken.IsCancellationRequested) break;

                await ProcessAssetOrdersAsync(asset, buyOrders, sellOrders, orderRepository, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing all pending orders");
        }
    }

    private async Task<List<Order>> GetMatchingOrdersAsync(
        Order order, 
        IOrderRepository orderRepository, 
        CancellationToken cancellationToken)
    {
        var oppositeType = order.Type == OrderType.Buy ? OrderType.Sell : OrderType.Buy;
        
        // Get all active orders of the opposite type for the same asset
        var allOrders = await orderRepository.GetOrdersByAssetAsync(order.Asset);
        
        return allOrders.Where(o => 
            o.Type == oppositeType && 
            o.Id != order.Id &&
            IsPriceCompatible(order, o))
            .OrderBy(o => order.Type == OrderType.Buy ? o.Price : -o.Price) // Best price first
            .ToList();
    }

    private bool IsPriceCompatible(Order order1, Order order2)
    {
        if (order1.Type == order2.Type) return false;

        var buyOrder = order1.Type == OrderType.Buy ? order1 : order2;
        var sellOrder = order1.Type == OrderType.Sell ? order1 : order2;

        // Exact price match required - prices must be exactly equal
        return buyOrder.Price == sellOrder.Price;
    }

    private async Task MatchOrdersAsync(
        Order incomingOrder,
        List<Order> matchingOrders,
        IOrderRepository orderRepository,
        ITradeRepository tradeRepository,
        CancellationToken cancellationToken)
    {
        var remainingAmount = incomingOrder.RemainingAmount;
        var trades = new List<Trade>();

        foreach (var matchingOrder in matchingOrders)
        {
            if (remainingAmount <= 0) break;
            if (cancellationToken.IsCancellationRequested) break;

            var tradeAmount = Math.Min(remainingAmount, matchingOrder.RemainingAmount);
            var tradePrice = DetermineTradePrice(incomingOrder, matchingOrder);

            // Create trade
            var trade = CreateTrade(incomingOrder, matchingOrder, tradeAmount, tradePrice);
            trades.Add(trade);

            // Update order remaining amounts
            remainingAmount -= tradeAmount;
            await UpdateOrderRemainingAmountAsync(incomingOrder, remainingAmount, orderRepository);
            await UpdateOrderRemainingAmountAsync(matchingOrder, matchingOrder.RemainingAmount - tradeAmount, orderRepository);

            _logger.LogInformation("Created trade {TradeId} between orders {Order1Id} and {Order2Id} for {Amount} @ {Price}",
                trade.Id, incomingOrder.Id, matchingOrder.Id, tradeAmount, tradePrice);
        }

        // Save all trades
        foreach (var trade in trades)
        {
            await tradeRepository.AddAsync(trade);
        }

        // Update order statuses if needed
        await UpdateOrderStatusesAsync(incomingOrder, remainingAmount, orderRepository);
        
        foreach (var matchingOrder in matchingOrders)
        {
            var updatedOrder = await orderRepository.GetByIdAsync(matchingOrder.Id);
            if (updatedOrder != null)
            {
                await UpdateOrderStatusesAsync(updatedOrder, updatedOrder.RemainingAmount, orderRepository);
            }
        }
    }

    private async Task ProcessAssetOrdersAsync(
        string asset,
        Dictionary<string, List<Order>> buyOrders,
        Dictionary<string, List<Order>> sellOrders,
        IOrderRepository orderRepository,
        CancellationToken cancellationToken)
    {
        if (!buyOrders.ContainsKey(asset) || !sellOrders.ContainsKey(asset))
            return;

        var assetBuyOrders = buyOrders[asset];
        var assetSellOrders = sellOrders[asset];

        var buyIndex = 0;
        var sellIndex = 0;

        while (buyIndex < assetBuyOrders.Count && sellIndex < assetSellOrders.Count)
        {
            if (cancellationToken.IsCancellationRequested) break;

            var buyOrder = assetBuyOrders[buyIndex];
            var sellOrder = assetSellOrders[sellIndex];

            // Check if prices are exactly equal
            if (buyOrder.Price != sellOrder.Price)
                break; // No more matches possible

            // Create trade
            var tradeAmount = Math.Min(buyOrder.RemainingAmount, sellOrder.RemainingAmount);
            
            // Skip if no trade amount available
            if (tradeAmount <= 0)
                break;
                
            var tradePrice = DetermineTradePrice(buyOrder, sellOrder);

            using var scope = _serviceProvider.CreateScope();
            var tradeRepository = scope.ServiceProvider.GetRequiredService<ITradeRepository>();

            var trade = CreateTrade(buyOrder, sellOrder, tradeAmount, tradePrice);
            await tradeRepository.AddAsync(trade);

            _logger.LogInformation("Created trade {TradeId} for {Asset}: {Amount} @ {Price}",
                trade.Id, asset, tradeAmount, tradePrice);

            // Update order remaining amounts
            buyOrder = await UpdateOrderRemainingAmountAsync(buyOrder, buyOrder.RemainingAmount - tradeAmount, orderRepository);
            sellOrder = await UpdateOrderRemainingAmountAsync(sellOrder, sellOrder.RemainingAmount - tradeAmount, orderRepository);

            // Update order statuses
            await UpdateOrderStatusesAsync(buyOrder, buyOrder.RemainingAmount, orderRepository);
            await UpdateOrderStatusesAsync(sellOrder, sellOrder.RemainingAmount, orderRepository);

            // Update local lists
            assetBuyOrders[buyIndex] = buyOrder;
            assetSellOrders[sellIndex] = sellOrder;

            // Move to next order if current one is fully filled
            if (buyOrder.RemainingAmount <= 0) buyIndex++;
            if (sellOrder.RemainingAmount <= 0) sellIndex++;

            // Break if no more orders can be matched
            if (buyIndex >= assetBuyOrders.Count || sellIndex >= assetSellOrders.Count)
                break;
        }
    }

    private decimal DetermineTradePrice(Order order1, Order order2)
    {
        // Price-time priority: the order that was placed first gets the better price
        if (order1.CreatedAt <= order2.CreatedAt)
        {
            return order1.Type == OrderType.Buy ? order2.Price : order1.Price;
        }
        else
        {
            return order1.Type == OrderType.Buy ? order1.Price : order2.Price;
        }
    }

    private Trade CreateTrade(Order order1, Order order2, decimal amount, decimal price)
    {
        var buyOrder = order1.Type == OrderType.Buy ? order1 : order2;
        var sellOrder = order1.Type == OrderType.Sell ? order1 : order2;

        var quoteQuantity = amount * price;
        var feeRate = 0.001m; // 0.1% fee - this should come from configuration
        var feeBuyer = quoteQuantity * feeRate;
        var feeSeller = quoteQuantity * feeRate;

        return Trade.Create(
            buyOrderId: buyOrder.Id,
            sellOrderId: sellOrder.Id,
            symbol: buyOrder.Asset,
            price: price,
            quantity: amount,
            quoteQuantity: quoteQuantity,
            buyerUserId: buyOrder.UserId,
            sellerUserId: sellOrder.UserId,
            feeBuyer: feeBuyer,
            feeSeller: feeSeller
        );
    }

    private async Task<Order> UpdateOrderRemainingAmountAsync(Order order, decimal newRemainingAmount, IOrderRepository orderRepository)
    {
        // Use the UpdateRemainingAmount method on the existing order
        order.UpdateRemainingAmount(newRemainingAmount);
        return await orderRepository.UpdateAsync(order);
    }

    private async Task UpdateOrderStatusesAsync(Order order, decimal remainingAmount, IOrderRepository orderRepository)
    {
        if (remainingAmount <= 0)
        {
            // Order is fully filled
            await orderRepository.UpdateStatusAsync(order.Id, OrderStatus.Completed);
            _logger.LogInformation("Order {OrderId} completed", order.Id);
        }
        else if (remainingAmount < order.Amount)
        {
            // Order is partially filled
            await orderRepository.UpdateStatusAsync(order.Id, OrderStatus.Partially);
            _logger.LogInformation("Order {OrderId} partially filled, remaining: {RemainingAmount}", 
                order.Id, remainingAmount);
        }
    }
}
