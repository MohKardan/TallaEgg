using TallaEgg.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.TelegramBot.Infrastructure.Handlers;

namespace Wallet.Tests;

/// <summary>
/// The quote history that replaced order history in the accounting menu.
///
/// The risk this covers is the buyer/seller inversion, which has already appeared three
/// times in this product in different disguises: in matching (#40), in the trade history
/// (which did not say buy or sell at all), and in the dealer pricing itself (#48). Here it
/// takes yet another form — one row of data has two correct readings depending on who is
/// looking. The admin publishes the prices, so the buy price is what *they* pay. The
/// customer trades against them, so the same number is what *they* receive.
///
/// Label it for one audience and the other audience reads every price backwards.
/// </summary>
public class QuoteHistoryMessageTests
{
    private const string Gold = CurrenciesConstant.MAUA_IRT;

    /// <summary>17,000,000/gram is 73,640,600/mesghal — recognisably a different magnitude.</summary>
    private const decimal BuyPerGram = 17_000_000m;
    private const decimal SellPerGram = 17_100_000m;

    private static string Fa(decimal value) => TallaEgg.Core.Utilties.PersianFormat.Number(value);

    private static PagedResult<QuoteDto> OnePage(params QuoteDto[] quotes) => new()
    {
        Items = quotes,
        TotalCount = quotes.Length,
        PageNumber = 1,
        PageSize = 5
    };

    private static QuoteDto Quote(bool isActive = true, DateTime? deactivatedAt = null) => new()
    {
        Id = Guid.NewGuid(),
        Symbol = Gold,
        BuyPrice = BuyPerGram,
        SellPrice = SellPerGram,
        PublishedAt = new DateTime(2026, 7, 30, 8, 42, 0, DateTimeKind.Utc),
        IsActive = isActive,
        DeactivatedAt = deactivatedAt
    };

    // ── the two readings of one row ─────────────────────────────────────────────

    /// <summary>
    /// For the admin, the buy price is the price they pay. Saying "you buy" next to the
    /// price the customer buys at would have them setting their margin upside down.
    /// </summary>
    [Fact]
    public void ForAnAdmin_TheBuyPriceIsLabelledAsTheirOwnPurchase()
    {
        var text = QuoteHistoryHandler.BuildQuoteHistoryAsync(OnePage(Quote()), 1, isAdmin: true);

        Assert.Contains("شما می‌خرید", text);
        Assert.Contains("شما می‌فروشید", text);
    }

    /// <summary>
    /// For the customer the same two numbers swap meaning: the admin's buy price is the
    /// price the customer sells at.
    /// </summary>
    [Fact]
    public void ForACustomer_TheAdminsBuyPriceIsTheirSellPrice()
    {
        var text = QuoteHistoryHandler.BuildQuoteHistoryAsync(OnePage(Quote()), 1, isAdmin: false);

        Assert.Contains("قیمت فروش شما", text);
        Assert.Contains("قیمت خرید شما", text);
        Assert.DoesNotContain("شما می‌خرید", text);
    }

    /// <summary>
    /// The decisive assertion: the two audiences must not be shown the same wording for the
    /// same row, because the correct wording is opposite for each.
    /// </summary>
    [Fact]
    public void TheAdminAndCustomerViewsAreNotIdentical()
    {
        var page = OnePage(Quote());

        var adminText = QuoteHistoryHandler.BuildQuoteHistoryAsync(page, 1, isAdmin: true);
        var customerText = QuoteHistoryHandler.BuildQuoteHistoryAsync(page, 1, isAdmin: false);

        Assert.NotEqual(adminText, customerText);
    }

    /// <summary>Margin is the admin's own number to decide; a customer has no business seeing it.</summary>
    [Fact]
    public void TheMarginIsShownOnlyToTheAdmin()
    {
        var page = OnePage(Quote());

        Assert.Contains("حاشیه", QuoteHistoryHandler.BuildQuoteHistoryAsync(page, 1, isAdmin: true));
        Assert.DoesNotContain("حاشیه", QuoteHistoryHandler.BuildQuoteHistoryAsync(page, 1, isAdmin: false));
    }

    // ── units ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Prices are stored per gram and read per mesghal. Showing the stored figure would
    /// understate every historical price by a factor of 4.3318 — and each row would still
    /// look like a plausible gold price.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void PricesAreConvertedToPerMesghal(bool isAdmin)
    {
        var text = QuoteHistoryHandler.BuildQuoteHistoryAsync(OnePage(Quote()), 1, isAdmin);

        Assert.Contains("هر مثقال", text);
        Assert.Contains(Fa(BuyPerGram * CurrenciesConstant.GramsPerMesghal), text);
        Assert.Contains(Fa(SellPerGram * CurrenciesConstant.GramsPerMesghal), text);
        Assert.DoesNotContain(Fa(BuyPerGram), text);
    }

