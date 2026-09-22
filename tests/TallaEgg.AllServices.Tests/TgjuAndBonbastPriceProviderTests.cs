using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Infrastructure.Clients;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The two keyless price sources added for issue #305, tested against the shape their hosts
/// actually returned rather than an invented one: every figure below was captured live on
/// 2026-09-19 and 2026-09-22 and trimmed to the keys these providers read.
///
/// <para>
/// What is worth asserting here is not "it parses JSON" but the two conversions that have no
/// visible effect until money moves: tgju.org quotes <b>Rial</b> where everything stored is
/// Toman, and both quote gold per mesghal where MAUA/IRT trades per gram. A provider that
/// returned the raw number would look perfectly healthy in a log and be wrong by a factor of
/// forty-three.
/// </para>
/// </summary>
public class TgjuAndBonbastPriceProviderTests
{
    /// <summary>Answers each request from a queue, and records what it was asked for.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

        public StubHandler(params (HttpStatusCode Status, string Body)[] responses) =>
            _responses = new Queue<(HttpStatusCode, string)>(responses);

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            // Read it now: the content is disposed with the request, and a test that asserts on a
            // posted form after the fact would otherwise see an empty body.
            if (request.Content is not null)
                PostedBodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));

            var (status, body) = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.NotFound, "");
            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }

        public List<string> PostedBodies { get; } = [];
    }

    private static IConfiguration EmptyConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();

    private static IConfiguration Configuration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    /// <summary>
    /// tgju.org, trimmed to the four keys this provider knows. Prices are Rial strings with
    /// thousands separators, exactly as the host returns them.
    /// </summary>
    private const string TgjuBody = """
    {
      "current": {
        "mesghal":            { "p": "1,026,980,000", "ts": "2026-09-21 00:00:00" },
        "sekeb":              { "p": "2,301,300,000", "ts": "2026-09-21 00:00:00" },
        "crypto-bitcoin-irr": { "p": "195,122,716,000", "ts": "2026-09-22 08:05:29" },
        "crypto-bitcoin":     { "p": "85538.87", "ts": "2026-09-22 08:05:29" },
        "geram18":            { "p": "237,072,000", "ts": "2026-09-21 00:00:00" }
      }
    }
    """;

    /// <summary>bonbast.com, trimmed. Every value is a Toman string, coins quoted buy and sell.</summary>
    private const string BonbastBody = """
    {
      "azadi1": "233000000",
      "azadi12": "229000000",
      "mithqal": "103650000",
      "gol18": "23927697",
      "last_modified": "September 19, 2026 11:56"
    }
    """;

    private const string BonbastPage =
        "<html><body><script>$.post('/json', {param: \"99d8f481f71d02df6c386f742cf60709,UYZvk,2026-09-19-11-56-27\"}, function(json){});</script></body></html>";

    /// <summary>
    /// A cache of its own per provider under test. Sharing one would carry a stubbed response
    /// from one test into the next, which is exactly why the cache is an injected object rather
    /// than a static field inside each provider.
    /// </summary>
    private static ReferencePriceDocumentCache FreshCache() => new(TimeSpan.FromSeconds(90));

    private static TgjuPriceProvider Tgju(
        StubHandler handler, IConfiguration? configuration = null, ReferencePriceDocumentCache? cache = null) =>
        new(new HttpClient(handler), NullLogger<TgjuPriceProvider>.Instance,
            configuration ?? EmptyConfiguration(), cache ?? FreshCache());

    private static BonbastPriceProvider Bonbast(
        StubHandler handler, IConfiguration? configuration = null, ReferencePriceDocumentCache? cache = null) =>
        new(new HttpClient(handler), NullLogger<BonbastPriceProvider>.Instance,
            configuration ?? EmptyConfiguration(), cache ?? FreshCache());

    // ---- tgju.org -------------------------------------------------------------------------

    /// <summary>
    /// 1,026,980,000 Rial per mesghal → 102,698,000 Toman per mesghal → about 23.7 million Toman
    /// per gram. Both conversions in one assertion, because either one alone leaves a plausible
    /// number: skipping the Rial step gives a price ten times the market, and skipping the
    /// mesghal step gives one 4.3 times too high — neither reads as obviously broken.
    /// </summary>
    [Fact]
    public async Task Tgju_ForMeltedGold_ConvertsRialPerMesghalToTomanPerGram()
    {
        var provider = Tgju(new StubHandler((HttpStatusCode.OK, TgjuBody)));

        var price = await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.NotNull(price);
        Assert.Equal(1026980000m / 10m / CurrenciesConstant.GramsPerMesghal, price!.Value);
        // Sanity in absolute terms, not only relative to the formula under test.
        Assert.InRange(price.Value, 20_000_000m, 30_000_000m);
    }

    [Fact]
    public async Task Tgju_ForTheCoin_ReturnsTomanPerWholeCoin()
    {
        var provider = Tgju(new StubHandler((HttpStatusCode.OK, TgjuBody)));

        var price = await provider.GetPriceAsync(CurrenciesConstant.SEKE_BAHAR_IRT);

        Assert.Equal(230_130_000m, price);
    }

    /// <summary>
    /// The Rial entry, not the USD one sitting beside it under a nearly identical key. Nothing in
    /// the response says which currency either is in, so this is the mapping mistake that would
    /// publish a Bitcoin quote of about 8,554 Toman.
    /// </summary>
    [Fact]
    public async Task Tgju_ForBitcoin_ReadsTheRialKeyNotTheDollarOne()
    {
        var provider = Tgju(new StubHandler((HttpStatusCode.OK, TgjuBody)));

        var price = await provider.GetPriceAsync(CurrenciesConstant.BTC_IRT);

        Assert.Equal(19_512_271_600m, price);
    }

    [Fact]
    public async Task Tgju_ForAConfiguredSymbol_UsesTheConfiguredKey()
    {
        var configuration = Configuration(new Dictionary<string, string?>
        {
            ["Symbols:GERAM18/IRT:Tgju:Key"] = "geram18"
        });
        var provider = Tgju(new StubHandler((HttpStatusCode.OK, TgjuBody)), configuration);

        var price = await provider.GetPriceAsync("GERAM18/IRT");

        Assert.Equal(23_707_200m, price);
    }

    [Fact]
    public async Task Tgju_ForAnUnmappedSymbol_ReturnsNullWithoutCallingTheHost()
    {
        var handler = new StubHandler((HttpStatusCode.OK, TgjuBody));
        var provider = Tgju(handler);

        var price = await provider.GetPriceAsync("XAU/USD");

        Assert.Null(price);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, TgjuBody)]
    [InlineData(HttpStatusCode.OK, "{\"current\":{}}")]
    [InlineData(HttpStatusCode.OK, "{\"current\":{\"mesghal\":{\"p\":\"not a number\"}}}")]
    [InlineData(HttpStatusCode.OK, "<html>blocked</html>")]
    public async Task Tgju_WhenTheHostMisbehaves_ReturnsNullRatherThanThrowing(HttpStatusCode status, string body)
    {
        var provider = Tgju(new StubHandler((status, body)));

        // The interface promises a null, never an exception: the chain has no try/catch.
        Assert.Null(await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT));
    }

    // ---- bonbast.com ----------------------------------------------------------------------

    [Fact]
    public async Task Bonbast_ForMeltedGold_ConvertsTomanPerMesghalToTomanPerGram()
    {
        var provider = Bonbast(new StubHandler(
            (HttpStatusCode.OK, BonbastPage),
            (HttpStatusCode.OK, BonbastBody)));

        var price = await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.NotNull(price);
        Assert.Equal(103650000m / CurrenciesConstant.GramsPerMesghal, price!.Value);
    }

    /// <summary>
    /// The coin is quoted as bonbast's own buy and sell. Taking either leg would carry their
    /// spread into a price this platform then applies its own spread to.
    /// </summary>
    [Fact]
    public async Task Bonbast_ForTheCoin_UsesTheMidOfBuyAndSell()
    {
        var provider = Bonbast(new StubHandler(
            (HttpStatusCode.OK, BonbastPage),
            (HttpStatusCode.OK, BonbastBody)));

        var price = await provider.GetPriceAsync(CurrenciesConstant.SEKE_BAHAR_IRT);

        Assert.Equal(231_000_000m, price);
    }

    /// <summary>
    /// The token is not decoration: the endpoint refuses a request without it. This asserts the
    /// value read from the page is the value posted, so a provider that fetched the page and then
    /// ignored it — which would still parse a cached response in a stub — fails here.
    /// </summary>
    [Fact]
    public async Task Bonbast_PostsTheTokenItReadFromThePage()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, BonbastPage),
            (HttpStatusCode.OK, BonbastBody));

        await Bonbast(handler).GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Post, handler.Requests[1].Method);
        var posted = Assert.Single(handler.PostedBodies);
        Assert.Contains("99d8f481f71d02df6c386f742cf60709", posted, StringComparison.Ordinal);
    }

    /// <summary>
    /// A page whose markup no longer carries the token is the failure this provider is most
    /// likely to meet — it reads a site, not an API. It must cost a null, not an exception, and
    /// must not post a request that cannot succeed.
    /// </summary>
    [Fact]
    public async Task Bonbast_WhenThePageHasNoToken_ReturnsNullWithoutPosting()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, "<html><body>nothing here</body></html>"),
            (HttpStatusCode.OK, BonbastBody));

        var price = await Bonbast(handler).GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.Null(price);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Bonbast_ForBitcoin_ReturnsNullBecauseItIsDeliberatelyUnmapped()
    {
        var handler = new StubHandler((HttpStatusCode.OK, BonbastPage), (HttpStatusCode.OK, BonbastBody));

        Assert.Null(await Bonbast(handler).GetPriceAsync(CurrenciesConstant.BTC_IRT));
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, BonbastBody)]
    [InlineData(HttpStatusCode.OK, "{\"gol18\":\"23927697\"}")]
    [InlineData(HttpStatusCode.OK, "not json at all")]
    public async Task Bonbast_WhenTheHostMisbehaves_ReturnsNullRatherThanThrowing(HttpStatusCode status, string body)
    {
        var provider = Bonbast(new StubHandler((HttpStatusCode.OK, BonbastPage), (status, body)));

        Assert.Null(await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT));
    }

    /// <summary>
    /// Valid JSON that is not an object. <c>TryGetProperty</c> throws on it rather than returning
    /// false, and at the call site that reads the price keys there is no catch left between the
    /// provider and the chain — which has none of its own, because the interface promises a null
    /// for every failure. Found by the review of PR #315.
    /// </summary>
    [Theory]
    [InlineData("[]")]
    [InlineData("\"maintenance\"")]
    [InlineData("null")]
    public async Task Bonbast_WhenTheBodyIsNotAnObject_ReturnsNullRatherThanThrowing(string body)
    {
        var provider = Bonbast(new StubHandler((HttpStatusCode.OK, BonbastPage), (HttpStatusCode.OK, body)));

        Assert.Null(await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT));
    }

    [Theory]
    [InlineData("[]")]
    [InlineData("{\"current\":[]}")]
    [InlineData("{\"current\":{\"mesghal\":\"1,026,980,000\"}}")]
    public async Task Tgju_WhenTheDocumentIsNotShapedAsExpected_ReturnsNullRatherThanThrowing(string body)
    {
        var provider = Tgju(new StubHandler((HttpStatusCode.OK, body)));

        Assert.Null(await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT));
    }

    /// <summary>
    /// A price arriving as a JSON number instead of a string is still reported as a price this
    /// provider could not use — and, here, parsed rather than rejected.
    /// </summary>
    [Fact]
    public async Task Tgju_WhenThePriceIsANumber_ReadsItAnyway()
    {
        var provider = Tgju(new StubHandler((HttpStatusCode.OK, "{\"current\":{\"sekeb\":{\"p\":2301300000}}}")));

        Assert.Equal(230_130_000m, await provider.GetPriceAsync(CurrenciesConstant.SEKE_BAHAR_IRT));
    }

    // ---- one fetch per tick, not one per symbol ---------------------------------------------

    /// <summary>
    /// Both hosts answer with every instrument in one document, and the publisher asks per symbol
    /// in a scope of its own. Without the shared cache that is the same ~180 KB document three
    /// times a tick from tgju, and three page-and-token handshakes from bonbast — about 390 MB a
    /// day against a host that serves this as a courtesy. Found by the review of PR #315.
    /// </summary>
    [Fact]
    public async Task Tgju_AcrossSymbolsInOneTick_FetchesTheDocumentOnce()
    {
        var handler = new StubHandler((HttpStatusCode.OK, TgjuBody));
        var cache = FreshCache();

        // A provider instance per symbol, as the publisher's per-symbol DI scope produces.
        foreach (var symbol in new[] { CurrenciesConstant.MAUA_IRT, CurrenciesConstant.SEKE_BAHAR_IRT, CurrenciesConstant.BTC_IRT })
        {
            Assert.NotNull(await Tgju(handler, cache: cache).GetPriceAsync(symbol));
        }

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task Bonbast_AcrossSymbolsInOneTick_RepeatsNeitherRequest()
    {
        var handler = new StubHandler((HttpStatusCode.OK, BonbastPage), (HttpStatusCode.OK, BonbastBody));
        var cache = FreshCache();

        foreach (var symbol in new[] { CurrenciesConstant.MAUA_IRT, CurrenciesConstant.SEKE_BAHAR_IRT })
        {
            Assert.NotNull(await Bonbast(handler, cache: cache).GetPriceAsync(symbol));
        }

        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>
    /// An expired entry is a miss. Without this the test above would also pass on a cache that
    /// never expires, which would hold one price for the life of the process.
    /// </summary>
    [Fact]
    public async Task ReferencePriceDocumentCache_AfterItsLifetime_FetchesAgain()
    {
        var handler = new StubHandler((HttpStatusCode.OK, TgjuBody), (HttpStatusCode.OK, TgjuBody));
        var cache = new ReferencePriceDocumentCache(TimeSpan.Zero);

        await Tgju(handler, cache: cache).GetPriceAsync(CurrenciesConstant.MAUA_IRT);
        await Tgju(handler, cache: cache).GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.Equal(2, handler.Requests.Count);
    }

    /// <summary>A failed fetch must not be cached, or one bad minute becomes ninety seconds of them.</summary>
    [Fact]
    public async Task Bonbast_WhenAFetchFails_DoesNotCacheTheFailure()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, BonbastPage),
            (HttpStatusCode.ServiceUnavailable, ""),
            (HttpStatusCode.OK, BonbastPage),
            (HttpStatusCode.OK, BonbastBody));
        var cache = FreshCache();

        Assert.Null(await Bonbast(handler, cache: cache).GetPriceAsync(CurrenciesConstant.MAUA_IRT));
        Assert.NotNull(await Bonbast(handler, cache: cache).GetPriceAsync(CurrenciesConstant.MAUA_IRT));
    }

    // ---- both ------------------------------------------------------------------------------

    /// <summary>
    /// The two agreed to within 0.2% on live data, which is what makes them a fallback pair
    /// rather than two readings of one source. Asserted on the fixtures so a future change to
    /// either provider's conversions cannot quietly move one away from the other; the band is
    /// deliberately wide, because the two were captured on different days.
    /// </summary>
    [Fact]
    public async Task BothProviders_ReadTheSameProductForMeltedGold()
    {
        var fromTgju = await Tgju(new StubHandler((HttpStatusCode.OK, TgjuBody)))
            .GetPriceAsync(CurrenciesConstant.MAUA_IRT);
        var fromBonbast = await Bonbast(new StubHandler((HttpStatusCode.OK, BonbastPage), (HttpStatusCode.OK, BonbastBody)))
            .GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        var deviation = Math.Abs(fromTgju!.Value - fromBonbast!.Value) / fromBonbast.Value * 100m;
        Assert.True(deviation < 2m, $"tgju {fromTgju} and bonbast {fromBonbast} differ by {deviation:F2}%");
    }
}
