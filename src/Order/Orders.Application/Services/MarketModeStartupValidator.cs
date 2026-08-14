using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core.Enums.Order;

namespace Orders.Application.Services;

/// <summary>
/// At startup, checks that every symbol with an active published quote is actually configured
/// for Dealer mode. See issue #73.
///
/// <para>
/// The two facts — a published quote, and a market-mode setting — are produced by different
/// people at different times: an admin publishing a price, and a developer or operator writing
/// configuration. Nothing else in the system ever compares them, so a mismatch between the two
/// was previously silent: the customer was told there were no prices, while an active quote sat
/// in the database the whole time.
/// </para>
///
/// <para>
/// <b>Logs, never throws.</b> A configuration mistake here should not stop the service — the
/// rest of the product still works, and a service that refuses to boot during a demo is worse
/// than one that reports a problem. <see cref="MarketModeProvider"/>'s own default (falling back
/// to <see cref="MarketMode.OrderBook"/> with no configuration at all) is intentionally
/// unchanged; this only adds the system-level check that the unit-level default cannot see.
/// </para>
/// </summary>
public class MarketModeStartupValidator
{
    private readonly IQuoteRepository _quoteRepository;
    private readonly MarketModeProvider _marketModeProvider;
    private readonly ILogger<MarketModeStartupValidator> _logger;

    public MarketModeStartupValidator(
        IQuoteRepository quoteRepository,
        MarketModeProvider marketModeProvider,
        ILogger<MarketModeStartupValidator> logger)
    {
        _quoteRepository = quoteRepository;
        _marketModeProvider = marketModeProvider;
        _logger = logger;
    }

    public async Task ValidateAsync()
    {
        var activeSymbols = await _quoteRepository.GetActiveSymbolsAsync();

        foreach (var symbol in activeSymbols)
        {
            if (_marketModeProvider.GetMode(symbol) != MarketMode.Dealer)
            {
                _logger.LogError(
                    "Symbol {Symbol} has an active published quote but is not configured for " +
                    "Dealer mode, so the quote will never be used and every fill against it is " +
                    "refused. Set \"Matching:MarketModes:{Symbol}\": \"Dealer\" in " +
                    "appsettings.global.json.",
                    symbol, symbol);
            }
        }
    }
}
