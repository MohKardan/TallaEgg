using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core;
using TallaEgg.AllServices.Tests.Fakes;
using Microsoft.Extensions.Logging;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// One tick of the auto-quote publisher (issue #90): whether it publishes at all, whether what
/// it publishes is arithmetically what an admin would have typed by hand, and — since the coin
/// and Bitcoin symbols were added — whether each symbol is published independently of the
/// others.
///
/// Runs against a real <see cref="OrdersDbContext"/> (SQLite in-memory), the same pattern
/// <c>OutboxDueSelectionTests</c> uses for the other <c>BackgroundService</c> in this project —
/// the interesting bugs here are in what actually gets read from and written to the database,
/// not in isolated arithmetic.
/// </summary>
public class AutoQuotePublisherServiceTests : IDisposable
{
    private const string Symbol = CurrenciesConstant.MAUA_IRT;

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly StubProvider _stubProvider = new();
    private readonly CapturingLogger<AutoQuotePublisherService> _logger = new();

    public AutoQuotePublisherServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var setup = new OrdersDbContext(Options()))
            setup.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddScoped(_ => new OrdersDbContext(Options()));
        services.AddScoped<IAutoQuoteSettingsRepository, AutoQuoteSettingsRepository>();
        services.AddScoped<IQuoteRepository, QuoteRepository>();
        services.AddScoped<IPendingQuoteRepository, PendingQuoteRepository>();
        services.AddScoped(_ => new ReferencePriceProviderChain([_stubProvider], NullLogger<ReferencePriceProviderChain>.Instance));
        _provider = services.BuildServiceProvider();
    }

    private DbContextOptions<OrdersDbContext> Options() =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    /// <summary>
    /// The one reference price source the test container knows about; mutable per test.
    /// Returns the same price regardless of symbol asked about, since these tests each
    /// exercise one symbol's tick logic at a time — <see cref="ReferencePriceProviderChainTests"/>
    /// covers per-symbol routing at the chain level.
    /// </summary>
    private sealed class StubProvider : IReferencePriceProvider
    {
        public string Name => "stub";
        public decimal? Price { get; set; }
        public Task<decimal?> GetPriceAsync(string symbol, CancellationToken cancellationToken = default) =>
            Task.FromResult(Price);
    }

    // These tests drive PublishIfDueAsync directly, so the leader gate never runs; the lease is
    // stubbed to "yes" purely to satisfy the constructor.
    private AutoQuotePublisherService NewService() => new(
        _provider.GetRequiredService<IServiceScopeFactory>(), _logger, new AlwaysLeaderLease());

    private AutoQuotePublisherService? _service;

    /// <summary>
    /// One service instance for every tick a test runs, because the consecutive-rejection count
    /// that decides when to stop quoting lives on it.
    /// <see cref="RunTickOnAFreshServiceAsync"/> is the deliberate exception.
    /// </summary>
    private Task RunTickAsync(string symbol = Symbol) =>
        (_service ??= NewService()).PublishIfDueAsync(symbol, CancellationToken.None);

    /// <summary>
    /// A tick from a service that has just started and holds nothing in memory — a restart. What
    /// the band compares against has to come from the database for this to behave at all.
    /// </summary>
    private Task RunTickOnAFreshServiceAsync(string symbol = Symbol) =>
        NewService().PublishIfDueAsync(symbol, CancellationToken.None);

    private async Task SeedSettingsAsync(bool isEnabled, decimal spreadPercent, Guid updatedBy, string symbol = Symbol)
    {
        using var db = new OrdersDbContext(Options());
        var settings = AutoQuoteSettings.CreateDefault(symbol);
        settings.UpdateSpread(spreadPercent, updatedBy);
        settings.SetEnabled(isEnabled, updatedBy);
        db.AutoQuoteSettings.Add(settings);
        await db.SaveChangesAsync();
    }

    private async Task<List<PendingQuote>> ProposalsAsync(string symbol = Symbol)
    {
        using var db = new OrdersDbContext(Options());
        return await db.PendingQuotes.Where(p => p.Symbol == symbol).OrderBy(p => p.CreatedAt).ToListAsync();
    }

    private async Task<PendingQuote?> AwaitingApprovalAsync(string symbol = Symbol)
    {
        using var db = new OrdersDbContext(Options());
        return await db.PendingQuotes
            .SingleOrDefaultAsync(p => p.Symbol == symbol && p.Status == PendingQuoteStatus.Pending);
    }

    private async Task<Quote?> ActiveQuoteAsync(string symbol = Symbol)
    {
        using var db = new OrdersDbContext(Options());
        return await db.Quotes.Where(q => q.Symbol == symbol && q.IsActive).SingleOrDefaultAsync();
    }

    [Fact]
    public async Task PublishesNothing_WhenDisabled()
    {
        await SeedSettingsAsync(isEnabled: false, spreadPercent: 1m, Guid.NewGuid());
        _stubProvider.Price = 80_000_000m;

        await RunTickAsync();

        Assert.Null(await ActiveQuoteAsync());
    }

    [Fact]
    public async Task PublishesNothing_WhenNoProviderAnswers()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        _stubProvider.Price = null;

        await RunTickAsync();

        Assert.Null(await ActiveQuoteAsync());
    }

    /// <summary>
    /// The provider now returns Toman per traded unit directly — mesghal-to-gram conversion
    /// moved into the gold-specific providers (<c>NerkhPriceProvider</c>,
    /// <c>BrsApiPriceProvider</c>) since it doesn't apply to the coin or Bitcoin symbols. So the
    /// publisher's own math is just the spread, applied to whatever the chain returned: 1% of
    /// 1,000,000 is 10,000 either side.
    /// </summary>
    [Fact]
    public async Task PublishesTheSpreadAroundTheReferencePrice()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        _stubProvider.Price = 1_000_000m;

        await RunTickAsync();

        var quote = await ActiveQuoteAsync();
        Assert.NotNull(quote);
        Assert.Equal(995_000m, quote!.BuyPrice);
        Assert.Equal(1_005_000m, quote.SellPrice);
    }

    [Fact]
    public async Task ThePublishedQuoteBelongsToWhoeverLastConfiguredAutoQuote()
    {
        var admin = Guid.NewGuid();
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 0.5m, admin);
        _stubProvider.Price = 1_000_000m;

        await RunTickAsync();

        var quote = await ActiveQuoteAsync();
        Assert.Equal(admin, quote!.PublishedByUserId);
    }

    [Fact]
    public async Task ASecondTickReplacesTheFirstQuoteRatherThanAddingASecondActiveOne()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());

        _stubProvider.Price = 1_000_000m;
        await RunTickAsync();

        _stubProvider.Price = 1_050_000m;
        await RunTickAsync();

        using var db = new OrdersDbContext(Options());
        Assert.Single(db.Quotes.Where(q => q.Symbol == Symbol && q.IsActive));
    }

    /// <summary>
    /// Added alongside the coin and Bitcoin symbols: each symbol's <c>AutoQuoteSettings</c> row
    /// is independent, so one being enabled and another disabled must not cross-contaminate —
    /// the publisher loop in <c>ExecuteAsync</c> calls this per symbol, but the per-symbol
    /// isolation is what <see cref="PublishIfDueAsync"/> itself is responsible for.
    /// </summary>
    [Fact]
    public async Task EachSymbolPublishesIndependently()
    {
        var coin = CurrenciesConstant.SEKE_BAHAR_IRT;

        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid(), symbol: Symbol);
        await SeedSettingsAsync(isEnabled: false, spreadPercent: 1m, Guid.NewGuid(), symbol: coin);

        _stubProvider.Price = 1_000_000m;
        await RunTickAsync(Symbol);
        await RunTickAsync(coin);

        Assert.NotNull(await ActiveQuoteAsync(Symbol));
        Assert.Null(await ActiveQuoteAsync(coin));
    }

    // ---------------------------------------------------------------------------------------
    // Plausibility band (issue #158). A glitching source — a per-mithqal figure read as
    // per-gram, a misplaced decimal, a partial response parsed as a number — used to become a
    // quote customers could trade against within one two-minute tick. Nothing checked whether
    // the number was believable; the only rejections were a price <= 0 and an inverted spread.
    //
    // The audit that raised this could not reproduce it and marked it "reasoned, not executed"
    // for want of a mockable provider. The provider was already mockable — StubProvider above
    // predates the finding — so these run the real tick logic against a real database.
    // ---------------------------------------------------------------------------------------

    /// <summary>Publishes one quote from <paramref name="reference"/> so later ticks have a mid to be measured against.</summary>
    private async Task SeedPublishedQuoteAsync(decimal reference)
    {
        _stubProvider.Price = reference;
        await RunTickAsync();
        _logger.Entries.Clear();
    }

    /// <summary>
    /// The headline case, using the unit slip the issue names: nerkh.io and brsapi.ir both quote
    /// gold per mithqal natively, and a mithqal is about 4.33 grams, so a conversion that fails
    /// to happen multiplies the price by that much. 8,000,000 per gram becoming 34,640,000 is
    /// not a market move.
    /// </summary>
    [Fact]
    public async Task RejectsAReferencePriceAboveTheBand_AndKeepsThePreviousQuote()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        await SeedPublishedQuoteAsync(8_000_000m);

        _stubProvider.Price = 34_640_000m;
        await RunTickAsync();

        var quote = await ActiveQuoteAsync();
        Assert.NotNull(quote);
        Assert.Equal(7_960_000m, quote!.BuyPrice);   // still the quote seeded from 8,000,000
        Assert.Equal(8_040_000m, quote.SellPrice);
    }

    /// <summary>A stale cache or a truncated response reads low rather than high; the band is two-sided.</summary>
    [Fact]
    public async Task RejectsAReferencePriceBelowTheBand_AndKeepsThePreviousQuote()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        await SeedPublishedQuoteAsync(1_000_000m);

        _stubProvider.Price = 500_000m;
        await RunTickAsync();

        Assert.Equal(995_000m, (await ActiveQuoteAsync())!.BuyPrice);
    }

    /// <summary>
    /// Silence is the worst outcome the issue names: a skipped tick and a quiet market look
    /// identical from outside. The rejected value and the band it violated both have to be in
    /// the message, or the log cannot say what happened without re-querying the price source.
    /// </summary>
    [Fact]
    public async Task ARejectedPriceIsLoggedAsAWarningWithTheValueAndTheBand()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        await SeedPublishedQuoteAsync(1_000_000m);

        _stubProvider.Price = 2_000_000m;
        await RunTickAsync();

        var warning = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);

        // Each number is asserted together with the words that give it its meaning. Bare
        // substrings would not do: "100" for the deviation also matches inside "1000000",
        // so the assertion would still pass with the deviation missing from the message.
        Assert.Contains("held for approval", warning.Message);
        Assert.Matches(@"proposed buy 1990000(\.0+)?, sell 2010000(\.0+)?", warning.Message);
        Assert.Matches(@"is 100(\.0+)?% away from the last published mid 1000000", warning.Message);
        Assert.Contains("plausibility band of ±5%", warning.Message);
        Assert.Contains("The previous quote stands until an admin answers", warning.Message);
    }

    /// <summary>
    /// The band must not fight the market. A 4% move in two minutes would be remarkable but it is
    /// a price, not a glitch, and refusing it would leave the shop quoting a stale number.
    /// </summary>
    [Fact]
    public async Task PublishesAMoveInsideTheBand()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        await SeedPublishedQuoteAsync(1_000_000m);

        _stubProvider.Price = 1_040_000m;
        await RunTickAsync();

        Assert.Equal(1_034_800m, (await ActiveQuoteAsync())!.BuyPrice);
    }

    /// <summary>The edge is inclusive: exactly 5% publishes, a hair over does not.</summary>
    [Fact]
    public async Task PublishesAMoveExactlyOnTheBandEdge_ButNotOneJustPastIt()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        await SeedPublishedQuoteAsync(1_000_000m);

        _stubProvider.Price = 1_050_000m;              // exactly +5%, so the mid becomes 1,050,000
        await RunTickAsync();
        Assert.Equal(1_044_750m, (await ActiveQuoteAsync())!.BuyPrice);

        _stubProvider.Price = 1_102_501m;              // +5.0001% of the new mid
        await RunTickAsync();
        Assert.Equal(1_044_750m, (await ActiveQuoteAsync())!.BuyPrice);
    }

    /// <summary>
    /// Cold start, decided with the product owner: a symbol that has never had a quote has
    /// nothing for a price to be implausible relative to, and refusing would mean auto-quote
    /// could never bootstrap a newly activated symbol. It publishes, and says in the log that
    /// the band was not applied — that one tick is the only unguarded one.
    /// </summary>
    [Fact]
    public async Task PublishesTheFirstEverQuoteWithoutABandCheck_AndSaysSoInTheLog()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());

        _stubProvider.Price = 34_640_000m;             // would be rejected against any sane mid
        await RunTickAsync();

        Assert.NotNull(await ActiveQuoteAsync());
        Assert.Contains(_logger.Entries, e =>
            e.Level == LogLevel.Information && e.Message.Contains("not applied"));
    }

    /// <summary>
    /// The first tick after a restart is the case the issue asks about, and it needs no special
    /// handling: the active quote lives in the database, so a service instance that has just
    /// started still has a mid to compare against. Seeding the quote without ever running a tick
    /// is what a restart looks like from the publisher's side.
    /// </summary>
    [Fact]
    public async Task TheBandComesFromTheStoredQuote_SoARestartIsStillGuarded()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());

        using (var db = new OrdersDbContext(Options()))
        {
            db.Quotes.Add(Quote.Publish(Symbol, 7_960_000m, 8_040_000m, Guid.NewGuid()));
            await db.SaveChangesAsync();
        }

        _stubProvider.Price = 34_640_000m;
        await RunTickOnAFreshServiceAsync();

        var quote = await ActiveQuoteAsync();
        Assert.Equal(7_960_000m, quote!.BuyPrice);
        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>
    /// The handler that could never run (issue #158, same shape as #143). Quote.Publish rejects
    /// through BusinessRuleException, which derives straight from Exception, so the
    /// <c>catch (ArgumentException)</c> that stood here was unreachable and the tick threw
    /// instead — landing in the poll loop's generic handler and being recorded at Error, as a
    /// service fault rather than the ordinary rejection it is.
    ///
    /// The trigger is the one the handler's own comment describes: a price small enough that
    /// rounding to two decimals leaves nothing. 0.001 survives the chain's positive-price check
    /// and then rounds to 0.00 either side of the spread.
    /// </summary>
    [Fact]
    public async Task APriceRejectedByQuoteDotPublishIsLoggedAsAWarningInsteadOfThrowing()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());

        _stubProvider.Price = 0.001m;
        await RunTickAsync();

        Assert.Null(await ActiveQuoteAsync());
        var warning = Assert.Single(_logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.IsType<TallaEgg.Core.ErrorHandling.BusinessRuleException>(warning.Exception);
    }

    /// <summary>
    /// The band is a ratio, so it should not care how large the numbers are — but Bitcoin prices
    /// are five orders of magnitude above gold's and the simulator only ever trades MAUA/IRT
    /// (issue #147), so a symbol-specific arithmetic problem would survive a clean smoke run.
    /// Both directions at BTC scale, on the same 5% boundary.
    /// </summary>
    [Fact]
    public async Task TheBandBehavesTheSameAtBitcoinScale()
    {
        const string btc = CurrenciesConstant.BTC_IRT;

        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid(), symbol: btc);

        _stubProvider.Price = 52_000_000_000m;
        await RunTickAsync(btc);
        var seeded = await ActiveQuoteAsync(btc);
        Assert.Equal(51_740_000_000m, seeded!.BuyPrice);

        _stubProvider.Price = 54_600_000_000m;          // exactly +5%, accepted
        await RunTickAsync(btc);
        Assert.Equal(54_327_000_000m, (await ActiveQuoteAsync(btc))!.BuyPrice);

        _stubProvider.Price = 5_460_000_000m;           // a decimal slipped: 10x too low, rejected
        await RunTickAsync(btc);
        Assert.Equal(54_327_000_000m, (await ActiveQuoteAsync(btc))!.BuyPrice);
    }

    // -----------------------------------------------------------------------------------
    // Holding an out-of-band price for an admin, rather than stopping the symbol.
    //
    // The first design refused the tick and, after three in a row, deactivated the quote and
    // switched auto-quote off. That turned a suspicious price into a silent outage, and it could
    // be walked around: with the quote deactivated the symbol had no anchor, so re-enabling
    // auto-quote hit the cold-start path and published the very price that had been refused. Seen
    // happening in a live session, which is what produced this design.
    // -----------------------------------------------------------------------------------

    /// <summary>The price is not published, and it is recorded as a question rather than dropped.</summary>
    [Fact]
    public async Task AnOutOfBandPriceIsHeldForApprovalInsteadOfPublished()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        await SeedPublishedQuoteAsync(1_000_000m);

        _stubProvider.Price = 2_000_000m;
        await RunTickAsync();

        Assert.Equal(995_000m, (await ActiveQuoteAsync())!.BuyPrice);   // the previous quote stands

        var held = await AwaitingApprovalAsync();
        Assert.NotNull(held);
        Assert.Equal(QuoteSource.Auto, held!.Source);
        Assert.Equal(1_990_000m, held.BuyPrice);
        Assert.Equal(2_010_000m, held.SellPrice);
        Assert.Equal(1_000_000m, held.PreviousMid);
        Assert.Equal(100m, held.DeviationPercent);
    }

    /// <summary>
    /// Auto-quote stays on. Switching the feature off was the old design's mistake: deciding what
    /// gets published is the band's job, and turning the feature off is the admin's.
    /// </summary>
    [Fact]
    public async Task HoldingAPriceDoesNotDisableAutoQuoteOrDeactivateTheQuote()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        await SeedPublishedQuoteAsync(1_000_000m);

        _stubProvider.Price = 2_000_000m;
        for (var tick = 0; tick < 5; tick++) await RunTickAsync();

        using var db = new OrdersDbContext(Options());
        Assert.True((await db.AutoQuoteSettings.SingleAsync(a => a.Symbol == Symbol)).IsEnabled);
        Assert.NotNull(await ActiveQuoteAsync());
    }

    /// <summary>
    /// A newer out-of-band price replaces the one waiting. The admin should always be deciding
    /// about the newest price the shop has seen, not working through a backlog of stale ones.
    /// </summary>
    [Fact]
    public async Task ANewerOutOfBandPriceSupersedesTheOneWaiting()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        await SeedPublishedQuoteAsync(1_000_000m);

        _stubProvider.Price = 2_000_000m;
        await RunTickAsync();

        _stubProvider.Price = 3_000_000m;
        await RunTickAsync();

        var all = await ProposalsAsync();
        Assert.Equal(2, all.Count);
        Assert.Equal(PendingQuoteStatus.Superseded, all[0].Status);
        Assert.Equal(PendingQuoteStatus.Pending, all[1].Status);
        Assert.Equal(2_985_000m, all[1].BuyPrice);

        // Still exactly one question outstanding, whatever the feed does.
        Assert.NotNull(await AwaitingApprovalAsync());
    }

    /// <summary>A price that comes back inside the band publishes normally, question or no question.</summary>
    [Fact]
    public async Task APriceThatReturnsToTheBandPublishesUnaided()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        await SeedPublishedQuoteAsync(1_000_000m);

        _stubProvider.Price = 2_000_000m;
        await RunTickAsync();

        _stubProvider.Price = 1_020_000m;
        await RunTickAsync();

        Assert.Equal(1_014_900m, (await ActiveQuoteAsync())!.BuyPrice);
    }

    /// <summary>One symbol waiting on an answer must not hold up another.</summary>
    [Fact]
    public async Task AQuestionAboutOneSymbolDoesNotBlockAnother()
    {
        var coin = CurrenciesConstant.SEKE_BAHAR_IRT;

        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid(), symbol: Symbol);
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid(), symbol: coin);

        _stubProvider.Price = 1_000_000m;
        await RunTickAsync(Symbol);
        await RunTickAsync(coin);

        _stubProvider.Price = 2_000_000m;
        await RunTickAsync(Symbol);
        await RunTickAsync(coin);

        Assert.NotNull(await AwaitingApprovalAsync(Symbol));
        Assert.NotNull(await AwaitingApprovalAsync(coin));
        Assert.Equal(995_000m, (await ActiveQuoteAsync(Symbol))!.BuyPrice);
        Assert.Equal(995_000m, (await ActiveQuoteAsync(coin))!.BuyPrice);
    }

    /// <summary>
    /// Approving publishes the held price, and the quote is attributed to whoever proposed it —
    /// approval says "this price is real", it does not make the approver its author.
    /// </summary>
    [Fact]
    public async Task ApprovingAHeldPricePublishesIt()
    {
        var configuringAdmin = Guid.NewGuid();
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, configuringAdmin);
        await SeedPublishedQuoteAsync(1_000_000m);

        _stubProvider.Price = 2_000_000m;
        await RunTickAsync();

        var held = (await AwaitingApprovalAsync())!;
        var approver = Guid.NewGuid();

        using (var scope = _provider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPendingQuoteRepository>();
            await repo.ApproveAsync((await repo.GetAsync(held.Id))!, approver);
        }

        var published = await ActiveQuoteAsync();
        Assert.Equal(1_990_000m, published!.BuyPrice);
        Assert.Equal(configuringAdmin, published.PublishedByUserId);

        using var db = new OrdersDbContext(Options());
        var resolved = await db.PendingQuotes.SingleAsync(p => p.Id == held.Id);
        Assert.Equal(PendingQuoteStatus.Approved, resolved.Status);
        Assert.Equal(approver, resolved.ResolvedByUserId);
    }

    /// <summary>Rejecting publishes nothing and leaves the previous quote in force.</summary>
    [Fact]
    public async Task RejectingAHeldPriceLeavesThePreviousQuoteInForce()
    {
        await SeedSettingsAsync(isEnabled: true, spreadPercent: 1m, Guid.NewGuid());
        await SeedPublishedQuoteAsync(1_000_000m);

        _stubProvider.Price = 2_000_000m;
        await RunTickAsync();

        var held = (await AwaitingApprovalAsync())!;

        using (var scope = _provider.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IPendingQuoteRepository>();
            await repo.RejectAsync((await repo.GetAsync(held.Id))!, Guid.NewGuid());
        }

        Assert.Equal(995_000m, (await ActiveQuoteAsync())!.BuyPrice);
        Assert.Null(await AwaitingApprovalAsync());
    }
}