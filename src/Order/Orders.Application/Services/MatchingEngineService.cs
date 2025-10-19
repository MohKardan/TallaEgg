using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orders.Application;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Responses.Order;
using TallaEgg.Infrastructure.Clients;

namespace Orders.Application.Services;

/// <summary>
/// Thread-Safe Matching Engine with Database Locking and Maker/Taker Support
/// موتور تطبیق ایمن با قفل پایگاه داده و پشتیبانی از Maker/Taker
/// </summary>
public class MatchingEngineService : BackgroundService, IMatchingEngine
{
    /// <summary>
    /// .NET اجازه نمی‌دهد Singleton به Scoped وابسته شود، چون ممکن است Scoped قبلاً Dispose شده باشد.
    /// بخاطر همین از ین روش استفاده کردم
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<MatchingEngineService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(1);
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1); // Prevent concurrent processing
    private bool _isRunning = false;

    public MatchingEngineService(
        IServiceScopeFactory scopeFactory,
        ILogger<MatchingEngineService> logger,
        IServiceProvider serviceProvider)
    {
        _scopeFactory = scopeFactory;

        _logger = logger;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Matching Engine Service is starting...");
        _isRunning = true;

        try
        {
            while (!stoppingToken.IsCancellationRequested && _isRunning)
            {
                using var scope = _scopeFactory.CreateScope();
                var _walletApiClient = scope.ServiceProvider.GetRequiredService<IWalletApiClient>();

                // Use semaphore to ensure only one processing cycle runs at a time
                // استفاده از semaphore برای اطمینان از اجرای یک چرخه در هر زمان
                if (await _processingSemaphore.WaitAsync(100, stoppingToken))
                {
                    try
                    {
                        await ProcessAllPendingOrdersAsync(stoppingToken);
                    }
                    finally
                    {
                        _processingSemaphore.Release();
                    }
                }

                await Task.Delay(_processingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("⏹️ Matching Engine Service is stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Critical error in Matching Engine Service");
        }
        finally
        {
            _isRunning = false;
            _processingSemaphore.Dispose();
            _logger.LogInformation("🛑 Matching Engine Service has stopped");
        }
    }

    public new Task StartAsync(CancellationToken cancellationToken)
    {
        _isRunning = true;
        _logger.LogInformation("▶️ Matching Engine started manually");
        return Task.CompletedTask;
    }

    public new Task StopAsync(CancellationToken cancellationToken)
    {
        _isRunning = false;
        _logger.LogInformation("⏸️ Matching Engine stopped manually");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Process single order with immediate Maker/Taker identification
    /// پردازش سفارش منفرد با تشخیص فوری Maker/Taker
    /// </summary>
    public async Task<bool> ProcessOrderForMatchingAsync(Guid orderId)
    {
        if (!await _processingSemaphore.WaitAsync(5000))
        {
            _logger.LogWarning("⏰ Could not acquire processing lock for order {OrderId}", orderId);
            return false;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var matchingRepository = scope.ServiceProvider.GetRequiredService<OrderMatchingRepository>();
            var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            
            var order = await orderRepository.GetByIdAsync(orderId);
            if (order == null)
            {
                _logger.LogWarning("📭 Order {OrderId} not found", orderId);
                return false;
            }

            // Only process Confirmed orders - skip Pending orders
            if (order.Status != OrderStatus.Confirmed)
            {
                _logger.LogDebug("⏭️ Order {OrderId} is not Confirmed (Status: {Status}), skipping matching", orderId, order.Status);
                return false;
            }

            // Get matching orders from order book
            var matchingOrders = await GetMatchingOrdersAsync(matchingRepository, order);
            
            if (matchingOrders.Any())
            {
                // This order is TAKER (consumes liquidity)
                _logger.LogInformation("🛍️ Order {OrderId} identified as TAKER - will match immediately", orderId);
                
                foreach (var makerOrder in matchingOrders)
                {
                    var matchQuantity = Math.Min(makerOrder.RemainingAmount, order.RemainingAmount);
                    await ExecuteTradeWithMakerTakerLogic(matchingRepository, makerOrder, order, matchQuantity);
                    
                    if (order.RemainingAmount <= 0)
                        break; // Taker order fully filled
                }
                
                return true; // Order was matched immediately
            }
            else
            {
                // This order becomes MAKER (provides liquidity) - stays in order book
                _logger.LogInformation("🏪 Order {OrderId} identified as MAKER - added to order book", orderId);
                return false; // Order goes to order book
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error processing order {OrderId} for matching", orderId);
            return false;
        }
        finally
        {
            _processingSemaphore.Release();
        }
    }

    /// <summary>
    /// Process single order by ID (new method)
    /// پردازش سفارش منفرد با شناسه
    /// </summary>
    public async Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await ProcessOrderForMatchingAsync(orderId);
    }

    /// <summary>
    /// Process single order (legacy method - enhanced with Maker/Taker)
    /// پردازش سفارش منفرد (متد قدیمی - بهبود یافته با Maker/Taker)
    /// </summary>
    public async Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        await ProcessOrderForMatchingAsync(order.Id);
    }

    /// <summary>
    /// Process all pending orders with thread-safe atomic matching
    /// پردازش تمام سفارشات در انتظار با تطبیق اتمی ایمن
    /// </summary>
    public async Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var matchingRepository = scope.ServiceProvider.GetRequiredService<OrderMatchingRepository>();

            // Get all assets with active orders
            // دریافت تمام دارایی‌هایی که سفارش فعال دارند
            var activeAssets = await matchingRepository.GetActiveAssetsAsync();
            
            if (!activeAssets.Any())
            {
                _logger.LogDebug("📭 No active assets found for processing");
                return;
            }

            _logger.LogDebug("🔄 Processing {Count} assets: {Assets}", 
                activeAssets.Count, string.Join(", ", activeAssets));

            // Process each asset independently
            // پردازش مستقل هر دارایی
            var tasks = activeAssets.Select(asset => 
                ProcessSingleAssetAsync(asset, cancellationToken)
            ).ToArray();

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error processing all pending orders");
        }
    }

    /// <summary>
    /// Process orders for a single asset with atomic matching
    /// پردازش سفارشات یک دارایی با تطبیق اتمی
    /// </summary>
    private async Task ProcessSingleAssetAsync(string asset, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var matchingRepository = scope.ServiceProvider.GetRequiredService<OrderMatchingRepository>();

            var matchCount = 0;
            var maxMatches = 100; // Prevent infinite loops
            
            while (matchCount < maxMatches && !cancellationToken.IsCancellationRequested)
            {
                // Get locked orders for this asset
                // دریافت سفارشات قفل‌شده برای این دارایی
                var buyOrders = await matchingRepository.GetBuyOrdersWithLockAsync(asset);
                var sellOrders = await matchingRepository.GetSellOrdersWithLockAsync(asset);

                if (!buyOrders.Any() || !sellOrders.Any())
                {
                    _logger.LogDebug("📭 No matching orders available for asset {Asset}", asset);
                    break;
                }

                // Find best matching pair
                // یافتن بهترین جفت برای تطبیق
                var (buyOrder, sellOrder, matchQty) = FindBestMatch(buyOrders, sellOrders);

                if (buyOrder == null || sellOrder == null || matchQty <= 0)
                {
                    _logger.LogDebug("❌ No compatible match found for asset {Asset}", asset);
                    break;
                }

                // Execute atomic match with enhanced Maker/Taker logic
                // اجرای تطبیق اتمی با منطق بهبود یافته Maker/Taker
                var result = await ExecuteAtomicMatchWithMakerTakerAsync(
                    matchingRepository, buyOrder, sellOrder, matchQty);

                if (result.Success)
                {
                    matchCount++;
                    _logger.LogInformation(
                        "✅ Match #{MatchCount} for {Asset}: {Quantity} @ {Price} (Maker/Taker fees applied)",
                        matchCount, asset, matchQty, result.Trade?.Price ?? 0);
                }
                else
                {
                    _logger.LogWarning("⚠️ Match failed for {Asset}: {Error}", asset, result.ErrorMessage);
                    break; // Don't retry immediately
                }
            }

            if (matchCount > 0)
            {
                _logger.LogInformation("🎯 Completed {MatchCount} matches for asset {Asset}", matchCount, asset);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error processing asset {Asset}", asset);
        }
    }

    /// <summary>
    /// Find the best matching pair using Price-Time Priority
    /// یافتن بهترین جفت با اولویت قیمت-زمان
    /// </summary>
    private static (Order? BuyOrder, Order? SellOrder, decimal MatchQuantity) FindBestMatch(
        List<Order> buyOrders, 
        List<Order> sellOrders)
    {
        // Buy orders are sorted by Price DESC, Time ASC (highest price first)
        // Sell orders are sorted by Price ASC, Time ASC (lowest price first)
        // سفارشات خرید بر اساس قیمت نزولی، زمان صعودی (بالاترین قیمت اول)
        // سفارشات فروش بر اساس قیمت صعودی، زمان صعودی (پایین‌ترین قیمت اول)
        
        foreach (var buyOrder in buyOrders.Where(b => b.RemainingAmount > 0))
        {
            foreach (var sellOrder in sellOrders.Where(s => s.RemainingAmount > 0))
            {
                // Check price compatibility: Buy price >= Sell price
                // بررسی سازگاری قیمت: قیمت خرید >= قیمت فروش
                if (buyOrder.Price >= sellOrder.Price)
                {
                    var matchQty = Math.Min(buyOrder.RemainingAmount, sellOrder.RemainingAmount);
                    return (buyOrder, sellOrder, matchQty);
                }
                
                // Since sell orders are sorted by price ASC, 
                // if current sell is too expensive, all following will be too
                // چون سفارشات فروش بر اساس قیمت صعودی مرتب شده‌اند،
                // اگر فروش فعلی خیلی گران باشد، بقیه هم گران خواهند بود
                break;
            }
        }

        return (null, null, 0);
    }

    /// <summary>
    /// Get matching orders for immediate execution (Taker identification)
    /// دریافت سفارشات قابل تطبیق برای اجرای فوری (تشخیص Taker)
    /// </summary>
    private async Task<List<Order>> GetMatchingOrdersAsync(OrderMatchingRepository matchingRepository, Order incomingOrder)
    {
        try
        {
            if (incomingOrder.Side == OrderSide.Buy)
            {
                // For buy orders, find sell orders with price <= buy price
                var sellOrders = await matchingRepository.GetSellOrdersWithLockAsync(incomingOrder.Asset);
                return sellOrders.Where(s => s.Price <= incomingOrder.Price && s.RemainingAmount > 0).ToList();
            }
            else
            {
                // For sell orders, find buy orders with price >= sell price
                var buyOrders = await matchingRepository.GetBuyOrdersWithLockAsync(incomingOrder.Asset);
                return buyOrders.Where(b => b.Price >= incomingOrder.Price && b.RemainingAmount > 0).ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error getting matching orders for {OrderId}", incomingOrder.Id);
            return new List<Order>();
        }
    }

    /// <summary>
    /// Execute trade with proper Maker/Taker fee calculation
    /// اجرای معامله با محاسبه صحیح کارمزد Maker/Taker
    /// </summary>
    private async Task ExecuteTradeWithMakerTakerLogic(
        OrderMatchingRepository matchingRepository,
        Order makerOrder,
        Order takerOrder,
        decimal quantity)
    {
        try
        {
            // Create trade with enhanced Maker/Taker logic
            var trade = CreateMakerTakerTrade(makerOrder, takerOrder, quantity);
            
            // Execute atomic match (the method will create its own trade)
            var result = await matchingRepository.ExecuteAtomicMatchAsync(makerOrder, takerOrder, quantity);
            
            if (result.Success)
            {
                //TODO باید آنفریز کنه و تراکنش های مربوط به معامله را ثبت کند
                //await _walletApiClient.UnlockBalanceAsync(result.Trade.BuyerUserId, result.Trade.Symbol, result.Trade.Quantity);
                //await _walletApiClient.UnlockBalanceAsync(result.Trade.SellerUserId, result.Trade.Symbol, result.Trade.Quantity * result.Trade.Price);

                ////dotnet add package AutoMapper
                ////dotnet add package AutoMapper.Extensions.Microsoft.DependencyInjection

                //TradeDto tradeDto = new TradeDto()
                //{
                //    Id = result.Trade.Id,
                //    BuyOrderId = result.Trade.BuyOrderId,
                //    SellOrderId = result.Trade.SellOrderId,
                //    MakerOrderId = result.Trade.MakerOrderId,
                //    TakerOrderId = result.Trade.TakerOrderId,
                //    Symbol = result.Trade.Symbol,
                //    Price = result.Trade.Price,
                //    Quantity = result.Trade.Quantity,
                //    QuoteQuantity = result.Trade.QuoteQuantity,
                //    FeeBuyer = result.Trade.FeeBuyer,
                //    FeeSeller = result.Trade.FeeSeller,
                //    CreatedAt = result.Trade.CreatedAt,

                //};

                //await _walletApiClient.TradeTransactionAndBalanceChangeAsync(tradeDto);
                if (result.Trade != null)
                {
                    var tradeDto = new TradeDto()
                    {
                        Id = result.Trade.Id,
                        BuyOrderId = result.Trade.BuyOrderId,
                        SellOrderId = result.Trade.SellOrderId,
                        MakerOrderId = result.Trade.MakerOrderId,
                        TakerOrderId = result.Trade.TakerOrderId,
                        Symbol = result.Trade.Symbol,
                        Price = result.Trade.Price,
                        Quantity = result.Trade.Quantity,
                        QuoteQuantity = result.Trade.QuoteQuantity,
                        BuyerUserId = result.Trade.BuyerUserId,
                        SellerUserId = result.Trade.SellerUserId,
                        MakerUserId = result.Trade.MakerUserId,
                        TakerUserId = result.Trade.TakerUserId,
                        FeeBuyer = result.Trade.FeeBuyer,
                        FeeSeller = result.Trade.FeeSeller,
                        MakerFee = result.Trade.MakerFee,
                        TakerFee = result.Trade.TakerFee,
                        MakerFeeRate = result.Trade.MakerFeeRate,
                        TakerFeeRate = result.Trade.TakerFeeRate,
                        CreatedAt = result.Trade.CreatedAt,
                    };

                    using var walletScope = _serviceProvider.CreateScope();
                    var walletClient = walletScope.ServiceProvider.GetRequiredService<IWalletApiClient>();
                    var walletResp = await walletClient.TradeTransactionAndBalanceChangeAsync(tradeDto);
                    if (!walletResp.Success)
                    {
                        _logger.LogWarning("Wallet update failed for Trade {TradeId}: {Message}", result.Trade.Id, walletResp.Message);
                    }
                }

                _logger.LogInformation(
                    "✅ Maker/Taker trade executed: Maker:{MakerId} Taker:{TakerId} Qty:{Qty} Price:{Price}",
                    makerOrder.Id, takerOrder.Id, quantity, result.Trade?.Price);
            }
            else
            {
                _logger.LogError("❌ Failed to execute Maker/Taker trade: {Error}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error executing Maker/Taker trade");
        }
    }

    /// <summary>
    /// Execute atomic match with enhanced Maker/Taker logic
    /// اجرای تطبیق اتمی با منطق بهبود یافته Maker/Taker
    /// </summary>
    private async Task<(bool Success, Trade? Trade, string? ErrorMessage)> ExecuteAtomicMatchWithMakerTakerAsync(
        OrderMatchingRepository matchingRepository,
        Order buyOrder,
        Order sellOrder,
        decimal quantity)
    {
        try
        {
            // Determine Maker/Taker based on timestamp
            var isBuyOrderMaker = buyOrder.CreatedAt <= sellOrder.CreatedAt;
            var makerOrder = isBuyOrderMaker ? buyOrder : sellOrder;
            var takerOrder = isBuyOrderMaker ? sellOrder : buyOrder;

            // Execute with standard method (it will create trade internally)
            return await matchingRepository.ExecuteAtomicMatchAsync(makerOrder, takerOrder, quantity);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error in atomic Maker/Taker match");
            return (false, null, ex.Message);
        }
    }

    /// <summary>
    /// Create trade with proper Maker/Taker fee calculation
    /// ایجاد معامله با محاسبه صحیح کارمزد Maker/Taker
    /// </summary>
    private Trade CreateMakerTakerTrade(Order makerOrder, Order takerOrder, decimal quantity)
    {
        // Fee rates - Maker gets lower fee (0.1%), Taker gets higher fee (0.2%)
        var makerFeeRate = 0.000m; // 0.1%
        var takerFeeRate = 0.000m; // 0.2%
        
        var price = makerOrder.Price; // Execute at maker's price
        var quoteQuantity = quantity * price;
        
        // Determine buy/sell roles
        var (buyOrder, sellOrder, buyerUserId, sellerUserId) = 
            takerOrder.Side == OrderSide.Buy 
                ? (takerOrder, makerOrder, takerOrder.UserId, makerOrder.UserId)
                : (makerOrder, takerOrder, makerOrder.UserId, takerOrder.UserId);

        return Trade.Create(
            buyOrderId: buyOrder.Id,
            sellOrderId: sellOrder.Id,
            makerOrderId: makerOrder.Id,
            takerOrderId: takerOrder.Id,
            symbol: makerOrder.Asset,
            price: price,
            quantity: quantity,
            quoteQuantity: quoteQuantity,
            buyerUserId: buyerUserId,
            sellerUserId: sellerUserId,
            makerUserId: makerOrder.UserId,
            takerUserId: takerOrder.UserId,
            makerFeeRate: makerFeeRate,
            takerFeeRate: takerFeeRate,
            feeBuyer: buyerUserId == makerOrder.UserId ? makerFeeRate * quoteQuantity : takerFeeRate * quoteQuantity,
            feeSeller: sellerUserId == makerOrder.UserId ? makerFeeRate * quoteQuantity : takerFeeRate * quoteQuantity
        );
    }

}

