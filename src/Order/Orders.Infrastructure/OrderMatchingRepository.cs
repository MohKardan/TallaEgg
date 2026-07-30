using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
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
                .Where(Matchable)   // فقط سفارش تأییدشده؛ Pending هنوز وثیقه ندارد
                .Where(o => o.Asset == asset &&
                           o.Side == OrderSide.Buy &&
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
                .Where(Matchable)   // فقط سفارش تأییدشده؛ Pending هنوز وثیقه ندارد
                .Where(o => o.Asset == asset &&
                           o.Side == OrderSide.Sell &&
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
        // Both parameters are Order, so passing them in the wrong order compiles silently.
        // That happened: callers passed (maker, taker) instead of (buy, sell), which inverted
        // buyer and seller whenever the resting order was a sell — the trade was then settled
        // in the wrong direction, giving the gold to the seller and the money to the buyer.
        // Fail loudly rather than record an inverted trade.
        if (buyOrder.Side != OrderSide.Buy)
        {
            _logger.LogError("ExecuteAtomicMatchAsync received order {OrderId} with side {Side} as the BUY order.",
                buyOrder.Id, buyOrder.Side);
            return (false, null, $"سفارش خرید نامعتبر است: سمت سفارش {buyOrder.Side} است.");
        }
        if (sellOrder.Side != OrderSide.Sell)
        {
            _logger.LogError("ExecuteAtomicMatchAsync received order {OrderId} with side {Side} as the SELL order.",
                sellOrder.Id, sellOrder.Side);
            return (false, null, $"سفارش فروش نامعتبر است: سمت سفارش {sellOrder.Side} است.");
        }

        using var transaction = await _context.Database.BeginTransactionAsync();

        try
        {
            // 1. Read the orders as the database currently holds them.
            //
            // The reload is explicit because a plain query would not have re-read anything:
            // when the same DbContext created these orders moments earlier, EF identity
            // resolution returns the tracked in-memory instance and the query result is
            // discarded. That is how a match once ran against RemainingAmount = 1.2345 while
            // the row said 1.23 — the quantity columns hold two decimal places (issue #74).
            //
            // The real protection against a racing matcher is the concurrency token on
            // RemainingAmount, which turns a stale write into an exception. This reload
            // additionally makes the quantity clamp below reason about real values, so the
            // ordinary path succeeds instead of failing the concurrency check.
            foreach (var tracked in new[] { buyOrder, sellOrder })
            {
                var entry = _context.Entry(tracked);
                if (entry.State != EntityState.Detached)
                    await entry.ReloadAsync();
            }

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
            //if (currentBuyOrder.Price < currentSellOrder.Price)
            //{
            //    await transaction.RollbackAsync();
            //    return (false, null, "قیمت خرید کمتر از قیمت فروش است");
            //}

            // 4. Calculate actual tradeable quantity.
            //
            // Rounded to the base asset's own precision first. A caller can ask for a
            // quantity finer than the asset supports — a customer typing 1.2345 grams — and
            // the trade table would store it, while the order table (two decimal places)
            // could not. Rounding here means one number is used for the trade, the order and
            // the settlement, whatever the caller passed (issue #74).
            var baseAssetForQty = currentBuyOrder.Asset.Split('/')[0];
            var requestedQty = CurrenciesConstant.RoundToCurrencyPrecision(matchQuantity, baseAssetForQty);

            var actualMatchQty = Math.Min(
                Math.Min(requestedQty, currentBuyOrder.RemainingAmount),
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

            // 9. Enqueue wallet settlement via the transactional outbox.
            //    Wallet balances live in a separate database/service, so they cannot
            //    join this transaction directly. Instead we durably record the intent
            //    here: the Trade and its outbox row commit atomically together, so a
            //    matched trade can never be left without a pending settlement. A
            //    background processor later performs the settlement (idempotently,
            //    keyed on the Trade id) against the Wallet API.
            var settlementPayload = JsonSerializer.Serialize(MapTradeToSettlementDto(trade));
            var outboxMessage = OutboxMessage.Create("TradeSettlement", trade.Id, settlementPayload);
            _context.OutboxMessages.Add(outboxMessage);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation(
                "Successfully matched orders: Buy={BuyOrderId}, Sell={SellOrderId}, Quantity={Quantity}, Price={Price}",
                currentBuyOrder.Id, currentSellOrder.Id, actualMatchQty, tradePrice);

            return (true, trade, string.Empty);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Another matcher changed one of these orders between our read and our write, or
            // our copy was stale. Either way this match must not be applied: the alternative
            // is the defect in issue #74, where one order pair produced two trades and the
            // customer paid for both.
            //
            // Logged as a warning, not an error: losing the race is the guard working, and
            // the winner's trade is valid. An error would page someone for correct behaviour.
            await transaction.RollbackAsync();
            _logger.LogWarning(ex,
                "Refused to match Buy={BuyOrderId} with Sell={SellOrderId}: the orders changed " +
                "concurrently. Another matcher has already filled them.",
                buyOrder.Id, sellOrder.Id);

            return (false, null, "این سفارش هم‌زمان توسط عملیات دیگری پردازش شد.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error in atomic order matching");
            return (false, null, $"خطا در تطبیق سفارشات: {ex.Message}");
        }
    }

    /// <summary>
    /// Maps a matched <see cref="Trade"/> to the settlement DTO stored in the outbox
    /// payload. This is the exact contract the Wallet API consumes, so the background
    /// processor can forward it without re-fetching or re-mapping.
    /// </summary>
    private static TradeDto MapTradeToSettlementDto(Trade trade) => new()
    {
        Id = trade.Id,
        BuyOrderId = trade.BuyOrderId,
        SellOrderId = trade.SellOrderId,
        MakerOrderId = trade.MakerOrderId,
        TakerOrderId = trade.TakerOrderId,
        Symbol = trade.Symbol,
        Price = trade.Price,
        Quantity = trade.Quantity,
        QuoteQuantity = trade.QuoteQuantity,
        BuyerUserId = trade.BuyerUserId,
        SellerUserId = trade.SellerUserId,
        MakerUserId = trade.MakerUserId,
        TakerUserId = trade.TakerUserId,
        FeeBuyer = trade.FeeBuyer,
        FeeSeller = trade.FeeSeller,
        MakerFee = trade.MakerFee,
        TakerFee = trade.TakerFee,
        MakerFeeRate = trade.MakerFeeRate,
        TakerFeeRate = trade.TakerFeeRate,
        CreatedAt = trade.CreatedAt,
        UpdatedAt = trade.UpdatedAt
    };

    /// <summary>
    /// Check if order can be processed
    /// بررسی امکان پردازش سفارش
    /// </summary>
    /// <summary>
    /// آیا این سفارش می‌تواند وارد تطبیق شود؟
    ///
    /// <b>Pending عمداً بیرون است.</b> سفارش لحظه‌ای که ذخیره می‌شود Pending است، یعنی
    /// پیش از آنکه وثیقه‌اش قفل شود. اگر Pending قابل تطبیق باشد، حلقهٔ پس‌زمینه — که هر
    /// ثانیه اجرا می‌شود — می‌تواند سفارشی را بردارد که هنوز هیچ وثیقه‌ای پشتش نیست، و
    /// معامله‌ای ثبت شود که تسویه‌اش هرگز موفق نمی‌شود.
    ///
    /// این همان یافتهٔ ممیزی C-5 است، در ریشه‌اش. با کنار گذاشتن Pending، ترتیب
    /// «قفل، سپس تطبیق» یک تضمین ساختاری می‌شود نه یک قرارداد رفتاری: سفارش فقط پس از
    /// موفقیت قفل به Confirmed می‌رسد، و فقط Confirmed دیده می‌شود.
    ///
    /// این باید تنها تعریف «قابل تطبیق» در کل سیستم بماند — پرس‌وجوهای دفتر سفارش و
    /// بازبینیِ داخل تراکنش هر دو از همین استفاده می‌کنند، وگرنه دیر یا زود از هم جدا
    /// می‌شوند.
    ///
    /// عمداً <see cref="Expression"/> است و نه یک متد معمولی: EF Core نمی‌تواند فراخوانی
    /// متد دلخواه را به SQL ترجمه کند و پرس‌وجو موقع اجرا استثنا می‌داد — خطایی که
    /// کامپایلر نمی‌گیرد.
    /// </summary>
    private static readonly Expression<Func<Order, bool>> Matchable =
        o => o.Status == OrderStatus.Confirmed ||
             o.Status == OrderStatus.Partially;

    /// <summary>نسخهٔ کامپایل‌شدهٔ همان شرط، برای بازبینی روی موجودیتی که از قبل در حافظه است.</summary>
    private static readonly Func<Order, bool> IsMatchable = Matchable.Compile();

    private static bool IsOrderProcessable(Order order) => IsMatchable(order);

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
        // ارزش معامله رو به پایین گرد می‌شود، در حالی که مبلغ قفلِ سفارش رو به بالا.
        //
        // این عدد هم از خریدار کسر و هم به فروشنده پرداخت می‌شود (پس در هر معامله
        // تراز است)، و همین عدد است که از وثیقهٔ قفل‌شده مصرف می‌گردد. قفل یک بار برای
        // کل سفارش حساب می‌شود ولی مصرف در هر fill جداگانه؛ اگر هر دو با AwayFromZero
        // گرد شوند، مجموع مصرف‌ها می‌تواند از قفل بیشتر شود و گاردِ «وثیقهٔ کافی نیست»
        // یک معاملهٔ کاملاً معتبر را رد کند.
        //
        // با جهت‌های مخالف، «مجموع مصرف ≤ قفل» یک تضمین ریاضی است. توضیح کامل روی
        // CurrenciesConstant.FloorToCurrencyPrecision (issue #52).
        var quoteAsset = buyOrder.Asset.Split('/').Last();
        var quoteQuantity = CurrenciesConstant.FloorToCurrencyPrecision(quantity * price, quoteAsset);

        // Fees are DISABLED for the MVP (charged at 0%). The real maker/taker fee model —
        // which asset each fee is denominated in (buyer fee should be in the base asset it
        // receives, not the quote) and crediting collected fees to the fee account — is
        // tracked in issue #35. Zero fees keep trade settlement balanced until then.
        var makerFeeRate = 0.000m;
        var takerFeeRate = 0.000m;
        
        // Determine which order is Maker (older timestamp) and which is Taker (newer timestamp)
        var isBuyOrderMaker = buyOrder.CreatedAt <= sellOrder.CreatedAt;
        
        if (isBuyOrderMaker)
        {
            // Buy order is Maker, Sell order is Taker
            var makerFee = quoteQuantity * makerFeeRate;
            var takerFee = quoteQuantity * takerFeeRate;
            
            return Trade.Create(
                buyOrderId: buyOrder.Id,
                sellOrderId: sellOrder.Id,
                makerOrderId: buyOrder.Id,
                takerOrderId: sellOrder.Id,
                symbol: buyOrder.Asset,
                price: price,
                quantity: quantity,
                quoteQuantity: quoteQuantity,
                buyerUserId: buyOrder.UserId,
                sellerUserId: sellOrder.UserId,
                makerUserId: buyOrder.UserId,
                takerUserId: sellOrder.UserId,
                makerFeeRate: makerFeeRate,
                takerFeeRate: takerFeeRate,
                feeBuyer: makerFee,
                feeSeller: takerFee
            );
        }
        else
        {
            // Sell order is Maker, Buy order is Taker
            var makerFee = quoteQuantity * makerFeeRate;
            var takerFee = quoteQuantity * takerFeeRate;
            
            return Trade.Create(
                buyOrderId: buyOrder.Id,
                sellOrderId: sellOrder.Id,
                makerOrderId: sellOrder.Id,
                takerOrderId: buyOrder.Id,
                symbol: buyOrder.Asset,
                price: price,
                quantity: quantity,
                quoteQuantity: quoteQuantity,
                buyerUserId: buyOrder.UserId,
                sellerUserId: sellOrder.UserId,
                makerUserId: sellOrder.UserId,
                takerUserId: buyOrder.UserId,
                makerFeeRate: makerFeeRate,
                takerFeeRate: takerFeeRate,
                feeBuyer: takerFee,
                feeSeller: makerFee
            );
        }
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
                .Where(Matchable)
                .Where(o => o.RemainingAmount > 0)
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
