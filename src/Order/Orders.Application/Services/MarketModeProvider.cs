using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.Enums.Order;

namespace Orders.Application.Services;

/// <summary>
/// Reports which market mode a symbol runs in, and who its market maker is.
///
/// <para>
/// <b>Why configuration and not a table:</b> this value changes rarely — when a symbol gains real
/// liquidity. A table would have needed an admin UI and a migration without adding anything. The
/// values are not cached in the constructor, so editing the configuration file needs no restart.
/// </para>
///
/// <para>
/// <b>Built on what was already there:</b> the <c>Matching:RequireMarketMakerCounterparty</c>
/// setting already expressed exactly this rule — "a customer trades only with the market maker".
/// Rather than introducing a parallel concept, it is read as the global default, and
/// <c>Matching:MarketModes:{symbol}</c> can override it per symbol. Two parallel definitions of one
/// rule is a mistake this codebase has made several times already.
/// </para>
/// </summary>
public class MarketModeProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MarketModeProvider> _logger;

    public MarketModeProvider(IConfiguration configuration, ILogger<MarketModeProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// The market mode for a symbol. Resolution order: the symbol's own setting, then the older
    /// <c>RequireMarketMakerCounterparty</c> setting, then OrderBook, which is the historical behaviour.
    /// </summary>
    public MarketMode GetMode(string symbol)
    {
        var perSymbol = _configuration[$"Matching:MarketModes:{symbol}"];
        if (Enum.TryParse<MarketMode>(perSymbol, ignoreCase: true, out var mode))
            return mode;

        return _configuration.GetValue("Matching:RequireMarketMakerCounterparty", defaultValue: false)
            ? MarketMode.Dealer
            : MarketMode.OrderBook;
    }
}
