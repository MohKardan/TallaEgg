using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TallaEgg.AllServices.Tests.Fakes;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// <c>HandlePollingErrorAsync</c> used to log every polling hiccup at Error with a full stack
/// trace, so a dropped connection every ~71 seconds made the bot's error log 100% noise and hid
/// any real fault in it (issue #148). These pin the severity split (transport blip vs. a real
/// Telegram API fault vs. a dead receive loop) and the escalate-on-persistence behaviour that
/// replaced it.
///
/// Cadence matters more than it looks here. <c>getUpdates</c> long-polls for the client's whole
/// timeout, so under a total outage consecutive failures land a full poll cycle apart -- roughly
/// two minutes, not the ten seconds a naive reading suggests. Tests that drive failures ten
/// seconds apart therefore exercise a rhythm the deployed bot never produces, and would pass
/// against a handler that can never escalate in production. Everything below is derived from
/// <c>ITelegramBotClient.Timeout</c> for that reason.
///
/// The handler is intentionally private -- it's wired into Telegram.Bot's <c>StartReceiving</c>,
/// not a public API of this class -- so these reach it through reflection.
/// </summary>
public class TelegramBotHostedServicePollingErrorTests
{
    /// <summary>Longest a single failing poll can take before the transport gives up.</summary>
    private static readonly TimeSpan PollCycle = new TelegramBotClient("1:AA").Timeout;

    /// <summary>Worst-case spacing of two consecutive failures during a total outage.</summary>
    private static readonly TimeSpan OutageCadence = PollCycle + TimeSpan.FromSeconds(10);

    private static readonly TimeSpan DownAlertThreshold = TimeSpan.FromMinutes(2);

    private static (TelegramBotHostedService Service, CapturingLogger<TelegramBotHostedService> Logger) CreateService()
    {
        var logger = new CapturingLogger<TelegramBotHostedService>();
        var service = new TelegramBotHostedService(
            new TelegramBotClient("1:AA"),
            botHandler: null!, // unused by the polling-error path under test
            logger,
            Options.Create(new TelegramBotOptions()),
            new FakeHostApplicationLifetime());

        return (service, logger);
    }

