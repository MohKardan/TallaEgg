using Microsoft.Extensions.Logging;
using Orders.Core;

namespace Orders.Application.Services;

/// <summary>
/// Tries each configured <see cref="IReferencePriceProvider"/> in order and returns the first
/// price that answers, for whichever symbol is asked about. Adding a third or fourth source is
/// registering one more provider in DI, in the order it should be tried — nothing here changes.
/// </summary>
public class ReferencePriceProviderChain
{
    private readonly IReadOnlyList<IReferencePriceProvider> _providers;
    private readonly ILogger<ReferencePriceProviderChain> _logger;

    public ReferencePriceProviderChain(IEnumerable<IReferencePriceProvider> providers, ILogger<ReferencePriceProviderChain> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
    }

    public async Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            var price = await provider.GetPriceAsync(symbol, cancellationToken);

            if (price is > 0)
            {
                _logger.LogInformation("{Symbol} price {Price} obtained from {Provider}.", symbol, price, provider.Name);
                return price;
            }

            _logger.LogWarning("{Provider} did not answer for {Symbol}; trying the next source.", provider.Name, symbol);
        }

        _logger.LogWarning("No price source answered for {Symbol} ({Count} tried).", symbol, _providers.Count);
        return null;
    }
}
