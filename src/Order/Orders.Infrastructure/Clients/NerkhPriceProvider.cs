using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// nerkh.io — melted 18k gold ("آبشده"), the Bahar Azadi coin, and Bitcoin, all authenticated
/// with the same bearer token. <c>https://docs.nerkh.io/</c> (OpenAPI spec) is the source of the
/// endpoint shapes below; each verified live against the real token before this was written.
///
/// <para>
/// The instrument each symbol maps to (nerkh's endpoint path, its JSON property key, and
/// whether the price needs converting from mesghal) is a compiled default for the three symbols
/// this platform trades today, and falls back to <c>Symbols:{symbol}:Nerkh</c> in configuration
/// for anything else — so a new symbol nerkh.io also happens to price needs no code change here,
/// only a config block (see <see cref="InstrumentFor"/>).
/// </para>
/// </summary>
public class NerkhPriceProvider : IReferencePriceProvider
{
    private const string BaseUrl = "https://api.nerkh.io/v2/prices/json/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<NerkhPriceProvider> _logger;
    private readonly IConfiguration _configuration;

    public string Name => "nerkh.io";

    public NerkhPriceProvider(HttpClient httpClient, ILogger<NerkhPriceProvider> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var apiToken = _configuration["AutoQuote:NerkhApiToken"];
        if (string.IsNullOrWhiteSpace(apiToken))
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

        var (path, key, convertFromMesghal) = instrument.Value;
        var price = await FetchAsync(path, key, apiToken, cancellationToken);
        if (price is null) return null;

        // MESGHAL is nerkh's native gold unit; quotes for MAUA/IRT are stored per gram. Every
        // other instrument (coins, crypto) is already priced per the whole unit we trade.
        return convertFromMesghal ? price / CurrenciesConstant.GramsPerMesghal : price;
    }

    /// <summary>
    /// Maps our trading-pair symbol to nerkh.io's endpoint category and instrument key. The
    /// three symbols traded today are compiled defaults; anything else is looked up under
    /// <c>Symbols:{symbol}:Nerkh</c> (fields: <c>Path</c>, <c>Key</c>, and the optional bool
    /// <c>ConvertFromMesghal</c>) in configuration.
    /// </summary>
    private (string Path, string Key, bool ConvertFromMesghal)? InstrumentFor(string symbol)
    {
        (string, string, bool)? compiled = symbol switch
        {
            CurrenciesConstant.MAUA_IRT => ("gold/MESGHAL", "MESGHAL", true),
            CurrenciesConstant.SEKE_BAHAR_IRT => ("gold/SEKE_BAHAR", "SEKE_BAHAR", false),
            CurrenciesConstant.BTC_IRT => ("crypto/BTC", "BTC", false),
            _ => null
        };
        if (compiled is not null) return compiled;

        var section = _configuration.GetSection($"Symbols:{symbol}:Nerkh");
        var path = section["Path"];
        var key = section["Key"];
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(key)) return null;

        return (path, key, section.GetValue("ConvertFromMesghal", false));
    }

    private async Task<decimal?> FetchAsync(string path, string key, string apiToken, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, BaseUrl + path);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiToken);

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
