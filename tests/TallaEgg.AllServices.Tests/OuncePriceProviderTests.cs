using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Infrastructure.Clients;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The two sources that price the ounce (issue #304), against the shape their hosts really
/// returned: both fixtures below were captured live on 2026-09-22 and trimmed to the fields these
/// providers read.
///
/// <para>
/// Two things matter here beyond parsing. Their prices are in <b>dollars</b>, not Toman, which the
/// interface has always allowed and no provider had done before; and both report a real timestamp,
/// which is what lets the chain refuse a weekend's worth of Friday's closing price (issue #316).
/// </para>
/// </summary>
public class OuncePriceProviderTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static IConfiguration EmptyConfiguration() => new ConfigurationBuilder().Build();

    /// <summary>xaus.com, trimmed. The real response also carries a 160-entry FX table.</summary>
    private const string XausBody = """
    {
      "xau": { "price": 4320, "currency": "USD", "unit": "troy_oz" },
      "spot_usd_oz": 4320,
      "per_gram_usd": 138.8912,
      "silver_usd_oz": 66.38,
      "stale": false,
      "price_as_of": "2026-09-22T06:10:49.896Z",
      "updated_at": "2026-09-22T06:10:49.896Z",
      "price_source": "gold-api.com",
      "data_state": { "status": "fresh", "as_of": "2026-09-22T06:10:49.896Z", "source": "upstream", "age_seconds": 0 }
    }
    """;

    /// <summary>Swissquote, trimmed to two of its three platforms and two of each one's profiles.</summary>
    private const string SwissquoteBody = """
    [
      {
        "topo": { "platform": "SwissquoteCapitalMarkets", "server": "Live7" },
        "spreadProfilePrices": [
          { "spreadProfile": "premium", "bidSpread": 25.4, "askSpread": 25.4, "bid": 4318.216, "ask": 4318.874 },
          { "spreadProfile": "elite",   "bidSpread": 17.7, "askSpread": 17.7, "bid": 4318.293, "ask": 4318.797 }
        ],
        "ts": 1790057467975
      },
      {
        "topo": { "platform": "SwissquoteLtd", "server": "Live5" },
        "spreadProfilePrices": [
          { "spreadProfile": "premium", "bidSpread": 25.6, "askSpread": 25.6, "bid": 4318.21, "ask": 4318.88 }
        ],
        "ts": 1790057467975
      }
    ]
    """;

    private static XausPriceProvider Xaus(StubHandler handler) =>
        new(new HttpClient(handler), NullLogger<XausPriceProvider>.Instance, EmptyConfiguration(),
            new ReferencePriceDocumentCache(TimeSpan.FromSeconds(90)));

    private static SwissquotePriceProvider Swissquote(StubHandler handler) =>
        new(new HttpClient(handler), NullLogger<SwissquotePriceProvider>.Instance, EmptyConfiguration());

    // ---- xaus.com ---------------------------------------------------------------------------

    [Fact]
    public async Task Xaus_ForTheOunce_ReturnsDollarsPerTroyOunce()
    {
        var price = await Xaus(new StubHandler(HttpStatusCode.OK, XausBody)).GetPriceAsync(CurrenciesConstant.XAU_USD);

        Assert.Equal(4320m, price?.Price);
    }

    /// <summary>
    /// <c>price_as_of</c> is ISO 8601 with an explicit Z, so unlike the Iranian feeds no time zone
    /// has to be assumed — and unlike them, it is read rather than reported as unknown.
    /// </summary>
    [Fact]
    public async Task Xaus_ReportsWhenItsPriceWasSourced()
    {
        var price = await Xaus(new StubHandler(HttpStatusCode.OK, XausBody)).GetPriceAsync(CurrenciesConstant.XAU_USD);

        Assert.Equal(new DateTimeOffset(2026, 9, 22, 6, 10, 49, 896, TimeSpan.Zero), price!.Value.AsOf);
    }

    /// <summary>
    /// The weekend, from the ounce's side: xaus keeps serving Friday's close, and says so in
    /// <c>price_as_of</c>. The provider reports the price with its real age and lets the chain
    /// refuse it — a provider that substituted "now" would make the closed market invisible.
    /// </summary>
    [Fact]
    public async Task Xaus_WhenTheMarketIsClosed_ReportsTheOldTimestampRatherThanNow()
    {
        const string overTheWeekend = """
        { "spot_usd_oz": 4318.5, "stale": true, "price_as_of": "2026-09-18T21:00:00.000Z" }
        """;

        var price = await Xaus(new StubHandler(HttpStatusCode.OK, overTheWeekend)).GetPriceAsync(CurrenciesConstant.XAU_USD);

        Assert.Equal(4318.5m, price?.Price);
        Assert.Equal(new DateTimeOffset(2026, 9, 18, 21, 0, 0, TimeSpan.Zero), price!.Value.AsOf);
    }

    [Fact]
    public async Task Xaus_ForATomanSymbol_ReturnsNullWithoutCallingTheHost()
    {
        var handler = new StubHandler(HttpStatusCode.OK, XausBody);

        Assert.Null(await Xaus(handler).GetPriceAsync(CurrenciesConstant.MAUA_IRT));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, XausBody)]
    [InlineData(HttpStatusCode.OK, "{}")]
    [InlineData(HttpStatusCode.OK, "{\"spot_usd_oz\":\"4320\"}")]
    [InlineData(HttpStatusCode.OK, "[]")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task Xaus_WhenTheHostMisbehaves_ReturnsNullRatherThanThrowing(HttpStatusCode status, string body)
    {
        Assert.Null(await Xaus(new StubHandler(status, body)).GetPriceAsync(CurrenciesConstant.XAU_USD));
    }

    // ---- Swissquote -------------------------------------------------------------------------

    /// <summary>
    /// The mid, not a leg. A bank's bid or ask already carries the bank's spread, and this
    /// platform applies its own spread on top of whatever it is given.
    /// </summary>
    [Fact]
    public async Task Swissquote_ForTheOunce_ReturnsTheMidOfBidAndAsk()
    {
        var price = await Swissquote(new StubHandler(HttpStatusCode.OK, SwissquoteBody)).GetPriceAsync(CurrenciesConstant.XAU_USD);

        Assert.Equal((4318.216m + 4318.874m) / 2m, price?.Price);
    }

    [Fact]
    public async Task Swissquote_ReportsThePlatformsTimestamp()
    {
        var price = await Swissquote(new StubHandler(HttpStatusCode.OK, SwissquoteBody)).GetPriceAsync(CurrenciesConstant.XAU_USD);

        Assert.Equal(DateTimeOffset.FromUnixTimeMilliseconds(1790057467975), price!.Value.AsOf);
    }

    /// <summary>
    /// A platform carrying no usable prices is skipped for the next one rather than failing the
    /// fetch: the array holds several venues quoting the same market, and one being empty says
    /// nothing about the others.
    /// </summary>
    [Fact]
    public async Task Swissquote_SkipsAPlatformWithNoPrices()
    {
        const string firstPlatformEmpty = """
        [
          { "topo": { "platform": "Empty" }, "spreadProfilePrices": [], "ts": 1790057467975 },
          { "topo": { "platform": "Live" },
            "spreadProfilePrices": [ { "spreadProfile": "prime", "bid": 4300, "ask": 4301 } ],
            "ts": 1790057467975 }
        ]
        """;

        var price = await Swissquote(new StubHandler(HttpStatusCode.OK, firstPlatformEmpty)).GetPriceAsync(CurrenciesConstant.XAU_USD);

        Assert.Equal(4300.5m, price?.Price);
    }

    [Fact]
    public async Task Swissquote_ForATomanSymbol_ReturnsNullWithoutCallingTheHost()
    {
        var handler = new StubHandler(HttpStatusCode.OK, SwissquoteBody);

        Assert.Null(await Swissquote(handler).GetPriceAsync(CurrenciesConstant.BTC_IRT));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, SwissquoteBody)]
    [InlineData(HttpStatusCode.OK, "[]")]
    [InlineData(HttpStatusCode.OK, "{\"bid\":4318}")]
    [InlineData(HttpStatusCode.OK, "[{\"topo\":{},\"ts\":1790057467975}]")]
    [InlineData(HttpStatusCode.OK, "not json")]
    public async Task Swissquote_WhenTheHostMisbehaves_ReturnsNullRatherThanThrowing(HttpStatusCode status, string body)
    {
        Assert.Null(await Swissquote(new StubHandler(status, body)).GetPriceAsync(CurrenciesConstant.XAU_USD));
    }

    // ---- both ------------------------------------------------------------------------------

    /// <summary>
    /// The two are independent feeds — xaus reads gold-api.com, Swissquote is a bank's own book —
    /// which is what makes them a fallback pair rather than one source read twice. They agreed to
    /// well under a tenth of a percent when both fixtures were captured, minutes apart.
    /// </summary>
    [Fact]
    public async Task BothSources_AgreeOnTheOunce()
    {
        var fromXaus = await Xaus(new StubHandler(HttpStatusCode.OK, XausBody)).GetPriceAsync(CurrenciesConstant.XAU_USD);
        var fromSwissquote = await Swissquote(new StubHandler(HttpStatusCode.OK, SwissquoteBody)).GetPriceAsync(CurrenciesConstant.XAU_USD);

        var deviation = Math.Abs(fromXaus!.Value.Price - fromSwissquote!.Value.Price) / fromSwissquote.Value.Price * 100m;

        Assert.True(deviation < 0.5m,
            $"xaus {fromXaus.Value.Price} and swissquote {fromSwissquote.Value.Price} differ by {deviation:F3}%");
    }
}
