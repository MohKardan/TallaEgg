using TallaEgg.Core;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Messages;

namespace Wallet.Tests;

/// <summary>
/// The two remaining messages with a branch in them (issue #65): the balance screen, which
/// changes shape when the customer is in debt, and the market prices, which change shape
/// when one side of the book is empty.
///
/// The other <c>string.Format</c> calls left in the handlers substitute a single error
/// reason into a fixed sentence. There is no arithmetic, no unit, and no branch in those,
/// so a builder for each would add indirection without pinning anything down.
/// </summary>
public class BalanceAndPriceMessageTests
{
    private static WalletDTO Wallet(string asset, decimal balance, decimal locked = 0m) =>
        new() { Asset = asset, Balance = balance, LockedBalance = locked };

    // ── the balance screen ──────────────────────────────────────────────────────

    /// <summary>
    /// A negative available balance is normal under the credit model, but a bare minus sign
    /// reads as a malfunction. The debt is shown as a positive number under its own label —
    /// so the sign must be flipped exactly once.
    /// </summary>
    [Fact]
    public void ANegativeBalance_IsShownAsAPositiveDebt()
    {
        var text = WalletBalanceMessage.Build([Wallet(CurrenciesConstant.Toman, -5_000_000m)]);

        // The debt line specifically — the available-balance row above it still carries the
        // negative figure, which is correct: that is the raw ledger value.
        var debtLine = text.Split('\n').Single(l => l.Contains("بدهی شما"));

        Assert.Contains(PersianFormat.Amount(5_000_000m, CurrenciesConstant.Toman), debtLine);
        Assert.DoesNotContain("-", debtLine);
        Assert.DoesNotContain("−", debtLine); // the Unicode minus, in case a formatter emits it
    }

    /// <summary>A customer in credit must not be told they owe anything.</summary>
    [Fact]
    public void APositiveBalance_ShowsNoDebtLine()
    {
        var text = WalletBalanceMessage.Build([Wallet(CurrenciesConstant.Toman, 5_000_000m)]);

        Assert.DoesNotContain("بدهی", text);
    }

    /// <summary>
    /// Zero is not debt. An off-by-one in the comparison would tell every customer with an
    /// empty wallet that they owe nothing — as a warning.
    /// </summary>
    [Fact]
    public void AZeroBalance_ShowsNoDebtLine()
    {
        var text = WalletBalanceMessage.Build([Wallet(CurrenciesConstant.Toman, 0m)]);

        Assert.DoesNotContain("بدهی", text);
    }

    /// <summary>
    /// Locked collateral is money the customer still owns but cannot spend. Folding it into
    /// the available balance would overstate what they can trade with.
    /// </summary>
    [Fact]
    public void LockedCollateral_IsReportedSeparatelyFromTheAvailableBalance()
    {
        var text = WalletBalanceMessage.Build([Wallet(CurrenciesConstant.Maua, 10m, locked: 2.5m)]);

        Assert.Contains("موجودی آزاد", text);
        Assert.Contains("درگیر در سفارش", text);
        Assert.Contains(PersianFormat.Amount(10m, CurrenciesConstant.Maua), text);
        Assert.Contains(PersianFormat.Amount(2.5m, CurrenciesConstant.Maua), text);
    }

    /// <summary>Assets are named in Persian; the latin code is an internal detail.</summary>
    [Fact]
    public void AssetsAreNamedInPersian_NotByTheirLatinCode()
    {
        var text = WalletBalanceMessage.Build([Wallet(CurrenciesConstant.Maua, 10m)]);

        Assert.DoesNotContain("MAUA", text);
        Assert.Contains(PersianFormat.Asset(CurrenciesConstant.Maua), text);
    }

    [Fact]
    public void EveryWalletGetsARow_AndNoPlaceholderSurvives()
    {
        var text = WalletBalanceMessage.Build([
            Wallet(CurrenciesConstant.Maua, 10m),
            Wallet(CurrenciesConstant.Toman, -5_000_000m)
        ]);

        Assert.Contains(PersianFormat.Asset(CurrenciesConstant.Maua), text);
        Assert.Contains(PersianFormat.Asset(CurrenciesConstant.Toman), text);
        Assert.DoesNotContain("{", text);
    }

    // ── best market prices ──────────────────────────────────────────────────────

