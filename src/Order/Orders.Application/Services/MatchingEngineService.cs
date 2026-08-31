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
/// Matching engine, made safe by database locking, with maker/taker support.
/// </summary>
public class MatchingEngineService : BackgroundService, IMatchingEngine
{
    /// <summary>
    /// .NET does not allow a singleton to depend on a scoped service, since the scoped one may
    /// already have been disposed. Hence resolving a scope per unit of work instead.
    /// </summary>
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<MatchingEngineService> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly TimeSpan _processingInterval = TimeSpan.FromSeconds(1);
    private readonly SemaphoreSlim _processingSemaphore = new(1, 1); // Prevent concurrent processing
    private bool _isRunning = false;

    /// <summary>
    /// The market maker's (admin's) user id. When it is set and RequireMarketMakerCounterparty is
    /// on, every trade must have this user on one side.
    /// </summary>
    // The single-market-maker rule that used to live here is gone.
    //
    // It compared both sides of a candidate match against one configured user id. There is no
    // longer a single market maker to compare against: the counterparty of a quote fill is
    // whoever published that quote, so "the market maker" is a property of a quote rather than
    // of the system. The rule was also already unreachable — Matching:RequireMarketMakerCounterparty
    // makes every unlisted symbol a Dealer market, and dealer symbols never enter this loop at
    // all (issue #74).
    //
    // If peer-to-peer trading is ever opened, the rule has to be restated in terms of the new
    // model — probably "at least one side is an administrator" — rather than restored as it was.

