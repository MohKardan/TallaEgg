using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.TelegramBot.Infrastructure.Handlers;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// فهرست معاملات باید بگوید هر معامله از دید بیننده خرید بوده یا فروش.
///
/// پیش‌تر نمی‌گفت: کاربر فقط مقدار و مبلغ را می‌دید و هیچ راهی نداشت بفهمد طلا خریده
/// یا فروخته. این یک ویژگی خودِ معامله نیست — یک معاملهٔ واحد برای یک طرف خرید است و
/// برای طرف مقابل فروش — پس شناسهٔ بیننده باید به تابع ساخت متن داده شود.
/// </summary>
public class TradeListSideTests
{
    private static readonly Guid Customer = Guid.NewGuid();
    private static readonly Guid MarketMaker = Guid.NewGuid();

    private static PagedResult<TradeHistoryDto> PageWith(TradeHistoryDto trade) => new()
    {
        Items = new List<TradeHistoryDto> { trade },
        TotalCount = 1,
        PageNumber = 1,
        PageSize = 5
    };

    private static TradeHistoryDto Trade(Guid buyer, Guid seller) => new()
    {
        Id = Guid.NewGuid(),
        Symbol = "MAUA/IRT",
        Price = 18_468_073.32m,
        Quantity = 55m,
        QuoteQuantity = 1_015_744_033m,
        BuyerUserId = buyer,
        SellerUserId = seller,
        CreatedAt = new DateTime(2026, 7, 27, 9, 52, 0, DateTimeKind.Utc)
    };

    [Fact]
    public async Task WhenViewerIsTheBuyer_ItIsShownAsABuy()
    {
        var page = PageWith(Trade(buyer: Customer, seller: MarketMaker));

        var text = await TradeListHandler.BuildTradesListAsync(page, 1, Customer);

        Assert.Contains("خرید", text);
        Assert.DoesNotContain("فروش", text);
    }

    [Fact]
    public async Task WhenViewerIsTheSeller_ItIsShownAsASell()
    {
        var page = PageWith(Trade(buyer: MarketMaker, seller: Customer));

        var text = await TradeListHandler.BuildTradesListAsync(page, 1, Customer);

        Assert.Contains("فروش", text);
        Assert.DoesNotContain("خرید", text);
    }

    /// <summary>
    /// همان معاملهٔ واحد باید برای دو طرف برعکس هم نمایش داده شود. این تستی است که یک
    /// برچسب هاردکدشده را می‌گیرد — دو تست بالا به‌تنهایی با یک مقدار ثابت هم سبز
    /// می‌شدند اگر داده‌هایشان متفاوت نبود.
    /// </summary>
    [Fact]
    public async Task TheSameTrade_AppearsAsBuyToOnePartyAndSellToTheOther()
    {
        var trade = Trade(buyer: Customer, seller: MarketMaker);

        var asBuyer = await TradeListHandler.BuildTradesListAsync(PageWith(trade), 1, Customer);
        var asSeller = await TradeListHandler.BuildTradesListAsync(PageWith(trade), 1, MarketMaker);

        Assert.Contains("خرید", asBuyer);
        Assert.Contains("فروش", asSeller);
    }

    /// <summary>
    /// جهت پول هم باید صریح باشد: «ارزش کل» نمی‌گفت این مبلغ پرداخت شده یا دریافت.
    /// </summary>
    [Fact]
    public async Task TheMoneyDirectionIsLabelled()
    {
        var buyerView = await TradeListHandler.BuildTradesListAsync(
            PageWith(Trade(buyer: Customer, seller: MarketMaker)), 1, Customer);
        var sellerView = await TradeListHandler.BuildTradesListAsync(
            PageWith(Trade(buyer: MarketMaker, seller: Customer)), 1, Customer);

        Assert.Contains("پرداختی", buyerView);
        Assert.Contains("دریافتی", sellerView);
    }

    /// <summary>تاریخ نمایش‌داده‌شده باید شمسیِ درست باشد و نه روزِ میلادی.</summary>
    [Fact]
    public async Task TheDateIsCorrectPersianDate()
    {
        var text = await TradeListHandler.BuildTradesListAsync(
            PageWith(Trade(buyer: Customer, seller: MarketMaker)), 1, Customer);

        // ۲۷ ژوئیهٔ ۲۰۲۶ ساعت ۰۹:۵۲ UTC  =  ۵ مرداد ۱۴۰۵ ساعت ۱۳:۲۲ به وقت تهران
        Assert.Contains("۱۴۰۵/۰۵/۰۵", text);
        Assert.Contains("۱۳:۲۲", text);
    }
}
