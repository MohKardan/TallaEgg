using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace TallaEgg.TelegramBot.Simulator;

/// <summary>
/// Wipes whatever a previous simulation run left behind, scoped to users with
/// <c>TelegramId &lt; 0</c> so a real user's account, wallets and history can never be
/// touched. The one thing outside that scope which is deleted is the order sitting on the
/// other side of a simulated fill, which belongs to the market maker — a real account by
/// design; see <see cref="DeleteOrderDataAsync"/> for why that is required and why it does
/// not widen the guard. Runs first, every
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
/// addresses both faults with one condition — nothing is queued or in flight when the deletes
/// start — and it keeps the completed messages as the record of what the previous run settled,
/// which is the evidence both issues were diagnosed from.
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
            // Orders and Trades go before the wallet rows so that nothing is left able to
            // produce a settlement while those rows are being deleted. The matching sweep runs
            // every second and writes a Trade and its outbox message in one transaction, and
            // the processor dispatching that message writes Transactions rows — which is the
            // foreign key DeleteWalletDataAsync conflicts on. The wait above clears what is
            // already queued; this order removes what could still queue more.
            await DeleteOrderDataAsync(userIds, cancellationToken);
            await DeleteWalletDataAsync(userIds, cancellationToken);
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

    /// <summary>
    /// The order side of the reset, as one batch: identify the orders sitting on trades a
    /// simulated user was part of, delete those trades, then delete those orders and anything
    /// else a simulated user owns.
    ///
    /// <para>
    /// <b>One batch, and table variables rather than a <c>#temp</c> table</b>, because a
    /// parameterised <see cref="SqlCommand"/> is sent through <c>sp_executesql</c>: a
    /// <c>#temp</c> table created by one command is dropped when that call returns, so a second
    /// command cannot see it. Written as four commands first, and it failed with "Invalid object
    /// name '#CounterpartyOrders'".
    /// </para>
    ///
    /// <para>
    /// Maker and taker are matched alongside buy and sell because each of the four is its own
    /// foreign key, so any of them can hold an order in place.
    /// </para>
    ///
    /// <para>
    /// Exposed to the tests because the order of these statements is the whole correctness
    /// argument — see <see cref="DeleteOrderDataAsync"/> — and this suite has no SQL Server to
    /// run it against.
    /// </para>
    /// </summary>
    internal const string OrderDataDeleteSql =
        """
        DECLARE @Sim TABLE (Id uniqueidentifier PRIMARY KEY);
        INSERT INTO @Sim (Id)
        SELECT DISTINCT CAST(value AS uniqueidentifier) FROM STRING_SPLIT(@Ids, ',');

        DECLARE @Counterparty TABLE (Id uniqueidentifier PRIMARY KEY);
        INSERT INTO @Counterparty (Id)
        SELECT DISTINCT o.Id
        FROM Orders o
        INNER JOIN Trades t
            ON t.BuyOrderId = o.Id OR t.SellOrderId = o.Id
            OR t.MakerOrderId = o.Id OR t.TakerOrderId = o.Id
        WHERE t.BuyerUserId IN (SELECT Id FROM @Sim)
           OR t.SellerUserId IN (SELECT Id FROM @Sim);

        DELETE FROM Trades
        WHERE BuyerUserId IN (SELECT Id FROM @Sim)
           OR SellerUserId IN (SELECT Id FROM @Sim);
        DECLARE @TradesDeleted int = @@ROWCOUNT;

        DELETE FROM Orders
        WHERE Id IN (SELECT Id FROM @Counterparty)
          AND NOT EXISTS (
              SELECT 1 FROM Trades t
              WHERE t.BuyOrderId = Orders.Id OR t.SellOrderId = Orders.Id
                 OR t.MakerOrderId = Orders.Id OR t.TakerOrderId = Orders.Id);
        DECLARE @CounterpartyDeleted int = @@ROWCOUNT;

        DELETE FROM Orders WHERE UserId IN (SELECT Id FROM @Sim);
        DECLARE @OrdersDeleted int = @@ROWCOUNT;

        SELECT @TradesDeleted, @CounterpartyDeleted, @OrdersDeleted;
        """;

    /// <summary>
    /// Removes this run's trades and orders, including the order on the other side of every
    /// simulated fill even when its owner is a real account.
    ///
    /// <para>
    /// A quote fill creates two orders — the customer's and the market maker's
    /// (<c>QuoteFillService.CreateSideAsync</c>, called twice) — and the market maker is
    /// deliberately a real account, so <c>UserId IN (simulated)</c> can never see their half.
    /// Deleting trades by participant but orders by owner therefore left the dealer's order
    /// behind, <c>Completed</c> with nothing pointing at it: 4,451 of 4,503 orders on one dev
    /// machine were that residue, and issue #250 measured them believing they were product
    /// data (issue #257).
    /// </para>
    ///
    /// <para>
    /// The counterparty ids are captured <b>before</b> the trades are deleted, because the
    /// trades are the only thing that identifies them. They cannot simply be deleted first
    /// instead: <c>Trades</c> holds four foreign keys into <c>Orders</c> — BuyOrderId,
    /// SellOrderId, MakerOrderId and TakerOrderId, all NO ACTION — so an order still referenced
    /// by a trade cannot be removed. Capture, delete the trades, then delete the orders.
    /// </para>
    ///
    /// <para>
    /// This is a deliberate narrowing of the <c>TelegramId &lt; 0</c> guard described on the
    /// class, and the only one: an order belonging to a real account is deleted solely because a
    /// simulated user was the counterparty of a trade it was part of. It does not reopen the
    /// incident that guard exists to prevent — that was a filter which selected real
    /// <em>users</em> wholesale, deleting an account, its wallets and its history. Nothing here
    /// widens which users are selected, and the <c>NOT EXISTS</c> keeps any order still
    /// referenced by a surviving trade, so a real order filled against both a simulated and a
    /// real user keeps the history that is genuinely its own. Without that clause the delete
    /// does not merely over-reach, it fails outright on FK_Trades_Orders_SellOrderId.
    /// </para>
    /// </summary>
    private async Task DeleteOrderDataAsync(List<Guid> userIds, CancellationToken ct)
    {
        await using var conn = new SqlConnection(ordersDbConnectionString);
        await conn.OpenAsync(ct);
        var idsCsv = string.Join(',', userIds);

        await using var cmd = new SqlCommand(OrderDataDeleteSql, conn)
        {
            CommandTimeout = CommandTimeoutSeconds
        };

        cmd.Parameters.AddWithValue("@Ids", idsCsv);

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);

        logger.LogInformation("Reset: deleted {Count} Trades rows.", reader.GetInt32(0));
        logger.LogInformation("Reset: deleted {Count} counterparty Orders rows.", reader.GetInt32(1));
        logger.LogInformation("Reset: deleted {Count} Orders rows.", reader.GetInt32(2));
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
