using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;

namespace Orders.Application.Services;

/// <summary>
/// Periodically publishes a quote for MAUA/IRT from a live gold price, the same way an admin
/// does by hand with the <c>buyPrice-sellPrice</c> command — this calls the same
/// <see cref="Quote.Publish"/>, nothing about publishing itself changes (issue #90).
///
/// Only runs when an admin has explicitly turned it on: see <see cref="AutoQuoteSettings"/>.
/// A manually published quote always overrides the automatic one on the next customer action,
/// since only one quote per symbol is ever active — this service does not need to know or care
/// whether the previous quote was manual or automatic.
/// </summary>
public class AutoQuotePublisherService : BackgroundService
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;
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
            try
            {
                await PublishIfDueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // A misconfiguration or a bug here must never take the rest of the shop down —
                // manual quotes and trading are unrelated to this loop (same rule already
                // established for #73).
                _logger.LogError(ex, "Unexpected error in the auto-quote publishing loop.");
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
    internal async Task PublishIfDueAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var settingsRepo = scope.ServiceProvider.GetRequiredService<IAutoQuoteSettingsRepository>();

        var settings = await settingsRepo.GetOrCreateAsync(Symbol);
        if (!settings.IsEnabled) return;

        var chain = scope.ServiceProvider.GetRequiredService<GoldPriceProviderChain>();
        var mesghalPrice = await chain.GetMesghalPriceAsync(ct);

        if (mesghalPrice is null)
        {
            _logger.LogWarning("Auto-quote for {Symbol} skipped this tick: no price source answered.", Symbol);
            return;
        }

        // Quotes are stored per gram (Quote.cs), while the fetched price and the admin's manual
        // command are both per mesghal — the same conversion the manual path already applies.
        var perGram = mesghalPrice.Value / CurrenciesConstant.GramsPerMesghal;

        var halfSpread = settings.SpreadPercent / 100m / 2m;
        var buyPrice = decimal.Round(perGram * (1 - halfSpread), 2);
        var sellPrice = decimal.Round(perGram * (1 + halfSpread), 2);

        var quoteRepo = scope.ServiceProvider.GetRequiredService<IQuoteRepository>();

        try
        {
            var quote = Quote.Publish(Symbol, buyPrice, sellPrice, settings.UpdatedByUserId);
            await quoteRepo.PublishAsync(quote);

            _logger.LogInformation(
                "Auto-published quote for {Symbol}: buy {BuyPrice}, sell {SellPrice} (mesghal {Mesghal}, spread {Spread}%).",
                Symbol, buyPrice, sellPrice, mesghalPrice, settings.SpreadPercent);
        }
        catch (ArgumentException ex)
        {
            // Quote.Publish's own validation (e.g. a zero/negative price from a source
            // returning garbage) rejects the quote. Logged and skipped, not thrown further —
            // the previous quote stays active until the next tick gets a sane price.
            _logger.LogWarning(ex, "Auto-quote for {Symbol} rejected by Quote.Publish; keeping the previous quote.", Symbol);
        }
    }
}
