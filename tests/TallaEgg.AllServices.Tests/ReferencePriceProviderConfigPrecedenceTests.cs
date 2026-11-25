using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Infrastructure.Clients;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Configuration precedence for every <see cref="Orders.Core.IReferencePriceProvider"/>
/// implementation (issue #318).
///
/// <para>
/// Each provider carries compiled instrument defaults for the symbols traded today so the test
/// suite and fresh clones work without a config file. When <c>Symbols:{symbol}:{Provider}</c>
/// is configured, that config block must win over the compiled default so upstream key renames
/// can be fixed with a config edit and restart instead of a rebuild. When the block is absent
/// or blank/incomplete, the compiled default must continue to apply cleanly.
/// </para>
/// </summary>
public class ReferencePriceProviderConfigPrecedenceTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Queue<(HttpStatusCode Status, string Body)> _responses;

        public StubHandler(params (HttpStatusCode Status, string Body)[] responses) =>
            _responses = new Queue<(HttpStatusCode, string)>(responses);

        public List<Uri?> RequestUris { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUris.Add(request.RequestUri);
            var (status, body) = _responses.Count > 0 ? _responses.Dequeue() : (HttpStatusCode.NotFound, "");
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private static IConfiguration Config(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ReferencePriceDocumentCache FreshCache() => new(TimeSpan.FromSeconds(90));

    // ---- nerkh.io ---------------------------------------------------------------------------

    [Fact]
    public async Task Nerkh_ConfigBlock_OverridesCompiledDefault()
    {
        const string body = """
        {
          "data": {
            "prices": {
              "GERAM18": { "current": "23707200" }
            }
          }
        }
        """;

        var handler = new StubHandler((HttpStatusCode.OK, body));
        var config = Config(new Dictionary<string, string?>
        {
            ["AutoQuote:NerkhApiToken"] = "test-token",
            ["Symbols:MAUA/IRT:Nerkh:Path"] = "gold/GERAM18",
            ["Symbols:MAUA/IRT:Nerkh:Key"] = "GERAM18",
            ["Symbols:MAUA/IRT:Nerkh:ConvertFromMesghal"] = "false"
        });

        var provider = new NerkhPriceProvider(new HttpClient(handler), NullLogger<NerkhPriceProvider>.Instance, config);

        var price = await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.NotNull(price);
        Assert.Equal(23_707_200m, price!.Value.Price);
        var uri = Assert.Single(handler.RequestUris);
        Assert.EndsWith("/gold/GERAM18", uri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("gold/GERAM18", "")]
    [InlineData("", "GERAM18")]
    public async Task Nerkh_WhenConfigAbsentOrIncomplete_FallsBackToCompiledDefault(string? path, string? key)
    {
        const string body = """
        {
          "data": {
            "prices": {
              "MESGHAL": { "current": "102698000" }
            }
          }
        }
        """;

        var values = new Dictionary<string, string?>
        {
            ["AutoQuote:NerkhApiToken"] = "test-token"
        };
        if (path is not null) values["Symbols:MAUA/IRT:Nerkh:Path"] = path;
        if (key is not null) values["Symbols:MAUA/IRT:Nerkh:Key"] = key;

        var handler = new StubHandler((HttpStatusCode.OK, body));
        var provider = new NerkhPriceProvider(new HttpClient(handler), NullLogger<NerkhPriceProvider>.Instance, Config(values));

        var price = await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.NotNull(price);
        Assert.Equal(102_698_000m / CurrenciesConstant.GramsPerMesghal, price!.Value.Price);
        var uri = Assert.Single(handler.RequestUris);
        Assert.EndsWith("/gold/MESGHAL", uri!.AbsoluteUri, StringComparison.Ordinal);
    }

    // ---- brsapi.ir --------------------------------------------------------------------------

    private const string BrsApiBody = """
    {
      "gold": [
        { "symbol": "IR_GOLD_MELTED", "price": 102698000 },
        { "symbol": "IR_GOLD_18K",    "price": 23850000 }
      ],
      "currency": [
        { "symbol": "USD", "price": 90000 }
      ],
      "cryptocurrency": [
        { "symbol": "BTC", "price": "85000" }
      ]
    }
    """;

    [Fact]
    public async Task BrsApi_ConfigBlock_OverridesCompiledDefault()
    {
        var handler = new StubHandler((HttpStatusCode.OK, BrsApiBody));
        var config = Config(new Dictionary<string, string?>
        {
            ["AutoQuote:BrsApiKey"] = "test-key",
            ["Symbols:MAUA/IRT:BrsApi:Array"] = "gold",
            ["Symbols:MAUA/IRT:BrsApi:Symbol"] = "IR_GOLD_18K",
            ["Symbols:MAUA/IRT:BrsApi:ConvertFromMesghal"] = "false"
        });

        var provider = new BrsApiPriceProvider(new HttpClient(handler), NullLogger<BrsApiPriceProvider>.Instance, config);

        var price = await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.NotNull(price);
        Assert.Equal(23_850_000m, price!.Value.Price);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("   ", "   ")]
    [InlineData("gold", "")]
    [InlineData("", "IR_GOLD_18K")]
    public async Task BrsApi_WhenConfigAbsentOrIncomplete_FallsBackToCompiledDefault(string? array, string? symbol)
    {
        var values = new Dictionary<string, string?>
        {
            ["AutoQuote:BrsApiKey"] = "test-key"
        };
        if (array is not null) values["Symbols:MAUA/IRT:BrsApi:Array"] = array;
        if (symbol is not null) values["Symbols:MAUA/IRT:BrsApi:Symbol"] = symbol;

        var handler = new StubHandler((HttpStatusCode.OK, BrsApiBody));
        var provider = new BrsApiPriceProvider(new HttpClient(handler), NullLogger<BrsApiPriceProvider>.Instance, Config(values));

        var price = await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.NotNull(price);
        Assert.Equal(102_698_000m / CurrenciesConstant.GramsPerMesghal, price!.Value.Price);
    }

    // ---- tgju.org ---------------------------------------------------------------------------

    private const string TgjuBody = """
    {
      "current": {
        "mesghal": { "p": "1,026,980,000", "ts": "2026-09-21 00:00:00" },
        "geram18": { "p": "237,072,000",   "ts": "2026-09-21 00:00:00" }
      }
    }
    """;

    [Fact]
    public async Task Tgju_ConfigBlock_OverridesCompiledDefault()
    {
        var handler = new StubHandler((HttpStatusCode.OK, TgjuBody));
        var config = Config(new Dictionary<string, string?>
        {
            ["Symbols:MAUA/IRT:Tgju:Key"] = "geram18",
            ["Symbols:MAUA/IRT:Tgju:ConvertFromMesghal"] = "false"
        });

        var provider = new TgjuPriceProvider(
            new HttpClient(handler), NullLogger<TgjuPriceProvider>.Instance, config, FreshCache());

        var price = await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.NotNull(price);
        Assert.Equal(23_707_200m, price!.Value.Price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Tgju_WhenConfigAbsentOrEmpty_FallsBackToCompiledDefault(string? key)
    {
        var values = new Dictionary<string, string?>();
        if (key is not null) values["Symbols:MAUA/IRT:Tgju:Key"] = key;

        var handler = new StubHandler((HttpStatusCode.OK, TgjuBody));
        var provider = new TgjuPriceProvider(
            new HttpClient(handler), NullLogger<TgjuPriceProvider>.Instance, Config(values), FreshCache());

        var price = await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.NotNull(price);
        Assert.Equal(1_026_980_000m / 10m / CurrenciesConstant.GramsPerMesghal, price!.Value.Price);
    }

    // ---- bonbast.com ------------------------------------------------------------------------

    private const string BonbastPage =
        "<html><body><script>$.post('/json', {param: \"99d8f481f71d02df6c386f742cf60709,UYZvk,2026-09-19-11-56-27\"}, function(json){});</script></body></html>";

    private const string BonbastBody = """
    {
      "mithqal": "103650000",
      "gol18": "23927697",
      "azadi1": "233000000",
      "azadi12": "229000000",
      "emami1": "245000000",
      "emami12": "241000000"
    }
    """;

    [Fact]
    public async Task Bonbast_ConfigBlock_OverridesCompiledDefault()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, BonbastPage),
            (HttpStatusCode.OK, BonbastBody));
        var config = Config(new Dictionary<string, string?>
        {
            ["Symbols:MAUA/IRT:Bonbast:Key"] = "gol18",
            ["Symbols:MAUA/IRT:Bonbast:ConvertFromMesghal"] = "false"
        });

        var provider = new BonbastPriceProvider(
            new HttpClient(handler), NullLogger<BonbastPriceProvider>.Instance, config, FreshCache());

        var price = await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.NotNull(price);
        Assert.Equal(23_927_697m, price!.Value.Price);
    }

    [Fact]
    public async Task Bonbast_ConfigBlockWithBuyKey_OverridesCompiledCoinDefault()
    {
        var handler = new StubHandler(
            (HttpStatusCode.OK, BonbastPage),
            (HttpStatusCode.OK, BonbastBody));
        var config = Config(new Dictionary<string, string?>
        {
            ["Symbols:SEKE_BAHAR/IRT:Bonbast:Key"] = "emami1",
            ["Symbols:SEKE_BAHAR/IRT:Bonbast:BuyKey"] = "emami12",
            ["Symbols:SEKE_BAHAR/IRT:Bonbast:ConvertFromMesghal"] = "false"
        });

        var provider = new BonbastPriceProvider(
            new HttpClient(handler), NullLogger<BonbastPriceProvider>.Instance, config, FreshCache());

        var price = await provider.GetPriceAsync(CurrenciesConstant.SEKE_BAHAR_IRT);

        Assert.NotNull(price);
        Assert.Equal(243_000_000m, price!.Value.Price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Bonbast_WhenConfigAbsentOrEmpty_FallsBackToCompiledDefault(string? key)
    {
        var values = new Dictionary<string, string?>();
        if (key is not null) values["Symbols:MAUA/IRT:Bonbast:Key"] = key;

        var handler = new StubHandler(
            (HttpStatusCode.OK, BonbastPage),
            (HttpStatusCode.OK, BonbastBody));
        var provider = new BonbastPriceProvider(
            new HttpClient(handler), NullLogger<BonbastPriceProvider>.Instance, Config(values), FreshCache());

        var price = await provider.GetPriceAsync(CurrenciesConstant.MAUA_IRT);

        Assert.NotNull(price);
        Assert.Equal(103_650_000m / CurrenciesConstant.GramsPerMesghal, price!.Value.Price);
    }

    // ---- xaus.com ---------------------------------------------------------------------------

    private const string XausBody = """
    {
      "spot_usd_oz": 4320,
      "per_gram_usd": 138.8912,
      "stale": false,
      "price_as_of": "2026-09-22T06:10:49.896Z"
    }
    """;

    [Fact]
    public async Task Xaus_ConfigBlock_OverridesCompiledDefault()
    {
        var handler = new StubHandler((HttpStatusCode.OK, XausBody));
        var config = Config(new Dictionary<string, string?>
        {
            ["Symbols:XAU/USD:Xaus:Field"] = "per_gram_usd"
        });

        var provider = new XausPriceProvider(
            new HttpClient(handler), NullLogger<XausPriceProvider>.Instance, config, FreshCache());

        var price = await provider.GetPriceAsync(CurrenciesConstant.XAU_USD);

        Assert.NotNull(price);
        Assert.Equal(138.8912m, price!.Value.Price);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Xaus_WhenConfigAbsentOrEmpty_FallsBackToCompiledDefault(string? field)
    {
        var values = new Dictionary<string, string?>();
        if (field is not null) values["Symbols:XAU/USD:Xaus:Field"] = field;

        var handler = new StubHandler((HttpStatusCode.OK, XausBody));
        var provider = new XausPriceProvider(
            new HttpClient(handler), NullLogger<XausPriceProvider>.Instance, Config(values), FreshCache());

        var price = await provider.GetPriceAsync(CurrenciesConstant.XAU_USD);

        Assert.NotNull(price);
        Assert.Equal(4320m, price!.Value.Price);
    }

    // ---- swissquote.com ---------------------------------------------------------------------

    private const string SwissquoteBody = """
    [
      {
        "topo": { "platform": "SwissquoteCapitalMarkets", "server": "Live7" },
        "spreadProfilePrices": [
          { "spreadProfile": "premium", "bid": 4318.0, "ask": 4320.0 }
        ],
        "ts": 1790057467975
      }
    ]
    """;

    [Fact]
    public async Task Swissquote_ConfigBlock_OverridesCompiledDefault()
    {
        var handler = new StubHandler((HttpStatusCode.OK, SwissquoteBody));
        var config = Config(new Dictionary<string, string?>
        {
            ["Symbols:XAU/USD:Swissquote:Instrument"] = "XAG/USD"
        });

        var provider = new SwissquotePriceProvider(
            new HttpClient(handler), NullLogger<SwissquotePriceProvider>.Instance, config);

        var price = await provider.GetPriceAsync(CurrenciesConstant.XAU_USD);

        Assert.NotNull(price);
        Assert.Equal(4319.0m, price!.Value.Price);
        var uri = Assert.Single(handler.RequestUris);
        Assert.EndsWith("/XAG/USD", uri!.AbsoluteUri, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Swissquote_WhenConfigAbsentOrEmpty_FallsBackToCompiledDefault(string? instrument)
    {
        var values = new Dictionary<string, string?>();
        if (instrument is not null) values["Symbols:XAU/USD:Swissquote:Instrument"] = instrument;

        var handler = new StubHandler((HttpStatusCode.OK, SwissquoteBody));
        var provider = new SwissquotePriceProvider(
            new HttpClient(handler), NullLogger<SwissquotePriceProvider>.Instance, Config(values));

        var price = await provider.GetPriceAsync(CurrenciesConstant.XAU_USD);

        Assert.NotNull(price);
        Assert.Equal(4319.0m, price!.Value.Price);
        var uri = Assert.Single(handler.RequestUris);
        Assert.EndsWith("/XAU/USD", uri!.AbsoluteUri, StringComparison.Ordinal);
    }
}