    private static Task InvokeHandlePollingErrorAsync(
        TelegramBotHostedService service,
        Exception exception,
        HandleErrorSource source = HandleErrorSource.PollingError,
        CancellationToken cancellationToken = default)
    {
        var method = typeof(TelegramBotHostedService).GetMethod("HandlePollingErrorAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("HandlePollingErrorAsync not found.");

        return (Task)method.Invoke(service, [null, exception, source, cancellationToken])!;
    }

    private static Task InvokeHandleTransientPollingFailure(
        TelegramBotHostedService service, Exception exception, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var method = typeof(TelegramBotHostedService).GetMethod("HandleTransientPollingFailure", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("HandleTransientPollingFailure not found.");

        return (Task)method.Invoke(service, [HandleErrorSource.PollingError, exception, now, cancellationToken])!;
    }

    private static void InvokeRecordUpdateReceived(TelegramBotHostedService service, DateTimeOffset now)
    {
        var method = typeof(TelegramBotHostedService).GetMethod("RecordUpdateReceived", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("RecordUpdateReceived not found.");

        method.Invoke(service, [now]);
    }

    /// <summary>
    /// Drives a total outage from <paramref name="from"/> at the cadence a real one produces --
    /// no updates arriving, every poll dying only after the full long-poll timeout -- until the
    /// down alert has had its chance to fire. Returns the moment of the last failure.
    /// </summary>
    private static DateTimeOffset DriveOutageUntilAlert(TelegramBotHostedService service, DateTimeOffset from)
    {
        var elapsed = TimeSpan.Zero;
        while (elapsed < DownAlertThreshold)
        {
            InvokeHandleTransientPollingFailure(service, TransportFailure(), from + elapsed);
            elapsed += OutageCadence;
        }

        InvokeHandleTransientPollingFailure(service, TransportFailure(), from + elapsed);
        return from + elapsed;
    }

    /// <summary>What TelegramBotClient.SendRequest wraps every dropped connection into.</summary>
    private static RequestException TransportFailure() =>
        new("Exception during making request",
            new HttpRequestException("An error occurred while sending the request.",
                new IOException("The response ended prematurely. (ResponseEnded)")));

    // ---- severity split ----

    [Fact]
    public void ATransportLevelRequestException_LogsWarningNotErrorAndBacksOff()
    {
        var (service, logger) = CreateService();
        // The "ResponseEnded" case from the issue, which contains neither "timeout" nor
        // "timed out" and so took no backoff at all under the old substring check.
        var task = InvokeHandlePollingErrorAsync(service, TransportFailure());

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception); // no stack trace for a routine blip
        Assert.False(task.IsCompleted, "a transient failure must still back off before the next poll");
    }

    [Fact]
    public void ATransportLevelRequestException_LogsTheInnerCauseNotJustTheFixedWrapperMessage()
    {
        var (service, logger) = CreateService();

        InvokeHandlePollingErrorAsync(service, TransportFailure());

        // RequestException.Message is always "Exception during making request", so on its own it
        // makes all ~1200 daily lines identical. The cause lives in the inner exception.
        var entry = Assert.Single(logger.Entries);
        Assert.Contains("ResponseEnded", entry.Message);
    }

    [Fact]
    public void AnApiRequestException_LogsErrorImmediatelyWithNoBackoff()
    {
        var (service, logger) = CreateService();
        var exception = new ApiRequestException("Unauthorized", 401);

        var task = InvokeHandlePollingErrorAsync(service, exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.True(task.IsCompleted, "a real API fault must not be delayed like a transient blip");
    }

    [Fact]
    public void AFatalError_StaysAtErrorEvenWhenTheExceptionLooksTransient()
    {
        var (service, logger) = CreateService();

        // Telegram.Bot documents FatalError as "Polling of updates will stop". The exception on
        // that path is shaped exactly like a routine blip, so dispatching on type alone would
        // downgrade a permanently deaf bot to a single Warning.
        var task = InvokeHandlePollingErrorAsync(service, TransportFailure(), HandleErrorSource.FatalError);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.NotNull(entry.Exception);
        Assert.True(task.IsCompleted, "there is no receive loop left to back off for");
    }

    [Fact]
    public void AnUnexpectedExceptionType_StaysAtErrorSeverity()
    {
        var (service, logger) = CreateService();
        var exception = new InvalidOperationException("something genuinely unexpected");

        InvokeHandlePollingErrorAsync(service, exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
    }

    // ---- backoff ----

    [Fact]
    public void TheFirstBlipRetriesQuickly_AndOnlySustainedFailureSettlesAtTheLongDelay()
    {
        var (service, logger) = CreateService();
        var start = DateTimeOffset.UtcNow;

        // An isolated blip used to cost nothing (immediate retry) and must not now cost ten
        // seconds of deafness for a customer waiting on a keyboard tap.
        InvokeHandleTransientPollingFailure(service, TransportFailure(), start);
        Assert.Contains("00:00:01", logger.Entries[0].Message);

        InvokeHandleTransientPollingFailure(service, TransportFailure(), start + TimeSpan.FromSeconds(1));
        Assert.Contains("00:00:02", logger.Entries[1].Message);

        InvokeHandleTransientPollingFailure(service, TransportFailure(), start + TimeSpan.FromSeconds(3));
        Assert.Contains("00:00:05", logger.Entries[2].Message);

        // ...and it caps there rather than growing without bound.
        InvokeHandleTransientPollingFailure(service, TransportFailure(), start + TimeSpan.FromSeconds(8));
        InvokeHandleTransientPollingFailure(service, TransportFailure(), start + TimeSpan.FromSeconds(18));
        Assert.Contains("00:00:10", logger.Entries[3].Message);
        Assert.Contains("00:00:10", logger.Entries[4].Message);
    }

    // ---- escalate on persistence ----

    [Fact]
    public void ASustainedOutage_EscalatesOnceAtTheRealPollCadence()
    {
        var (service, logger) = CreateService();
        var start = DateTimeOffset.UtcNow;

        // No updates arrive and every poll dies only after the full long-poll timeout: the bot is
        // genuinely deaf. This is the cadence a real outage produces, not one failure per 10s.
        var lastFailure = DriveOutageUntilAlert(service, start);

        var alert = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("may not be receiving updates", alert.Message);
        Assert.Equal(alert, logger.Entries[^1]); // nothing before the threshold was an Error

        // Still down afterwards must not log a second Error -- one alert per outage.
        InvokeHandleTransientPollingFailure(service, TransportFailure(), lastFailure + OutageCadence);
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void BlipsWithUpdatesStillArriving_NeverEscalateHoweverLongTheyGoOn()
    {
        var (service, logger) = CreateService();
        var start = DateTimeOffset.UtcNow;

        // The exact pattern issue #148 measured: 1214 failures in a day, one roughly every 71
        // seconds, while trades placed through the bot settled normally throughout. The bot is
        // working; this must stay Warning-only for as long as it lasts.
        var cadence = TimeSpan.FromSeconds(71);
        for (var i = 0; i < 60; i++)
        {
            var at = start + cadence * i;
            InvokeHandleTransientPollingFailure(service, TransportFailure(), at);
            InvokeRecordUpdateReceived(service, at + TimeSpan.FromSeconds(35)); // a customer got through
        }

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Equal(60, logger.Entries.Count(e => e.Level == LogLevel.Warning));
    }

    [Fact]
    public void OccasionalBlipsOnAQuietBot_DoNotEscalateEitherEvenWithNoUpdatesToProveRecovery()
    {
        var (service, logger) = CreateService();
        var start = DateTimeOffset.UtcNow;

        // Overnight there may be no traffic at all, so no update can vouch for recovery. A gap
        // longer than a whole poll cycle is what stands in for it: these polls are succeeding,
        // they are just occasionally unlucky.
        for (var i = 0; i < 20; i++)
        {
            InvokeHandleTransientPollingFailure(service, TransportFailure(), start + TimeSpan.FromMinutes(10) * i);
        }

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void RecoveringBetweenOutages_ResetsTheStreakSoASecondOutageEscalatesAgain()
    {
        var (service, logger) = CreateService();
        var start = DateTimeOffset.UtcNow;

        var firstOutageEnded = DriveOutageUntilAlert(service, start);
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);

        // Polling comes back -- an update proves it, so a short recovery counts, not only one
        // longer than a poll cycle.
        var recovered = firstOutageEnded + TimeSpan.FromSeconds(5);
        InvokeRecordUpdateReceived(service, recovered);

        // A second outage after that must be able to escalate on its own.
        DriveOutageUntilAlert(service, recovered + TimeSpan.FromSeconds(1));

        Assert.Equal(2, logger.Entries.Count(e => e.Level == LogLevel.Error));
    }
}
