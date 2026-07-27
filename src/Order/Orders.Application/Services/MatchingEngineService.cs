using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orders.Application;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
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

    /// <summary>
    /// شناسهٔ کاربر بازارگردان (ادمین). اگر تنظیم شده باشد و RequireMarketMakerCounterparty
    /// روشن باشد، هر معامله باید یک طرفش این کاربر باشد.
    /// </summary>
    private readonly Guid? _marketMakerUserId;

    /// <summary>
    /// آیا الزام «یک طرف معامله باید بازارگردان باشد» فعال است؟ در مدل فعلی کسب‌وکار
    /// مشتری‌ها فقط با ادمین معامله می‌کنند، اما وقتی بازار نظیربه‌نظیر باز شود این
    /// تنظیم خاموش می‌شود (نه اینکه کد حذف شود).
    /// </summary>
    private readonly bool _requireMarketMakerCounterparty;

    public MatchingEngineService(
        IServiceScopeFactory scopeFactory,
        ILogger<MatchingEngineService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _scopeFactory = scopeFactory;

        _logger = logger;
        _serviceProvider = serviceProvider;

        _requireMarketMakerCounterparty =
            configuration.GetValue("Matching:RequireMarketMakerCounterparty", defaultValue: false);

        var marketMakerId = configuration.GetValue<string?>("Matching:MarketMakerUserId", null);
        _marketMakerUserId = Guid.TryParse(marketMakerId, out var parsed) ? parsed : null;

        if (_requireMarketMakerCounterparty && _marketMakerUserId is null)
        {
            // خاموش می‌ماند تا تطبیق به‌کلی متوقف نشود؛ اما باید دیده شود.
            _logger.LogError(
                "Matching:RequireMarketMakerCounterparty is enabled but Matching:MarketMakerUserId is not set. " +
                "The market-maker rule will NOT be enforced.");
        }
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
            _logger.LogInformation("🛑 Matching Engine Service has stopped");
        }
    }

    /// <summary>
    /// سمافور اینجا آزاد می‌شود، نه در انتهای ExecuteAsync. این نمونه بین حلقهٔ
    /// پس‌زمینه و مسیر درخواست‌ها مشترک است (issue #53)، پس اگر همزمان با توقف
    /// حلقه Dispose می‌شد، درخواستی که در همان لحظه در حال پردازش بود
    /// ObjectDisposedException می‌گرفت.
    /// </summary>
    public override void Dispose()
    {
        _processingSemaphore.Dispose();
        base.Dispose();
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
                return sellOrders
                    .Where(s => s.Price <= incomingOrder.Price && s.RemainingAmount > 0)
                    .Where(s => IsAllowedCounterparty(incomingOrder, s))
                    .ToList();
            }
            else
            {
                // For sell orders, find buy orders with price >= sell price
                var buyOrders = await matchingRepository.GetBuyOrdersWithLockAsync(incomingOrder.Asset);
                return buyOrders
                    .Where(b => b.Price >= incomingOrder.Price && b.RemainingAmount > 0)
                    .Where(b => IsAllowedCounterparty(incomingOrder, b))
                    .ToList();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error getting matching orders for {OrderId}", incomingOrder.Id);
            return new List<Order>();
        }
    }

    /// <summary>
    /// بررسی مجاز بودن طرف مقابل برای تطبیق.
    ///
    /// دو قاعده:
    /// ۱. هیچ کاربری با خودش معامله نمی‌کند. این قاعده همیشه فعال است — خودمعاملگی
    ///    از نظر اقتصادی خنثی است اما رد حسابرسی نادرست تولید می‌کند و می‌تواند برای
    ///    ساختن حجم صوری استفاده شود.
    /// ۲. اگر الزام بازارگردان فعال باشد، یک طرف معامله باید ادمین باشد. در این مرحله
    ///    از محصول، مشتری‌ها فقط با ادمین معامله می‌کنند و نه با یکدیگر.
    ///
    /// سفارش رد‌شده لغو نمی‌شود؛ فقط در این دور تطبیق نادیده گرفته می‌شود و باز می‌ماند.
    /// </summary>
    private bool IsAllowedCounterparty(Order incomingOrder, Order candidate)
    {
        if (incomingOrder.UserId == candidate.UserId)
        {
            _logger.LogDebug(
                "Skipping self-match for user {UserId} between orders {IncomingOrderId} and {CandidateOrderId}.",
                incomingOrder.UserId, incomingOrder.Id, candidate.Id);
            return false;
        }

        if (_requireMarketMakerCounterparty && _marketMakerUserId is Guid marketMaker)
        {
            var involvesMarketMaker =
                incomingOrder.UserId == marketMaker || candidate.UserId == marketMaker;

            if (!involvesMarketMaker)
            {
                _logger.LogDebug(
                    "Skipping customer-to-customer match between orders {IncomingOrderId} and {CandidateOrderId}: " +
                    "neither side is the market maker.",
                    incomingOrder.Id, candidate.Id);
                return false;
            }
        }

        return true;
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
            // ExecuteAtomicMatchAsync takes (buyOrder, sellOrder) — NOT (maker, taker).
            // Passing maker/taker positionally compiles (both are Order) but silently swaps
            // the roles whenever the resting order is a sell, which recorded the trade with
            // buyer and seller inverted and settled it in the wrong direction.
            var buyOrder = makerOrder.Side == OrderSide.Buy ? makerOrder : takerOrder;
            var sellOrder = makerOrder.Side == OrderSide.Buy ? takerOrder : makerOrder;

            var result = await matchingRepository.ExecuteAtomicMatchAsync(buyOrder, sellOrder, quantity);
            
            if (result.Success)
            {
                // Wallet settlement is no longer called inline here. ExecuteAtomicMatchAsync
                // enqueues a 'TradeSettlement' outbox message in the same transaction as the
                // trade, and OutboxProcessorService delivers it to the Wallet service reliably
                // (with retry + idempotency). This removes the old fire-and-forget HTTP calls
                // that could silently lose money if the Wallet service was unavailable.
                _logger.LogInformation(
                    "Maker/Taker trade executed: Maker:{MakerId} Taker:{TakerId} Qty:{Qty} Price:{Price}",
                    makerOrder.Id, takerOrder.Id, quantity, result.Trade?.Price);

                // اگر سفارشی کاملاً پر شده، باقی‌ماندهٔ وثیقه‌اش را آزاد می‌کنیم.
                await ReleaseResidualLockIfCompletedAsync(buyOrder.Id);
                await ReleaseResidualLockIfCompletedAsync(sellOrder.Id);
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
    /// اگر سفارش کاملاً پر شده باشد، هر مقدار وثیقه‌ای که هنوز به نام آن قفل مانده آزاد می‌شود.
    ///
    /// چرا لازم است (issue #52): مبلغ قفل‌شده یک بار برای کل سفارش حساب می‌شود
    /// (Round(Amount × Price))، ولی مصرف در هر fill جداگانه گرد می‌شود. مجموع مقادیرِ
    /// جداگانه‌گردشده با مقدارِ یک‌بار‌گردشده برابر نیست، پس پس از پر شدن کامل سفارش یک
    /// باقی‌مانده در LockedBalance جا می‌ماند. آن باقی‌مانده متعلق به کاربر است و هیچ
    /// مسیری آن را برنمی‌گرداند.
    ///
    /// این کار عمداً بیرون از تراکنش تطبیق انجام می‌شود، چون کیف پول در سرویس و
    /// دیتابیس دیگری است. اگر شکست بخورد وضعیت از امروز بدتر نمی‌شود (باقی‌مانده
    /// همان‌جا می‌ماند) و مغایرت‌گیری #39 می‌تواند بعداً آن را بردارد.
    /// </summary>
    private async Task ReleaseResidualLockIfCompletedAsync(Guid orderId)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var orderRepository = scope.ServiceProvider.GetRequiredService<IOrderRepository>();
            var tradeRepository = scope.ServiceProvider.GetRequiredService<ITradeRepository>();
            var walletApiClient = scope.ServiceProvider.GetRequiredService<IWalletApiClient>();

            var order = await orderRepository.GetByIdAsync(orderId);
            if (order is null || order.Status != OrderStatus.Completed)
                return;

            var (asset, residual) = await ComputeResidualLockAsync(order, tradeRepository);
            if (residual <= 0)
                return;

            var (success, message) = await walletApiClient.UnlockBalanceAsync(order.UserId, asset, residual);

            if (success)
                _logger.LogInformation(
                    "Released residual lock of {Residual} {Asset} for completed order {OrderId}.",
                    residual, asset, orderId);
            else
                _logger.LogWarning(
                    "Could not release residual lock of {Residual} {Asset} for completed order {OrderId}: {Message}",
                    residual, asset, orderId, message);
        }
        catch (Exception ex)
        {
            // آزاد نشدن باقی‌مانده نباید تطبیق را بشکند؛ معامله از قبل commit شده است.
            _logger.LogError(ex, "Error releasing residual lock for order {OrderId}", orderId);
        }
    }

    /// <summary>
    /// باقی‌ماندهٔ وثیقهٔ یک سفارش: «آنچه قفل شد» منهای «آنچه معاملات آن مصرف کردند».
    ///
    /// «آنچه قفل شد» دقیقاً از روی خودِ سفارش بازمحاسبه می‌شود و این تنها به این دلیل
    /// ممکن است که قیمت هنگام ثبت سفارش به دقت ستون گرد می‌شود. پیش از آن، قفل با
    /// قیمتِ گرد‌نشده حساب می‌شد و از روی ردیف ذخیره‌شده قابل بازسازی نبود.
    /// </summary>
    private static async Task<(string Asset, decimal Residual)> ComputeResidualLockAsync(
        Order order, ITradeRepository tradeRepository)
    {
        var parts = order.Asset.Split('/');
        var baseAsset = parts[0];
        var quoteAsset = parts.Length > 1 ? parts[1] : parts[0];

        if (order.Side == OrderSide.Buy)
        {
            // خریدار ارز مظنه را قفل می‌کند و معاملات، QuoteQuantity مصرف می‌کنند.
            // Ceiling — همان فرمول و همان جهتی که هنگام ثبت سفارش قفل کرد.
            var locked = CurrenciesConstant.CeilingToCurrencyPrecision(order.Amount * order.Price, quoteAsset);
            var trades = await tradeRepository.GetTradesByBuyOrderIdAsync(order.Id);
            var consumed = trades.Sum(t => t.QuoteQuantity);

            return (quoteAsset, locked - consumed);
        }

        // فروشنده دارایی پایه را قفل می‌کند و معاملات دقیقاً Quantity مصرف می‌کنند —
        // بدون گرد کردن، چون همان واحدی است که سفارش با آن ثبت شده. پس این سمت در
        // حالت عادی باقی‌مانده‌ای ندارد؛ محاسبه‌اش را نگه می‌داریم تا اگر روزی این
        // فرض عوض شد، خودبه‌خود پوشش داده شود.
        var lockedBase = CurrenciesConstant.RoundToCurrencyPrecision(order.Amount, baseAsset);
        var sellTrades = await tradeRepository.GetTradesBySellOrderIdAsync(order.Id);
        var consumedBase = sellTrades.Sum(t => t.Quantity);

        return (baseAsset, lockedBase - consumedBase);
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
            // ExecuteAtomicMatchAsync takes (buyOrder, sellOrder). Maker/taker is derived
            // inside it from the order timestamps, so the orders must be passed by SIDE,
            // never by maker/taker role — doing the latter inverts buyer and seller
            // whenever the resting order is a sell.
            return await matchingRepository.ExecuteAtomicMatchAsync(buyOrder, sellOrder, quantity);
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
