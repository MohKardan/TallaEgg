using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orders.Application;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.Enums.Order;

namespace Orders.Application.Services;

/// <summary>
/// Thread-Safe Matching Engine with Database Locking
/// موتور تطبیق ایمن با قفل پایگاه داده
/// </summary>
public class MatchingEngineService : BackgroundService, IMatchingEngine
{
    private readonly ILogger<MatchingEngineService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(1);
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1); // Prevent concurrent processing
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
        _logger.LogInformation("🚀 Matching Engine Service is starting...");
        _isRunning = true;

        try
        {
            while (!stoppingToken.IsCancellationRequested && _isRunning)
            {
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
    /// Process single order (deprecated - use batch processing)
    /// پردازش سفارش منفرد (منسوخ - از پردازش دسته‌ای استفاده کنید)
    /// </summary>
    public async Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("⚠️ ProcessOrderAsync is deprecated. Use ProcessAllPendingOrdersAsync for better performance.");
        
        if (!_processingSemaphore.Wait(5000))
        {
            _logger.LogWarning("⏰ Could not acquire processing lock for order {OrderId}", order.Id);
            return;
        }

        try
        {
            await ProcessSingleAssetAsync(order.Asset, cancellationToken);
        }
        finally
        {
            _processingSemaphore.Release();
        }
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

                // Execute atomic match
                // اجرای تطبیق اتمی
                var result = await matchingRepository.ExecuteAtomicMatchAsync(
                    buyOrder, sellOrder, matchQty);

                if (result.Success)
                {
                    matchCount++;
                    _logger.LogInformation(
                        "✅ Match #{MatchCount} for {Asset}: {Quantity} @ {Price}",
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

}
