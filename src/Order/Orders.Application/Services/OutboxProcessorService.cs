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

    /// <summary>
    /// internal rather than private so the batch-resilience behaviour required by issue #44
    /// can be tested directly, without driving the timing of the background loop.
    /// </summary>
    internal async Task ProcessDueMessagesAsync(CancellationToken ct)
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

                // پس از تسویه، اگر سفارشی کاملاً پر شده باشد باقی‌ماندهٔ وثیقه‌اش آزاد
                // می‌شود.
                //
                // چرا اینجا و نه بلافاصله پس از تطبیق: قفلِ موجودی بعد از تطبیق ساخته
                // می‌شود (یافتهٔ C-5) و همین‌جا تازه مصرف شده است. اجرای زودتر با هر دو
                // مسابقه می‌داد — و در تست واقعی روی کیف پولی اجرا شد که هنوز چیزی در
                // آن قفل نشده بود و شکست خورد (issue #52).
                //
                // این فراخوانی گارد خودش را دارد و هرگز استثنا بیرون نمی‌دهد. اگر می‌داد،
                // بلوک catch پایین یک پیامِ outbox که واقعاً موفق شده را «شکست‌خورده»
                // علامت می‌زد و دوباره تحویلش می‌داد.
                await ReleaseResidualCollateralAsync(message, scope);
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
            //
            // This save has its own guard on purpose. It used to sit outside any try/catch, so a
            // persistence failure on one message threw out of the whole loop: the generic handler
            // logged "Unexpected error in the outbox processing loop" without naming the row, the
            // in-memory RetryCount was discarded so the message never advanced towards Failed, and
            // every remaining message in the batch was skipped for that cycle — one bad row could
            // stall settlement for all the others. See issue #44.
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception saveEx)
            {
                _logger.LogError(saveEx,
                    "Could not persist the outcome of outbox message {Id} (trade {AggregateId}). " +
                    "Its state is unchanged and it will be retried; continuing with the rest of the batch.",
                    message.Id, message.AggregateId);

                // Detach only THIS message, so the next message's save does not retry the write
                // that just failed. Detaching everything would also drop the messages still to be
                // processed in this batch, silently leaving them Pending — which is the very
                // stall this guard exists to prevent.
                db.Entry(message).State = EntityState.Detached;
            }
        }
    }

    /// <summary>
    /// پس از تسویهٔ موفقِ یک معامله، باقی‌ماندهٔ وثیقهٔ هر سفارشی که با آن کامل شده را آزاد می‌کند.
    ///
    /// عمداً هیچ استثنایی بیرون نمی‌دهد: تسویه از قبل موفق بوده و نباید به‌خاطر این کارِ
    /// جانبی «شکست‌خورده» علامت بخورد و دوباره تحویل داده شود. اگر آزادسازی انجام نشود
    /// هیچ پولی گم نمی‌شود — باقی‌مانده قفل می‌ماند و مغایرت‌گیری (#39) می‌تواند بعداً
    /// برش دارد.
    /// </summary>
    private async Task ReleaseResidualCollateralAsync(OutboxMessage message, IServiceScope scope)
    {
        if (message.Type != "TradeSettlement") return;

        try
        {
            var trade = JsonSerializer.Deserialize<TradeDto>(message.Payload);
            if (trade is null) return;

            var reconciler = scope.ServiceProvider.GetRequiredService<OrderCollateralReconciler>();

            await reconciler.ReleaseResidualLockIfCompletedAsync(trade.BuyOrderId);
            await reconciler.ReleaseResidualLockIfCompletedAsync(trade.SellOrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error releasing residual collateral after settling trade {AggregateId}.", message.AggregateId);
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
