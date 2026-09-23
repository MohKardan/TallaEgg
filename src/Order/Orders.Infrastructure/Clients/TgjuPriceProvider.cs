using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;
using TallaEgg.Core.Utilties;

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
    private readonly ReferencePriceDocumentCache _cache;

    public string Name => "tgju.org";

    public TgjuPriceProvider(
        HttpClient httpClient,
        ILogger<TgjuPriceProvider> logger,
        IConfiguration configuration,
        ReferencePriceDocumentCache cache)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<ReferencePrice?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var instrument = InstrumentFor(symbol);
        if (instrument is null)
        {
            _logger.LogWarning("tgju.org has no configured instrument for {Symbol}.", symbol);
            return null;
        }

        var (key, convertFromMesghal) = instrument.Value;
        var quoted = await FetchAsync(key, cancellationToken);
        if (quoted is null) return null;

        var toman = quoted.Value.Rials / RialsPerToman;
        var price = convertFromMesghal ? toman / CurrenciesConstant.GramsPerMesghal : toman;

        return new ReferencePrice(price, quoted.Value.AsOf);
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

    private async Task<(decimal Rials, DateTimeOffset? AsOf)?> FetchAsync(string key, CancellationToken cancellationToken)
    {
        try
        {
            var body = await DocumentAsync(cancellationToken);
            if (body is null) return null;

            using var doc = JsonDocument.Parse(body);

            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("current", out var current) ||
                current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(key, out var instrument) ||
                instrument.ValueKind != JsonValueKind.Object ||
                !instrument.TryGetProperty("p", out var price))
            {
                _logger.LogWarning("tgju.org response did not contain current.{Key}.p.", key);
                return null;
            }

            // "1,026,980,000" — a string with thousands separators, not a JSON number. The raw
            // text is read if it ever arrives as one, so that shape is reported as the price
            // problem it is rather than throwing and being logged as a failed request.
            var text = price.ValueKind == JsonValueKind.String ? price.GetString() : price.GetRawText();
            if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
            {
                _logger.LogWarning("tgju.org returned an unparsable price for {Key}: {Price}", key, text);
                return null;
            }

            return (parsed, TimestampOf(instrument, key));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "tgju.org request failed for {Key}.", key);
            return null;
        }
    }

    /// <summary>
    /// When tgju says this instrument last moved, from its <c>ts</c> field.
    ///
    /// <para>
    /// The value is Tehran local time with no zone marker — <c>"2026-09-22 08:05:29"</c> — so the
    /// fixed +03:30 offset every date in this system already uses is applied (see
    /// <c>Utils.TehranOffset</c>; Iran has observed no daylight saving since 2022).
    /// </para>
    ///
    /// <para>
    /// Gold and the coin carry a date with a midnight time while the market is shut
    /// (<c>"2026-09-21 00:00:00"</c> at eight the next morning), which is precisely the state a
    /// staleness check exists to notice. A missing or unreadable <c>ts</c> is reported as an
    /// unknown age, not as now.
    /// </para>
    /// </summary>
    private DateTimeOffset? TimestampOf(JsonElement instrument, string key)
    {
        if (!instrument.TryGetProperty("ts", out var ts) || ts.ValueKind != JsonValueKind.String)
        {
            _logger.LogWarning("tgju.org gave no timestamp for {Key}; its price will be treated as of unknown age.", key);
            return null;
        }

        if (!DateTime.TryParseExact(ts.GetString(), "yyyy-MM-dd HH:mm:ss",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out var tehranTime))
        {
            _logger.LogWarning("tgju.org returned an unreadable timestamp for {Key}: {Timestamp}", key, ts.GetString());
            return null;
        }

        try
        {
            return new DateTimeOffset(tehranTime, Utils.TehranOffset);
        }
        catch (ArgumentOutOfRangeException)
        {
            // A date that parses but cannot carry an offset — "0001-01-01 00:00:00", the sentinel
            // a source might use for "never" — would otherwise throw into FetchAsync's catch and
            // lose the price along with the timestamp. An unreadable time costs the age, not the
            // price.
            _logger.LogWarning("tgju.org returned a timestamp out of range for {Key}: {Timestamp}", key, ts.GetString());
            return null;
        }
    }

    /// <summary>
    /// The whole document, fetched at most once per cache lifetime however many symbols ask.
    /// One response carries every instrument, so pulling it per symbol would be the same ~180 KB
    /// three times a tick — see <see cref="ReferencePriceDocumentCache"/>.
    /// </summary>
    private Task<string?> DocumentAsync(CancellationToken cancellationToken) =>
        _cache.GetOrFetchAsync(Name, FetchDocumentAsync, cancellationToken);

    private async Task<string?> FetchDocumentAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(Url, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            // Truncated: the document is ~180 KB, and an error page can be just as long.
            _logger.LogWarning("tgju.org returned {StatusCode}: {Body}", (int)response.StatusCode, Truncate(body));
            return null;
        }

        return body;
    }

    private static string Truncate(string body) =>
        body.Length <= 500 ? body : body[..500] + "…";
}
