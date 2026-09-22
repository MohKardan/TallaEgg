using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orders.Core;
using TallaEgg.Core;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// Swissquote's public quote feed — a real bank's bid and ask for XAU/USD, with no key
/// (issue #304). The fallback behind <see cref="XausPriceProvider"/>, and an independent one:
/// xaus.com's own upstream is gold-api.com, so pairing those two would be one source read twice.
///
/// <para>
/// This is not a published API. It is what Swissquote's own price pages read, so it can change
/// shape or stop answering without notice — the same standing as tgju.org and bonbast.com have
/// for the Toman symbols, and the reason it is second rather than first.
/// </para>
///
/// <para>
/// The response is an array of trading platforms, each carrying several spread profiles of the
/// same underlying market. This takes the mid of the first profile of the first platform: the
/// profiles differ only in how much spread is added around that mid, so their mids agree to about
/// a thousandth of a percent, and the mid is what this platform then applies its own spread to.
/// Taking a bid or an ask would import a bank's spread on top of ours.
/// </para>
/// </summary>
public class SwissquotePriceProvider : IReferencePriceProvider
{
    private const string BaseUrl = "https://forex-data-feed.swissquote.com/public-quotes/bboquotes/instrument/";

    private readonly HttpClient _httpClient;
    private readonly ILogger<SwissquotePriceProvider> _logger;
    private readonly IConfiguration _configuration;

    public string Name => "swissquote.com";

    public SwissquotePriceProvider(HttpClient httpClient, ILogger<SwissquotePriceProvider> logger, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _logger = logger;
        _configuration = configuration;
    }

    public async Task<ReferencePrice?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var instrument = InstrumentFor(symbol);
        if (instrument is null)
        {
            _logger.LogWarning("swissquote.com has no configured instrument for {Symbol}.", symbol);
            return null;
        }

        try
        {
            using var response = await _httpClient.GetAsync(BaseUrl + instrument, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("swissquote.com returned {StatusCode} for {Instrument}.", (int)response.StatusCode, instrument);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            return Read(doc.RootElement, instrument);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "swissquote.com request failed for {Instrument}.", instrument);
            return null;
        }
    }

    /// <summary>
    /// The instrument path, e.g. <c>XAU/USD</c>. Compiled for the ounce; anything else comes from
    /// <c>Symbols:{symbol}:Swissquote:Instrument</c>, which is how a second metal or an FX pair
    /// would be added without code.
    /// </summary>
    private string? InstrumentFor(string symbol)
    {
        if (symbol == CurrenciesConstant.XAU_USD) return "XAU/USD";

        var instrument = _configuration[$"Symbols:{symbol}:Swissquote:Instrument"];
        return string.IsNullOrWhiteSpace(instrument) ? null : instrument;
    }

    private ReferencePrice? Read(JsonElement root, string instrument)
    {
        if (root.ValueKind != JsonValueKind.Array)
        {
            _logger.LogWarning("swissquote.com returned {Kind} where an array of platforms was expected.", root.ValueKind);
            return null;
        }

        foreach (var platform in root.EnumerateArray())
        {
            if (platform.ValueKind != JsonValueKind.Object ||
                !platform.TryGetProperty("spreadProfilePrices", out var profiles) ||
                profiles.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var profile in profiles.EnumerateArray())
            {
                if (profile.ValueKind != JsonValueKind.Object ||
                    !profile.TryGetProperty("bid", out var bid) || bid.ValueKind != JsonValueKind.Number ||
                    !profile.TryGetProperty("ask", out var ask) || ask.ValueKind != JsonValueKind.Number)
                {
                    continue;
                }

                return new ReferencePrice((bid.GetDecimal() + ask.GetDecimal()) / 2m, TimestampOf(platform));
            }
        }

        _logger.LogWarning("swissquote.com response for {Instrument} carried no usable bid/ask.", instrument);
        return null;
    }

    /// <summary>
    /// The platform's own <c>ts</c>, in milliseconds since the Unix epoch. This is the field that
    /// makes a closed market visible: over a weekend it keeps returning Friday's closing quote
    /// with Friday's timestamp, which the chain's age check refuses (issue #316).
    /// </summary>
    private DateTimeOffset? TimestampOf(JsonElement platform)
    {
        // The ValueKind check is not redundant: TryGetInt64 throws rather than returning false
        // when the value is not a number, and the catch-all above would then discard a perfectly
        // good bid and ask over a timestamp. An unreadable time costs the age, not the price.
        if (!platform.TryGetProperty("ts", out var ts) ||
            ts.ValueKind != JsonValueKind.Number ||
            !ts.TryGetInt64(out var milliseconds))
        {
            _logger.LogWarning("swissquote.com gave no timestamp; its price will be treated as of unknown age.");
            return null;
        }

        try
        {
            return DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
        }
        catch (ArgumentOutOfRangeException)
        {
            _logger.LogWarning("swissquote.com returned a timestamp out of range: {Timestamp}", milliseconds);
            return null;
        }
    }
}
