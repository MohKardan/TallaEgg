using Microsoft.Extensions.Configuration;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// XAU/USD is the first symbol this platform quotes in something other than Toman (issue #304),
/// and these are the facts that had to become true for it to be tradable at all.
///
/// <para>
/// The gap was narrow and total: every part of the order path was already written against
/// <c>pair.QuoteAsset</c>, but <c>BuildCurrencies</c> registered only base assets and Toman, so
/// <c>USD</c> was not a currency. <c>IsValidCurrency</c> answered false, which meant an admin
/// could not credit a customer's dollars and <c>WalletRepository.SettleTradeAsync</c> — which
/// checks both sides of the symbol before it moves anything — refused to settle the trade.
/// </para>
/// </summary>
public class XauUsdSymbolTests
{
    /// <summary>
    /// The invariant behind the whole change, stated over every symbol rather than over USD, so
    /// that a future non-Toman pair added by configuration alone cannot reintroduce the gap.
    /// </summary>
    [Fact]
    public void EveryTradingPairsQuoteAsset_IsARegisteredCurrency()
    {
        foreach (var pair in CurrenciesConstant.AllTradingPairs)
        {
            Assert.True(CurrenciesConstant.IsValidCurrency(pair.QuoteAsset),
                $"{pair.Symbol} is quoted in {pair.QuoteAsset}, which is not a currency — the wallet would refuse to settle it.");
        }
    }

    [Fact]
    public void TheDollar_IsACurrencyThatIsHeldRatherThanTraded()
    {
        var usd = CurrenciesConstant.GetCurrencyInfo(CurrenciesConstant.Usd);

        Assert.NotNull(usd);
        Assert.False(usd!.IsTradable);
        Assert.Equal("دلار", usd.PersianName);
        // Two decimals, not Toman's zero: rounding a dollar amount to whole units would lose
        // cents on every settlement.
        Assert.Equal(2, usd.DecimalPlaces);
    }

    /// <summary>
    /// The owner's decision on #304: the ounce gets a credit ledger like every other tradable
    /// asset, and the dollar does not — exactly as Toman, a quote currency, never had one. A
    /// quote-side credit ledger is the open half of #36, and minting one here would answer that
    /// question by accident.
    /// </summary>
    [Fact]
    public void TheOunceHasACreditLedgerAndTheDollarDoesNot()
    {
        Assert.True(CurrenciesConstant.HasCreditLedger(CurrenciesConstant.Xau));
        Assert.False(CurrenciesConstant.HasCreditLedger(CurrenciesConstant.Usd));
        Assert.False(CurrenciesConstant.IsValidCurrency("CREDIT_USD"));
    }

    /// <summary>
    /// Toman is registered structurally, before any pair is read. Three pairs now name it as
    /// their quote asset, and none of them may redefine it — a pair carrying no quote metadata
    /// would otherwise blank its name and unit.
    /// </summary>
    [Fact]
    public void TomanKeepsItsOwnDefinition_ThoughThreePairsQuoteIt()
    {
        var toman = CurrenciesConstant.GetCurrencyInfo(CurrenciesConstant.Toman);

        Assert.NotNull(toman);
        Assert.Equal("تومان", toman!.PersianName);
        Assert.Equal("تومان", toman.Unit);
        Assert.Equal(0, toman.DecimalPlaces);
        Assert.False(toman.IsTradable);
    }

    [Fact]
    public void TheOunce_IsTradableAndPricedPerTroyOunce()
    {
        var xau = CurrenciesConstant.GetCurrencyInfo(CurrenciesConstant.Xau);
        var pair = CurrenciesConstant.GetTradingPairInfo(CurrenciesConstant.XAU_USD);

        Assert.NotNull(xau);
        Assert.True(xau!.IsTradable);
        Assert.Equal("انس", xau.Unit);

        Assert.NotNull(pair);
        Assert.Equal(CurrenciesConstant.Xau, pair!.BaseAsset);
        Assert.Equal(CurrenciesConstant.Usd, pair.QuoteAsset);
        Assert.Equal(0.01m, pair.MinQuantity);
        Assert.Equal(5m, pair.MaxQuantity);
        Assert.Equal(2, pair.PriceDecimalPlaces);
    }

