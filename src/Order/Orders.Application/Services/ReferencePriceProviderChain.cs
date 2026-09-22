using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Core;

namespace Orders.Application.Services;

/// <summary>
/// Tries each configured <see cref="IReferencePriceProvider"/> in order and returns the first
/// price that answers, for whichever symbol is asked about. Adding a third or fourth source is
/// registering one more provider in DI, in the order it should be tried — nothing here changes.
///
/// <para>
/// A price older than the symbol's configured <c>MaxPriceAgeMinutes</c> counts as not answering,
/// so the next source gets a turn (issue #316). That matters when one source freezes
/// while another stays live; when every source is stale — a closed market — nothing answers at
/// all, and the caller's existing "no price source answered" path leaves the previous quote
/// standing rather than republishing the last price before the market shut.
/// </para>
/// </summary>
public class ReferencePriceProviderChain
{
    private readonly IReadOnlyList<IReferencePriceProvider> _providers;
    private readonly ILogger<ReferencePriceProviderChain> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeProvider _time;

    public ReferencePriceProviderChain(
        IEnumerable<IReferencePriceProvider> providers,
        ILogger<ReferencePriceProviderChain> logger,
        IConfiguration configuration,
        TimeProvider time)
    {
        _providers = providers.ToList();
        _logger = logger;
        _configuration = configuration;
        // Injected rather than DateTimeOffset.UtcNow so a test can age a price without waiting
        // for one; Orders.Api registers TimeProvider.System.
        _time = time;
    }

    public async Task<ReferencePrice?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var maxAge = MaxAgeFor(symbol);

        foreach (var provider in _providers)
        {
            var quoted = await provider.GetPriceAsync(symbol, cancellationToken);

            if (quoted is not { Price: > 0 })
            {
                _logger.LogWarning("{Provider} did not answer for {Symbol}; trying the next source.", provider.Name, symbol);
                continue;
            }

            var price = quoted.Value;

            if (IsStale(price, maxAge, out var age))
            {
                // Loud, and carrying both numbers: outside market hours this is the line that
                // explains why a symbol stopped being re-quoted, and it should not take a second
                // source of truth to interpret.
                _logger.LogWarning(
                    "{Provider} answered for {Symbol} with a price from {AsOf}, {Age} minutes old, older than the " +
                    "{MaxAge} minutes allowed for this symbol; trying the next source.",
                    provider.Name, symbol, price.AsOf, (int)age.TotalMinutes, maxAge!.Value.TotalMinutes);
                continue;
            }

            _logger.LogInformation("{Symbol} price {Price} obtained from {Provider}.", symbol, price.Price, provider.Name);
            return price;
        }

        _logger.LogWarning("No price source answered for {Symbol} ({Count} tried).", symbol, _providers.Count);
        return null;
    }

    /// <summary>
    /// How old this symbol's price may be, from <c>Symbols:{symbol}:MaxPriceAgeMinutes</c> — the
    /// same config block each provider reads its instrument mapping from — or null for no limit,
    /// which is what every symbol gets until someone sets one.
    ///
    /// <para>
    /// Absent by default on purpose: imposing an age on a symbol nobody has chosen a limit for
    /// would stop it quoting the moment its market closed for the night, and that is a business
    /// decision rather than this change's to make. It is per symbol rather than per provider
    /// because it describes the market — one that shuts overnight and one that trades around the
    /// clock tolerate very different ages from the same feed.
    /// </para>
    /// </summary>
    private TimeSpan? MaxAgeFor(string symbol)
    {
        var minutes = _configuration.GetValue<int?>($"Symbols:{symbol}:MaxPriceAgeMinutes");
        return minutes is > 0 ? TimeSpan.FromMinutes(minutes.Value) : null;
    }

    /// <summary>
    /// An unknown <see cref="ReferencePrice.AsOf"/> is never stale: a source that publishes no
    /// timestamp is not evidence that its price is old. A price dated in the future is not stale
    /// either — clock skew between two hosts is not an argument against a price.
    /// </summary>
    private bool IsStale(ReferencePrice price, TimeSpan? maxAge, out TimeSpan age)
    {
        age = TimeSpan.Zero;
        if (maxAge is null || price.AsOf is null) return false;

        age = _time.GetUtcNow() - price.AsOf.Value;
        return age > maxAge.Value;
    }
}
