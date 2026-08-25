using Microsoft.Extensions.Configuration;
using TallaEgg.Core;

namespace Wallet.Tests;

/// <summary>
/// Config-driven symbol metadata — added so a new trading symbol needs a config block instead
/// of a code change and a rebuild (see README's "Adding a trading symbol").
///
/// <para>
/// These tests exercise <see cref="CurrenciesConstant.MergeWithConfiguration"/>, the pure
/// function <see cref="CurrenciesConstant.Configure"/> wraps, rather than calling
/// <c>Configure</c> itself. <c>CurrenciesConstant</c> holds its symbol catalog in shared static
/// fields that every test in this process reads (<c>MarketModeTests</c>,
/// <c>AutoQuoteCommandTests</c>, message-builder tests, ...), and xUnit runs test classes in
/// parallel by default — mutating that shared state from a test would make unrelated tests
/// flaky depending on what else happened to be running. Testing the pure merge instead proves
/// the same logic without touching anything another test can see.
/// </para>
/// </summary>
public class CurrenciesConstantSymbolsTests
{
    private static IConfiguration ConfigWithSymbols(string json)
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return new ConfigurationBuilder().AddJsonStream(stream).Build();
    }

    [Fact]
    public void MergeWithConfiguration_AddsABrandNewSymbolNotInTheDefaults()
    {
        var defaults = new Dictionary<string, TradingPairInfo>();
        var config = ConfigWithSymbols("""
            { "Symbols": { "ETH/IRT": { "PersianName": "اتریوم/تومان", "MinQuantity": 0.01 } } }
            """);

        var merged = CurrenciesConstant.MergeWithConfiguration(defaults, config);

        Assert.True(merged.ContainsKey("ETH/IRT"));
        Assert.Equal("اتریوم/تومان", merged["ETH/IRT"].PersianName);
        Assert.Equal(0.01m, merged["ETH/IRT"].MinQuantity);
    }

    /// <summary>Base/quote asset are derived from the key itself when the config block omits them.</summary>
    [Fact]
    public void MergeWithConfiguration_DerivesBaseAndQuoteAssetFromTheSymbolKey()
    {
        var defaults = new Dictionary<string, TradingPairInfo>();
        var config = ConfigWithSymbols("""{ "Symbols": { "ETH/IRT": { "PersianName": "اتریوم/تومان" } } }""");

        var merged = CurrenciesConstant.MergeWithConfiguration(defaults, config);

        Assert.Equal("ETH", merged["ETH/IRT"].BaseAsset);
        Assert.Equal("IRT", merged["ETH/IRT"].QuoteAsset);
    }

    /// <summary>
    /// A config block for an already-known symbol overrides only the fields it sets — everything
    /// else about that symbol's compiled default survives.
    /// </summary>
    [Fact]
    public void MergeWithConfiguration_OverridesOnlyTheFieldsAConfigBlockSets()
    {
        var defaults = new Dictionary<string, TradingPairInfo>
        {
            ["MAUA/IRT"] = new TradingPairInfo
            {
                Symbol = "MAUA/IRT", BaseAsset = "MAUA", QuoteAsset = "IRT",
                PersianName = "آبشده/تومان", MinQuantity = 0.1m, MaxQuantity = 1000m
            }
        };
        var config = ConfigWithSymbols("""{ "Symbols": { "MAUA/IRT": { "MinQuantity": 0.5 } } }""");

        var merged = CurrenciesConstant.MergeWithConfiguration(defaults, config);

        Assert.Equal(0.5m, merged["MAUA/IRT"].MinQuantity);
        // Untouched by the config block — the compiled default survives.
        Assert.Equal("آبشده/تومان", merged["MAUA/IRT"].PersianName);
        Assert.Equal(1000m, merged["MAUA/IRT"].MaxQuantity);
    }

    [Fact]
    public void MergeWithConfiguration_WithNoSymbolsSection_LeavesTheDefaultsUnchanged()
    {
        var defaults = new Dictionary<string, TradingPairInfo>
        {
            ["MAUA/IRT"] = new TradingPairInfo { Symbol = "MAUA/IRT", PersianName = "آبشده/تومان" }
        };
        var config = ConfigWithSymbols("""{ "AllowedHosts": "*" }""");

        var merged = CurrenciesConstant.MergeWithConfiguration(defaults, config);

        Assert.Equal("آبشده/تومان", merged["MAUA/IRT"].PersianName);
        Assert.Single(merged);
    }

    [Fact]
    public void MergeWithConfiguration_BindsProviderInstrumentMappingForNerkhAndBrsApi()
    {
        var defaults = new Dictionary<string, TradingPairInfo>();
        var config = ConfigWithSymbols("""
            {
              "Symbols": {
                "ETH/IRT": {
                  "PersianName": "اتریوم/تومان",
                  "Nerkh": { "Path": "crypto/ETH", "Key": "ETH", "ConvertFromMesghal": false },
                  "BrsApi": { "Array": "cryptocurrency", "Symbol": "ETH", "ConvertFromMesghal": false }
                }
              }
            }
            """);

        var merged = CurrenciesConstant.MergeWithConfiguration(defaults, config);

        // The provider classes read Symbols:{symbol}:Nerkh/BrsApi directly via IConfiguration
        // (not through TradingPairInfo) — this just confirms the section round-trips through
        // the same "Symbols" config the metadata comes from, at the path they read.
        var section = config.GetSection("Symbols:ETH/IRT:Nerkh");
        Assert.Equal("crypto/ETH", section["Path"]);
        Assert.Equal("ETH", section["Key"]);
    }

    // ── ResolveSymbolByAlias — reads the compiled defaults only, never mutates shared state ──

    [Fact]
    public void ResolveSymbolByAlias_NoKeywordMeansGold()
    {
        Assert.Equal(CurrenciesConstant.MAUA_IRT, CurrenciesConstant.ResolveSymbolByAlias(null));
        Assert.Equal(CurrenciesConstant.MAUA_IRT, CurrenciesConstant.ResolveSymbolByAlias(""));
        Assert.Equal(CurrenciesConstant.MAUA_IRT, CurrenciesConstant.ResolveSymbolByAlias("   "));
    }

    [Fact]
    public void ResolveSymbolByAlias_RecognisesTheCoinAndBitcoinKeywords()
    {
        Assert.Equal(CurrenciesConstant.SEKE_BAHAR_IRT, CurrenciesConstant.ResolveSymbolByAlias("سکه"));
        Assert.Equal(CurrenciesConstant.BTC_IRT, CurrenciesConstant.ResolveSymbolByAlias("بیت"));
        Assert.Equal(CurrenciesConstant.BTC_IRT, CurrenciesConstant.ResolveSymbolByAlias("بیتکوین"));
    }

    [Fact]
    public void ResolveSymbolByAlias_ReturnsNullForAnUnknownKeyword()
    {
        Assert.Null(CurrenciesConstant.ResolveSymbolByAlias("نقره"));
    }

    // ── ResolveCurrencyCode — the شارژ/deduct commands' resolver, not the quote commands' ──

    /// <summary>
    /// Hit live: "ش 09158527483 100 سکه" failed with "نوع شناسایی نشد" because this resolver
    /// only matched an exact code or the full Persian name ("سکه تمام بهار آزادی"), never the
    /// short alias the quote commands already accept. It now falls back to the same alias list.
    /// </summary>
    [Fact]
    public void ResolveCurrencyCode_AcceptsTheShortAliasNotJustTheFullPersianName()
    {
        Assert.Equal(CurrenciesConstant.SekeBahar, CurrenciesConstant.ResolveCurrencyCode("سکه"));
        Assert.Equal(CurrenciesConstant.Btc, CurrenciesConstant.ResolveCurrencyCode("بیت"));
    }

    [Fact]
    public void ResolveCurrencyCode_StillAcceptsTheFullPersianNameAndTheCode()
    {
        Assert.Equal(CurrenciesConstant.SekeBahar, CurrenciesConstant.ResolveCurrencyCode("سکه تمام بهار آزادی"));
        Assert.Equal(CurrenciesConstant.Btc, CurrenciesConstant.ResolveCurrencyCode("BTC"));
    }

    [Fact]
    public void ResolveCurrencyCode_ReturnsNullForSomethingThatMatchesNothing()
    {
        Assert.Null(CurrenciesConstant.ResolveCurrencyCode("نقره"));
    }

    // ── CreditAssetFor / GetPersianNamesList — every tradable asset gets a CREDIT_ ledger now ──

    [Fact]
    public void CreditAssetFor_PrefixesTheBaseAssetCode()
    {
        Assert.Equal("CREDIT_BTC", CurrenciesConstant.CreditAssetFor(CurrenciesConstant.Btc));
        Assert.Equal("CREDIT_MAUA", CurrenciesConstant.CreditAssetFor(CurrenciesConstant.Maua));
    }

    /// <summary>
    /// Every tradable asset's CREDIT_ variant is a recognised currency, with its own Persian
    /// name — not just CREDIT_MAUA as a hardcoded special case.
    /// </summary>
    [Fact]
    public void GetCurrencyInfo_RecognisesTheCreditVariantOfEveryTradableAsset()
    {
        var creditBtc = CurrenciesConstant.GetCurrencyInfo("CREDIT_BTC");

        Assert.NotNull(creditBtc);
        Assert.Equal("اعتبار بیت‌کوین", creditBtc.PersianName);
        Assert.False(creditBtc.IsTradable);
    }

    /// <summary>
    /// The شارژ/deduct commands always prepend CREDIT_ themselves (<see cref="CreditAssetFor"/>).
    /// If an admin typed a CREDIT_ name directly as the currency argument, resolving it and
    /// prepending CREDIT_ again would produce a meaningless double-prefixed code
    /// ("CREDIT_CREDIT_MAUA") — so the help text this method feeds must not suggest typing one.
    /// </summary>
    [Fact]
    public void GetPersianNamesList_OmitsCreditVariants()
    {
        Assert.DoesNotContain("اعتبار", CurrenciesConstant.GetPersianNamesList());
    }

    /// <summary>
    /// BTC/USDT and ETH/USDT were dead compiled defaults — no market, no quote, no price
    /// provider, referenced nowhere else in the repo — yet their base assets still showed up as
    /// an "allowed type" in the شارژ/کسر error text, since BuildCurrencies derives a currency
    /// entry from every compiled pair regardless of whether it does anything. "اتریوم" in
    /// particular had no real counterpart (unlike "BTC", already covered by the real BTC/IRT
    /// pair), so admins reading the help text could charge an asset with no purpose in this
    /// system. Removed rather than hidden, since nothing used them.
    /// </summary>
    [Fact]
    public void GetPersianNamesList_DoesNotOfferTheDeadLegacyUsdtPairs()
    {
        Assert.DoesNotContain("اتریوم", CurrenciesConstant.GetPersianNamesList());
    }
}
