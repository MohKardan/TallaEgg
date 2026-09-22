using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// tgju.org — melted gold ("مثقال آبشده"), the Bahar Azadi coin and Bitcoin, from the single
/// unauthenticated document its own pages poll. No key, and it answers from outside Iran, which
/// is the whole point: nerkh.io and brsapi.ir refuse non-Iranian IPs, so on a foreign-hosted
/// instance they are the two providers that can never answer (issue #305).
///
/// <para>
/// This is not a published API. tgju sells one; <c>call1.tgju.org/ajax.json</c> is what its site
/// reads, so the response shape can change without notice and the host may block a datacenter
/// range. That is why it is registered <b>after</b> nerkh.io and brsapi.ir rather than in front
/// of them: where those two work, nothing about the price a customer sees changes.
/// </para>
///
/// <para>
/// Two unit conversions are this provider's own, not the caller's: tgju quotes <b>Rial</b> while
/// every stored price is Toman, and it quotes gold per mesghal while MAUA/IRT trades per gram.
/// </para>
/// </summary>
public class TgjuPriceProvider : IReferencePriceProvider
{
    private const string Url = "https://call1.tgju.org/ajax.json";

    /// <summary>Rial per Toman. tgju publishes Rial; the entire platform stores Toman.</summary>
    private const decimal RialsPerToman = 10m;

    private readonly HttpClient _httpClient;
    private readonly ILogger<TgjuPriceProvider> _logger;
    private readonly IConfiguration _configuration;

    public string Name => "tgju.org";

    public TgjuPriceProvider(HttpClient httpClient, ILogger<TgjuPriceProvider> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var instrument = InstrumentFor(symbol);
        if (instrument is null)
        {
            _logger.LogWarning("tgju.org has no configured instrument for {Symbol}.", symbol);
            return null;
        }

        var (key, convertFromMesghal) = instrument.Value;
        var rials = await FetchAsync(key, cancellationToken);
        if (rials is null) return null;

        var toman = rials.Value / RialsPerToman;
        return convertFromMesghal ? toman / CurrenciesConstant.GramsPerMesghal : toman;
    }

    /// <summary>
    /// Maps our trading-pair symbol to tgju's instrument key. The three symbols traded today are
    /// compiled defaults; anything else is looked up under <c>Symbols:{symbol}:Tgju</c> (fields:
    /// <c>Key</c>, and the optional bool <c>ConvertFromMesghal</c>) in configuration.
    ///
    /// <para>
    /// <c>crypto-bitcoin-irr</c>, not <c>crypto-bitcoin</c>: the latter is the USD price, and
    /// there is no marker in the response saying which currency an entry is in. A config block
    /// naming a USD key would therefore be read as Rial and produce a price roughly four orders
    /// of magnitude too small, which is the one mistake this mapping can make silently.
    /// </para>
    /// </summary>
    private (string Key, bool ConvertFromMesghal)? InstrumentFor(string symbol)
    {
        (string, bool)? compiled = symbol switch
        {
            CurrenciesConstant.MAUA_IRT => ("mesghal", true),
            CurrenciesConstant.SEKE_BAHAR_IRT => ("sekeb", false),
            CurrenciesConstant.BTC_IRT => ("crypto-bitcoin-irr", false),
            _ => null
        };
        if (compiled is not null) return compiled;

        var section = _configuration.GetSection($"Symbols:{symbol}:Tgju");
        var key = section["Key"];
        if (string.IsNullOrWhiteSpace(key)) return null;

        return (key, section.GetValue("ConvertFromMesghal", false));
    }

    private async Task<decimal?> FetchAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await _httpClient.GetAsync(Url, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                // Truncated: the document is ~180 KB, and an error page can be just as long.
                _logger.LogWarning("tgju.org returned {StatusCode}: {Body}", (int)response.StatusCode, Truncate(body));
                return null;
            }

            using var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("current", out var current) ||
                !current.TryGetProperty(key, out var instrument) ||
                !instrument.TryGetProperty("p", out var price))
            {
                _logger.LogWarning("tgju.org response did not contain current.{Key}.p.", key);
                return null;
            }

            // "1,026,980,000" — a string with thousands separators, not a JSON number.
            var text = price.GetString();
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                _logger.LogWarning("tgju.org returned an unparsable price for {Key}: {Price}", key, text);
                return null;
            }

            return parsed;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "tgju.org request failed for {Key}.", key);
            return null;
        }
    }

    private static string Truncate(string body) =>
        body.Length <= 500 ? body : body[..500] + "…";
}
