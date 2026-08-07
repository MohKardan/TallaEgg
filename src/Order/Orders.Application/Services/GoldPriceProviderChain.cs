using Microsoft.Extensions.Logging;
using Orders.Core;

namespace Orders.Application.Services;

/// <summary>
/// Tries each configured <see cref="IGoldPriceProvider"/> in order and returns the first price
/// that answers. Adding a third or fourth source is registering one more provider in DI, in the
/// order it should be tried — nothing here changes.
/// </summary>
public class GoldPriceProviderChain
{
    private readonly IReadOnlyList<IGoldPriceProvider> _providers;
    private readonly ILogger<GoldPriceProviderChain> _logger;

    public GoldPriceProviderChain(IEnumerable<IGoldPriceProvider> providers, ILogger<GoldPriceProviderChain> logger)
    {
        _providers = providers.ToList();
        _logger = logger;
    }

    public async Task<decimal?> GetMesghalPriceAsync(CancellationToken cancellationToken = default)
    {
        foreach (var provider in _providers)
        {
            var price = await provider.GetMesghalPriceAsync(cancellationToken);

            if (price is > 0)
            {
                _logger.LogInformation("Gold price {Price} obtained from {Provider}.", price, provider.Name);
                return price;
            }

            _logger.LogWarning("{Provider} did not answer; trying the next source.", provider.Name);
        }

        _logger.LogWarning("No gold price source answered ({Count} tried).", _providers.Count);
        return null;
    }
}
