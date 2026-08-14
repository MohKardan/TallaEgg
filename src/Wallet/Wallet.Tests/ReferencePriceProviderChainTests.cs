using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using TallaEgg.Core;

namespace Wallet.Tests;

/// <summary>
/// Which reference price source wins when more than one is configured (issue #90). The whole
/// point of having two providers (nerkh.io, brsapi.ir) is that one being down does not stop
/// automatic quoting — this is what proves the fallback actually falls back.
/// </summary>
public class ReferencePriceProviderChainTests
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;

    private sealed class StubProvider : IReferencePriceProvider
    {
        public string Name { get; init; } = "stub";
        public decimal? Price { get; init; }

        public Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default) =>
            Task.FromResult(Price);
    }

    [Fact]
    public async Task ReturnsThePriceFromTheFirstProviderThatAnswers()
    {
        var chain = new ReferencePriceProviderChain(
            [new StubProvider { Name = "first", Price = 80_000_000m }, new StubProvider { Name = "second", Price = 79_000_000m }],
            NullLogger<ReferencePriceProviderChain>.Instance);

        var price = await chain.GetPriceAsync(Symbol);

        Assert.Equal(80_000_000m, price);
    }

    [Fact]
    public async Task FallsBackToTheNextProvider_WhenTheFirstReturnsNull()
    {
        var chain = new ReferencePriceProviderChain(
            [new StubProvider { Name = "down", Price = null }, new StubProvider { Name = "up", Price = 79_000_000m }],
            NullLogger<ReferencePriceProviderChain>.Instance);

        var price = await chain.GetPriceAsync(Symbol);

        Assert.Equal(79_000_000m, price);
    }

    [Fact]
    public async Task ReturnsNull_WhenEveryProviderFails()
    {
        var chain = new ReferencePriceProviderChain(
            [new StubProvider { Name = "a", Price = null }, new StubProvider { Name = "b", Price = null }],
            NullLogger<ReferencePriceProviderChain>.Instance);

        var price = await chain.GetPriceAsync(Symbol);

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
        var chain = new ReferencePriceProviderChain(
            [new StubProvider { Name = "broken", Price = 0m }, new StubProvider { Name = "good", Price = 79_000_000m }],
            NullLogger<ReferencePriceProviderChain>.Instance);

        var price = await chain.GetPriceAsync(Symbol);

        Assert.Equal(79_000_000m, price);
    }

    [Fact]
    public async Task AnEmptyProviderListReturnsNull()
    {
        var chain = new ReferencePriceProviderChain([], NullLogger<ReferencePriceProviderChain>.Instance);

        Assert.Null(await chain.GetPriceAsync(Symbol));
    }

    /// <summary>
    /// The symbol asked about is passed through to each provider untouched — the chain has no
    /// symbol logic of its own, only fallback ordering. A stub that only answers for one
    /// specific symbol proves the chain isn't substituting or ignoring what it was asked for.
    /// </summary>
    [Fact]
    public async Task PassesTheRequestedSymbolThroughToEachProvider()
    {
        var sekeOnly = new FakeSymbolAwareProvider(CurrenciesConstant.SEKE_BAHAR_IRT, 187_800_000m);

        var chain = new ReferencePriceProviderChain([sekeOnly], NullLogger<ReferencePriceProviderChain>.Instance);

        Assert.Null(await chain.GetPriceAsync(CurrenciesConstant.MAUA_IRT));
        Assert.Equal(187_800_000m, await chain.GetPriceAsync(CurrenciesConstant.SEKE_BAHAR_IRT));
    }

    private sealed class FakeSymbolAwareProvider(string knownSymbol, decimal price) : IReferencePriceProvider
    {
        public string Name => "fake";

        public Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default) =>
            Task.FromResult(symbol == knownSymbol ? price : (decimal?)null);
    }
}
