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
    /// Identifies this platform rather than posing as a browser, on the sources that are read as a
    /// courtesy rather than sold to us.
    ///
    /// <para>
    /// Deliberately <b>not</b> sent to nerkh.io or brsapi.ir, which have never received one. They
    /// are the two sources that answer on the production host, they authenticate by token, and a
    /// filtering rule keyed on an unfamiliar User-Agent would take the whole toman feed out at
    /// once. Their credentials answer 403 at the moment, so the change could not be tested against
    /// them either — and an untested change to a working header is not worth the tidiness.
    /// </para>
    /// </summary>
    public const string UserAgent = "TallaEgg/1.0 (+https://github.com/MohKardan/TallaEgg)";

    /// <summary>The sources that receive <see cref="UserAgent"/>; see the note there.</summary>
    private static readonly HashSet<string> Identifying = new(StringComparer.Ordinal) { Tgju, Xaus, Swissquote, Bonbast };

    /// <summary>Every source's client. The names are the ones the providers resolve.</summary>
    public static IEnumerable<string> Names => [Nerkh, BrsApi, Tgju, Xaus, Swissquote, Bonbast];

    public static IServiceCollection AddReferencePriceClients(this IServiceCollection services)
    {
        foreach (var name in Names.Where(name => name != Bonbast))
        {
            services.AddHttpClient(name, client => Configure(name, client));
        }

        // bonbast.com hands out a request token in a cookie alongside the one in its markup, so
        // this client keeps a cookie jar; the two requests it makes are a pair, not independent
        // calls.
        services.AddHttpClient(Bonbast, client => Configure(Bonbast, client))
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                CookieContainer = new CookieContainer(),
                UseCookies = true
            });

        return services;
    }

    private static void Configure(string name, HttpClient client)
    {
        client.Timeout = Timeout;

        if (Identifying.Contains(name))
            client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }
}
