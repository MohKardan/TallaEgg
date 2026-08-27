using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Each symbol's market mode (issue #48).
///
/// <para>
/// The point of these tests is backward compatibility: <c>Matching:RequireMarketMakerCounterparty</c>
/// already existed and expressed exactly this rule. Had <c>MarketMode</c> ignored it, there would
/// be two parallel definitions of one rule — the pattern that has repeatedly produced bugs in this
/// codebase.
/// </para>
///
/// <para>
/// <b>Who the market maker is no longer lives here.</b> This class only reports which mode a symbol
/// runs in. The counterparty is now the quote itself — whoever published it — which is covered in
/// <see cref="QuoteFillCounterpartyTests"/>.
/// </para>
/// </summary>
public class MarketModeTests
{
    private const string Symbol = "MAUA/IRT";

    private static MarketModeProvider Provider(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build();

        return new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance);
    }

    /// <summary>With no configuration at all, the historical behaviour holds: the order book.</summary>
    [Fact]
    public void WithNoConfiguration_TheModeIsOrderBook()
    {
        Assert.Equal(MarketMode.OrderBook, Provider().GetMode(Symbol));
    }

    /// <summary>
    /// The old setting must keep working. Anyone who had turned it on must not have it silently
    /// turned off by this change.
    /// </summary>
    [Fact]
    public void TheLegacyMarketMakerSettingStillMeansDealerMode()
    {
        var provider = Provider(("Matching:RequireMarketMakerCounterparty", "true"));

        Assert.Equal(MarketMode.Dealer, provider.GetMode(Symbol));
    }

    /// <summary>A symbol's own setting takes precedence over the global one.</summary>
    [Fact]
    public void APerSymbolSettingOverridesTheGlobalOne()
    {
        var provider = Provider(
            ("Matching:RequireMarketMakerCounterparty", "true"),
            ($"Matching:MarketModes:{Symbol}", "OrderBook"));

        Assert.Equal(MarketMode.OrderBook, provider.GetMode(Symbol));
    }

    /// <summary>
    /// Two symbols can be in two different modes at once — which is what coexistence means in
    /// issue #48: one symbol stays on quotes while another, with real liquidity, moves to the order
    /// book.
    /// </summary>
    [Fact]
    public void TwoSymbolsCanRunInDifferentModes()
    {
        var provider = Provider(
            ("Matching:MarketModes:MAUA/IRT", "Dealer"),
            ("Matching:MarketModes:BTC/IRT", "OrderBook"));

        Assert.Equal(MarketMode.Dealer, provider.GetMode("MAUA/IRT"));
        Assert.Equal(MarketMode.OrderBook, provider.GetMode("BTC/IRT"));
    }

    /// <summary>An invalid value must not throw; it falls back to the default.</summary>
    [Fact]
    public void AnUnrecognisedModeFallsBackInsteadOfThrowing()
    {
        var provider = Provider(($"Matching:MarketModes:{Symbol}", "چیز نامعتبر"));

        Assert.Equal(MarketMode.OrderBook, provider.GetMode(Symbol));
    }
}
