using System.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Orders.Infrastructure.Clients;

/// <summary>
/// The named <see cref="HttpClient"/> each reference price source fetches through, registered
/// together so that every one of them carries the same timeout (issue #317).
///
/// <para>
/// They were registered one by one in <c>Orders.Api/Program.cs</c>, and the two oldest — nerkh.io
/// and brsapi.ir — were left on <see cref="HttpClient"/>'s 100-second default while the four added
/// later set fifteen seconds. Nothing marked the difference, and nothing could fail because of it
/// until a source accepted a connection and then went quiet.
/// </para>
///
/// <para>
/// Keeping the names here rather than as literals at each call site matters for the same reason:
/// <c>IHttpClientFactory.CreateClient</c> answers an unknown name with a default client rather
/// than an error, so a typo in either place would silently produce exactly the unconfigured client
/// this class exists to prevent.
/// </para>
/// </summary>
public static class ReferencePriceClients
{
    public const string Nerkh = "NerkhPriceProvider";
    public const string BrsApi = "BrsApiPriceProvider";
    public const string Tgju = "TgjuPriceProvider";
    public const string Xaus = "XausPriceProvider";
    public const string Swissquote = "SwissquotePriceProvider";
    public const string Bonbast = "BonbastPriceProvider";

    /// <summary>
    /// How long one fetch may take.
    ///
    /// <para>
    /// Not <see cref="HttpClient"/>'s 100-second default, because a publisher tick fetches a price
    /// per symbol in sequence while holding a six-minute lease (issue #160), and that lease is
    /// renewed between ticks rather than during one. With every source hanging, the defaults would
    /// put one tick well past the lease and let a second instance publish the same prices.
    /// </para>
    ///
    /// <para>
    /// Fifteen seconds also costs nothing when a source is healthy: the next tick is two minutes
    /// away, so a price that has not arrived by then is of no use to this one.
    /// </para>
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Identifies this platform rather than posing as a browser. Three of the six sources publish
    /// no API and are read as a courtesy; saying who is calling is part of that.
    /// </summary>
    public const string UserAgent = "TallaEgg/1.0 (+https://github.com/MohKardan/TallaEgg)";

    /// <summary>Every source's client. The names are the ones the providers resolve.</summary>
    public static IEnumerable<string> Names => [Nerkh, BrsApi, Tgju, Xaus, Swissquote, Bonbast];

    public static IServiceCollection AddReferencePriceClients(this IServiceCollection services)
    {
        foreach (var name in Names.Where(name => name != Bonbast))
        {
            services.AddHttpClient(name, Configure);
        }

        // bonbast.com hands out a request token in a cookie alongside the one in its markup, so
        // this client keeps a cookie jar; the two requests it makes are a pair, not independent
        // calls.
        services.AddHttpClient(Bonbast, Configure)
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true
            });

        return services;
    }

    private static void Configure(HttpClient client)
    {
        client.Timeout = Timeout;
        client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }
}
