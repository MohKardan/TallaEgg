using TallaEgg.Core;
using TallaEgg.TelegramBot.Simulator;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The simulator's per-symbol trade sizing (issue #147).
///
/// <para>
/// What these tests are really pinning is that two symbols do <b>not</b> get the same trade size.
/// The simulator traded only MAUA/IRT, whose precision is two decimal places — exactly what the
/// <c>Orders.Amount</c> column held — so 1009 clean simulated trades sat on top of #146, which one
/// manual Bitcoin trade then found in minutes. A shared step, or a shared minimum, would flatten
/// that difference again and the run would be back to proving one symbol works.
/// </para>
///
/// <para>
/// Read from <see cref="CurrenciesConstant"/>'s compiled defaults, never through
/// <c>Configure</c>: that method mutates static state every other test class in this process
/// reads, and xUnit runs classes in parallel.
/// </para>
/// </summary>
public class SymbolPlanTests
{
    private static TradingPairInfo Pair(string symbol) =>
        CurrenciesConstant.GetTradingPairInfo(symbol)
            ?? throw new InvalidOperationException($"{symbol} is not a compiled-in trading pair.");

    /// <summary>Reference prices close to the levels the live feed publishes, so the sizes are the real ones.</summary>
    private static SymbolPlan PlanFor(string symbol) => symbol switch
    {
        CurrenciesConstant.MAUA_IRT => SymbolPlan.For(Pair(symbol), 22_100_000m),
        CurrenciesConstant.SEKE_BAHAR_IRT => SymbolPlan.For(Pair(symbol), 118_900_000m),
        CurrenciesConstant.BTC_IRT => SymbolPlan.For(Pair(symbol), 16_620_000_000m),
        _ => throw new ArgumentOutOfRangeException(nameof(symbol), symbol, "No reference price for this symbol.")
    };

    private static List<decimal> Draw(SymbolPlan plan, int count = 200)
    {
        var random = new Random(1);
        return Enumerable.Range(0, count).Select(_ => plan.RandomQuantity(random)).ToList();
    }

    private static int DecimalPlaces(decimal value) => (decimal.GetBits(value)[3] >> 16) & 0xFF;

    [Fact]
    public void RandomQuantity_Bitcoin_UsesMoreThanTwoDecimalPlaces()
    {
        var quantities = Draw(PlanFor(CurrenciesConstant.BTC_IRT));

        // The whole point of the change: a Bitcoin quantity that survives a decimal(18,2) column
        // unchanged is a Bitcoin quantity that could not have caught #146.
        Assert.Contains(quantities, q => q != decimal.Round(q, 2));
        Assert.All(quantities, q => Assert.Equal(q, decimal.Round(q, 8)));
    }

    [Fact]
    public void RandomQuantity_Gold_StaysAtTheAssetsOwnTwoDecimalPlaces()
    {
        var quantities = Draw(PlanFor(CurrenciesConstant.MAUA_IRT));

        Assert.All(quantities, q => Assert.True(DecimalPlaces(q) <= 2, $"{q} has more than two decimal places."));
    }

    [Theory]
    [InlineData(CurrenciesConstant.MAUA_IRT)]
    [InlineData(CurrenciesConstant.SEKE_BAHAR_IRT)]
    [InlineData(CurrenciesConstant.BTC_IRT)]
    public void RandomQuantity_EverySymbol_RespectsThatSymbolsTradingLimits(string symbol)
    {
        var plan = PlanFor(symbol);

        // OrderService.ValidateTradingLimits refuses any of the three, and a refused order is a
        // trade the run never makes — which would read as a symbol quietly contributing nothing.
        Assert.All(Draw(plan), q =>
        {
            Assert.True(q >= plan.Pair.MinQuantity, $"{q} is below {symbol}'s MinQuantity.");
            Assert.True(q <= plan.Pair.MaxQuantity, $"{q} is above {symbol}'s MaxQuantity.");
            Assert.True(q * plan.ReferenceUnitPrice >= plan.Pair.MinNotional, $"{q} is worth less than {symbol}'s MinNotional.");
        });
    }

    [Fact]
    public void RandomQuantity_BitcoinAndGold_ProduceQuantitiesOrdersOfMagnitudeApart()
    {
        var bitcoin = Draw(PlanFor(CurrenciesConstant.BTC_IRT)).Max();
        var gold = Draw(PlanFor(CurrenciesConstant.MAUA_IRT)).Max();

        Assert.True(gold > bitcoin * 100m,
            $"Gold's largest simulated quantity ({gold}) and Bitcoin's ({bitcoin}) are on the same scale, " +
            "so the run is no longer exercising both.");
    }