    /// <summary>
    /// An empty side of the book has no price. Rendering null as a number gives "۰ تومان",
    /// which says the price is zero rather than that there is no price — a customer seeing
    /// gold bid at zero would read the market as collapsed.
    /// </summary>
    [Fact]
    public void AMissingPrice_SaysSoInsteadOfShowingZero()
    {
        var text = BestPricesMessage.Build(bestBidPrice: null, bestAskPrice: 80_000_000m, CurrenciesConstant.BTC_IRT);

        // Only the missing side is affected; the present one still shows its number.
        var buyLine = text.Split('\n').Single(l => l.Contains("خرید"));
        var sellLine = text.Split('\n').Single(l => l.Contains("فروش"));

        Assert.Contains(BotMsgs.MsgPriceNotAvailable, buyLine);
        Assert.DoesNotContain("تومان", buyLine);
        Assert.Contains(PersianFormat.Number(80_000_000m), sellLine);
    }

    [Fact]
    public void BothSidesMissing_ProducesNoNumbersAtAll()
    {
        var text = BestPricesMessage.Build(null, null, CurrenciesConstant.BTC_IRT);

        Assert.Equal(2, text.Split(BotMsgs.MsgPriceNotAvailable).Length - 1);
    }

    [Fact]
    public void PresentPricesAreShownWithTheirUnit()
    {
        var text = BestPricesMessage.Build(79_000_000m, 80_000_000m, CurrenciesConstant.BTC_IRT);

        Assert.Contains(PersianFormat.Number(79_000_000m), text);
        Assert.Contains(PersianFormat.Number(80_000_000m), text);
        Assert.DoesNotContain("{", text);
    }

    /// <summary>
    /// Bid before ask, matching the labels. Swapping them would show the customer they can
    /// sell above the buy price — an arbitrage that does not exist.
    /// </summary>
    [Fact]
    public void TheBidIsShownOnTheBuyLineAndTheAskOnTheSellLine()
    {
        var text = BestPricesMessage.Build(bestBidPrice: 79_000_000m, bestAskPrice: 80_000_000m, CurrenciesConstant.BTC_IRT);

        var buyLine = text.Split('\n').Single(l => l.Contains("خرید"));
        var sellLine = text.Split('\n').Single(l => l.Contains("فروش"));

        Assert.Contains(PersianFormat.Number(79_000_000m), buyLine);
        Assert.Contains(PersianFormat.Number(80_000_000m), sellLine);
    }

    /// <summary>
    /// Gold is quoted internally per gram but shown per mesghal everywhere else in the bot
    /// (order confirmations, trade history, quote history) — this message must not be the
    /// one place a customer sees the raw per-gram figure under a «مثقال» label.
    /// </summary>
    [Fact]
    public void ForGold_ThePriceIsConvertedToPerMesghalAndLabelledAsSuch()
    {
        var text = BestPricesMessage.Build(79_000_000m, 80_000_000m, CurrenciesConstant.MAUA_IRT);

        Assert.Contains("هر مثقال", text);
        Assert.Contains(PersianFormat.Number(79_000_000m * CurrenciesConstant.GramsPerMesghal), text);
        Assert.Contains(PersianFormat.Number(80_000_000m * CurrenciesConstant.GramsPerMesghal), text);
    }

    /// <summary>
    /// Hit live: every non-gold symbol showed «هر مثقال» and a price multiplied by 4.3318
    /// regardless of what it actually traded in, because the conversion used to be applied
    /// unconditionally in the bot handler rather than gated on the symbol. Bitcoin isn't
    /// bought or sold by the gram at all, so both the label and the number were wrong.
    /// </summary>
    [Fact]
    public void ForBitcoin_ThePriceIsShownAsIsUnderItsOwnUnit()
    {
        var text = BestPricesMessage.Build(79_000_000m, 80_000_000m, CurrenciesConstant.BTC_IRT);

        Assert.Contains("هر بیت‌کوین", text);
        Assert.DoesNotContain("هر مثقال", text);
        Assert.Contains(PersianFormat.Number(79_000_000m), text);
        Assert.Contains(PersianFormat.Number(80_000_000m), text);
    }

    [Fact]
    public void ForTheCoin_ThePriceIsShownAsIsUnderItsOwnUnit()
    {
        var text = BestPricesMessage.Build(79_000_000m, 80_000_000m, CurrenciesConstant.SEKE_BAHAR_IRT);

        Assert.Contains("هر سکه", text);
        Assert.Contains(PersianFormat.Number(79_000_000m), text);
        Assert.Contains(PersianFormat.Number(80_000_000m), text);
    }
}
