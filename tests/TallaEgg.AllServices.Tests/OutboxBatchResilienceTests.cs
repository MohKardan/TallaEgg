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
/// One outbox message that cannot be persisted must not stall the rest of the batch.
///
/// The per-message SaveChangesAsync used to sit outside any try/catch. A failure there threw
/// out of the whole loop: the generic handler logged "Unexpected error in the outbox
/// processing loop" without naming the row, the in-memory RetryCount was discarded so the
/// message never advanced towards Failed (and so never surfaced to an operator), and every
/// message after it in the batch was skipped for that cycle. See issue #44.
/// </summary>
public class OutboxBatchResilienceTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;

    public OutboxBatchResilienceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var setup = new OrdersDbContext(Options()))
            setup.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddScoped<OrdersDbContext>(_ => new FailFirstSaveDbContext(Options()));
        services.AddScoped<IWalletApiClient, AlwaysSettlesWalletClient>();
        _provider = services.BuildServiceProvider();
    }

    private DbContextOptions<OrdersDbContext> Options() =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    /// <summary>Fails the first SaveChangesAsync, then behaves normally.</summary>
    private sealed class FailFirstSaveDbContext : OrdersDbContext
    {
        private bool _hasFailed;

        public FailFirstSaveDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (!_hasFailed)
            {
                _hasFailed = true;
                throw new DbUpdateException("simulated persistence failure");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class AlwaysSettlesWalletClient : StubWalletApiClient
    {
        public override Task<(bool Success, string Message)> TradeTransactionAndBalanceChangeAsync(TradeDto trade) =>
            Task.FromResult((true, "settled"));
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

    private Guid SeedPendingMessage()
    {
        using var db = new OrdersDbContext(Options());
        var tradeId = Guid.NewGuid();
        var message = OutboxMessage.Create("TradeSettlement", tradeId, PayloadFor(tradeId));
        db.OutboxMessages.Add(message);
        db.SaveChanges();
        return message.Id;
    }

    [Fact]
    public async Task AMessageThatCannotBePersisted_DoesNotStopTheRestOfTheBatch()
    {
        var firstId = SeedPendingMessage();
        // Created second, so the processor's OrderBy(CreatedAt) reaches it after the failing one.
        await Task.Delay(10);
        var secondId = SeedPendingMessage();

        var processor = new OutboxProcessorService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxProcessorService>.Instance);

        // Must not throw — the loop absorbs the persistence failure.
        await processor.ProcessDueMessagesAsync(CancellationToken.None);

        using var db = new OrdersDbContext(Options());

        var first = await db.OutboxMessages.SingleAsync(m => m.Id == firstId);
        var second = await db.OutboxMessages.SingleAsync(m => m.Id == secondId);

        Assert.Equal(OutboxMessageStatus.Pending, first.Status);
        Assert.Equal(OutboxMessageStatus.Completed, second.Status);
    }
}