    [Fact]
    public void For_PriceTooLowForTheNotionalMinimum_RaisesTheFloorAboveMinQuantity()
    {
        // MinQuantity alone would allow 0.0001 BTC, worth 100,000 toman at this price — under the
        // pair's own 1,000,000 MinNotional, which the order path rejects.
        var plan = SymbolPlan.For(Pair(CurrenciesConstant.BTC_IRT), 1_000_000_000m);

        Assert.True(plan.MinTradeQuantity > Pair(CurrenciesConstant.BTC_IRT).MinQuantity);
        Assert.True(plan.MinTradeQuantity * plan.ReferenceUnitPrice >= Pair(CurrenciesConstant.BTC_IRT).MinNotional);
    }

    [Fact]
    public void For_MinimumTimesTheSpreadAboveMaxQuantity_ClampsToTheConfiguredMaximum()
    {
        var pair = Pair(CurrenciesConstant.SEKE_BAHAR_IRT);

        // A price low enough that thirty times the notional floor would run past MaxQuantity.
        var plan = SymbolPlan.For(pair, pair.MinNotional / (pair.MaxQuantity / 2m));

        Assert.Equal(pair.MaxQuantity, plan.MaxTradeQuantity);
        Assert.True(plan.MaxTradeQuantity >= plan.MinTradeQuantity);
    }

    [Fact]
    public void For_EverySymbolAtItsRealPrice_HasATradableBand()
    {
        Assert.All(
            new[] { CurrenciesConstant.MAUA_IRT, CurrenciesConstant.SEKE_BAHAR_IRT, CurrenciesConstant.BTC_IRT },
            symbol => Assert.True(PlanFor(symbol).HasTradableBand, $"{symbol} has no tradable band."));
    }

    [Fact]
    public void For_MaxQuantityBelowTheNotionalFloor_ReportsNoTradableBand()
    {
        var pair = Pair(CurrenciesConstant.SEKE_BAHAR_IRT);

        // A price so low that even MaxQuantity coins are worth less than MinNotional: no quantity
        // this pair accepts exists. Reported rather than repaired — squeezing a size out of that
        // range produces orders ValidateTradingLimits refuses, one per trade, which reads as a
        // broken simulator rather than a broken symbol.
        var plan = SymbolPlan.For(pair, pair.MinNotional / (pair.MaxQuantity * 2m));

        Assert.False(plan.HasTradableBand);
    }

    [Fact]
    public void QuoteKeyword_SymbolWithAnAlias_IsThatAlias()
    {
        Assert.Equal("سکه", PlanFor(CurrenciesConstant.SEKE_BAHAR_IRT).QuoteKeyword);
        Assert.Equal("بیت", PlanFor(CurrenciesConstant.BTC_IRT).QuoteKeyword);
    }

    [Fact]
    public void QuoteKeyword_TheSymbolAnAbsentKeywordMeans_IsEmptyRatherThanNull()
    {
        // MAUA/IRT has no alias and needs none: the admin's quote command already defaults to it.
        Assert.Equal(string.Empty, PlanFor(CurrenciesConstant.MAUA_IRT).QuoteKeyword);
    }

    [Fact]
    public void QuoteKeyword_PairWithNoAliasAndNotTheDefault_IsNull()
    {
        // A pair added by configuration with no Aliases entry cannot be named in the admin's quote
        // command at all, so the simulator publishes it through the API client instead.
        var orphan = new TradingPairInfo
        {
            Symbol = "ETH/IRT",
            BaseAsset = "ETH",
            QuoteAsset = CurrenciesConstant.Toman,
            MinQuantity = 0.01m,
            MaxQuantity = 100m,
            MinNotional = 1_000_000m,
            BaseDecimalPlaces = 8
        };

        Assert.Null(SymbolPlan.For(orphan, 200_000_000m).QuoteKeyword);
    }

    [Fact]
    public void CreditAsset_EverySymbol_IsThatSymbolsOwnCreditLedger()
    {
        // Credit is per-asset in storage: funding CREDIT_MAUA does not let a customer trade
        // Bitcoin, so a run that funds one ledger has funded one symbol.
        Assert.Equal("CREDIT_MAUA", PlanFor(CurrenciesConstant.MAUA_IRT).CreditAsset);
        Assert.Equal("CREDIT_BTC", PlanFor(CurrenciesConstant.BTC_IRT).CreditAsset);
        Assert.Equal("CREDIT_SEKE_BAHAR", PlanFor(CurrenciesConstant.SEKE_BAHAR_IRT).CreditAsset);
    }
}
