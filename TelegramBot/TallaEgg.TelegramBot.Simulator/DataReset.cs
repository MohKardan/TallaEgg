using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace TallaEgg.TelegramBot.Simulator;

/// <summary>
/// Wipes whatever a previous simulation run left behind, scoped strictly to
/// <c>TelegramId &lt; 0</c> so a real user's data can never be touched. Runs first, every
/// time: the whole point of a clean slate is that every run exercises
/// registration-through-settlement from scratch rather than building on state a previous run
/// happened to leave around.
///
/// The filter used to be "TelegramId >= 900,000,000", on the theory that a real Telegram
/// user id would never reach that. It reached it: a real dev account in this database is
/// 6,389,449,308, and a run's reset deleted that account, its wallets, and its trade history
/// before this was caught. Negative is the range genuinely guaranteed empty — see
/// <see cref="SimulationOptions.TelegramIdBase"/> — so the predicate no longer depends on a
/// threshold that growth in real Telegram ids could ever catch up to again.
///
/// Plain SQL, not the services' EF DbContexts — each service owns its own database with no
/// cross-database foreign keys, so a direct, minimal delete is simpler than pulling in three
/// Infrastructure projects' DbContexts for a one-off cleanup.
///
/// Ids are matched through an explicit uniqueidentifier cast rather than comparing
/// STRING_SPLIT's nvarchar output to the column directly — at a few hundred rows the implicit
/// conversion looked fine, but a JOIN-based delete built the same way against a fully-loaded
/// database (a completed 100-user/1000-trade run) left some Transactions rows behind and the
/// following Wallets delete then failed on the foreign key. Two independent subqueries with an
/// explicit cast, one per table, replaced it.
///
/// Nothing is deleted until the Orders outbox has drained (issues #184, #175). A run is not
/// finished when the simulator prints its summary: settlement is queued, and a 60-trade run
/// outpaces a processor that polls every 5s and takes 20 at a time, so a run reliably ends
/// with settlements still in the queue. Deleting straight away took the wallets those
/// settlements needed out from under them — they retried five times, were marked Failed, and
/// stayed there, 97 of them across three consecutive runs on one machine. Worse, while those
/// doomed messages were still retrying they were the oldest in the queue, so
/// <c>ClaimDueMessagesAsync</c>'s OrderBy(CreatedAt) spent every batch on them and the current
/// run's own settlements never ran at all — a run that exercised no settlement while
/// reporting itself healthy.
///
/// Deleting this run's own outbox rows here instead would be faster, and was considered: the
/// rows are identifiable, since AggregateId is the trade id. It was not chosen. It leaves the
/// processor writing Transactions rows between the two deletes below, which is the second way
/// this class fails on FK_Transactions_Wallets_WalletId (reproduced on unmodified main), and it
/// would need ordering against the Trades delete that destroys the link it depends on. Waiting
/// closes both faults with one condition, and it keeps the completed messages as the record of
/// what the previous run settled — which is the evidence both issues were diagnosed from.
/// </summary>
public sealed class DataReset(string usersDbConnectionString, string walletDbConnectionString,
    string ordersDbConnectionString, ILogger<DataReset> logger)
{
    private const int CommandTimeoutSeconds = 120;

    /// <summary>How often <see cref="WaitForOutboxToDrainAsync"/> re-reads the queue depth.</summary>
    private static readonly TimeSpan DrainPollInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How many polls the drain wait makes before giving up — 300 × 2s = 10 minutes.
    ///
    /// The bound is counted in polls rather than measured against the clock so the loop's
    /// give-up behaviour can be tested without waiting for real time to pass. Ten minutes is
    /// well past both cases that legitimately take a while: a message that can never succeed
    /// exhausts its five retries in about three minutes of backoff and then stops being
    /// Pending, and the largest run drains a thousand messages at 20 per 5s in about four.
    /// </summary>
    private const int MaxDrainPolls = 300;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        await WaitForOutboxToDrainAsync(cancellationToken);

        var userIds = await GetSimulatedUserIdsAsync(cancellationToken);
        logger.LogInformation("Reset: found {Count} previously simulated users.", userIds.Count);

        if (userIds.Count > 0)
        {
            await DeleteWalletDataAsync(userIds, cancellationToken);
            await DeleteOrderDataAsync(userIds, cancellationToken);
        }

        await DeleteUsersAsync(cancellationToken);
        logger.LogInformation("Reset complete.");
    }

    private async Task<List<Guid>> GetSimulatedUserIdsAsync(CancellationToken ct)
    {
        var ids = new List<Guid>();
        await using var conn = new SqlConnection(usersDbConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("SELECT Id FROM Users WHERE TelegramId < 0", conn);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            ids.Add(reader.GetGuid(0));
        }
        return ids;
    }

    private async Task DeleteWalletDataAsync(List<Guid> userIds, CancellationToken ct)
    {
        await using var conn = new SqlConnection(walletDbConnectionString);
        await conn.OpenAsync(ct);
        var idsCsv = string.Join(',', userIds);

        await using (var cmd = new SqlCommand(
            """
            DELETE FROM Transactions
            WHERE WalletId IN (
                SELECT Id FROM Wallets
                WHERE UserId IN (SELECT CAST(value AS uniqueidentifier) FROM STRING_SPLIT(@Ids, ','))
            )
            """, conn) { CommandTimeout = CommandTimeoutSeconds })
        {
            cmd.Parameters.AddWithValue("@Ids", idsCsv);
            var deleted = await cmd.ExecuteNonQueryAsync(ct);
            logger.LogInformation("Reset: deleted {Count} Transactions rows.", deleted);
        }

        await using (var cmd = new SqlCommand(
            "DELETE FROM Wallets WHERE UserId IN (SELECT CAST(value AS uniqueidentifier) FROM STRING_SPLIT(@Ids, ','))",
            conn) { CommandTimeout = CommandTimeoutSeconds })
        {
            cmd.Parameters.AddWithValue("@Ids", idsCsv);
            var deleted = await cmd.ExecuteNonQueryAsync(ct);
            logger.LogInformation("Reset: deleted {Count} Wallets rows.", deleted);
        }
    }

    private async Task DeleteOrderDataAsync(List<Guid> userIds, CancellationToken ct)
    {
        await using var conn = new SqlConnection(ordersDbConnectionString);
        await conn.OpenAsync(ct);
        var idsCsv = string.Join(',', userIds);

        await using (var cmd = new SqlCommand(
            """
            DELETE FROM Trades
            WHERE BuyerUserId IN (SELECT CAST(value AS uniqueidentifier) FROM STRING_SPLIT(@Ids, ','))
               OR SellerUserId IN (SELECT CAST(value AS uniqueidentifier) FROM STRING_SPLIT(@Ids, ','))
            """, conn) { CommandTimeout = CommandTimeoutSeconds })
        {
            cmd.Parameters.AddWithValue("@Ids", idsCsv);
            var deleted = await cmd.ExecuteNonQueryAsync(ct);
            logger.LogInformation("Reset: deleted {Count} Trades rows.", deleted);
        }

        await using (var cmd = new SqlCommand(
            "DELETE FROM Orders WHERE UserId IN (SELECT CAST(value AS uniqueidentifier) FROM STRING_SPLIT(@Ids, ','))",
            conn) { CommandTimeout = CommandTimeoutSeconds })
        {
            cmd.Parameters.AddWithValue("@Ids", idsCsv);
            var deleted = await cmd.ExecuteNonQueryAsync(ct);
            logger.LogInformation("Reset: deleted {Count} Orders rows.", deleted);
        }
    }

    private Task WaitForOutboxToDrainAsync(CancellationToken ct) =>
        WaitForOutboxToDrainAsync(
            GetPendingOutboxCountAsync,
            token => Task.Delay(DrainPollInterval, token),
            MaxDrainPolls,
            logger,
            ct);

    /// <summary>
    /// Blocks until the Orders outbox holds no Pending message, so the deletes that follow
    /// cannot take data out from under a settlement that is still queued or in flight.
    ///
    /// <para>
    /// Pending is the right condition to wait on because a message holds that status for its
    /// whole working life: it is still Pending while an instance holds the lease and the wallet
    /// call is in flight, and while it sits between retries. It leaves Pending only by
    /// completing or by exhausting its retries. Zero Pending therefore means nothing is queued
    /// and nothing is mid-dispatch on any instance — which is also what makes the deletes below
    /// safe from a concurrent writer.
    /// </para>
    ///
    /// <para>
    /// The polling and the give-up bound are parameters rather than reads of the clock so this
    /// loop can be tested directly; the private overload above is what production passes.
    /// </para>
    /// </summary>
    internal static async Task WaitForOutboxToDrainAsync(
        Func<CancellationToken, Task<int>> getPendingCount,
        Func<CancellationToken, Task> waitBetweenPolls,
        int maxPolls,
        ILogger logger,
        CancellationToken ct)
    {
        var pending = await getPendingCount(ct);
        if (pending == 0)
        {
            return;
        }

        logger.LogInformation(
            "Reset: {Count} settlement(s) from the previous run are still queued; waiting for the outbox to drain before deleting anything.",
            pending);

        for (var poll = 0; poll < maxPolls; poll++)
        {
            await waitBetweenPolls(ct);

            var remaining = await getPendingCount(ct);
            if (remaining == 0)
            {
                logger.LogInformation("Reset: outbox drained.");
                return;
            }

            // Progress matters more than the number here: someone watching a wait that is going
            // nowhere needs to see that it is going nowhere, not a silent pause.
            if (remaining != pending)
            {
                logger.LogInformation("Reset: {Count} settlement(s) still queued.", remaining);
                pending = remaining;
            }
        }

        throw new TimeoutException(
            $"The Orders outbox still has {pending} Pending message(s) after waiting for it to drain. " +
            "Nothing was deleted: resetting now would orphan those settlements. Inspect them at " +
            "GET /api/outbox/unsettled, then re-drive or abandon them and run again.");
    }

    private async Task<int> GetPendingOutboxCountAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(ordersDbConnectionString);
        await conn.OpenAsync(ct);
        // Status 0 is OutboxMessageStatus.Pending. Spelled out rather than referenced because
        // this class deliberately talks to the databases in plain SQL — see the class comment.
        await using var cmd = new SqlCommand(
            "SELECT COUNT(*) FROM OutboxMessages WHERE Status = 0", conn)
        {
            CommandTimeout = CommandTimeoutSeconds
        };
        return (int)(await cmd.ExecuteScalarAsync(ct))!;
    }

    private async Task DeleteUsersAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(usersDbConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("DELETE FROM Users WHERE TelegramId < 0", conn)
        {
            CommandTimeout = CommandTimeoutSeconds
        };
        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        logger.LogInformation("Reset: deleted {Count} Users rows.", deleted);
    }
}