    /// <summary>The admin's margin must be in the same unit as the prices printed beside it.</summary>
    [Fact]
    public void TheMarginIsQuotedPerMesghal_MatchingThePricesAboveIt()
    {
        var text = QuoteHistoryHandler.BuildQuoteHistoryAsync(OnePage(Quote()), 1, isAdmin: true);

        var expected = (SellPerGram - BuyPerGram) * CurrenciesConstant.GramsPerMesghal;

        Assert.Contains(Fa(expected), text);
        Assert.DoesNotContain(Fa(SellPerGram - BuyPerGram), text);
    }

    // ── which row is live ───────────────────────────────────────────────────────

    /// <summary>
    /// Superseded quotes are kept rather than deleted so it stays possible to see which price
    /// was in force for any trade. That only helps if the list says which row is still live —
    /// otherwise the reader assumes it is the top one, which is true only until it isn't.
    /// </summary>
    [Fact]
    public void TheActiveQuoteIsDistinguishedFromReplacedOnes()
    {
        var replaced = Quote(isActive: false, deactivatedAt: new DateTime(2026, 7, 30, 11, 34, 0, DateTimeKind.Utc));

        var activeText = QuoteHistoryHandler.BuildQuoteHistoryAsync(OnePage(Quote()), 1, isAdmin: false);
        var replacedText = QuoteHistoryHandler.BuildQuoteHistoryAsync(OnePage(replaced), 1, isAdmin: false);

        Assert.Contains("مظنهٔ فعال", activeText);
        Assert.DoesNotContain("مظنهٔ فعال", replacedText);
        Assert.Contains("منقضی شد", replacedText);
    }

    [Fact]
    public void AReplacedQuoteShowsWhenItEnded_AndAnActiveOneDoesNot()
    {
        var replaced = Quote(isActive: false, deactivatedAt: new DateTime(2026, 7, 30, 11, 34, 0, DateTimeKind.Utc));

        Assert.Contains("پایان", QuoteHistoryHandler.BuildQuoteHistoryAsync(OnePage(replaced), 1, isAdmin: false));
        Assert.DoesNotContain("پایان", QuoteHistoryHandler.BuildQuoteHistoryAsync(OnePage(Quote()), 1, isAdmin: false));
    }

    // ── edges ───────────────────────────────────────────────────────────────────

    [Fact]
    public void AnEmptyHistoryReadsAsEmpty_RatherThanAsAnErrorOrABlankMessage()
    {
        var empty = new PagedResult<QuoteDto> { Items = new List<QuoteDto>(), TotalCount = 0, PageNumber = 1, PageSize = 5 };

        var text = QuoteHistoryHandler.BuildQuoteHistoryAsync(empty, 1, isAdmin: false);

        Assert.Contains("هنوز مظنه‌ای منتشر نشده", text);
    }

    [Fact]
    public void NoUnfilledPlaceholderSurvives()
    {
        var text = QuoteHistoryHandler.BuildQuoteHistoryAsync(OnePage(Quote(), Quote(isActive: false)), 1, isAdmin: true);

        Assert.DoesNotContain("{", text);
    }

    // ── paging ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// The symbol travels in the callback data and contains a '/'. The page number is split
    /// off at the *last* underscore for that reason; splitting on the first would work only
    /// until a symbol contained one.
    /// </summary>
    [Fact]
    public void ThePagingCallbackCarriesTheSymbolAndTheTargetPage()
    {
        var page = new PagedResult<QuoteDto> { Items = new[] { Quote() }, TotalCount = 12, PageNumber = 2, PageSize = 5 };

        var keyboard = QuoteHistoryHandler.BuildPagingKeyboard(page, currentPage: 2, symbol: Gold);

        Assert.NotNull(keyboard);
        var data = keyboard!.InlineKeyboard.SelectMany(row => row).Select(b => b.CallbackData!).ToArray();

        Assert.Contains($"{QuoteHistoryHandler.CallbackPrefix}{Gold}_1", data);
        Assert.Contains($"{QuoteHistoryHandler.CallbackPrefix}{Gold}_3", data);

        // Telegram rejects callback data over 64 bytes, and rejection happens at send time.
        Assert.All(data, d => Assert.True(
            System.Text.Encoding.UTF8.GetByteCount(d) <= 64, $"callback data too long: {d}"));
    }

    [Fact]
    public void ASinglePageHasNoNavigationButtons()
    {
        var page = new PagedResult<QuoteDto> { Items = new[] { Quote() }, TotalCount = 1, PageNumber = 1, PageSize = 5 };

        Assert.Null(QuoteHistoryHandler.BuildPagingKeyboard(page, 1, Gold));
    }
}
