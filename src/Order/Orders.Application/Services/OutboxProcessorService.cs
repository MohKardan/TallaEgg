using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Infrastructure.Clients;

namespace Orders.Application.Services;

/// <summary>
/// Drains the transactional outbox: periodically reads Pending messages that are due,
/// performs the cross-service action (calling the Wallet API), and marks each Completed
/// or schedules a retry with exponential backoff. Because the Wallet settlement is
/// idempotent (keyed on the trade id), redelivering a message that actually succeeded
/// is harmless — the wallet reports "already settled".
///
/// Assumes a single Orders.Api instance (MVP). For multi-instance, add a claim/lease
/// column so two processors can't grab the same message.
/// </summary>
public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxProcessorService> _logger;

    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;
    private const int MaxRetries = 5;
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(10);

    public OutboxProcessorService(IServiceScopeFactory scopeFactory, ILogger<OutboxProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxProcessorService started (poll every {Seconds}s).", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessDueMessagesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in the outbox processing loop.");
            }

            try
            {
                await Task.Delay(_pollInterval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("OutboxProcessorService stopped.");
    }

    private async Task ProcessDueMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var walletClient = scope.ServiceProvider.GetRequiredService<IWalletApiClient>();

        var now = DateTime.UtcNow;
        var due = await db.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending
                        && (m.NextAttemptAt == null || m.NextAttemptAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        if (due.Count == 0) return;

        foreach (var message in due)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                await DispatchAsync(message, walletClient);
                message.MarkCompleted();
                _logger.LogInformation("Outbox message {Id} ({Type}, aggregate {AggregateId}) completed.",
                    message.Id, message.Type, message.AggregateId);
            }
            catch (Exception ex)
            {
                // The stored reason is length-limited by the column; truncate so persisting
                // the failure can never itself fail and leave the message stuck as Pending.
                var reason = ex.Message.Length > 1900
                    ? ex.Message[..1900] + "…(truncated)"
                    : ex.Message;

                message.MarkAttemptFailed(reason, MaxRetries, BaseRetryDelay);
                if (message.Status == OutboxMessageStatus.Failed)
                    // A trade is now recorded but unsettled, with the participants' collateral
                    // still locked. It needs an operator: inspect /api/outbox/unsettled and
                    // re-drive once the cause is fixed (settlement is idempotent).
                    _logger.LogError(ex,
                        "SETTLEMENT STUCK — outbox message {Id} (trade {AggregateId}) permanently failed after {Retries} attempts. " +
                        "The trade is recorded but NOT settled and collateral remains locked. " +
                        "Fix the cause, then POST /api/outbox/{Id}/redrive.",
                        message.Id, message.AggregateId, message.RetryCount);
                else
                    _logger.LogWarning(ex, "Outbox message {Id} (aggregate {AggregateId}) failed attempt {Retry}; will retry.",
                        message.Id, message.AggregateId, message.RetryCount);
            }

            // Persist per message so a crash mid-batch never loses progress or double-marks.
            await db.SaveChangesAsync(ct);
        }
    }

    /// <summary>Routes a message to the right side-effect based on its type. Throws on failure so the caller schedules a retry.</summary>
    private static async Task DispatchAsync(OutboxMessage message, IWalletApiClient walletClient)
    {
        switch (message.Type)
        {
            case "TradeSettlement":
                var trade = JsonSerializer.Deserialize<TradeDto>(message.Payload)
                    ?? throw new InvalidOperationException("TradeSettlement payload could not be deserialized.");

                var (success, responseMessage) = await walletClient.TradeTransactionAndBalanceChangeAsync(trade);
                if (!success)
                    throw new InvalidOperationException($"Wallet settlement rejected: {responseMessage}");
                break;

            default:
                throw new NotSupportedException($"Unknown outbox message type '{message.Type}'.");
        }
    }
}
