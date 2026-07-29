using Microsoft.Extensions.Logging;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.Enums.Order;

namespace Orders.Application.Services;

/// <summary>
/// مشتری مظنهٔ ادمین را می‌پذیرد و معامله بلافاصله انجام می‌شود.
///
/// <para>
/// این جایگزین مدل «ادمین دو سفارش ۱۰۰۰ گرمی می‌گذارد» است (issue #48). به‌جای اینکه
/// نقدینگی از قبل در دفتر بخوابد و وثیقه‌اش قفل بماند، دو سفارش <b>دقیقاً به اندازهٔ
/// مقدار درخواستی</b> ساخته می‌شوند و در همان لحظه مصرف می‌گردند.
/// </para>
///
/// <para>
/// <b>چرا سفارش می‌سازد و نه مستقیم معامله:</b> جدول <c>Trade</c> شناسهٔ سفارش خرید و
/// فروش را الزامی کرده (FK). ساختن معامله بدون سفارش یا تغییر schema می‌خواست یا یک مسیر
/// دوم ساخت معامله — و مسیر دوم دقیقاً همان چیزی است که issue #40 دربارهٔ آن است. این
/// طراحی عمداً از همان <c>ExecuteAtomicMatchAsync</c> استفاده می‌کند که امروز کار می‌کند،
/// پس outbox، تسویه، تاریخچه و گزارش‌ها همگی دست‌نخورده می‌مانند.
/// </para>
/// </summary>
public class QuoteFillService
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly OrderService _orderService;
    private readonly MarketModeProvider _marketMode;
    private readonly OrderMatchingRepository _matchingRepository;
    private readonly ILogger<QuoteFillService> _logger;

    public QuoteFillService(
        IQuoteRepository quoteRepository,
        OrderService orderService,
        MarketModeProvider marketMode,
        OrderMatchingRepository matchingRepository,
        ILogger<QuoteFillService> logger)
    {
        _quoteRepository = quoteRepository;
        _orderService = orderService;
        _marketMode = marketMode;
        _matchingRepository = matchingRepository;
        _logger = logger;
    }

    /// <summary>
    /// مشتری مقدار مشخصی را روی مظنهٔ جاری معامله می‌کند. مشتری قیمت وارد نمی‌کند — قیمت
    /// از مظنه می‌آید، که ابهام مثقال/گرم را هم از جریان مشتری کاملاً حذف می‌کند.
    /// </summary>
    public async Task<(bool Success, string Message, Trade? Trade)> AcceptQuoteAsync(
        Guid customerUserId, string symbol, OrderSide customerSide, decimal quantity)
    {
        if (quantity <= 0)
            return (false, "مقدار باید بزرگ‌تر از صفر باشد.", null);

        if (!_marketMode.IsDealerMarket(symbol, out var marketMakerUserId))
            return (false, "این نماد در حالت مظنه‌ای نیست.", null);

        // ادمین نمی‌تواند مظنهٔ خودش را بپذیرد: طرفین یکی می‌شدند و تسویه هم آن را رد
        // می‌کرد — بهتر است همین‌جا با پیام روشن جلویش گرفته شود.
        if (customerUserId == marketMakerUserId)
            return (false, "بازارگردان نمی‌تواند مظنهٔ خودش را بپذیرد.", null);

        var quote = await _quoteRepository.GetActiveAsync(symbol);
        if (quote is null)
            return (false, "در حال حاضر مظنه‌ای منتشر نشده است.", null);

        var price = quote.PriceFor(customerSide);

        // هر دو سفارش با یک قیمت و یک مقدار ساخته می‌شوند، پس تطبیق حتماً کامل انجام
        // می‌شود و چیزی در دفتر باقی نمی‌ماند.
        var customerOrder = await CreateSideAsync(customerUserId, symbol, customerSide, quantity, price,
            $"پذیرش مظنه {quote.Id}");

        if (customerOrder is null)
            return (false, "ثبت سفارش شما انجام نشد.", null);

        var adminSide = customerSide == OrderSide.Buy ? OrderSide.Sell : OrderSide.Buy;
        var adminOrder = await CreateSideAsync(marketMakerUserId, symbol, adminSide, quantity, price,
            $"طرف مقابل مظنه {quote.Id}");

        if (adminOrder is null)
        {
            // سفارش مشتری ساخته و قفل شده ولی طرف مقابلی ندارد. لغو می‌شود تا وثیقه‌اش
            // آزاد گردد، وگرنه پول مشتری بی‌دلیل قفل می‌ماند.
            await _orderService.CancelOrderAsync(customerOrder.Id, "طرف مقابل مظنه ثبت نشد");
            return (false, "در حال حاضر امکان انجام این معامله نیست.", null);
        }

        var buyOrder = customerSide == OrderSide.Buy ? customerOrder : adminOrder;
        var sellOrder = customerSide == OrderSide.Buy ? adminOrder : customerOrder;

        var (success, trade, error) =
            await _matchingRepository.ExecuteAtomicMatchAsync(buyOrder, sellOrder, quantity);

        if (!success)
        {
            _logger.LogError(
                "Quote fill failed to match for user {UserId} on {Symbol}: {Error}",
                customerUserId, symbol, error);

            // هیچ معامله‌ای ثبت نشده، پس هر دو سفارش باید لغو شوند تا وثیقهٔ هر دو طرف
            // آزاد شود.
            await _orderService.CancelOrderAsync(customerOrder.Id, "تطبیق مظنه انجام نشد");
            await _orderService.CancelOrderAsync(adminOrder.Id, "تطبیق مظنه انجام نشد");

            return (false, error ?? "انجام معامله ممکن نشد.", null);
        }

        _logger.LogInformation(
            "Quote fill: user {UserId} {Side} {Quantity} {Symbol} at {Price} against market maker {MarketMaker}.",
            customerUserId, customerSide, quantity, symbol, price, marketMakerUserId);

        return (true, "معامله با موفقیت انجام شد.", trade);
    }

    /// <summary>
    /// یک سمت معامله را می‌سازد: سفارش ثبت، وثیقه قفل و سفارش تأیید می‌شود — ولی تطبیق
    /// نمی‌خورد. تطبیق یک بار و برای هر دو سفارش با هم انجام می‌شود.
    /// </summary>
    private async Task<Order?> CreateSideAsync(
        Guid userId, string symbol, OrderSide side, decimal quantity, decimal price, string notes)
    {
        try
        {
            var command = new CreateOrderCommand(
                symbol, quantity, price, userId, side, TradingType.Spot, notes);

            return await _orderService.CreateLockedAndConfirmedOrderForQuoteAsync(command);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Could not create the {Side} side of a quote fill for user {UserId} on {Symbol}.",
                side, userId, symbol);
            return null;
        }
    }
}
