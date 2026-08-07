using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;

namespace Wallet.Tests;

/// <summary>
/// Which gold price source wins when more than one is configured (issue #90). The whole point
/// of having two providers (nerkh.io, brsapi.ir) is that one being down does not stop automatic
/// quoting — this is what proves the fallback actually falls back.
/// </summary>
public class GoldPriceProviderChainTests
{
    private sealed class StubProvider : IGoldPriceProvider
    {
        public string Name { get; init; } = "stub";
        public decimal? Price { get; init; }

        public Task<decimal?> GetMesghalPriceAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Price);
    }

    [Fact]
    public async Task ReturnsThePriceFromTheFirstProviderThatAnswers()
    {
        var chain = new GoldPriceProviderChain(
            [new StubProvider { Name = "first", Price = 80_000_000m }, new StubProvider { Name = "second", Price = 79_000_000m }],
            NullLogger<GoldPriceProviderChain>.Instance);

        var price = await chain.GetMesghalPriceAsync();

        Assert.Equal(80_000_000m, price);
    }

    [Fact]
    public async Task FallsBackToTheNextProvider_WhenTheFirstReturnsNull()
    {
        var chain = new GoldPriceProviderChain(
            [new StubProvider { Name = "down", Price = null }, new StubProvider { Name = "up", Price = 79_000_000m }],
            NullLogger<GoldPriceProviderChain>.Instance);

        var price = await chain.GetMesghalPriceAsync();

        Assert.Equal(79_000_000m, price);
    }

    [Fact]
    public async Task ReturnsNull_WhenEveryProviderFails()
    {
        var chain = new GoldPriceProviderChain(
            [new StubProvider { Name = "a", Price = null }, new StubProvider { Name = "b", Price = null }],
            NullLogger<GoldPriceProviderChain>.Instance);

        var price = await chain.GetMesghalPriceAsync();

        Assert.Null(price);
    }

    /// <summary>
    /// A provider returning zero or a negative number is a malformed answer, not a usable
    /// price — the chain must treat it the same as "no answer" and move on, not publish a
    /// quote built on garbage.
    /// </summary>
    [Fact]
    public async Task TreatsAZeroOrNegativePriceAsAFailure()
    {
        var chain = new GoldPriceProviderChain(
            [new StubProvider { Name = "broken", Price = 0m }, new StubProvider { Name = "good", Price = 79_000_000m }],
            NullLogger<GoldPriceProviderChain>.Instance);

        var price = await chain.GetMesghalPriceAsync();

        Assert.Equal(79_000_000m, price);
    }

    [Fact]
    public async Task AnEmptyProviderListReturnsNull()
    {
        var chain = new GoldPriceProviderChain([], NullLogger<GoldPriceProviderChain>.Instance);

        Assert.Null(await chain.GetMesghalPriceAsync());
    }
}
