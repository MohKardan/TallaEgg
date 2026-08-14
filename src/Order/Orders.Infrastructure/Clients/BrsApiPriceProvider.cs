using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// brsapi.ir — melted 18k gold, the Bahar Azadi coin, and Bitcoin, all from one authenticated
/// endpoint (<c>Gold_Currency.php</c>), keyed with an API key in the query string. Same product
/// as nerkh.io's gold/coin instruments — cross-checked live and agreed to within 0.1% before
/// this was written, which is what makes the two a meaningful fallback pair.
///
/// <para>
/// The instrument each symbol maps to (which of brsapi's response arrays, and which symbol
/// inside it) is a compiled default for the three symbols this platform trades today, and falls
/// back to <c>Symbols:{symbol}:BrsApi</c> in configuration for anything else — see
/// <see cref="InstrumentFor"/>.
/// </para>
/// </summary>
public class BrsApiPriceProvider : IReferencePriceProvider
{
    private const string Url = "https://Api.BrsApi.ir/Market/Gold_Currency.php";

    private readonly HttpClient _httpClient;
    private readonly ILogger<BrsApiPriceProvider> _logger;
    private readonly IConfiguration _configuration;

    public string Name => "brsapi.ir";

    public BrsApiPriceProvider(HttpClient httpClient, ILogger<BrsApiPriceProvider> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var apiKey = _configuration["AutoQuote:BrsApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("brsapi.ir skipped: no API key configured (Services:Orders.Api:AutoQuote:BrsApiKey).");
            return null;
        }

        var instrument = InstrumentFor(symbol);
        if (instrument is null)
        {
            _logger.LogWarning("brsapi.ir has no configured instrument for {Symbol}.", symbol);
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync($"{Url}?key={apiKey}", cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("brsapi.ir returned {StatusCode}: {Body}", (int)response.StatusCode, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var (array, instrumentSymbol, convertFromMesghal) = instrument.Value;

            // brsapi's "cryptocurrency" array is USD-denominated (a JSON string) and needs a
            // USD/Toman conversion from the same response's "currency" array; "gold" is already
            // Toman (a JSON number) and covers both metal and coin instruments alike.
            return array == "cryptocurrency"
                ? ConvertCryptoToToman(doc, instrumentSymbol)
                : FindGoldPrice(doc, instrumentSymbol, convertFromMesghal);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "brsapi.ir request failed for {Symbol}.", symbol);
            return null;
        }
    }

    /// <summary>
    /// Maps our trading-pair symbol to brsapi.ir's response array and instrument symbol. The
    /// three symbols traded today are compiled defaults; anything else is looked up under
    /// <c>Symbols:{symbol}:BrsApi</c> (fields: <c>Array</c> — "gold" or "cryptocurrency" —
    /// <c>Symbol</c>, and the optional bool <c>ConvertFromMesghal</c>) in configuration.
    /// </summary>
    private (string Array, string Symbol, bool ConvertFromMesghal)? InstrumentFor(string symbol)
    {
        (string, string, bool)? compiled = symbol switch
        {
            CurrenciesConstant.MAUA_IRT => ("gold", "IR_GOLD_MELTED", true),
            CurrenciesConstant.SEKE_BAHAR_IRT => ("gold", "IR_COIN_BAHAR", false),
            CurrenciesConstant.BTC_IRT => ("cryptocurrency", "BTC", false),
            _ => null
        };
        if (compiled is not null) return compiled;

        var section = _configuration.GetSection($"Symbols:{symbol}:BrsApi");
        var array = section["Array"];
        var instrumentSymbol = section["Symbol"];
        if (string.IsNullOrWhiteSpace(array) || string.IsNullOrWhiteSpace(instrumentSymbol)) return null;

        return (array, instrumentSymbol, section.GetValue("ConvertFromMesghal", false));
    }

    /// <summary>Toman-denominated instruments — the "gold" array covers metal and coins alike.</summary>
    private decimal? FindGoldPrice(JsonDocument doc, string targetSymbol, bool convertFromMesghal)
    {
        foreach (var item in doc.RootElement.GetProperty("gold").EnumerateArray())
        {
            if (item.GetProperty("symbol").GetString() != targetSymbol) continue;

            // brsapi returns gold prices as a JSON number, unlike nerkh's string — each
            // provider parses its own shape rather than forcing a shared response type onto two
            // APIs that were never designed to agree.
            var price = item.GetProperty("price").GetDecimal();
            return convertFromMesghal ? price / CurrenciesConstant.GramsPerMesghal : price;
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
