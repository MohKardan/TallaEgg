using Microsoft.Extensions.Logging;
using Orders.Core;
using Orders.Infrastructure.Clients;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Order;

namespace Orders.Application;

public class BestBidAskResult
{
    public string Asset { get; set; } = string.Empty;
    public TradingType TradingType { get; set; }
    public decimal? BestBid { get; set; }
    public decimal? BestAsk { get; set; }
    public decimal? Spread { get; set; }
    public Guid? MatchingOrderId { get; set; } // ID of the order that would be matched
}

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
    private readonly IWalletApiClient _walletApiClient;
    private readonly IMatchingEngine _matchingEngine;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        IOrderRepository orderRepository,
        IWalletApiClient walletApiClient,
        IMatchingEngine matchingEngine,
        ILogger<OrderService> logger)
    {
        _orderRepository = orderRepository ?? throw new ArgumentNullException(nameof(orderRepository));
        _walletApiClient = walletApiClient ?? throw new ArgumentNullException(nameof(walletApiClient));
        _matchingEngine = matchingEngine ?? throw new ArgumentNullException(nameof(matchingEngine));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Order> CreateMakerOrderAsync(CreateOrderCommand command)
    {
        try
        {
            _logger.LogInformation("Creating maker order for user {UserId} with asset {Asset} and trading type {TradingType}", 
                command.UserId, command.Asset, command.TradingType);

            //TODO Check authorization
            var canCreateOrder = true;//await _authorizationService.CanCreateOrderAsync(command.UserId);
            if (!canCreateOrder)
            {
                _logger.LogWarning("User {UserId} is not authorized to create orders", command.UserId);
                throw new UnauthorizedAccessException("شما مجوز ثبت سفارش ندارید. فقط مدیران می‌توانند سفارش ثبت کنند.");
            }

            // 1. Calculate required balance based on order type
            var (requiredAsset, requiredAmount) = CalculateRequiredBalance(command.Type, command.Asset, command.Amount, command.Price);

            // 2. Validate user balance via Wallet service
            var balanceValidation = await _walletApiClient.ValidateBalanceAsync(
                command.UserId, 
                requiredAsset, 
                requiredAmount, 
                (int)command.Type);

            if (!balanceValidation.Success)
            {
                _logger.LogWarning("Failed to validate balance for user {UserId}: {Message}", command.UserId, balanceValidation.Message);
                throw new InvalidOperationException($"خطا در بررسی موجودی: {balanceValidation.Message}");
            }

            if (!balanceValidation.HasSufficientBalance)
            {
                _logger.LogWarning("Insufficient balance for user {UserId} to create order", command.UserId);
                throw new InvalidOperationException($"موجودی ناکافی: {balanceValidation.Message}");
            }

            // 3. Lock balance (freeze) for the order
            var lockResult = await _walletApiClient.LockBalanceAsync(command.UserId, requiredAsset, requiredAmount);
            if (!lockResult.Success)
            {
                _logger.LogError("Failed to lock balance for user {UserId}: {Message}", command.UserId, lockResult.Message);
                throw new InvalidOperationException($"خطا در قفل کردن موجودی: {lockResult.Message}");
            }

            try
            {
                // 4. Create maker order using domain factory method
                var order = Order.CreateMakerOrder(
                    command.Asset,
                    command.Amount,
                    command.Price,
                    command.UserId,
                    command.Type,
                    command.TradingType,
                    command.Notes
                );

                // 5. Save to repository
                var createdOrder = await _orderRepository.AddAsync(order);
                
                // 6. Process order through matching engine to check for immediate matches
                // سفارش maker ممکن است بلافاصله با سفارشات taker موجود تطبیق یابد
                await _matchingEngine.ProcessOrderAsync(createdOrder);
                
                _logger.LogInformation("Maker order created successfully with ID: {OrderId}, Balance locked: {LockedAmount} {Asset}", 
                    createdOrder.Id, requiredAmount, requiredAsset);
                return createdOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Order creation failed for user {UserId}, asset {Asset}, amount {Amount}", 
                    command.UserId, command.Asset, command.Amount);
                    
                // Rollback: Unlock the balance if order creation fails
                _logger.LogWarning("Order creation failed, attempting to unlock balance for user {UserId}", command.UserId);
                var unlockResult = await _walletApiClient.UnlockBalanceAsync(command.UserId, requiredAsset, requiredAmount);
                if (!unlockResult.Success)
                {
                    _logger.LogError("Failed to unlock balance during rollback for user {UserId}: {Message}", 
                        command.UserId, unlockResult.Message);
                }
                throw;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not InvalidOperationException)
        {
            _logger.LogError(ex, "Error creating maker order for user {UserId}", command.UserId);
            throw new InvalidOperationException("خطا در ایجاد سفارش maker", ex);
        }
    }

    public async Task<Order> CreateLimitOrderAsync(string symbol, decimal quantity, decimal price, Guid userId)
    {
        try
        {
            _logger.LogInformation("Creating limit order for user {UserId} with symbol {Symbol}, quantity {Quantity}, price {Price}", 
                userId, symbol, quantity, price);

            // Note: For limit orders, we need to determine order type (Buy/Sell)
            // This method signature doesn't include order type, so we'll assume this is a legacy method
            // In production, this should be updated to include order type parameter

            // For now, we'll create a simple limit order without balance validation
            // In a real scenario, you'd need the order type to validate balance properly

            // Create limit order using domain factory method
            var order = Order.CreateLimitOrder(symbol, quantity, price, userId);

            // Save to repository
            var createdOrder = await _orderRepository.AddAsync(order);
            
            _logger.LogInformation("Limit order created successfully with ID: {OrderId}", createdOrder.Id);
            _logger.LogWarning("Limit order created without balance validation - consider updating method signature to include OrderType");
            return createdOrder;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating limit order for user {UserId}", userId);
            throw new InvalidOperationException("خطا در ایجاد سفارش limit", ex);
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

    /// <summary>
    /// Create a market order that immediately executes against existing maker orders
    /// ایجاد سفارش بازار که فوراً در برابر سفارشات maker موجود اجرا می‌شود
    /// </summary>
    public async Task<Order> CreateMarketOrderAsync(CreateMarketOrderRequest command)
    {
        try
        {
            _logger.LogInformation("Creating market order (Taker) for user {UserId} with asset {Asset} and trading type {TradingType}", 
                command.UserId, command.Asset, command.TradingType);

            //TODO Check authorization
            var canCreateOrder = true;
            if (!canCreateOrder)
            {
                _logger.LogWarning("User {UserId} is not authorized to create orders", command.UserId);
                throw new UnauthorizedAccessException("شما مجوز ثبت سفارش ندارید. فقط مدیران می‌توانند سفارش ثبت کنند.");
            }

            // 1. Get available maker orders to determine market execution feasibility
            var availableOrders = await GetAvailableMakerOrdersForMarketOrderAsync(command.Asset, command.TradingType, command.Type);
            
            if (!availableOrders.Any())
            {
                throw new InvalidOperationException(command.Type == OrderType.Buy 
                    ? "هیچ فروشنده‌ای برای این نماد در بازار وجود ندارد"
                    : "هیچ خریداری برای این نماد در بازار وجود ندارد");
            }

            // 2. Calculate average execution price and required balance
            var estimatedPrice = CalculateAverageExecutionPrice(availableOrders, command.Amount);
            var (requiredAsset, requiredAmount) = CalculateRequiredBalance(command.Type, command.Asset, command.Amount, estimatedPrice);

            // 3. Validate user balance
            var balanceValidation = await _walletApiClient.ValidateBalanceAsync(
                command.UserId, 
                requiredAsset, 
                requiredAmount, 
                (int)command.Type);

            if (!balanceValidation.Success || !balanceValidation.HasSufficientBalance)
            {
                _logger.LogWarning("Insufficient balance for user {UserId} to create market order", command.UserId);
                throw new InvalidOperationException($"موجودی ناکافی: {balanceValidation.Message}");
            }

            // 4. Lock balance for the order
            var lockResult = await _walletApiClient.LockBalanceAsync(command.UserId, requiredAsset, requiredAmount);
            if (!lockResult.Success)
            {
                _logger.LogError("Failed to lock balance for user {UserId}: {Message}", command.UserId, lockResult.Message);
                throw new InvalidOperationException($"خطا در قفل کردن موجودی: {lockResult.Message}");
            }

            try
            {
                // 5. Create market order (acts as Taker - removes liquidity immediately)
                var marketOrder = Order.CreateMarketOrder(
                    command.Asset,
                    command.Amount,
                    estimatedPrice, // Use estimated price for the order
                    command.UserId,
                    command.Type,
                    command.TradingType,
                    command.Notes
                );

                // 6. Save to repository
                var createdOrder = await _orderRepository.AddAsync(marketOrder);

                // 7. Immediately process through matching engine (Taker behavior)
                // سفارش بازار فوراً باید با سفارشات maker موجود تطبیق یابد
                await _matchingEngine.ProcessOrderAsync(createdOrder);

                _logger.LogInformation("Market order (Taker) created and processed with ID: {OrderId}, Estimated price: {Price}", 
                    createdOrder.Id, estimatedPrice);
                    
                return createdOrder;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Market order creation failed for user {UserId}, asset {Asset}, amount {Amount}", 
                    command.UserId, command.Asset, command.Amount);
                    
                // Rollback: Unlock the balance if order creation fails
                _logger.LogWarning("Market order creation failed, attempting to unlock balance for user {UserId}", command.UserId);
                var unlockResult = await _walletApiClient.UnlockBalanceAsync(command.UserId, requiredAsset, requiredAmount);
                if (!unlockResult.Success)
                {
                    _logger.LogError("Failed to unlock balance during rollback for user {UserId}: {Message}", 
                        command.UserId, unlockResult.Message);
                }
                throw;
            }
        }
        catch (Exception ex) when (ex is not UnauthorizedAccessException and not InvalidOperationException)
        {
            _logger.LogError(ex, "Error creating market order for user {UserId}", command.UserId);
            throw new InvalidOperationException("خطا در ایجاد سفارش بازار", ex);
        }
    }

    public async Task<BestBidAskResult> GetBestBidAskAsync(string asset, TradingType tradingType)
    {
        try
        {
            _logger.LogInformation("Getting best bid/ask for asset {Asset} and trading type {TradingType}", asset, tradingType);

            var orders = await _orderRepository.GetOrdersByAssetAsync(asset);
            
            // Filter active maker orders for the specific trading type
            var activeOrders = orders.Where(o => 
                o.IsMaker() && 
                o.IsActive() && 
                o.TradingType == tradingType)
                .ToList();

            decimal? bestBid = null;
            decimal? bestAsk = null;
            Guid? matchingBidOrderId = null;
            Guid? matchingAskOrderId = null;

            // Find best bid (highest buy price) and its order ID
            var buyOrders = activeOrders.Where(o => o.Type == OrderType.Buy && o.Status == OrderStatus.Pending).ToList();
            if (buyOrders.Any())
            {
                var bestBidOrder = buyOrders.OrderByDescending(o => o.Price).First();
                bestBid = bestBidOrder.Price;
                matchingBidOrderId = bestBidOrder.Id;
            }

            // Find best ask (lowest sell price) and its order ID
            var sellOrders = activeOrders.Where(o => o.Type == OrderType.Sell && o.Status == OrderStatus.Pending).ToList();
            if (sellOrders.Any())
            {
                var bestAskOrder = sellOrders.OrderBy(o => o.Price).First();
                bestAsk = bestAskOrder.Price;
                matchingAskOrderId = bestAskOrder.Id;
            }

            var result = new BestBidAskResult
            {
                Asset = asset,
                TradingType = tradingType,
                BestBid = bestBid,
                BestAsk = bestAsk,
                Spread = bestBid.HasValue && bestAsk.HasValue ? bestAsk.Value - bestBid.Value : null,
                MatchingOrderId = null // Will be set based on order type when used
            };

            _logger.LogInformation("Best bid/ask for {Asset}: Bid={BestBid}, Ask={BestAsk}, Spread={Spread}", 
                asset, bestBid, bestAsk, result.Spread);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting best bid/ask for asset: {Asset}", asset);
            throw new InvalidOperationException("خطا در دریافت بهترین قیمت‌های خرید و فروش", ex);
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
            
            // 1. Get the order to determine what balance needs to be unlocked
            var order = await _orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found for cancellation", orderId);
                return false;
            }

            // 2. Check if order can be cancelled
            if (order.Status != OrderStatus.Pending && order.Status != OrderStatus.Confirmed)
            {
                _logger.LogWarning("Order {OrderId} cannot be cancelled - current status: {Status}", orderId, order.Status);
                throw new InvalidOperationException("فقط سفارشات در انتظار یا تایید شده قابل لغو هستند");
            }

            // 3. Calculate what balance was locked for this order
            var (lockedAsset, lockedAmount) = CalculateRequiredBalance(order.Type, order.Asset, order.RemainingAmount, order.Price);

            // 4. Cancel the order first
            var cancelSuccess = await _orderRepository.UpdateStatusAsync(orderId, OrderStatus.Cancelled, reason);
            if (!cancelSuccess)
            {
                _logger.LogError("Failed to update order {OrderId} status to cancelled", orderId);
                return false;
            }

            // 5. Unlock the balance
            var unlockResult = await _walletApiClient.UnlockBalanceAsync(order.UserId, lockedAsset, lockedAmount);
            if (!unlockResult.Success)
            {
                _logger.LogError("Order {OrderId} cancelled but failed to unlock balance for user {UserId}: {Message}", 
                    orderId, order.UserId, unlockResult.Message);
                // Note: In production, you might want to implement a compensation mechanism
                // or retry logic here, as the order is cancelled but balance is still locked
            }
            else
            {
                _logger.LogInformation("Order {OrderId} cancelled and {Amount} {Asset} unlocked for user {UserId}", 
                    orderId, lockedAmount, lockedAsset, order.UserId);
            }

            return true;
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

    /// <summary>
    /// Calculate required balance and asset for order placement
    /// محاسبه موجودی و دارایی مورد نیاز برای ثبت سفارش
    /// </summary>
    private static (string RequiredAsset, decimal RequiredAmount) CalculateRequiredBalance(
        OrderType orderType, 
        string tradingAsset, 
        decimal orderAmount, 
        decimal orderPrice)
    {
        if (orderType == OrderType.Buy)
        {
            // For buy orders, we need base currency (USDT) to purchase the asset
            // برای سفارش خرید، نیاز به ارز پایه (USDT) برای خرید دارایی داریم
            var baseCurrency = "USDT"; // This should be configurable or retrieved from trading pair
            var requiredAmount = orderAmount * orderPrice;
            return (baseCurrency, requiredAmount);
        }
        else // Sell order
        {
            // For sell orders, we need the actual asset to sell
            // برای سفارش فروش، نیاز به دارایی واقعی برای فروش داریم
            return (tradingAsset, orderAmount);
        }
    }

    /// <summary>
    /// Get available maker orders that can be matched with a market order
    /// دریافت سفارشات maker موجود که می‌توانند با سفارش بازار تطبیق یابند
    /// </summary>
    private async Task<List<Order>> GetAvailableMakerOrdersForMarketOrderAsync(string asset, TradingType tradingType, OrderType marketOrderType)
    {
        try
        {
            var allOrders = await _orderRepository.GetOrdersByAssetAsync(asset);
            
            // Get opposite side orders (if market order is Buy, get Sell orders and vice versa)
            var oppositeOrderType = marketOrderType == OrderType.Buy ? OrderType.Sell : OrderType.Buy;
            
            var availableOrders = allOrders
                .Where(o => o.IsMaker() && 
                           o.IsActive() && 
                           o.TradingType == tradingType &&
                           o.Type == oppositeOrderType &&
                           o.Status == OrderStatus.Pending &&
                           o.RemainingAmount > 0)
                .ToList();

            // Sort by price priority
            if (marketOrderType == OrderType.Buy)
            {
                // For buy market orders, prioritize cheapest sell orders
                availableOrders = availableOrders
                    .OrderBy(o => o.Price)
                    .ThenBy(o => o.CreatedAt)
                    .ToList();
            }
            else
            {
                // For sell market orders, prioritize highest buy orders
                availableOrders = availableOrders
                    .OrderByDescending(o => o.Price)
                    .ThenBy(o => o.CreatedAt)
                    .ToList();
            }

            return availableOrders;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting available maker orders for market order");
            throw;
        }
    }

    /// <summary>
    /// Calculate average execution price for a market order
    /// محاسبه قیمت متوسط اجرا برای سفارش بازار
    /// </summary>
    private static decimal CalculateAverageExecutionPrice(List<Order> availableOrders, decimal requestedAmount)
    {
        if (!availableOrders.Any())
            return 0;

        decimal totalCost = 0;
        decimal totalQuantity = 0;
        decimal remainingAmount = requestedAmount;

        foreach (var order in availableOrders)
        {
            if (remainingAmount <= 0)
                break;

            var quantityFromThisOrder = Math.Min(remainingAmount, order.RemainingAmount);
            totalCost += quantityFromThisOrder * order.Price;
            totalQuantity += quantityFromThisOrder;
            remainingAmount -= quantityFromThisOrder;
        }

        return totalQuantity > 0 ? totalCost / totalQuantity : availableOrders.First().Price;
    }

    public async Task<List<Order>> GetOrdersByStatusAsync(OrderStatus status)
    {
        try
        {
            _logger.LogInformation("Retrieving orders with status: {Status}", status);
            return await _orderRepository.GetOrdersByStatusAsync(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving orders with status: {Status}", status);
            throw new InvalidOperationException("خطا در بازیابی سفارشات", ex);
        }
    }
}
