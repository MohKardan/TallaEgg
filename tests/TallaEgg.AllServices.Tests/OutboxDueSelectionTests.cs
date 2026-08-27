using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The outbox processor must pick up exactly the messages that are due, and none of the others
/// (issue #46).
///
/// This selection is the only thing separating a retry from an unbroken hammering. Exponential
/// backoff is proven in <see cref="OutboxMessageTests"/> — but that only advances
/// <c>NextAttemptAt</c>. If the query ignores it, the backoff has no effect and a message that
/// felled the wallet service keeps hitting it every five seconds until its attempts run out.
///
/// Likewise a <c>Failed</c> message must not be picked up again: doing so would make the manual
/// re-drive path (#39) meaningless, since the system would keep repeating the very action an
/// operator is supposed to take deliberately.
/// </summary>
public class OutboxDueSelectionTests : IDisposable
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(10);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly RecordingWalletClient _wallet = new();

    public OutboxDueSelectionTests()
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

    /// <summary>Accepts every settlement and records the trade id it was called for.</summary>
    private sealed class RecordingWalletClient : StubWalletApiClient
    {
        public List<Guid> SettledTradeIds { get; } = [];

        public override Task<(bool Success, string Message)> TradeTransactionAndBalanceChangeAsync(TradeDto trade)
        {
            SettledTradeIds.Add(trade.Id);
            return Task.FromResult((true, "settled"));
        }
    }

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

    /// <summary>Stores one message, allowing its state to be adjusted before saving.</summary>
    private (Guid MessageId, Guid TradeId) Seed(string type = "TradeSettlement", Action<OutboxMessage>? arrange = null)
    {
        using var db = new OrdersDbContext(Options());
        var tradeId = Guid.NewGuid();
        var message = OutboxMessage.Create(type, tradeId, PayloadFor(tradeId));
        arrange?.Invoke(message);
        db.OutboxMessages.Add(message);
        db.SaveChanges();
        return (message.Id, tradeId);
    }

    private async Task RunProcessorAsync()
    {
        var processor = new OutboxProcessorService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxProcessorService>.Instance);

        await processor.ProcessDueMessagesAsync(CancellationToken.None);
    }

    private async Task<OutboxMessage> ReloadAsync(Guid messageId)
    {
        using var db = new OrdersDbContext(Options());
        return await db.OutboxMessages.SingleAsync(m => m.Id == messageId);
    }

    // ── What gets picked up ─────────────────────────────────────────────────────

    [Fact]
    public async Task AFreshPendingMessage_IsProcessed()
    {
        var (messageId, tradeId) = Seed();

        await RunProcessorAsync();

        Assert.Equal([tradeId], _wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Completed, (await ReloadAsync(messageId)).Status);
    }

    /// <summary>
    /// A message that has failed once is skipped until it comes due. Without this filter,
    /// exponential backoff is just a number in the database and takes no pressure off a service that
    /// is already struggling.
    /// </summary>
    [Fact]
    public async Task APendingMessageWhoseRetryIsNotDueYet_IsSkipped()
    {
        var (messageId, _) = Seed(arrange: m => m.MarkAttemptFailed("boom", MaxRetries, BaseDelay));

        await RunProcessorAsync();

        Assert.Empty(_wallet.SettledTradeIds);

        var message = await ReloadAsync(messageId);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(1, message.RetryCount); // تلاشی مصرف نشد
    }

    /// <summary>
    /// A permanently failed message is skipped. Picking it up would repeat the "settlement stuck"
    /// log without end and make an operator's re-drive meaningless.
    /// </summary>
    [Fact]
    public async Task AFailedMessage_IsSkipped()
    {
        var (messageId, _) = Seed(arrange: m =>
        {
            for (var i = 0; i < MaxRetries; i++)
                m.MarkAttemptFailed("boom", MaxRetries, BaseDelay);
        });

        Assert.Equal(OutboxMessageStatus.Failed, (await ReloadAsync(messageId)).Status);

        await RunProcessorAsync();

        Assert.Empty(_wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Failed, (await ReloadAsync(messageId)).Status);
    }

    /// <summary>
    /// An abandoned message (issue #39) is skipped too, for the same reason as a <c>Failed</c> one:
    /// picking it up would override an operator's deliberate decision that it will never settle.
    /// </summary>
    [Fact]
    public async Task AnAbandonedMessage_IsSkipped()
    {
        var (messageId, _) = Seed(arrange: m =>
        {
            for (var i = 0; i < MaxRetries; i++)
                m.MarkAttemptFailed("boom", MaxRetries, BaseDelay);
            m.MarkAbandoned("collateral consumed by later activity");
        });

        await RunProcessorAsync();

        Assert.Empty(_wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Abandoned, (await ReloadAsync(messageId)).Status);
    }

    /// <summary>
    /// A completed message is not redelivered. Settlement is idempotent, so this is safety rather
    /// than correctness — but every redelivery is a pointless HTTP call to the wallet service.
    /// </summary>
    [Fact]
    public async Task ACompletedMessage_IsSkipped()
    {
        var (messageId, _) = Seed(arrange: m => m.MarkCompleted());

        await RunProcessorAsync();

        Assert.Empty(_wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Completed, (await ReloadAsync(messageId)).Status);
    }

    /// <summary>
    /// A message that has come due must be picked up — otherwise the filter above becomes "never
    /// retry" and produces exactly the stuck settlement the outbox exists to prevent. Being due is
    /// simulated by placing the timestamp in the past, so the test does not have to wait.
    /// </summary>
    [Fact]
    public async Task APendingMessageWhoseRetryHasComeDue_IsProcessed()
    {
        var (messageId, tradeId) = Seed(arrange: m =>
            m.MarkAttemptFailed("boom", MaxRetries, TimeSpan.FromSeconds(-30)));

        await RunProcessorAsync();

        Assert.Equal([tradeId], _wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Completed, (await ReloadAsync(messageId)).Status);
    }

    // ── Unknown message type ────────────────────────────────────────────────────

    /// <summary>
    /// A message type with no dispatch path must be refused explicitly, not silently completed.
    ///
    /// If <c>default</c> marked the message <c>Completed</c>, adding a new message type without
    /// writing its dispatch path would produce a drained queue that had done nothing, leaving no
    /// trace. The error message has to name the type, because that is the operator's only clue in
    /// <c>LastError</c>.
    /// </summary>
    [Fact]
    public async Task AnUnknownMessageType_FailsLoudlyAndNamesTheType()
    {
        var (messageId, _) = Seed(type: "SomethingNobodyWrote");

        await RunProcessorAsync();

        Assert.Empty(_wallet.SettledTradeIds);

        var message = await ReloadAsync(messageId);
        Assert.NotEqual(OutboxMessageStatus.Completed, message.Status);
        Assert.Equal(1, message.RetryCount);
        Assert.Contains("SomethingNobodyWrote", message.LastError);
    }

    /// <summary>
    /// Malformed content behaves the same way: a recorded failed attempt, not an exception that
    /// escapes the loop and drops the rest of the batch.
    /// </summary>
    [Fact]
    public async Task AnUndeserializablePayload_IsRecordedAsAFailedAttempt()
    {
        Guid messageId;
        using (var db = new OrdersDbContext(Options()))
        {
            var message = OutboxMessage.Create("TradeSettlement", Guid.NewGuid(), "this is not json");
            db.OutboxMessages.Add(message);
            db.SaveChanges();
            messageId = message.Id;
        }

        await RunProcessorAsync();

        var reloaded = await ReloadAsync(messageId);
        Assert.Equal(OutboxMessageStatus.Pending, reloaded.Status); // هنوز سهمیهٔ تلاش دارد
        Assert.Equal(1, reloaded.RetryCount);
        Assert.False(string.IsNullOrWhiteSpace(reloaded.LastError));
    }
}
