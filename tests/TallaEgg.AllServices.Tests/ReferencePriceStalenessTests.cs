using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A price older than the symbol allows counts as no answer (issue #316).
///
/// <para>
/// Before this, a market that had closed, a feed that had frozen and a live price were
/// indistinguishable: providers returned a bare number, so the last value before a market shut
/// was published as the current one, tick after tick. <c>QuotePlausibility</c> cannot catch it —
/// it measures how far a price has moved from the last one, and the whole problem is a price that
/// has stopped moving.
/// </para>
/// </summary>
public class ReferencePriceStalenessTests
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;
    private static readonly DateTimeOffset Now = new(2026, 9, 22, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A clock that does not move, so an age is whatever the test says it is.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class StubProvider(string name, decimal? price, DateTimeOffset? asOf) : IReferencePriceProvider
    {
        public string Name => name;
        public int Calls { get; private set; }

        public Task<ReferencePrice?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(price is null ? null : (ReferencePrice?)new ReferencePrice(price.Value, asOf));
        }
    }

    /// <summary>
    /// The limit lives beside each provider's instrument mapping, under the symbol's own config
    /// block, and is absent unless set.
    /// </summary>
    private static IConfiguration WithMaxAge(int? minutes)
    {
        var values = new Dictionary<string, string?>();
        if (minutes is not null)
            values[$"Symbols:{Symbol}:MaxPriceAgeMinutes"] = minutes.Value.ToString();

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static ReferencePriceProviderChain Chain(IConfiguration configuration, params IReferencePriceProvider[] providers) =>
        new(providers, NullLogger<ReferencePriceProviderChain>.Instance, configuration, new FixedClock(Now));

    [Fact]
    public async Task APriceOlderThanTheSymbolAllows_IsSkippedForTheNextSource()
    {
        var frozen = new StubProvider("frozen", 80_000_000m, Now.AddHours(-3));
        var live = new StubProvider("live", 79_000_000m, Now.AddMinutes(-1));

        var price = await Chain(WithMaxAge(15), frozen, live).GetPriceAsync(Symbol);

        Assert.Equal(79_000_000m, price?.Price);
        Assert.Equal(1, frozen.Calls);
    }

    /// <summary>
    /// The weekend, and the reason #304's ounce needs this: every source repeats the last price
    /// before the market closed. Nothing answers, so <c>AutoQuotePublisherService</c> takes its
    /// existing "no price source answered" path and the previous quote stands — rather than being
    /// republished, with a fresh timestamp, at Friday's close.
    /// </summary>
    [Fact]
    public async Task WhenEverySourceIsStale_NothingAnswers()
    {
        var price = await Chain(WithMaxAge(15),
            new StubProvider("a", 80_000_000m, Now.AddDays(-2)),
            new StubProvider("b", 80_100_000m, Now.AddDays(-2))).GetPriceAsync(Symbol);

        Assert.Null(price);
    }

    [Fact]
    public async Task APriceInsideTheLimit_IsUsed()
    {
        var price = await Chain(WithMaxAge(15),
            new StubProvider("fresh", 80_000_000m, Now.AddMinutes(-14))).GetPriceAsync(Symbol);

        Assert.Equal(80_000_000m, price?.Price);
    }

    /// <summary>
    /// No limit configured is today's behaviour for every symbol, and has to stay that way: a
    /// symbol nobody has chosen a limit for cannot start refusing prices because this shipped.
    /// </summary>
    [Fact]
    public async Task WithNoLimitConfigured_EvenAVeryOldPriceIsUsed()
    {
        var price = await Chain(WithMaxAge(null),
            new StubProvider("ancient", 80_000_000m, Now.AddYears(-1))).GetPriceAsync(Symbol);

        Assert.Equal(80_000_000m, price?.Price);
    }

    /// <summary>
    /// A source that publishes no timestamp is not evidence of an old price. Three of the four
    /// providers report null today, so treating unknown as stale would stop those symbols quoting
    /// the moment a limit was set for them.
    /// </summary>
    [Fact]
    public async Task APriceOfUnknownAge_IsUsed()
    {
        var price = await Chain(WithMaxAge(15),
            new StubProvider("no clock", 80_000_000m, null)).GetPriceAsync(Symbol);

        Assert.Equal(80_000_000m, price?.Price);
    }

    /// <summary>
    /// Clock skew between this host and a source is not an argument against the price. A future
    /// timestamp produces a negative age, which must not read as "older than the limit".
    /// </summary>
    [Fact]
    public async Task APriceDatedInTheFuture_IsUsed()
    {
        var price = await Chain(WithMaxAge(15),
            new StubProvider("fast clock", 80_000_000m, Now.AddMinutes(2))).GetPriceAsync(Symbol);

        Assert.Equal(80_000_000m, price?.Price);
    }
}
