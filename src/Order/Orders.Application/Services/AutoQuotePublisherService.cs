using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;
using TallaEgg.Core.ErrorHandling;

namespace Orders.Application.Services;

/// <summary>
/// Periodically publishes a quote for every active, auto-quote-enabled symbol from a live
/// reference price, the same way an admin does by hand with the <c>buyPrice-sellPrice</c>
/// command — this calls the same <see cref="Quote.Publish"/>, nothing about publishing itself
/// changes (issue #90).
///
/// <para>
/// Originally MAUA/IRT only; generalized to loop over every symbol <see cref="ISymbolSettingsRepository"/>
/// reports active when coin and Bitcoin quoting were added, so a newly activated symbol needs no
/// change here — only a bot command to turn it on (<see cref="SymbolSettings"/>) and, per symbol,
/// an admin explicitly opting into auto-quote too (see <see cref="AutoQuoteSettings"/>). The two
/// are independent switches on purpose: a symbol can be tradable with only manual quotes, or not
/// tradable at all regardless of its auto-quote setting.
/// </para>
///
/// A manually published quote always overrides the automatic one on the next customer action,
/// since only one quote per symbol is ever active — this service does not need to know or care
/// whether the previous quote was manual or automatic.
/// </summary>
public class AutoQuotePublisherService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(2);

    /// <summary>
    /// How far the reference price may sit from the last published quote before the tick is
    /// refused, as a percentage of that quote's mid price.
    ///
    /// <para>
    /// This is a business number rather than an engineering one, set by the product owner
    /// (issue #158). Gold does not move 5% in the two minutes between ticks, so a source that
    /// says it did is reporting a broken feed, not a market. The value is deliberately far wider
    /// than any real move and still far narrower than the failures it exists to catch: a
    /// per-mithqal figure read as per-gram is roughly 4.33x, a misplaced decimal is 10x.
    /// </para>
    ///
    /// <para>
    /// One band covers every symbol. If the coin or Bitcoin ever needs its own, it belongs
    /// alongside the spread in <see cref="AutoQuoteSettings"/> so an admin can set it per symbol,
    /// which is a larger change than this one.
    /// </para>
    /// </summary>
    private const decimal MaxDeviationPercent = 5m;

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
            var activeSymbols = await ActiveSymbolsAsync(stoppingToken);

            foreach (var symbol in activeSymbols)
            {
                try
                {
                    await PublishIfDueAsync(symbol, stoppingToken);
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
                    _logger.LogError(ex, "Unexpected error auto-publishing a quote for {Symbol}.", symbol);
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

    private async Task<IReadOnlyList<string>> ActiveSymbolsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var symbolSettingsRepo = scope.ServiceProvider.GetRequiredService<ISymbolSettingsRepository>();
        return await symbolSettingsRepo.GetActiveSymbolsAsync();
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

        var quoteRepo = scope.ServiceProvider.GetRequiredService<IQuoteRepository>();

        if (!IsPlausible(symbol, referencePrice.Value, await quoteRepo.GetActiveAsync(symbol)))
            return;

        // referencePrice is already Toman per traded base unit (a gram of gold, a whole coin, a
        // whole Bitcoin) — each provider does its own unit conversion, so nothing here is
        // specific to any one symbol.
        var halfSpread = settings.SpreadPercent / 100m / 2m;
        var buyPrice = decimal.Round(referencePrice.Value * (1 - halfSpread), 2);
        var sellPrice = decimal.Round(referencePrice.Value * (1 + halfSpread), 2);

        try
        {
            var quote = Quote.Publish(symbol, buyPrice, sellPrice, settings.UpdatedByUserId);
            await quoteRepo.PublishAsync(quote);

            _logger.LogInformation(
                "Auto-published quote for {Symbol}: buy {BuyPrice}, sell {SellPrice} (reference {Reference}, spread {Spread}%).",
                symbol, buyPrice, sellPrice, referencePrice, settings.SpreadPercent);
        }
        catch (BusinessRuleException ex)
        {
            // Quote.Publish's own validation (e.g. a zero/negative price from a source
            // returning garbage) rejects the quote. Logged and skipped, not thrown further —
            // the previous quote stays active until the next tick gets a sane price.
            //
            // This used to catch ArgumentException, which Quote.Publish has never thrown: all
            // five of its validation paths raise BusinessRuleException, which derives straight
            // from Exception. The handler could not run, so a rejected price was reported by the
            // loop's generic catch at Error — reading as a service fault rather than the normal
            // rejection it is — and this warning was never written (issue #158, same shape
            // as #143).
            _logger.LogWarning(ex, "Auto-quote for {Symbol} rejected by Quote.Publish; keeping the previous quote.", symbol);
        }
    }

    /// <summary>
    /// Whether a reference price is close enough to the last published quote to be believable.
    ///
    /// <para>
    /// The comparison is against that quote's mid price, which for an auto-published quote is
    /// exactly the reference price it was built from — the spread is applied symmetrically either
    /// side. A manually published quote need not be symmetric, but its mid is still the best
    /// statement available of what the shop last considered this symbol to be worth.
    /// </para>
    ///
    /// <para>
    /// Rejecting rather than clamping is deliberate. Clamping would publish a price no source
    /// ever reported, which the shop is then bound to honour, and a feed stuck on a bad number
    /// would walk the quote towards it one tick at a time until it arrived. Holding the previous
    /// quote leaves the shop on the last price a source actually agreed with.
    /// </para>
    /// </summary>
    /// <param name="symbol">The symbol being quoted, for the log message.</param>
    /// <param name="referencePrice">The price the provider chain returned this tick.</param>
    /// <param name="lastQuote">The symbol's active quote, or null if it has never had one.</param>
    private bool IsPlausible(string symbol, decimal referencePrice, Quote? lastQuote)
    {
        if (lastQuote is null)
        {
            // Cold start: nothing has ever been published for this symbol, so there is nothing
            // for the price to be implausible relative to. Accepting it is what lets auto-quote
            // bootstrap a newly activated symbol at all; the band applies from the next tick on.
            // A restart does not reach here — the active quote is read from the database, so it
            // survives one.
            _logger.LogInformation(
                "Auto-quote for {Symbol}: no previous quote to compare against, so the plausibility band was not applied to reference {Reference}.",
                symbol, referencePrice);
            return true;
        }

        var lastMid = (lastQuote.BuyPrice + lastQuote.SellPrice) / 2m;
        var deviationPercent = Math.Abs(referencePrice - lastMid) / lastMid * 100m;

        if (deviationPercent <= MaxDeviationPercent) return true;

        // Loud on purpose. Skipping the tick silently would leave a broken feed looking exactly
        // like a quiet market, and the point of the band is that somebody finds out. The rejected
        // value and the band it violated are both in the message so the log alone says what
        // happened, without needing the price source to be queried again.
        _logger.LogWarning(
            "Auto-quote for {Symbol} rejected: reference price {Reference} is {Deviation}% away from the last published mid {LastMid}, " +
            "outside the plausibility band of ±{Band}%. Keeping the previous quote (buy {LastBuy}, sell {LastSell}).",
            symbol, referencePrice, decimal.Round(deviationPercent, 2), lastMid, MaxDeviationPercent,
            lastQuote.BuyPrice, lastQuote.SellPrice);

        return false;
    }
}
