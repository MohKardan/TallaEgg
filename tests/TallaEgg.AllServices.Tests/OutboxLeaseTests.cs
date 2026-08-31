using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Two instances of the outbox processor must not dispatch the same message (issue #160).
///
/// The processor used to select due messages with a plain query and start work on whatever came
/// back, so two instances read the same rows and both called the wallet. Nothing was paid twice —
/// <c>TradeSettlement</c> is keyed on the trade id and the second settlement was refused — but the
/// duplicate work was invisible, and the "single instance only" rule that made it acceptable lived
/// in a code comment.
///
/// These tests drive two processors with different identities against one database, which is what
/// two hosts sharing a database look like from the outbox's point of view.
/// </summary>
public class OutboxLeaseTests : IDisposable
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(10);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly CountingWalletClient _wallet = new();

    public OutboxLeaseTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var setup = new OrdersDbContext(Options()))
            setup.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddScoped(_ => new OrdersDbContext(Options()));
        services.AddScoped<IWalletApiClient>(_ => _wallet);
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
    /// Counts settlement calls and can be told to block, so a second processor can be run while
    /// the first is still mid-dispatch — which is the only moment the lease has to hold.
    /// </summary>
    private sealed class CountingWalletClient : StubWalletApiClient
    {
        private readonly List<Guid> _settled = new();

        public IReadOnlyList<Guid> SettledTradeIds
        {
            get { lock (_settled) return _settled.ToList(); }
        }

        /// <summary>Set to have the client fail, so the failure path can be checked.</summary>
        public bool Reject { get; set; }

        /// <summary>Runs while the wallet call is in flight. Used to act as a second instance mid-dispatch.</summary>
        public Func<Task>? WhileDispatching { get; set; }

        public override async Task<(bool Success, string Message)> TradeTransactionAndBalanceChangeAsync(TradeDto trade)
        {
            lock (_settled) _settled.Add(trade.Id);

            if (WhileDispatching is not null)
                await WhileDispatching();

            return Reject ? (false, "rejected") : (true, "settled");
        }
    }

    private OutboxProcessorService ProcessorFor(string instance) => new(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        new InstanceIdentity(instance),
        NullLogger<OutboxProcessorService>.Instance);

    private static string PayloadFor(Guid tradeId) =>
        System.Text.Json.JsonSerializer.Serialize(new TradeDto
        {
            Id = tradeId,
            BuyerUserId = Guid.NewGuid(),
            SellerUserId = Guid.NewGuid(),
            Symbol = "MAUA/IRT",
            Quantity = 10m,
            QuoteQuantity = 184_680_733m
        });

    private (Guid MessageId, Guid TradeId) Seed(Action<OutboxMessage>? arrange = null)
    {
        using var db = new OrdersDbContext(Options());
        var tradeId = Guid.NewGuid();
        var message = OutboxMessage.Create("TradeSettlement", tradeId, PayloadFor(tradeId));
        arrange?.Invoke(message);
        db.OutboxMessages.Add(message);
        db.SaveChanges();
        return (message.Id, tradeId);
    }

    private async Task<OutboxMessage> ReloadAsync(Guid messageId)
    {
        using var db = new OrdersDbContext(Options());
        return await db.OutboxMessages.AsNoTracking().SingleAsync(m => m.Id == messageId);
    }

    /// <summary>
    /// The claim is what this whole change is for. While one instance is mid-dispatch, a second
    /// one runs a full cycle over the same database and must find nothing to do.
    ///
    /// Running the second processor from inside the wallet call is deliberate: at that moment the
    /// message is claimed but not yet completed, which is the only window in which the old code
    /// could hand the same settlement to both.
    /// </summary>
    [Fact]
    public async Task WhileOneInstanceIsDispatching_ASecondInstanceSettlesNothing()
    {
        var (messageId, tradeId) = Seed();

        var second = ProcessorFor("instance-b");

        // Fires once. Without the claim the second processor dispatches the same message, which
        // would re-enter this hook and recurse until the test host died — a crash that takes the
        // whole run's results with it. Firing once turns that regression into a failed assertion
        // on the settlement count.
        var secondHasRun = false;
        _wallet.WhileDispatching = async () =>
        {
            if (secondHasRun) return;
            secondHasRun = true;

            await second.ProcessDueMessagesAsync(CancellationToken.None);
        };

        await ProcessorFor("instance-a").ProcessDueMessagesAsync(CancellationToken.None);

        Assert.Equal(new[] { tradeId }, _wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Completed, (await ReloadAsync(messageId)).Status);
    }

    /// <summary>
    /// A message another instance holds is not picked up at all — the same rule as above, stated
    /// against a lease sitting in the database rather than one taken during the test.
    /// </summary>
    [Fact]
    public async Task AMessageLeasedByAnotherInstance_IsNotPickedUp()
    {
        var (messageId, _) = Seed();
        LeaseTo(messageId, "instance-b", DateTime.UtcNow.AddMinutes(1));

        await ProcessorFor("instance-a").ProcessDueMessagesAsync(CancellationToken.None);

        Assert.Empty(_wallet.SettledTradeIds);

        var message = await ReloadAsync(messageId);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal("instance-b", message.LeasedBy); // claim untouched
    }

    /// <summary>
    /// The expiry is what separates a lease from a lock. An instance that died holding a message
    /// must not strand it: a recorded trade nobody settles leaves the participants' collateral
    /// locked with nothing to release it.
    /// </summary>
    [Fact]
    public async Task AMessageWhoseLeaseHasExpired_IsClaimedAgain()
    {
        var (messageId, tradeId) = Seed();
        LeaseTo(messageId, "instance-that-died", DateTime.UtcNow.AddMinutes(-1));

        await ProcessorFor("instance-a").ProcessDueMessagesAsync(CancellationToken.None);

        Assert.Equal(new[] { tradeId }, _wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Completed, (await ReloadAsync(messageId)).Status);
    }

    [Fact]
    public async Task AfterASuccessfulDispatch_TheLeaseIsReleased()
    {
        var (messageId, _) = Seed();

        await ProcessorFor("instance-a").ProcessDueMessagesAsync(CancellationToken.None);

        var message = await ReloadAsync(messageId);
        Assert.Null(message.LeasedBy);
        Assert.Null(message.LeaseExpiresAt);
    }

    /// <summary>
    /// A failed attempt releases the claim too. Holding it would add the remainder of the lease to
    /// the backoff that has already been scheduled, delaying the retry for no reason.
    /// </summary>
    [Fact]
    public async Task AfterAFailedDispatch_TheLeaseIsReleasedAndTheBackoffDecidesTheRetry()
    {
        var (messageId, _) = Seed();
        _wallet.Reject = true;

        await ProcessorFor("instance-a").ProcessDueMessagesAsync(CancellationToken.None);

        var message = await ReloadAsync(messageId);
        Assert.Null(message.LeasedBy);
        Assert.Null(message.LeaseExpiresAt);
        Assert.Equal(1, message.RetryCount);
        Assert.NotNull(message.NextAttemptAt);
    }

    /// <summary>
    /// An operator re-driving a message clears whatever claim was left on it, so "run this now"
    /// means now rather than after a lease belonging to an instance that already failed.
    /// </summary>
    [Fact]
    public async Task ReDrivingAFailedMessage_ClearsAStaleLease()
    {
        var (messageId, _) = Seed(arrange: m =>
        {
            for (var i = 0; i < MaxRetries; i++)
                m.MarkAttemptFailed("boom", MaxRetries, BaseDelay);
        });

        LeaseTo(messageId, "instance-that-died", DateTime.UtcNow.AddMinutes(30));

        using (var db = new OrdersDbContext(Options()))
        {
            var message = await db.OutboxMessages.SingleAsync(m => m.Id == messageId);
            message.ResetForRetry();
            await db.SaveChangesAsync();
        }

        var reloaded = await ReloadAsync(messageId);
        Assert.Null(reloaded.LeasedBy);
        Assert.Null(reloaded.LeaseExpiresAt);
    }

    /// <summary>
    /// Writes a claim straight to the database, the way another instance would have.
    /// The entity deliberately has no public way to do this — claiming is one atomic UPDATE in the
    /// processor, not something a caller can do to a loaded object.
    /// </summary>
    private void LeaseTo(Guid messageId, string owner, DateTime expiresAt)
    {
        using var db = new OrdersDbContext(Options());
        db.OutboxMessages
            .Where(m => m.Id == messageId)
            .ExecuteUpdate(s => s
                .SetProperty(m => m.LeasedBy, owner)
                .SetProperty(m => m.LeaseExpiresAt, expiresAt));
    }
}
