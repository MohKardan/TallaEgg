using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace TallaEgg.Core.Startup;

/// <summary>
/// The database work a service has to complete before it can answer a request: its EF Core
/// migration, and whatever seed that migration makes possible.
/// </summary>
/// <param name="services">A scope created fresh for each attempt.</param>
/// <param name="cancellationToken">Fires when the host is shutting down.</param>
public delegate Task DatabaseMigrationStep(IServiceProvider services, CancellationToken cancellationToken);

/// <summary>
/// Applies a service's database migration <b>after</b> the host has started, retrying with
/// backoff while the database is not answering yet (issue #230).
/// </summary>
/// <remarks>
/// The three deployed APIs used to migrate in top-level statements, between
/// <c>builder.Build()</c> and <c>app.Run()</c>. Under <c>UseWindowsService()</c> the process does
/// not connect to the SCM dispatcher until <c>app.Run()</c>, so every second spent migrating was a
/// second the SCM counted against its 45-second start timeout with nothing connected yet. A
/// database that was not listening did not fail fast either — it blocked, the SCM killed the
/// process, and the service never reported anything. That is the outage in issue #228, whose
/// deployment fix orders SQL Server ahead of these services but cannot make <i>Running</i> mean
/// <i>accepting connections</i>.
///
/// <para>
/// Retrying around the old call site would have made this worse rather than better: the loop
/// still runs before <c>app.Run()</c>, so it lengthens precisely the window the SCM is timing —
/// the same kill, later, with a less obvious cause. The migration has to leave the pre-start path
/// first, which is what this class is for.
/// </para>
///
/// <para>
/// The retry budget is bounded on purpose. A database that never comes back has to end in a
/// logged failure and a stopped service, not in one that reports <i>Running</i> and is useless.
/// </para>
/// </remarks>
public sealed class DatabaseMigrationHostedService : BackgroundService
{
    /// <summary>
    /// Attempts before the service gives up. With the backoff below this spends roughly six
    /// minutes waiting, plus whatever each failed attempt takes to time out — comfortably longer
    /// than SQL Server Express's own two-minute delayed start (issue #228), and short enough that
    /// a database which is genuinely gone is reported rather than waited on forever.
    /// </summary>
    private const int MAX_ATTEMPTS = 10;

    /// <summary>
    /// Exit code when the migration is abandoned, so this is distinguishable from a clean stop.
    /// </summary>
    private const int MIGRATION_FAILED_EXIT_CODE = 1;

    private static readonly TimeSpan FirstRetryDelay = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxRetryDelay = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly DatabaseMigrationStep _migrate;
    private readonly DatabaseReadiness _readiness;
    private readonly IHostApplicationLifetime _lifetime;
    private readonly ILogger<DatabaseMigrationHostedService> _logger;

    private readonly int _maxAttempts;
    private readonly TimeSpan _firstRetryDelay;
    private readonly TimeSpan _maxRetryDelay;

    public DatabaseMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        DatabaseMigrationStep migrate,
        DatabaseReadiness readiness,
        IHostApplicationLifetime lifetime,
        ILogger<DatabaseMigrationHostedService> logger)
        : this(scopeFactory, migrate, readiness, lifetime, logger, MAX_ATTEMPTS, FirstRetryDelay, MaxRetryDelay)
    {
    }

    /// <summary>
    /// Takes the retry schedule so a test can exercise give-up and recovery without waiting out
    /// the real backoff. DI always uses the public constructor above and the constants beside it.
    /// </summary>
    internal DatabaseMigrationHostedService(
        IServiceScopeFactory scopeFactory,
        DatabaseMigrationStep migrate,
        DatabaseReadiness readiness,
        IHostApplicationLifetime lifetime,
        ILogger<DatabaseMigrationHostedService> logger,
        int maxAttempts,
        TimeSpan firstRetryDelay,
        TimeSpan maxRetryDelay)
    {
        _scopeFactory = scopeFactory;
        _migrate = migrate;
        _readiness = readiness;
        _lifetime = lifetime;
        _logger = logger;
        _maxAttempts = maxAttempts;
        _firstRetryDelay = firstRetryDelay;
        _maxRetryDelay = maxRetryDelay;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Yield before touching the database. The host awaits StartAsync of every hosted service
        // before it reports started, and BackgroundService.StartAsync returns only once
        // ExecuteAsync reaches its first incomplete await — so anything run synchronously above
        // this line would land back in the pre-start window this class exists to leave.
        await Task.Yield();

        var delay = _firstRetryDelay;

        for (var attempt = 1; attempt <= _maxAttempts; attempt++)
        {
            try
            {
                // A fresh scope per attempt: a DbContext that failed to connect is not reused.
                using var scope = _scopeFactory.CreateScope();
                await _migrate(scope.ServiceProvider, stoppingToken);

                _readiness.MarkReady();
                _logger.LogInformation(
                    "Database migration succeeded on attempt {Attempt} of {MaxAttempts}. The service is now answering requests.",
                    attempt,
                    _maxAttempts);
                return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("Database migration abandoned on attempt {Attempt}: the service is shutting down.", attempt);
                return;
            }
            catch (Exception ex)
            {
                if (attempt == _maxAttempts)
                {
                    _logger.LogCritical(
                        ex,
                        "Database migration failed on all {MaxAttempts} attempts. The service cannot serve a request without its schema, so it is stopping rather than staying up and answering 503 forever.",
                        _maxAttempts);

                    Environment.ExitCode = MIGRATION_FAILED_EXIT_CODE;
                    _lifetime.StopApplication();
                    return;
                }

                _logger.LogWarning(
                    ex,
                    "Database migration attempt {Attempt} of {MaxAttempts} failed. Retrying in {DelaySeconds}s; requests are answered 503 until it succeeds.",
                    attempt,
                    _maxAttempts,
                    delay.TotalSeconds);
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            delay = delay * 2 < _maxRetryDelay ? delay * 2 : _maxRetryDelay;
        }
    }
}

/// <summary>
/// Registers the migration host and the readiness flag it writes.
/// </summary>
public static class DatabaseMigrationRegistration
{
    /// <summary>
    /// Runs <paramref name="migrate"/> once the host has started, retrying while the database is
    /// unreachable, and opens <see cref="DatabaseReadiness"/> when it succeeds (issue #230).
    /// </summary>
    /// <remarks>
    /// Pair this with <c>UseDatabaseReadinessGate()</c> on the application, or the service will
    /// answer requests against a schema that has not been migrated yet.
    /// </remarks>
    /// <param name="services">The service collection being built.</param>
    /// <param name="migrate">The migration, and any seed that depends on it.</param>
    public static IServiceCollection AddDatabaseMigrationAtStartup(
        this IServiceCollection services,
        DatabaseMigrationStep migrate)
    {
        services.AddSingleton<DatabaseReadiness>();
        services.AddSingleton(migrate);
        services.AddHostedService<DatabaseMigrationHostedService>();

        return services;
    }
}
