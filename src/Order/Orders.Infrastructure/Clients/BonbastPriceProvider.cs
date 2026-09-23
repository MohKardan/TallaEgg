using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// bonbast.com — melted gold per mesghal and the Bahar Azadi coin, in Toman, with no key. Like
/// <see cref="TgjuPriceProvider"/> it answers from outside Iran, and it is an independent
/// operation rather than a second reading of the same upstream, which is what makes the pair a
/// real fallback for each other (issue #305).
///
/// <para>
/// Two requests, not one. The JSON endpoint only answers a request carrying a token minted into
/// the home page, so this fetches the page, reads the token out of it, and posts it back. That
/// makes the provider dependent on the page's markup: when the token cannot be found the whole
/// fetch is abandoned and the chain moves on, exactly as if the host were down.
/// </para>
///
/// <para>
/// Bitcoin is deliberately unmapped here. bonbast quotes it in USD, so BTC/IRT would need a
/// second conversion through the same response's dollar rate; tgju.org already answers BTC/IRT
/// directly in Rial, and a conversion nobody needs is a conversion that can be wrong.
/// </para>
/// </summary>
public class BonbastPriceProvider : IReferencePriceProvider
{
    private const string PageUrl = "https://www.bonbast.com/";
    private const string JsonUrl = "https://www.bonbast.com/json";

    /// <summary>
    /// The token the JSON endpoint requires, as the page embeds it: <c>param: "…"</c>. Bounded
    /// rather than greedy so a second occurrence later in the page cannot extend the match.
    /// </summary>
    private static readonly Regex ParamPattern =
        new("param:\\s*\"(?<value>[^\"]{1,200})\"", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly HttpClient _httpClient;
    private readonly ILogger<BonbastPriceProvider> _logger;
    private readonly IConfiguration _configuration;
    private readonly ReferencePriceDocumentCache _cache;

    public string Name => "bonbast.com";

    public BonbastPriceProvider(
        HttpClient httpClient,
        ILogger<BonbastPriceProvider> logger,
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
            _logger.LogWarning("bonbast.com has no configured instrument for {Symbol}.", symbol);
            return null;
        }

        var (sellKey, buyKey, convertFromMesghal) = instrument.Value;

        var document = await FetchAsync(cancellationToken);
        if (document is null) return null;

        var prices = document.Value;
        var sell = Read(prices, sellKey);
        if (sell is null) return null;

        // Coins are quoted as a dealer's own buy and sell. The mid of the two is the neutral
        // reference this platform then applies its own spread to; taking either leg would import
        // bonbast's spread on top of ours. Instruments quoted once (the mesghal) have no buy key
        // and use the single value as-is.
        var price = sell.Value;
        if (!string.IsNullOrWhiteSpace(buyKey))
        {
            var buy = Read(prices, buyKey);
            if (buy is null) return null;

            price = (sell.Value + buy.Value) / 2m;
        }

        var toman = convertFromMesghal ? price / CurrenciesConstant.GramsPerMesghal : price;

        // AsOf is null rather than "now": the response carries "last_modified", but as a bare
        // "September 19, 2026 11:56" with no zone marker, and bonbast does not say which zone it
        // means. A timestamp read in the wrong zone is an age wrong by hours, which is worse for
        // a staleness check than no age at all (issue #316).
        return new ReferencePrice(toman, null);
    }

    /// <summary>
    /// Maps our trading-pair symbol to bonbast's JSON keys. The two symbols it covers are
    /// compiled defaults; anything else is looked up under <c>Symbols:{symbol}:Bonbast</c>
    /// (fields: <c>Key</c>, the optional <c>BuyKey</c>, and the optional bool
    /// <c>ConvertFromMesghal</c>) in configuration.
    /// </summary>
    private (string SellKey, string? BuyKey, bool ConvertFromMesghal)? InstrumentFor(string symbol)
    {
        (string, string?, bool)? compiled = symbol switch
        {
            CurrenciesConstant.MAUA_IRT => ("mithqal", null, true),
            CurrenciesConstant.SEKE_BAHAR_IRT => ("azadi1", "azadi12", false),
            _ => null
        };
        if (compiled is not null) return compiled;

        var section = _configuration.GetSection($"Symbols:{symbol}:Bonbast");
        var key = section["Key"];
        if (string.IsNullOrWhiteSpace(key)) return null;

        return (key, section["BuyKey"], section.GetValue("ConvertFromMesghal", false));
    }

