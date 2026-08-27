using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.TelegramBot.Infrastructure.Handlers;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The trade list must say whether each trade was a buy or a sell from the viewer's point of view.
///
/// It used not to: the user saw only a quantity and an amount, with no way to tell whether they had
/// bought or sold. This is not a property of the trade — one trade is a buy to one party and a sell
/// to the other — so the viewer's id has to be passed to the message builder.
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
    /// The same trade must render oppositely for the two parties. This is the test that catches a
    /// hard-coded label — the two above would pass against a constant if their data did not differ.
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
    /// The direction of the money must be explicit too: a bare total did not say whether the amount
    /// was paid or received.
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

    /// <summary>The displayed date must be the correct Jalali one, not the Gregorian day.</summary>
    [Fact]
    public async Task TheDateIsCorrectPersianDate()
    {
        var text = await TradeListHandler.BuildTradesListAsync(
            PageWith(Trade(buyer: Customer, seller: MarketMaker)), 1, Customer);

        // 27 July 2026 at 09:52 UTC = 5 Mordad 1405 at 13:22 Tehran time.
        Assert.Contains("۱۴۰۵/۰۵/۰۵", text);
        Assert.Contains("۱۳:۲۲", text);
    }
}
