using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Messages;

namespace TallaEgg.AllServices.Tests;

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

    // ── per-symbol grouping (issue #93 part A) ──────────────────────────────────

    /// <summary>
    /// Toman is spending cash, not a traded position — it belongs after the symbols a
    /// customer actually holds, not interleaved with them by wallet-record order.
    /// </summary>
    [Fact]
    public void TomanIsShownLast_AfterEveryTradedSymbol()
    {
        var text = WalletBalanceMessage.Build([
            Wallet(CurrenciesConstant.Toman, 1_000_000m),
            Wallet(CurrenciesConstant.Btc, 0.01m),
            Wallet(CurrenciesConstant.Maua, 10m)
        ]);

        var tomanIndex = text.IndexOf(PersianFormat.Asset(CurrenciesConstant.Toman), StringComparison.Ordinal);
        var btcIndex = text.IndexOf(PersianFormat.Asset(CurrenciesConstant.Btc), StringComparison.Ordinal);
        var mauaIndex = text.IndexOf(PersianFormat.Asset(CurrenciesConstant.Maua), StringComparison.Ordinal);

        Assert.True(tomanIndex > btcIndex && tomanIndex > mauaIndex,
            "Toman must come after every traded symbol, not among them");
    }

    /// <summary>
    /// A symbol's credit ceiling is shown as a line under that symbol's own section, not as
    /// an unrelated-looking row for "اعتبار آبشده" — a customer reading the wallet screen
    /// should see gold's credit next to gold, not somewhere else in the list.
    /// </summary>
    [Fact]
    public void ASymbolsCredit_IsShownUnderThatSymbolNotAsItsOwnRow()
    {
        var text = WalletBalanceMessage.Build([
            Wallet(CurrenciesConstant.Maua, 10m),
            Wallet(CurrenciesConstant.CreditAssetFor(CurrenciesConstant.Maua), 240m)
        ]);

        var mauaSection = text[text.IndexOf(PersianFormat.Asset(CurrenciesConstant.Maua), StringComparison.Ordinal)..];
        Assert.Contains(PersianFormat.Amount(240m, CurrenciesConstant.Maua), mauaSection);

        // "اعتبار آبشده" (the credit currency's own Persian name) must never appear as its
        // own bullet — only merged into gold's section via the "اعتبار:" sub-line.
        Assert.DoesNotContain(PersianFormat.Asset(CurrenciesConstant.CreditAssetFor(CurrenciesConstant.Maua)), text);
    }

    /// <summary>
    /// A customer can have credit against an asset they have never actually held or traded
    /// (an admin extended it in advance). Dropping the credit line because there is no base
    /// wallet yet would hide real spending power from the one person it matters most to.
    /// </summary>
    [Fact]
    public void CreditWithNoUnderlyingWallet_IsStillShown()
    {
        var text = WalletBalanceMessage.Build([
            Wallet(CurrenciesConstant.CreditAssetFor(CurrenciesConstant.Btc), 0.5m)
        ]);

        Assert.Contains(PersianFormat.Asset(CurrenciesConstant.Btc), text);
        Assert.Contains(PersianFormat.Amount(0.5m, CurrenciesConstant.Btc), text);
        Assert.Contains(PersianFormat.Amount(0m, CurrenciesConstant.Btc), text); // the synthesised zero balance
    }

    /// <summary>Zero credit is not worth a line — every wallet has a CREDIT_ record once touched once.</summary>
    [Fact]
    public void ZeroCredit_ShowsNoCreditLine()
    {
        var text = WalletBalanceMessage.Build([
            Wallet(CurrenciesConstant.Maua, 10m),
            Wallet(CurrenciesConstant.CreditAssetFor(CurrenciesConstant.Maua), 0m)
        ]);

        Assert.DoesNotContain("اعتبار:", text);
    }

    // ── profit and loss (issue #93 part B) ──────────────────────────────────────

    private static PositionDto Position(
        string symbol, decimal quantity, decimal? averageCost, decimal? markPrice,
        decimal realizedPnl, decimal? unrealizedPnl) => new()
    {
        Symbol = symbol,
        Quantity = quantity,
        AverageCost = averageCost,
        MarkPrice = markPrice,
        RealizedPnl = realizedPnl,
        UnrealizedPnl = unrealizedPnl
    };

    private static PositionsResponseDto Positions(params PositionDto[] positions) => new()
    {
        Positions = positions.ToList(),
        TotalRealizedPnl = positions.Sum(p => p.RealizedPnl),
        TotalUnrealizedPnl = positions.Sum(p => p.UnrealizedPnl ?? 0m)
    };

    /// <summary>No positions argument at all -- the positions API call failed or was skipped. The balance screen from part A must still render exactly as before.</summary>
    [Fact]
    public void WithNoPositionsArgument_TheScreenIsUnchangedFromPartA()
    {
        var text = WalletBalanceMessage.Build([Wallet(CurrenciesConstant.Maua, 10m)]);

        Assert.DoesNotContain("سود", text);
        Assert.DoesNotContain("زیان", text);
    }

    /// <summary>A symbol the customer holds a wallet for but has no position data on (e.g. only ever received it via a direct credit) gets no P&amp;L lines.</summary>
    [Fact]
    public void ASymbolWithNoPositionData_ShowsNoPnlLines()
    {
        var text = WalletBalanceMessage.Build(
            [Wallet(CurrenciesConstant.Maua, 10m)],
            Positions()); // no entries at all

        Assert.DoesNotContain("میانگین قیمت خرید", text);
        Assert.DoesNotContain("ارزش فعلی", text);
    }

    [Fact]
    public void AnOpenLongPositionInProfit_ShowsAverageCostCurrentValueAndAGain()
    {
        var text = WalletBalanceMessage.Build(
            [Wallet(CurrenciesConstant.Btc, 2m)],
            Positions(Position(CurrenciesConstant.BTC_IRT, quantity: 2m, averageCost: 1_000_000m,
                markPrice: 1_200_000m, realizedPnl: 0m, unrealizedPnl: 400_000m)));

        Assert.Contains(PersianFormat.Amount(1_000_000m, CurrenciesConstant.Toman), text); // average cost
        Assert.Contains(PersianFormat.Amount(2_400_000m, CurrenciesConstant.Toman), text); // current value = qty * mark
        Assert.Contains("📈 سود تحقق‌نیافته", text);
        Assert.Contains(PersianFormat.Amount(400_000m, CurrenciesConstant.Toman), text);
        Assert.DoesNotContain("📉", text);
    }

    [Fact]
    public void AnOpenPositionAtALoss_IsLabelledAsALossNotANegativeGain()
    {
        var text = WalletBalanceMessage.Build(
            [Wallet(CurrenciesConstant.Btc, 2m)],
            Positions(Position(CurrenciesConstant.BTC_IRT, quantity: 2m, averageCost: 1_000_000m,
                markPrice: 800_000m, realizedPnl: 0m, unrealizedPnl: -400_000m)));

        var lossLine = text.Split('\n').Single(l => l.Contains("زیان تحقق‌نیافته"));
        Assert.Contains(PersianFormat.Amount(400_000m, CurrenciesConstant.Toman), lossLine); // shown positive, under the loss label
        Assert.DoesNotContain("-", lossLine);
        Assert.DoesNotContain("📈 سود تحقق‌نیافته", text);
    }

    /// <summary>Gold's average cost is stored per gram internally but must display per mesghal, matching every other price the bot shows (BestPricesMessage, order confirmations, ...).</summary>
    [Fact]
    public void ForGold_AverageCostIsConvertedToPerMesghal()
    {
        var text = WalletBalanceMessage.Build(
            [Wallet(CurrenciesConstant.Maua, 5m)],
            Positions(Position(CurrenciesConstant.MAUA_IRT, quantity: 5m, averageCost: 1_000_000m,
                markPrice: null, realizedPnl: 0m, unrealizedPnl: null)));

        Assert.Contains(PersianFormat.Amount(1_000_000m * CurrenciesConstant.GramsPerMesghal, CurrenciesConstant.Toman), text);
    }

    /// <summary>An open position with no published quote can't be valued -- must say so, not silently omit the line or show a stale/zero number.</summary>
    [Fact]
    public void AnOpenPositionWithNoQuote_SaysSoInsteadOfShowingNothing()
    {
        var text = WalletBalanceMessage.Build(
            [Wallet(CurrenciesConstant.Btc, 2m)],
            Positions(Position(CurrenciesConstant.BTC_IRT, quantity: 2m, averageCost: 1_000_000m,
                markPrice: null, realizedPnl: 0m, unrealizedPnl: null)));

        Assert.Contains("قیمتی برای این نماد منتشر نشده", text);
        Assert.DoesNotContain("ارزش فعلی", text);
    }

    /// <summary>A flat position (fully closed) shows realized P&amp;L but no average-cost/current-value lines -- there is nothing open to value.</summary>
    [Fact]
    public void AFlatPositionWithRealizedGain_ShowsOnlyTheRealizedLine()
    {
        var text = WalletBalanceMessage.Build(
            [Wallet(CurrenciesConstant.Btc, 0m)],
            Positions(Position(CurrenciesConstant.BTC_IRT, quantity: 0m, averageCost: null,
                markPrice: 1_200_000m, realizedPnl: 300_000m, unrealizedPnl: 0m)));

        Assert.Contains("✅ سود تحقق‌یافته", text);
        Assert.Contains(PersianFormat.Amount(300_000m, CurrenciesConstant.Toman), text);
        Assert.DoesNotContain("میانگین قیمت خرید", text);
        Assert.DoesNotContain("ارزش فعلی", text);
    }

    [Fact]
    public void ARealizedLossIsLabelledAsALoss()
    {
        var text = WalletBalanceMessage.Build(
            [Wallet(CurrenciesConstant.Btc, 0m)],
            Positions(Position(CurrenciesConstant.BTC_IRT, quantity: 0m, averageCost: null,
                markPrice: null, realizedPnl: -150_000m, unrealizedPnl: 0m)));

        Assert.Contains("❌ زیان تحقق‌یافته", text);
        Assert.Contains(PersianFormat.Amount(150_000m, CurrenciesConstant.Toman), text);
    }

    [Fact]
    public void TheTotalAcrossAllSymbols_IsShownAtTheBottom()
    {
        var text = WalletBalanceMessage.Build(
            [Wallet(CurrenciesConstant.Btc, 0m), Wallet(CurrenciesConstant.Maua, 5m)],
            new PositionsResponseDto
            {
                Positions =
                [
                    Position(CurrenciesConstant.BTC_IRT, 0m, null, null, 300_000m, 0m),
                    Position(CurrenciesConstant.MAUA_IRT, 5m, 100m, 120m, 0m, 100m)
                ],
                TotalRealizedPnl = 300_000m,
                TotalUnrealizedPnl = 100m
            });

        Assert.Contains("مجموع سود و زیان", text);
        Assert.Contains(PersianFormat.Amount(300_100m, CurrenciesConstant.Toman), text);
        Assert.Contains("📈", text);
    }

    [Fact]
    public void ZeroTotalPnl_IsReportedAsNoChangeRatherThanAsAGainOrLoss()
    {
        var text = WalletBalanceMessage.Build(
            [Wallet(CurrenciesConstant.Btc, 0m)],
            Positions(Position(CurrenciesConstant.BTC_IRT, 0m, null, null, 0m, 0m)));

        Assert.Contains("بدون تغییر", text);
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