    private decimal? Read(JsonElement prices, string key)
    {
        if (!prices.TryGetProperty(key, out var element))
        {
            _logger.LogWarning("bonbast.com response did not contain {Key}.", key);
            return null;
        }

        // Every value in this response is a JSON string, prices included ("233000000").
        var text = element.ValueKind == JsonValueKind.String ? element.GetString() : element.GetRawText();

        if (!decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            _logger.LogWarning("bonbast.com returned an unparsable price for {Key}: {Price}", key, text);
            return null;
        }

        return parsed;
    }

    /// <summary>
    /// The page-then-JSON pair, or the body a recent pair already produced. One document holds
    /// every instrument and reaching it costs two requests, so the whole handshake happens once per
    /// source per tick however many symbols ask and whether they ask at the same moment — see
    /// <see cref="ReferencePriceDocumentCache"/>.
    /// </summary>
    private async Task<JsonElement?> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            var body = await _cache.GetOrFetchAsync(Name, FetchDocumentAsync, cancellationToken);
            return body is null ? null : Parse(body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "bonbast.com request failed.");
            return null;
        }
    }

    private async Task<string?> FetchDocumentAsync(CancellationToken cancellationToken)
    {
        var token = await FetchTokenAsync(cancellationToken);
        if (token is null) return null;

        using var request = new HttpRequestMessage(HttpMethod.Post, JsonUrl)
        {
            Content = new FormUrlEncodedContent(new[] { new KeyValuePair<string, string>("param", token) })
        };
        request.Headers.Referrer = new Uri(PageUrl);
        request.Headers.Add("X-Requested-With", "XMLHttpRequest");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("bonbast.com returned {StatusCode} for the price document.", (int)response.StatusCode);
            return null;
        }

        // Only a document that parses is worth storing: a 200 carrying something else would
        // otherwise be served to every other symbol in the tick.
        return Parse(body) is null ? null : body;
    }

    /// <summary>
    /// The prices object, or null if the body is not one.
    ///
    /// <para>
    /// The <see cref="JsonElement"/> is cloned because its <see cref="JsonDocument"/> is disposed
    /// before this returns, and reading a disposed document's element throws — a failure that
    /// would read as a parsing bug rather than a lifetime one. The object check matters for the
    /// same reason the clone does: on valid JSON that is not an object — an empty array, a bare
    /// string — <c>TryGetProperty</c> throws instead of returning false, and at the one call site
    /// that reads keys there is no catch left between here and the chain, which has none of its
    /// own. The interface promises a null for every failure.
    /// </para>
    /// </summary>
    private JsonElement? Parse(string body)
    {
        using var doc = JsonDocument.Parse(body);

        if (doc.RootElement.ValueKind != JsonValueKind.Object)
        {
            _logger.LogWarning("bonbast.com returned {Kind} where an object of prices was expected.", doc.RootElement.ValueKind);
            return null;
        }

        return doc.RootElement.Clone();
    }

    private async Task<string?> FetchTokenAsync(CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(PageUrl, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("bonbast.com returned {StatusCode} for its home page.", (int)response.StatusCode);
            return null;
        }

        var page = await response.Content.ReadAsStringAsync(cancellationToken);
        var match = ParamPattern.Match(page);

        if (!match.Success)
        {
            // The markup changed, or something other than the site answered. Either way there is
            // no point posting: the endpoint refuses a request without the token.
            _logger.LogWarning("bonbast.com home page carried no request token; the page markup may have changed.");
            return null;
        }

        return match.Groups["value"].Value;
    }
}
