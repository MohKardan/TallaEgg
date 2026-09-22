using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// xaus.com — the XAU/USD spot price, in dollars per troy ounce, with no key (issue #304).
///
/// <para>
/// The first provider here whose price is not in Toman. Nothing about that is special-cased: the
/// interface has always promised "the symbol's quote currency per unit of its base asset", and
/// for XAU/USD that currency is the dollar.
/// </para>
///
/// <para>
/// Reachability runs the opposite way to the Iranian sources. nerkh.io and brsapi.ir refuse
/// non-Iranian IPs; this answers from the production VM and from Iran alike, which is what makes
/// the ounce the one symbol that can be auto-quoted on the server as it stands today.
/// </para>
///
/// <para>
/// Its own upstream is gold-api.com, which is why gold-api is not the fallback behind it: that
/// would be one source read twice. Swissquote is an independent feed — see
/// <see cref="SwissquotePriceProvider"/>.
/// </para>
/// </summary>
public class XausPriceProvider : IReferencePriceProvider
{
    private const string Url = "https://xaus.com/api/v1/spot";

    private readonly HttpClient _httpClient;
    private readonly ILogger<XausPriceProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly ReferencePriceDocumentCache _cache;

    public string Name => "xaus.com";

    public XausPriceProvider(
        HttpClient httpClient,
        ILogger<XausPriceProvider> logger,
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
        var field = FieldFor(symbol);
        if (field is null)
        {
            _logger.LogWarning("xaus.com has no configured instrument for {Symbol}.", symbol);
            return null;
        }

        var body = await DocumentAsync(cancellationToken);
        if (body is null) return null;

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

            if (!doc.RootElement.TryGetProperty(field, out var price) || price.ValueKind != JsonValueKind.Number)
            {
                _logger.LogWarning("xaus.com response did not contain a numeric {Field}.", field);
                return null;
            }

            // "stale": true means xaus is serving its last known real price through an upstream
            // outage. The price is still returned — deciding what to do with an old price belongs
            // to the chain's age check, which reads the timestamp below — but it is worth a line
            // of its own, because the timestamp alone does not say the source knows it is behind.
            if (doc.RootElement.TryGetProperty("stale", out var stale) && stale.ValueKind == JsonValueKind.True)
                _logger.LogWarning("xaus.com reports its own price as stale; its upstream may be down.");

            return new ReferencePrice(price.GetDecimal(), TimestampOf(doc.RootElement));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "xaus.com response could not be read.");
            return null;
        }
    }

    /// <summary>
    /// Which field of the one response holds this symbol's price. XAU/USD is compiled in; anything
    /// else is looked up under <c>Symbols:{symbol}:Xaus</c> (field: <c>Field</c>) — the same
    /// response also carries <c>per_gram_usd</c>, <c>per_kg_usd</c> and <c>silver_usd_oz</c>, so a
    /// gram- or kilo-denominated gold symbol would need no code here.
    /// </summary>
    private string? FieldFor(string symbol)
    {
        if (symbol == CurrenciesConstant.XAU_USD) return "spot_usd_oz";

        var field = _configuration[$"Symbols:{symbol}:Xaus:Field"];
        return string.IsNullOrWhiteSpace(field) ? null : field;
    }

    /// <summary>
    /// When xaus says the price was sourced. Unlike the Iranian feeds this is ISO 8601 with an
    /// explicit <c>Z</c>, so no zone has to be assumed. <c>price_as_of</c> rather than
    /// <c>updated_at</c>: the two differ exactly when the price is stale, and the older of them is
    /// the one that says how old the price really is.
    /// </summary>
    private DateTimeOffset? TimestampOf(JsonElement root)
    {
        if (!root.TryGetProperty("price_as_of", out var asOf) || asOf.ValueKind != JsonValueKind.String)
        {
            _logger.LogWarning("xaus.com gave no price_as_of; its price will be treated as of unknown age.");
            return null;
        }

        if (!DateTimeOffset.TryParse(asOf.GetString(), System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var parsed))
        {
            _logger.LogWarning("xaus.com returned an unreadable price_as_of: {Timestamp}", asOf.GetString());
            return null;
        }

        return parsed;
    }

    private async Task<string?> DocumentAsync(CancellationToken cancellationToken)
    {
        var cached = _cache.Get(Name);
        if (cached is not null) return cached;

        try
        {
            using var response = await _httpClient.GetAsync(Url, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("xaus.com returned {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            _cache.Set(Name, body);
            return body;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "xaus.com request failed.");
            return null;
        }
    }
}
