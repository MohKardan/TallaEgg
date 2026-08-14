using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;

namespace Orders.Application.Services;

/// <summary>
/// Periodically publishes a quote for every active, auto-quote-enabled symbol from a live
/// reference price, the same way an admin does by hand with the <c>buyPrice-sellPrice</c>
/// command — this calls the same <see cref="Quote.Publish"/>, nothing about publishing itself
/// changes (issue #90).
///
/// <para>
/// Originally MAUA/IRT only; generalized to loop over <see cref="CurrenciesConstant.AllTradingPairs"/>
/// when coin and Bitcoin quoting were added, so a new active symbol needs no change here — only
/// a <see cref="CurrenciesConstant"/> entry and, per symbol, an admin explicitly opting in (see
/// <see cref="AutoQuoteSettings"/>).
/// </para>
///
/// A manually published quote always overrides the automatic one on the next customer action,
/// since only one quote per symbol is ever active — this service does not need to know or care
/// whether the previous quote was manual or automatic.
/// </summary>
public class AutoQuotePublisherService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AutoQuotePublisherService> _logger;

    public AutoQuotePublisherService(IServiceScopeFactory scopeFactory, ILogger<AutoQuotePublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AutoQuotePublisherService started (poll every {Minutes}m).", PollInterval.TotalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var pair in CurrenciesConstant.AllTradingPairs.Where(p => p.IsActive))
            {
                try
                {
                    await PublishIfDueAsync(pair.Symbol, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // A misconfiguration or a bug for one symbol must never take the rest of the
                    // shop down — manual quotes and trading, and every other symbol's auto-quote,
                    // are unrelated to this one failing (same rule already established for #73).
                    _logger.LogError(ex, "Unexpected error auto-publishing a quote for {Symbol}.", pair.Symbol);
                }
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("AutoQuotePublisherService stopped.");
    }

    /// <summary>internal so the tick logic can be tested directly, without waiting on the poll loop.</summary>
    internal async Task PublishIfDueAsync(string symbol, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IAutoQuoteSettingsRepository>();

        var settings = await settingsRepo.GetOrCreateAsync(symbol);
        if (!settings.IsEnabled) return;

        var chain = scope.ServiceProvider.GetRequiredService<ReferencePriceProviderChain>();
        var referencePrice = await chain.GetPriceAsync(symbol, ct);

        if (referencePrice is null)
        {
            _logger.LogWarning("Auto-quote for {Symbol} skipped this tick: no price source answered.", symbol);
            return;
        }

        // referencePrice is already Toman per traded base unit (a gram of gold, a whole coin, a
        // whole Bitcoin) — each provider does its own unit conversion, so nothing here is
        // specific to any one symbol.
        var halfSpread = settings.SpreadPercent / 100m / 2m;
        var buyPrice = decimal.Round(referencePrice.Value * (1 - halfSpread), 2);
        var sellPrice = decimal.Round(referencePrice.Value * (1 + halfSpread), 2);

        var quoteRepo = scope.ServiceProvider.GetRequiredService<IQuoteRepository>();

        try
        {
            var quote = Quote.Publish(symbol, buyPrice, sellPrice, settings.UpdatedByUserId);
            await quoteRepo.PublishAsync(quote);

            _logger.LogInformation(
                "Auto-published quote for {Symbol}: buy {BuyPrice}, sell {SellPrice} (reference {Reference}, spread {Spread}%).",
                symbol, buyPrice, sellPrice, referencePrice, settings.SpreadPercent);
        }
        catch (ArgumentException ex)
        {
            // Quote.Publish's own validation (e.g. a zero/negative price from a source
            // returning garbage) rejects the quote. Logged and skipped, not thrown further —
            // the previous quote stays active until the next tick gets a sane price.
            _logger.LogWarning(ex, "Auto-quote for {Symbol} rejected by Quote.Publish; keeping the previous quote.", symbol);
        }
    }
}
