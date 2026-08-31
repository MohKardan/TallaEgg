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
/// Safe to run on more than one instance (issue #160): each message is claimed with a lease
/// before it is dispatched, so two processors cannot both take the same one. That idempotency
/// remains the backstop, not the mechanism — it bounded the damage while nothing enforced the
/// single-instance assumption this replaces.
/// </summary>
public class OutboxProcessorService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly InstanceIdentity _identity;
    private readonly ILogger<OutboxProcessorService> _logger;

    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 20;
    private const int MaxRetries = 5;
    private static readonly TimeSpan BaseRetryDelay = TimeSpan.FromSeconds(10);

    /// <summary>
    /// How long a claimed message stays claimed. Long enough that a slow wallet call cannot have
    /// its message taken away mid-flight, short enough that a processor killed mid-message does
    /// not strand a settlement for long. Dispatch is a single HTTP call measured in seconds.
    /// </summary>
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(2);

    public OutboxProcessorService(
        IServiceScopeFactory scopeFactory,
        InstanceIdentity identity,
        ILogger<OutboxProcessorService> logger)
    {
        _scopeFactory = scopeFactory;
        _identity = identity;
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

        var due = await ClaimDueMessagesAsync(db, ct);

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

                // After settlement, release the residual collateral of any order that is now
                // fully filled.
                //
                // Why here and not straight after matching: the balance lock is created after the
                // match (finding C-5) and is only consumed at this point. Running earlier raced
                // both, and in a real test ran against a wallet that had nothing locked in it yet
                // and failed (issue #52).
                //
                // This call guards itself and never lets an exception escape. If it did, the catch
                // block below would mark an outbox message that genuinely succeeded as failed and
                // redeliver it.
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
                    // The template had four placeholders and three arguments: {Id} appeared
                    // again in the re-drive URL. Placeholders bind positionally, so the
                    // fourth had nothing to bind to and the operator's one actionable line
                    // ended in a literal "{Id}" instead of the id to re-drive. Naming the
                    // repeat separately and passing it keeps the URL copy-pasteable.
                    _logger.LogError(ex,
                        "SETTLEMENT STUCK — outbox message {Id} (trade {AggregateId}) permanently failed after {Retries} attempts. " +
                        "The trade is recorded but NOT settled and collateral remains locked. " +
                        "Fix the cause, then POST /api/outbox/{RedriveId}/redrive.",
                        message.Id, message.AggregateId, message.RetryCount, message.Id);
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
    /// Takes ownership of the due messages and returns the ones this instance won (issue #160).
    ///
    /// <para>
    /// The claim is the point of this method. It used to be a plain SELECT, which meant two
    /// instances read the same rows and both dispatched them; the second settlement was refused by
    /// the wallet's key on the trade id, so nothing was paid twice, but nothing prevented the
    /// duplicate work either and nothing said the constraint existed.
    /// </para>
    ///
    /// <para>
    /// Selecting the candidates and claiming them are two statements, but only the second decides
    /// anything: it repeats every condition from the first in its own WHERE clause, so a row that
    /// another instance claimed in between simply is not updated. Reading first and trusting what
    /// was read would reintroduce the race — both instances would see "unclaimed" before either
    /// wrote. The rows are then re-read by owner, which is why the count from the claim is not
    /// itself used as the batch.
    /// </para>
    ///
    /// The claim runs before any message is tracked, because <c>ExecuteUpdateAsync</c> goes
    /// straight to the database and would not be reflected in entities already loaded.
    /// </summary>
    private async Task<List<OutboxMessage>> ClaimDueMessagesAsync(OrdersDbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var leaseExpiresAt = now.Add(LeaseDuration);

        var candidateIds = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Status == OutboxMessageStatus.Pending
                        && (m.NextAttemptAt == null || m.NextAttemptAt <= now)
                        && (m.LeaseExpiresAt == null || m.LeaseExpiresAt <= now))
            .OrderBy(m => m.CreatedAt)
            .Take(BatchSize)
            .Select(m => m.Id)
            .ToListAsync(ct);

        if (candidateIds.Count == 0) return new List<OutboxMessage>();

        var claimed = await db.OutboxMessages
            .Where(m => candidateIds.Contains(m.Id)
                        && m.Status == OutboxMessageStatus.Pending
                        && (m.NextAttemptAt == null || m.NextAttemptAt <= now)
                        && (m.LeaseExpiresAt == null || m.LeaseExpiresAt <= now))
            .ExecuteUpdateAsync(s => s
                .SetProperty(m => m.LeasedBy, _identity.Value)
                .SetProperty(m => m.LeaseExpiresAt, leaseExpiresAt), ct);

        // Every candidate went to another instance. Normal under contention, not a problem.
        if (claimed == 0) return new List<OutboxMessage>();

        return await db.OutboxMessages
            .Where(m => candidateIds.Contains(m.Id) && m.LeasedBy == _identity.Value)
            .OrderBy(m => m.CreatedAt)
            .ToListAsync(ct);
    }

    /// <summary>
    /// After a trade settles successfully, releases the residual collateral of any order it completed.
    ///
    /// Deliberately lets no exception escape: the settlement already succeeded and must not be
    /// marked failed and redelivered because of this follow-up step. If the release does not happen
    /// no money is lost — the residue stays locked and reconciliation (#39) can pick it up later.
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
