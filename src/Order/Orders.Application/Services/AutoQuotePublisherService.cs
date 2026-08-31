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
            await ExpireStaleProposalsAsync();

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

    /// <summary>
    /// Closes proposals nobody answered in time, on the same schedule as the tick that creates
    /// them. Running it here rather than on its own timer keeps the two in step: a proposal cannot
    /// outlive the tick that would have replaced it anyway.
    /// </summary>
    private async Task ExpireStaleProposalsAsync()
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IPendingQuoteRepository>().ExpireStaleAsync();
        }
        catch (Exception ex)
        {
            // Expiry failing must not stop the tick that publishes prices.
            _logger.LogError(ex, "Failed to expire stale quote proposals.");
        }
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

        // referencePrice is already Toman per traded base unit (a gram of gold, a whole coin, a
        // whole Bitcoin) — each provider does its own unit conversion, so nothing here is
        // specific to any one symbol.
        var halfSpread = settings.SpreadPercent / 100m / 2m;
        var buyPrice = decimal.Round(referencePrice.Value * (1 - halfSpread), 2);
        var sellPrice = decimal.Round(referencePrice.Value * (1 + halfSpread), 2);

        var quoteRepo = scope.ServiceProvider.GetRequiredService<IQuoteRepository>();
        var currentQuote = await quoteRepo.GetActiveAsync(symbol);

        // The spread is symmetric, so the proposed mid is the reference price itself — but it is
        // derived from the rounded legs anyway, so the number the band judges is exactly the number
        // that would be published.
        var proposedMid = QuotePlausibility.MidOf(buyPrice, sellPrice);
        var verdict = QuotePlausibility.Check(proposedMid, currentQuote);

        if (!verdict.IsWithinBand)
        {
            await HoldForApprovalAsync(scope, symbol, buyPrice, sellPrice, verdict, settings.UpdatedByUserId);
            return;
        }

        if (verdict.PreviousMid is null)
        {
            // Cold start: nothing has ever been published for this symbol, so there is nothing for
            // the price to be implausible relative to. Accepting it is what lets auto-quote
            // bootstrap a newly activated symbol at all; the band applies from the next tick on.
            // A restart does not reach here — the active quote is read from the database, so it
            // survives one.
            _logger.LogInformation(
                "Auto-quote for {Symbol}: no previous quote to compare against, so the plausibility band was not applied to reference {Reference}.",
                symbol, referencePrice.Value);
        }

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
    /// Puts an out-of-band price in front of an admin instead of publishing it, and leaves the
    /// symbol quoting its previous price meanwhile.
    ///
    /// <para>
    /// This replaced a rule that refused the tick and, after three in a row, deactivated the quote
    /// and switched auto-quote off. That was wrong twice over. It turned a suspicious price into a
    /// silent outage — the symbol simply stopped quoting, and only a log line said why — and it
    /// could be walked straight around: with the quote deactivated the symbol had no anchor, so an
    /// admin re-enabling auto-quote hit the cold-start path and published the very price that had
    /// just been refused three times, with no check at all. Observed happening in a live session,
    /// which is what prompted this design.
    /// </para>
    ///
    /// <para>
    /// Auto-quote is deliberately left enabled. Deciding what gets published is the band's job;
    /// switching the feature off is the admin's, and a price nobody has approved simply does not
    /// become a quote.
    /// </para>
    /// </summary>
    private async Task HoldForApprovalAsync(
        IServiceScope scope,
        string symbol,
        decimal buyPrice,
        decimal sellPrice,
        QuotePlausibility.Verdict verdict,
        Guid proposedByUserId)
    {
        var pendingQuotes = scope.ServiceProvider.GetRequiredService<IPendingQuoteRepository>();

        try
        {
            await pendingQuotes.ProposeAsync(PendingQuote.Propose(
                symbol, buyPrice, sellPrice, verdict.PreviousMid, verdict.DeviationPercent,
                QuoteSource.Auto, proposedByUserId));
        }
        catch (BusinessRuleException ex)
        {
            // The proposal itself is invalid — a price that rounds to zero, or auto-quote enabled
            // with no configuring admin on record. There is nothing to ask anybody about.
            _logger.LogWarning(ex,
                "Auto-quote for {Symbol} was outside the band and could not be held for approval either.", symbol);
            return;
        }

        // Loud on purpose. Silence is what made the earlier design dangerous: a symbol that had
        // stopped quoting looked exactly like a quiet market. The proposed value and the band it
        // violated are both here, so the log alone says what happened.
        _logger.LogWarning(
            "Auto-quote for {Symbol} held for approval: proposed buy {BuyPrice}, sell {SellPrice} is {Deviation}% away " +
            "from the last published mid {PreviousMid}, outside the plausibility band of ±{Band}%. The previous quote " +
            "stands until an admin answers.",
            symbol, buyPrice, sellPrice, decimal.Round(verdict.DeviationPercent, 4), verdict.PreviousMid,
            QuotePlausibility.MaxDeviationPercent);
    }
}
