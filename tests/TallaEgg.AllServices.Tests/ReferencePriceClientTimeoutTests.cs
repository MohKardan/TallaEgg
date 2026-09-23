using Microsoft.Extensions.DependencyInjection;
using Orders.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Every price source's HTTP client gives up in time for the publisher to keep its lease
/// (issue #317).
///
/// <para>
/// nerkh.io and brsapi.ir were registered without a timeout, so they carried
/// <see cref="HttpClient"/>'s 100-second default while the four sources added later set fifteen
/// seconds. Nothing in the code marked the difference and nothing could fail because of it until a
/// source accepted a connection and then went quiet — at which point a tick fetching a price per
/// symbol could outlast the six-minute lease that stops two instances publishing at once (#160).
/// </para>
/// </summary>
public class ReferencePriceClientTimeoutTests
{
    /// <summary>
    /// <c>AutoQuotePublisherService.LeaseDuration</c>, which is private. Restated here because it
    /// is what the timeout has to be measured against, and a change to it should bring someone
    /// past this test.
    /// </summary>
    private static readonly TimeSpan PublisherLease = TimeSpan.FromMinutes(6);

    private static IHttpClientFactory Factory() =>
        new ServiceCollection()
            .AddReferencePriceClients()
            .BuildServiceProvider()
            .GetRequiredService<IHttpClientFactory>();

    [Fact]
    public void EverySourcesClient_GivesUpWellInsideTheLease()
    {
        var factory = Factory();

        foreach (var name in ReferencePriceClients.Names)
        {
            var timeout = factory.CreateClient(name).Timeout;

            Assert.True(timeout < PublisherLease,
                $"{name} would hold a tick for {timeout.TotalSeconds:F0}s, against a {PublisherLease.TotalMinutes}-minute lease.");
            Assert.Equal(ReferencePriceClients.Timeout, timeout);
        }
    }

    /// <summary>
    /// The arithmetic the issue is actually about: one symbol tries every source in turn, so the
    /// worst case for a symbol is the sum of all six timeouts — and bonbast makes two requests for
    /// its one document, so it counts twice. Symbols themselves now run concurrently, which is why
    /// this is the whole worst case for a tick rather than the worst case per symbol.
    /// </summary>
    [Fact]
    public void TheSlowestPossibleSymbol_StillFinishesInsideTheLease()
    {
        var sources = ReferencePriceClients.Names.Count();
        var requests = sources + 1; // bonbast.com: a page for the token, then the document.
        var worstCase = ReferencePriceClients.Timeout * requests;

        Assert.True(worstCase < PublisherLease,
            $"{requests} requests at {ReferencePriceClients.Timeout.TotalSeconds:F0}s is {worstCase.TotalMinutes:F1} minutes, " +
            $"against a {PublisherLease.TotalMinutes}-minute lease.");
    }

    /// <summary>
    /// A client resolved by a name nothing registered is not an error — <c>CreateClient</c> hands
    /// back a default one, on the 100-second default. That is why the names are constants rather
    /// than literals at each call site, and this is the test that would notice if they drifted
    /// apart.
    /// </summary>
    [Fact]
    public void AnUnregisteredName_WouldSilentlyGetTheDefaultTimeout()
    {
        var mistyped = Factory().CreateClient("NerkhPriceProvder");

        Assert.NotEqual(ReferencePriceClients.Timeout, mistyped.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(100), mistyped.Timeout);
    }
}
