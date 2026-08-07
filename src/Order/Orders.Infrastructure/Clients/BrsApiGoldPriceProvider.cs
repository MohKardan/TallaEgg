using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orders.Core;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// brsapi.ir — melted 18k gold ("طلای آب‌شده نقدی", symbol <c>IR_GOLD_MELTED</c>) priced per
/// mesghal, authenticated with an API key in the query string. Same product as nerkh.io's
/// <c>MESGHAL</c> — the two were cross-checked live and agreed to within 0.1% before this was
/// written, which is what makes them a meaningful fallback pair rather than two different
/// instruments that happen to both be called "gold".
/// </summary>
public class BrsApiGoldPriceProvider : IGoldPriceProvider
{
    private const string BaseUrl = "https://Api.BrsApi.ir/Market/Gold_Currency.php";
    private const string TargetSymbol = "IR_GOLD_MELTED";

    private readonly HttpClient _httpClient;
    private readonly ILogger<BrsApiGoldPriceProvider> _logger;
    private readonly string? _apiKey;

    public string Name => "brsapi.ir";

    public BrsApiGoldPriceProvider(HttpClient httpClient, ILogger<BrsApiGoldPriceProvider> logger, string? apiKey)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
    }

    public async Task<decimal?> GetMesghalPriceAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("brsapi.ir skipped: no API key configured (Services:Orders.Api:AutoQuote:BrsApiKey).");
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync($"{BaseUrl}?key={_apiKey}", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("brsapi.ir returned {StatusCode}: {Body}", (int)response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);

            foreach (var item in doc.RootElement.GetProperty("gold").EnumerateArray())
            {
                if (item.GetProperty("symbol").GetString() != TargetSymbol) continue;

                // brsapi returns price as a JSON number, unlike nerkh's string — each provider
                // parses its own shape rather than forcing a shared response type onto two
                // APIs that were never designed to agree.
                return item.GetProperty("price").GetDecimal();
            }

            _logger.LogWarning("brsapi.ir response did not contain symbol {Symbol}.", TargetSymbol);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "brsapi.ir request failed.");
            return null;
        }
    }
}
