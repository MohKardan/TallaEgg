using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.TelegramBot.Infrastructure.Handlers;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Naming the other side of a trade in the admin's history.
///
/// The admin is one side of <b>every</b> trade in the dealer model, so without the
/// counterparty their history is a page of rows that differ only by amount and time — they
/// cannot tell which customer any of them was with. A customer needs no such row: all their
/// trades are with the shop.
///
/// The value is passed in as a lookup rather than fetched here, so this stays a pure builder
/// (issue #65). These tests therefore also pin the decision that "no lookup supplied" means
/// "do not show the line" — which is what keeps a customer from seeing the shop's number.
/// </summary>
public class TradeListCounterpartyTests
{
    private static readonly Guid Admin = Guid.NewGuid();
    private static readonly Guid CustomerA = Guid.NewGuid();
    private static readonly Guid CustomerB = Guid.NewGuid();

    private const string PhoneA = "09158527483";
    private const string PhoneB = "09037212111";

    private static PagedResult<TradeHistoryDto> Page(params TradeHistoryDto[] trades) => new()
    {
        Items = trades,
        TotalCount = trades.Length,
        PageNumber = 1,
        PageSize = 5
    };

    private static TradeHistoryDto Trade(Guid buyer, Guid seller) => new()
    {
        Id = Guid.NewGuid(),
        Symbol = "MAUA/IRT",
        Price = 16_875_201.99m,
        Quantity = 10m,
        QuoteQuantity = 168_752_019m,
        BuyerUserId = buyer,
        SellerUserId = seller,
        CreatedAt = new DateTime(2026, 7, 30, 11, 34, 0, DateTimeKind.Utc)
    };

    /// <summary>Persian digits, because that is how the number is rendered in the message.</summary>
    private static string Fa(string phone) => TallaEgg.Core.Utilties.PersianFormat.ToPersianDigits(phone);

    // ── shown when supplied ─────────────────────────────────────────────────────

    [Fact]
    public async Task WhenALookupIsSupplied_TheCounterpartyIsNamed()
    {
        var text = await TradeListHandler.BuildTradesListAsync(
            Page(Trade(buyer: Admin, seller: CustomerA)), 1, viewerUserId: Admin,
            counterpartyPhones: new Dictionary<Guid, string> { [CustomerA] = PhoneA });

        Assert.Contains("طرف معامله", text);
        Assert.Contains(Fa(PhoneA), text);
    }

    /// <summary>
    /// The counterparty is derived from which side the viewer is on, not assumed to be the
    /// seller. The admin buys on some trades and sells on others, so an implementation that
    /// always took one field would name the admin themselves on half the rows.
    /// </summary>
    [Fact]
    public async Task TheCounterpartyIsTheSideTheViewerIsNot_WhicheverThatIs()
    {
        var phones = new Dictionary<Guid, string> { [CustomerA] = PhoneA, [CustomerB] = PhoneB };

        var adminBought = await TradeListHandler.BuildTradesListAsync(
            Page(Trade(buyer: Admin, seller: CustomerA)), 1, Admin, phones);

        var adminSold = await TradeListHandler.BuildTradesListAsync(
            Page(Trade(buyer: CustomerB, seller: Admin)), 1, Admin, phones);

        Assert.Contains(Fa(PhoneA), adminBought);
        Assert.DoesNotContain(Fa(PhoneB), adminBought);

        Assert.Contains(Fa(PhoneB), adminSold);
        Assert.DoesNotContain(Fa(PhoneA), adminSold);
    }

    /// <summary>Each row names its own counterparty, not the first one found on the page.</summary>
    [Fact]
    public async Task EachRowNamesItsOwnCounterparty()
    {
        var text = await TradeListHandler.BuildTradesListAsync(
            Page(Trade(buyer: Admin, seller: CustomerA),
                 Trade(buyer: CustomerB, seller: Admin)),
            1, Admin,
            new Dictionary<Guid, string> { [CustomerA] = PhoneA, [CustomerB] = PhoneB });

        Assert.Contains(Fa(PhoneA), text);
        Assert.Contains(Fa(PhoneB), text);
    }

    // ── absent when not supplied ────────────────────────────────────────────────

    /// <summary>
    /// The customer's own history. No lookup is supplied for them, so the line disappears —
    /// they would otherwise be shown the shop's phone number on every row.
    /// </summary>
    [Fact]
    public async Task WithoutALookup_NoCounterpartyLineAppears()
    {
        var text = await TradeListHandler.BuildTradesListAsync(
            Page(Trade(buyer: CustomerA, seller: Admin)), 1, viewerUserId: CustomerA);

        Assert.DoesNotContain("طرف معامله", text);
    }

    /// <summary>
    /// A lookup that came back without this particular user — the Users service was
    /// unreachable for one id — must drop the line, not print an empty label or throw. A
    /// missing phone number costs one line, not the whole list.
    /// </summary>
    [Fact]
    public async Task AnUnresolvedCounterparty_IsOmittedRatherThanShownBlank()
    {
        var text = await TradeListHandler.BuildTradesListAsync(
            Page(Trade(buyer: Admin, seller: CustomerA)), 1, Admin,
            counterpartyPhones: new Dictionary<Guid, string>());

        Assert.DoesNotContain("طرف معامله", text);
    }

    [Fact]
    public async Task ABlankPhoneNumber_IsTreatedAsUnresolved()
    {
        var text = await TradeListHandler.BuildTradesListAsync(
            Page(Trade(buyer: Admin, seller: CustomerA)), 1, Admin,
            counterpartyPhones: new Dictionary<Guid, string> { [CustomerA] = "   " });

        Assert.DoesNotContain("طرف معامله", text);
    }

    /// <summary>
    /// Adding the counterparty must not have disturbed the buy/sell labelling that
    /// <see cref="TradeListSideTests"/> covers — the two live in the same loop.
    /// </summary>
    [Fact]
    public async Task TheSideLabelStillReflectsTheViewer()
    {
        var phones = new Dictionary<Guid, string> { [CustomerA] = PhoneA };

        var text = await TradeListHandler.BuildTradesListAsync(
            Page(Trade(buyer: Admin, seller: CustomerA)), 1, Admin, phones);

        Assert.Contains("🟢 خرید", text);   // the admin was the buyer here
        Assert.Contains("پرداختی", text);
    }
}