    /// <summary>
    /// Decides whether this instance runs the background sweep (issue #160).
    ///
    /// Thirty seconds, renewed at the halfway mark, so a loop that ticks every second makes one
    /// database round trip every fifteen rather than one per tick — and a host that dies still
    /// hands the sweep over within about a minute.
    /// </summary>
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);

    private readonly LeaderGate _leaderGate;

    public MatchingEngineService(
        IServiceScopeFactory scopeFactory,
        ILogger<MatchingEngineService> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILeaderLease leaderLease)
    {
        _scopeFactory = scopeFactory;

        _logger = logger;
        _serviceProvider = serviceProvider;

        _leaderGate = new LeaderGate(ServiceLeaseRoles.MatchingEngine, LeaseDuration, leaderLease, logger);
    }

    /// <summary>internal so a test can ask the gate directly, without driving the loop's timing.</summary>
    internal Task<bool> TryLeadAsync(CancellationToken ct = default) => _leaderGate.TryLeadAsync(ct);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🚀 Matching Engine Service is starting...");
        _isRunning = true;

        try
        {
            while (!stoppingToken.IsCancellationRequested && _isRunning)
            {
                // Only one instance sweeps the order book (issue #160). The semaphore below
                // serialises this loop against itself within one process and is invisible to a
                // second one; the lease is not.
                //
                // The sweep alone is gated. ProcessOrderAsync, which OrderService calls on the
                // request path, stays available on every instance — gating that would mean an
                // order placed against a follower was never matched at all.
                if (!await _leaderGate.TryLeadAsync(stoppingToken))
                {
                    await Task.Delay(_processingInterval, stoppingToken);
                    continue;
                }

                using var scope = _scopeFactory.CreateScope();
                var _walletApiClient = scope.ServiceProvider.GetRequiredService<IWalletApiClient>();

                // Use semaphore to ensure only one processing cycle runs at a time
                // The semaphore ensures only one cycle runs at a time.
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
    /// Hands the sweep back on a graceful shutdown so another instance can take it over at once
    /// rather than waiting out the lease (issue #160). A crash skips this, which is what the
    /// lease expiry is for.
    /// </summary>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        // Not the caller's token: on a forced shutdown it is already cancelled, and the release is
        // a single UPDATE worth finishing so the next instance does not sit idle for half a minute.
        await _leaderGate.ReleaseAsync(CancellationToken.None);
    }

    /// <summary>
    /// The semaphore is released here rather than at the end of ExecuteAsync. This instance is
    /// shared between the background loop and the request path (issue #53), so disposing it when
    /// the loop stops would give any request being processed at that moment an
    /// ObjectDisposedException.
    /// </summary>
    public override void Dispose()
    {
        _processingSemaphore.Dispose();
        base.Dispose();
    }

    /// <summary>
    /// Process single order with immediate Maker/Taker identification
    /// Processes a single order, determining maker/taker immediately.
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

            var marketMode = scope.ServiceProvider.GetRequiredService<MarketModeProvider>();
            if (!ShouldMatchInBackground(marketMode, order.Asset))
                return false;

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
    /// Processes a single order by id.
    /// </summary>
    public async Task ProcessOrderAsync(Guid orderId, CancellationToken cancellationToken = default)
    {
        await ProcessOrderForMatchingAsync(orderId);
    }

    /// <summary>
    /// Process single order (legacy method - enhanced with Maker/Taker)
    /// Processes a single order. Legacy entry point, since extended with maker/taker handling.
    /// </summary>
    public async Task ProcessOrderAsync(Order order, CancellationToken cancellationToken = default)
    {
        await ProcessOrderForMatchingAsync(order.Id);
    }

    /// <summary>
    /// Process all pending orders with thread-safe atomic matching
    /// Processes all pending orders using safe atomic matching.
    /// </summary>
    public async Task ProcessAllPendingOrdersAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var matchingRepository = scope.ServiceProvider.GetRequiredService<OrderMatchingRepository>();

            // Get all assets with active orders
            // Every asset that currently has active orders.
            var activeAssets = await matchingRepository.GetActiveAssetsAsync();
            
            if (!activeAssets.Any())
            {
                _logger.LogDebug("📭 No active assets found for processing");
                return;
            }

            _logger.LogDebug("🔄 Processing {Count} assets: {Assets}", 
                activeAssets.Count, string.Join(", ", activeAssets));

            // Process each asset independently
            // Each asset is processed independently.
            var marketMode = scope.ServiceProvider.GetRequiredService<MarketModeProvider>();

            var tasks = activeAssets
                .Where(asset => ShouldMatchInBackground(marketMode, asset))
                .Select(asset => ProcessSingleAssetAsync(asset, cancellationToken))
                .ToArray();

            await Task.WhenAll(tasks);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Error processing all pending orders");
        }
    }

    /// <summary>
    /// Whether this background loop should match a symbol at all.
    ///
    /// In Dealer mode it must not. A quote acceptance creates both orders, matches them
    /// completely and consumes them inside one operation (<c>QuoteFillService</c>), so there
    /// is never resting liquidity for this loop to pair up — its only possible effect is to
    /// reach the same pair the fill is already matching.
    ///
    /// That is exactly what happened in issue #74: one order pair produced two trades, one
    /// from the fill and one from this loop, and the customer paid for both. The semaphore
    /// added in #53 serialises this loop against itself; it does not cover the fill path,
    /// which calls the repository directly. Removing the second matcher is a smaller and
    /// surer fix than trying to make two matchers agree.
    ///
    /// The concurrency token on RemainingAmount would now refuse the loser of such a race, so
    /// this is the second line of defence rather than the only one — but a refusal still
    /// produces a rolled-back transaction and a warning for a situation that should not arise.
    /// </summary>
    /// <param name="marketMode">
    /// Resolved from the caller's scope rather than injected: this service is a singleton and
    /// <see cref="MarketModeProvider"/> is scoped, and it deliberately re-reads configuration
    /// on each call so a mode change needs no restart.
    /// </param>
    private bool ShouldMatchInBackground(MarketModeProvider marketMode, string asset)
    {
        if (marketMode.GetMode(asset) != MarketMode.Dealer)
            return true;

        _logger.LogDebug(
            "Skipping {Asset}: it is a dealer market, where fills are matched synchronously.", asset);
        return false;
    }

    /// <summary>
    /// Process orders for a single asset with atomic matching
    /// Processes one asset's orders using atomic matching.
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
                // Get this asset's orders for matching
                // Fetch this asset's buy and sell orders.
                var buyOrders = await matchingRepository.GetBuyOrdersAsync(asset);
                var sellOrders = await matchingRepository.GetSellOrdersAsync(asset);

                if (!buyOrders.Any() || !sellOrders.Any())
                {
                    _logger.LogDebug("📭 No matching orders available for asset {Asset}", asset);
                    break;
                }

                // Find best matching pair
                // Find the best pair to match.
                var (buyOrder, sellOrder, matchQty) = FindBestMatch(buyOrders, sellOrders);

                if (buyOrder == null || sellOrder == null || matchQty <= 0)
                {
                    _logger.LogDebug("❌ No compatible match found for asset {Asset}", asset);
                    break;
                }

                // Execute atomic match with enhanced Maker/Taker logic
                // Run the atomic match, including maker/taker handling.
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
    /// Finds the best pair under price-time priority.
    /// </summary>
    private static (Order? BuyOrder, Order? SellOrder, decimal MatchQuantity) FindBestMatch(
        List<Order> buyOrders, 
        List<Order> sellOrders)
    {
        // Buy orders are sorted by Price DESC, Time ASC (highest price first)
        // Sell orders are sorted by Price ASC, Time ASC (lowest price first)
        // Buys sort by price descending then time ascending (highest price first);
        // sells by price ascending then time ascending (lowest price first).
        
        foreach (var buyOrder in buyOrders.Where(b => b.RemainingAmount > 0))
        {
            foreach (var sellOrder in sellOrders.Where(s => s.RemainingAmount > 0))
            {
                // Check price compatibility: Buy price >= Sell price
                // Prices are compatible when the buy price is at least the sell price.
                if (buyOrder.Price >= sellOrder.Price)
                {
                    var matchQty = Math.Min(buyOrder.RemainingAmount, sellOrder.RemainingAmount);
                    return (buyOrder, sellOrder, matchQty);
                }
                
                // Since sell orders are sorted by price ASC, 
                // if current sell is too expensive, all following will be too
                // Sells are sorted by ascending price, so if this one is already too expensive
                // every later one is too.
                break;
            }
        }

        return (null, null, 0);
    }

    /// <summary>
    /// Get matching orders for immediate execution (Taker identification)
    /// Returns the orders eligible for immediate execution, identifying the taker.
    /// </summary>
    private async Task<List<Order>> GetMatchingOrdersAsync(OrderMatchingRepository matchingRepository, Order incomingOrder)
    {
        try
        {
            if (incomingOrder.Side == OrderSide.Buy)
            {
                // For buy orders, find sell orders with price <= buy price
                var sellOrders = await matchingRepository.GetSellOrdersAsync(incomingOrder.Asset);
                return sellOrders
                    .Where(s => s.Price <= incomingOrder.Price && s.RemainingAmount > 0)
                    .Where(s => IsAllowedCounterparty(incomingOrder, s))
                    .ToList();
            }
            else
            {
                // For sell orders, find buy orders with price >= sell price
                var buyOrders = await matchingRepository.GetBuyOrdersAsync(incomingOrder.Asset);
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
    /// Decides whether a counterparty is allowed to match.
    ///
    /// Two rules:
    /// 1. No user trades with themselves. Always enforced — self-trading is economically neutral
    ///    but produces a false audit trail and can be used to manufacture fake volume.
    /// 2. When the market-maker requirement is on, one side must be the admin. At this stage of
    ///    the product customers trade only with the admin, never with each other.
    ///
    /// A rejected order is not cancelled; it is skipped for this matching round and stays open.
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

        return true;
    }

    /// <summary>
    /// Execute trade with proper Maker/Taker fee calculation
    /// Executes the trade, computing maker and taker fees correctly.
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

                // Releasing residual collateral deliberately does not happen here.
                //
                // At this point the balance lock does not exist yet — OrderService locks after
                // matching, audit finding C-5 — and the settlement that consumes it runs later,
                // via the outbox. Releasing here races both, and did fail in a real test.
                // OutboxProcessorService now does it after a successful settlement (issue #52).
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
    /// Runs the atomic match, including maker/taker handling.
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

    // One trade-creation path, not two (issue #40).
    //
    // CreateMakerTakerTrade used to live here: it built a complete Trade that was never saved. The
    // engine immediately called ExecuteAtomicMatchAsync, which built its own trade with CreateTrade
    // — and that was the one actually stored and queued for settlement.
    //
    // Two paths meant two sources of truth: the fee rates here were 0.000 while the repository's
    // were 0.001/0.002, and because the repository's version was the one saved, fees came out
    // denominated in the quote currency and every settlement was refused with "fee exceeds trade
    // amount". Changing one was silently ignored by the other — which is exactly how that bug hid.
    //
    // Trade.Create is now called only from OrderMatchingRepository.CreateTrade.
    // SingleTradeCreationPathTests enforces that at the IL level so a second path cannot come back.
}
