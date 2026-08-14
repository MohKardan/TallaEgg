using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// nerkh.io — melted 18k gold ("آبشده"), the Bahar Azadi coin, and Bitcoin, all authenticated
/// with the same bearer token. <c>https://docs.nerkh.io/</c> (OpenAPI spec) is the source of the
/// endpoint shapes below; each verified live against the real token before this was written.
/// </summary>
public class NerkhPriceProvider : IReferencePriceProvider
{
    private const string BaseUrl = "https://api.nerkh.io/v2/prices/json/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<NerkhPriceProvider> _logger;
    private readonly string? _apiToken;

    public string Name => "nerkh.io";

    public NerkhPriceProvider(HttpClient httpClient, ILogger<NerkhPriceProvider> logger, string? apiToken)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiToken = apiToken;
    }

    public async Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            _logger.LogWarning("nerkh.io skipped: no API token configured (Services:Orders.Api:AutoQuote:NerkhApiToken).");
            return null;
        }

        var instrument = InstrumentFor(symbol);
        if (instrument is null)
        {
            _logger.LogWarning("nerkh.io has no configured instrument for {Symbol}.", symbol);
            return null;
        }

        var (path, key) = instrument.Value;
        var price = await FetchAsync(path, key, cancellationToken);
        if (price is null) return null;

        // MESGHAL is nerkh's native gold unit; quotes for MAUA/IRT are stored per gram. The
        // coin and Bitcoin instruments are already priced per the whole unit we trade, so no
        // further conversion applies to them.
        return key == "MESGHAL" ? price / CurrenciesConstant.GramsPerMesghal : price;
    }

    /// <summary>Maps our trading-pair symbol to nerkh.io's endpoint category and instrument key.</summary>
    private static (string Path, string Key)? InstrumentFor(string symbol) => symbol switch
    {
        CurrenciesConstant.MAUA_IRT => ("gold/MESGHAL", "MESGHAL"),
        CurrenciesConstant.SEKE_BAHAR_IRT => ("gold/SEKE_BAHAR", "SEKE_BAHAR"),
        CurrenciesConstant.BTC_IRT => ("crypto/BTC", "BTC"),
        _ => null
    };

    private async Task<decimal?> FetchAsync(string path, string key, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("nerkh.io returned {StatusCode}: {Body}", (int)response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);

            // data.prices.{key}.current — a string, not a number, in nerkh's own schema.
            var current = doc.RootElement
                .GetProperty("data")
                .GetProperty("prices")
                .GetProperty(key)
                .GetProperty("current")
                .GetString();

            if (!decimal.TryParse(current, out var price))
            {
                _logger.LogWarning("nerkh.io returned an unparsable price for {Key}: {Current}", key, current);
                return null;
            }

            return price;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "nerkh.io request failed for {Key}.", key);
            return null;
        }
    }
}