    /// <summary>
    /// A dollar price rounds to cents. The order path stores prices at two decimals anyway
    /// (<c>RoundOrderPrice</c>), but amounts go through the asset's own precision, and USD is the
    /// first asset on the quote side with any decimals at all.
    /// </summary>
    [Fact]
    public void ADollarAmount_RoundsToCents()
    {
        Assert.Equal(4318.54m, CurrenciesConstant.RoundToCurrencyPrecision(4318.5449m, CurrenciesConstant.Usd));
        // Away from zero, not to even: a half-cent goes up, as it does everywhere else here.
        Assert.Equal(4318.55m, CurrenciesConstant.RoundToCurrencyPrecision(4318.545m, CurrenciesConstant.Usd));
        Assert.Equal(4318m, CurrenciesConstant.RoundToCurrencyPrecision(4318.4m, CurrenciesConstant.Toman));
    }

    /// <summary>
    /// The admin commands take Persian words, not Latin codes. Both readings of the ounce are
    /// accepted because both are in everyday use.
    /// </summary>
    [Theory]
    [InlineData("انس")]
    [InlineData("اونس")]
    public void TheOunceIsReachableByItsPersianKeyword(string keyword)
    {
        Assert.Equal(CurrenciesConstant.XAU_USD, CurrenciesConstant.ResolveSymbolByAlias(keyword));
    }

    [Fact]
    public void TheDollarIsReachableByItsPersianName()
    {
        Assert.Equal(CurrenciesConstant.Usd, CurrenciesConstant.ResolveCurrencyCode("دلار"));
    }

    /// <summary>
    /// The staleness limit is compiled in, not left to the config file. The deployment recipe
    /// treats a <c>Symbols</c> block as optional — the symbol works without one — so a limit that
    /// lived only in configuration would be absent on exactly the host that needs it, and
    /// auto-quote would republish Friday's close all weekend. Found by the review of PR #320.
    /// </summary>
    [Fact]
    public void TheOunceCarriesItsStalenessLimitWithoutAnyConfiguration()
    {
        Assert.Equal(15, CurrenciesConstant.GetTradingPairInfo(CurrenciesConstant.XAU_USD)!.MaxPriceAgeMinutes);
    }

    /// <summary>
    /// A quote currency whose block says nothing about precision gets two decimals, not none.
    /// Zero is what an <c>int</c> defaults to, and it would round every fill to whole units in
    /// silence — where the old behaviour, before quote assets were registered at all, was a loud
    /// refusal at settlement. Found by the review of PR #320.
    /// </summary>
    [Fact]
    public void AQuoteCurrencyWithNoStatedPrecision_GetsCentsRatherThanWholeUnits()
    {
        var merged = CurrenciesConstant.MergeWithConfiguration(
            new Dictionary<string, TradingPairInfo>(),
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Symbols:FOO/EUR:BaseAssetPersianName"] = "فو"
                }).Build());

        // The pair itself records that nothing was said, rather than silently meaning zero.
        Assert.Null(merged["FOO/EUR"].QuoteDecimalPlaces);

        var euro = CurrenciesConstant.BuildCurrencies(merged)["EUR"];

        Assert.Equal(2, euro.DecimalPlaces);
        Assert.False(euro.IsTradable);
    }

    /// <summary>
    /// Adding a symbol must not move the one an empty keyword means. An admin typing a quote
    /// command with no symbol has always meant melted gold, and thousands of muscle-memory
    /// commands depend on it.
    /// </summary>
    [Fact]
    public void AnEmptyKeywordStillMeansMeltedGold()
    {
        Assert.Equal(CurrenciesConstant.MAUA_IRT, CurrenciesConstant.ResolveSymbolByAlias(""));
        Assert.Equal(CurrenciesConstant.MAUA_IRT, CurrenciesConstant.ResolveSymbolByAlias(null));
    }
}
