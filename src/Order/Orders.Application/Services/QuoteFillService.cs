using Microsoft.Extensions.Logging;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Infrastructure.Clients;

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
    private readonly IWalletApiClient _walletApiClient;
    private readonly ILogger<QuoteFillService> _logger;

    public QuoteFillService(
        IQuoteRepository quoteRepository,
        OrderService orderService,
        MarketModeProvider marketMode,
        OrderMatchingRepository matchingRepository,
        IWalletApiClient walletApiClient,
        ILogger<QuoteFillService> logger)
    {
        _quoteRepository = quoteRepository;
        _orderService = orderService;
        _marketMode = marketMode;
        _matchingRepository = matchingRepository;
        _walletApiClient = walletApiClient;
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

        // Rounded once, here, before anything is created — and this value is what both orders
        // and the match then use.
        //
        // The order columns hold two decimal places, so a customer's 1.2345 grams was stored
        // as 1.23 while the in-memory order kept 1.2345, and the match ran on the unrounded
        // figure. Rounding at the boundary means the order the customer gets is the order the
        // confirmation showed them: the confirmation already displays the rounded amount, so
        // 1.23 is the number they agreed to (issue #74).
        var baseAsset = symbol.Split('/')[0];
        quantity = CurrenciesConstant.RoundToCurrencyPrecision(quantity, baseAsset);

        if (quantity <= 0)
            return (false, $"مقدار وارد‌شده از حداقل قابل معامله کمتر است.", null);

        // این پیام قبلاً می‌گفت «این نماد در حالت مظنه‌ای نیست» — که به گوش مشتری یک قاعدهٔ
        // محصول می‌رسید، درحالی‌که همیشه یعنی یک تنظیم جا افتاده (issue #73). مشتری کاری از
        // دستش برنمی‌آید؛ فقط باید بداند بعداً دوباره امتحان کند. جزئیات برای اپراتور در
        // MarketModeStartupValidator لاگ می‌شود، نه اینجا.
        if (_marketMode.GetMode(symbol) != MarketMode.Dealer)
            return (false, "این نماد موقتاً در دسترس نیست. لطفاً کمی بعد دوباره تلاش کنید.", null);

        var quote = await _quoteRepository.GetActiveAsync(symbol);

        if (quote is null)
        {
            // The only thing that can name a counterparty is a published quote, so its absence
            // is now a hard stop rather than a detail. Logged as a warning because on a running
            // shop it means nobody has posted a price — the customer cannot fix it and the
            // operator needs to know.
            _logger.LogWarning(
                "Refusing quote fill for {UserId}: no active quote is published for {Symbol}.",
                customerUserId, symbol);

            return (false, "در حال حاضر مظنه‌ای منتشر نشده است.", null);
        }

        // ► طرف مقابلِ معامله، همان کسی است که این مظنه را منتشر کرده.
        //
        // پیش‌تر یک شناسه در فایل پیکربندی بود. آن مقدار روی دیتابیسی که از صفر ساخته می‌شود
        // قابل دانستن نبود، پس استقرار به «بالا بیاور، ثبت‌نام کن، شناسه را کپی کن، ری‌استارت
        // کن» تبدیل می‌شد — و مقدار غلط در این میان بی‌اثر نبود: سیستم بالا می‌آمد و هر
        // معامله را رد می‌کرد.
        //
        // خودِ مظنه از قبل می‌داند چه کسی منتشرش کرده، پس این اطلاعات هرگز لازم نبود جای
        // دیگری نگه‌داری شود. اقتصادش هم درست‌تر است: مشتری روی قیمتی معامله می‌کند که آن
        // شخص اعلام کرده، پس معامله باید روی دفتر همان شخص بنشیند. با یک ادمین رفتار دقیقاً
        // مثل قبل است؛ با چند ادمین، هر کدام دفتر خودشان را دارند بدون هیچ تنظیم اضافه‌ای.
        var marketMakerUserId = quote.PublishedByUserId;

        // ادمین نمی‌تواند مظنهٔ خودش را بپذیرد: طرفین یکی می‌شدند و تسویه هم آن را رد
        // می‌کرد — بهتر است همین‌جا با پیام روشن جلویش گرفته شود.
        if (customerUserId == marketMakerUserId)
            return (false, "بازارگردان نمی‌تواند مظنهٔ خودش را بپذیرد.", null);

        var price = quote.PriceFor(customerSide);

        // ► بررسی موجودی و اعتبار مشتری، پیش از ساختن هر سفارشی.
        //
        // این بررسی در مدل دفترِ سفارش داخل SubmitOrderAsync انجام می‌شد، و مسیر مظنه‌ای
        // (issue #48) از آن مسیر عبور نمی‌کند — پس بی‌سروصدا کنار گذاشته شده بود. تنها
        // چیزی که باقی مانده بود قفلِ وثیقه بود، و LockBalance هیچ بررسی موجودی ندارد:
        // گاردش در Wallet.cs کامنت شده تا حسابِ بازارگردان بتواند منفی شود.
        //
        // نتیجه این بود که هر کاربر تازه، بدون یک ریال اعتبار، می‌توانست معامله کند و
        // موجودی‌اش منفی می‌شد. روی دیتابیس قبلی دیده نمی‌شد چون همه از پیش اعتبار
        // داشتند؛ روی دیتابیس خالی بلافاصله ظاهر شد.
        //
        // بازارگردان عمداً مستثنی است: در مدل معامله‌گری او همیشه طرف مقابل است و
        // موقعیتِ منفی‌اش همان دفترِ کسب‌وکار است، نه یک خطا.
        var (checkSucceeded, checkMessage, hasBaseAsset, hasQuoteAsset) =
            await _walletApiClient.ValidateCreditAndBalanceAsync(customerUserId, symbol, quantity, price);

        if (!checkSucceeded)
        {
            _logger.LogWarning(
                "Refusing quote fill for {UserId} on {Symbol}: balance check failed — {Message}",
                customerUserId, symbol, checkMessage);

            return (false, "بررسی موجودی انجام نشد. لطفاً دوباره تلاش کنید.", null);
        }

        // خرید با دارایی مظنه پرداخت می‌شود و فروش با دارایی پایه.
        var hasEnough = customerSide == OrderSide.Buy ? hasQuoteAsset : hasBaseAsset;

        if (!hasEnough)
        {
            _logger.LogInformation(
                "Refusing quote fill for {UserId}: not enough {Side} funds for {Quantity} {Symbol} at {Price}.",
                customerUserId, customerSide, quantity, symbol, price);

            return (false, "موجودی یا اعتبار شما برای این معامله کافی نیست.", null);
        }

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
