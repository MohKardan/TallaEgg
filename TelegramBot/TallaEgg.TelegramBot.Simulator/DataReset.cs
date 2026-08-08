using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace TallaEgg.TelegramBot.Simulator;

/// <summary>
/// Wipes whatever a previous simulation run left behind, scoped strictly to the reserved
/// Telegram id range (<see cref="SimulationOptions.TelegramIdBase"/>+) so a real user's data
/// can never be touched. Runs first, every time: the whole point of a clean slate is that
/// every run exercises registration-through-settlement from scratch rather than building on
/// state a previous run happened to leave around.
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
/// </summary>
public sealed class DataReset(string usersDbConnectionString, string walletDbConnectionString,
    string ordersDbConnectionString, ILogger<DataReset> logger)
{
    private const int CommandTimeoutSeconds = 120;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
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
        await using var cmd = new SqlCommand("SELECT Id FROM Users WHERE TelegramId >= @Base", conn);
        cmd.Parameters.AddWithValue("@Base", SimulationOptions.TelegramIdBase);
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

    private async Task DeleteUsersAsync(CancellationToken ct)
    {
        await using var conn = new SqlConnection(usersDbConnectionString);
        await conn.OpenAsync(ct);
        await using var cmd = new SqlCommand("DELETE FROM Users WHERE TelegramId >= @Base", conn)
        {
            CommandTimeout = CommandTimeoutSeconds
        };
        cmd.Parameters.AddWithValue("@Base", SimulationOptions.TelegramIdBase);
        var deleted = await cmd.ExecuteNonQueryAsync(ct);
        logger.LogInformation("Reset: deleted {Count} Users rows.", deleted);
    }
}
