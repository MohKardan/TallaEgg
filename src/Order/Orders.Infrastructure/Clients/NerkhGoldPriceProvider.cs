using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orders.Core;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// nerkh.io — melted 18k gold ("آبشده") priced per mesghal (<c>MESGHAL</c>), authenticated with
/// a bearer token. <c>https://docs.nerkh.io/</c> (OpenAPI spec) is the source of the endpoint
/// shape below; verified live against the real token before this was written.
/// </summary>
public class NerkhGoldPriceProvider : IGoldPriceProvider
{
    private const string Endpoint = "https://api.nerkh.io/v2/prices/json/gold/MESGHAL";

    private readonly HttpClient _httpClient;
    private readonly ILogger<NerkhGoldPriceProvider> _logger;
    private readonly string? _apiToken;

    public string Name => "nerkh.io";

    public NerkhGoldPriceProvider(HttpClient httpClient, ILogger<NerkhGoldPriceProvider> logger, string? apiToken)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiToken = apiToken;
    }

    public async Task<decimal?> GetMesghalPriceAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiToken))
        {
            _logger.LogWarning("nerkh.io skipped: no API token configured (Services:Orders.Api:AutoQuote:NerkhApiToken).");
            return null;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, Endpoint);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("nerkh.io returned {StatusCode}: {Body}", (int)response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);

            // data.prices.MESGHAL.current — a string, not a number, in nerkh's own schema.
            var current = doc.RootElement
                .GetProperty("data")
                .GetProperty("prices")
                .GetProperty("MESGHAL")
                .GetProperty("current")
                .GetString();

            if (!decimal.TryParse(current, out var price))
            {
                _logger.LogWarning("nerkh.io returned an unparsable price: {Current}", current);
                return null;
            }

            return price;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "nerkh.io request failed.");
            return null;
        }
    }
}
