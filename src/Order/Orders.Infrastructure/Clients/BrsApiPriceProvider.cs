using System.Text.Json;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// brsapi.ir — melted 18k gold, the Bahar Azadi coin, and Bitcoin, all from one authenticated
/// endpoint (<c>Gold_Currency.php</c>), keyed with an API key in the query string. Same product
/// as nerkh.io's gold/coin instruments — cross-checked live and agreed to within 0.1% before
/// this was written, which is what makes the two a meaningful fallback pair.
/// </summary>
public class BrsApiPriceProvider : IReferencePriceProvider
{
    private const string Url = "https://Api.BrsApi.ir/Market/Gold_Currency.php";

    private readonly HttpClient _httpClient;
    private readonly ILogger<BrsApiPriceProvider> _logger;
    private readonly string? _apiKey;

    public string Name => "brsapi.ir";

    public BrsApiPriceProvider(HttpClient httpClient, ILogger<BrsApiPriceProvider> logger, string? apiKey)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = apiKey;
    }

    public async Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("brsapi.ir skipped: no API key configured (Services:Orders.Api:AutoQuote:BrsApiKey).");
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync($"{Url}?key={_apiKey}", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("brsapi.ir returned {StatusCode}: {Body}", (int)response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);

            switch (symbol)
            {
                case CurrenciesConstant.MAUA_IRT:
                    var melted = FindGoldPrice(doc, "IR_GOLD_MELTED");
                    return melted is null ? null : melted / CurrenciesConstant.GramsPerMesghal;

                case CurrenciesConstant.SEKE_BAHAR_IRT:
                    return FindGoldPrice(doc, "IR_COIN_BAHAR");

                case CurrenciesConstant.BTC_IRT:
                    return ConvertCryptoToToman(doc, "BTC");

                default:
                    _logger.LogWarning("brsapi.ir has no configured instrument for {Symbol}.", symbol);
                    return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "brsapi.ir request failed for {Symbol}.", symbol);
            return null;
        }
    }

    /// <summary>Toman-denominated instruments — the "gold" array covers metal and coins alike.</summary>
    private decimal? FindGoldPrice(JsonDocument doc, string targetSymbol)
    {
        foreach (var item in doc.RootElement.GetProperty("gold").EnumerateArray())
        {
            if (item.GetProperty("symbol").GetString() != targetSymbol) continue;

            // brsapi returns gold prices as a JSON number, unlike nerkh's string — each
            // provider parses its own shape rather than forcing a shared response type onto two
            // APIs that were never designed to agree.
            return item.GetProperty("price").GetDecimal();
        }

        _logger.LogWarning("brsapi.ir response did not contain gold symbol {Symbol}.", targetSymbol);
        return null;
    }

    /// <summary>
    /// Crypto instruments are priced in USD, not Toman — brsapi.ir has no direct crypto/Toman
    /// pair. The same response carries the USD/Toman rate in its "currency" array, so the two
    /// are multiplied here rather than asking every caller to know this API's quirk.
    /// </summary>
    private decimal? ConvertCryptoToToman(JsonDocument doc, string targetSymbol)
    {
        decimal? usdPrice = null;
        foreach (var item in doc.RootElement.GetProperty("cryptocurrency").EnumerateArray())
        {
            if (item.GetProperty("symbol").GetString() != targetSymbol) continue;

            // Unlike gold, brsapi returns crypto prices as a JSON string (e.g. "63232").
            if (decimal.TryParse(item.GetProperty("price").GetString(), out var parsed))
                usdPrice = parsed;
            break;
        }

        if (usdPrice is null)
        {
            _logger.LogWarning("brsapi.ir response did not contain cryptocurrency symbol {Symbol}.", targetSymbol);
            return null;
        }

        decimal? usdToToman = null;
        foreach (var item in doc.RootElement.GetProperty("currency").EnumerateArray())
        {
            if (item.GetProperty("symbol").GetString() != "USD") continue;
            usdToToman = item.GetProperty("price").GetDecimal();
            break;
        }

        if (usdToToman is null)
        {
            _logger.LogWarning("brsapi.ir response did not contain a USD/Toman rate to convert {Symbol}.", targetSymbol);
            return null;
        }

        return usdPrice * usdToToman;
    }
}
