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
/// trace, so a dropped connection every ~50 seconds made the bot's error log 100% noise and hid
/// any real fault in it (issue #148). These pin the severity split (transport blip vs. a real
/// Telegram API fault) and the escalate-on-persistence behaviour that replaced it.
///
/// The handler is intentionally private -- it's wired into Telegram.Bot's <c>StartReceiving</c>,
/// not a public API of this class -- so these reach it through reflection.
/// </summary>
public class TelegramBotHostedServicePollingErrorTests
{
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

    private static Task InvokeHandlePollingErrorAsync(TelegramBotHostedService service, Exception exception, CancellationToken cancellationToken = default)
    {
        var method = typeof(TelegramBotHostedService).GetMethod("HandlePollingErrorAsync", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("HandlePollingErrorAsync not found.");

        return (Task)method.Invoke(service, [null, exception, HandleErrorSource.PollingError, cancellationToken])!;
    }

    private static Task InvokeHandleTransientPollingFailure(
        TelegramBotHostedService service, Exception exception, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        var method = typeof(TelegramBotHostedService).GetMethod("HandleTransientPollingFailure", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("HandleTransientPollingFailure not found.");

        return (Task)method.Invoke(service, [HandleErrorSource.PollingError, exception, now, cancellationToken])!;
    }

    [Fact]
    public void ATransportLevelRequestException_LogsWarningNotErrorAndBacksOff()
    {
        var (service, logger) = CreateService();
        // What TelegramBotClient.SendRequest wraps every dropped connection into -- including
        // the "ResponseEnded" case from the issue, which contains neither "timeout" nor
        // "timed out" and so took no backoff at all under the old substring check.
        var exception = new RequestException("Exception during making request", new HttpRequestException());

        var task = InvokeHandlePollingErrorAsync(service, exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception); // no stack trace for a routine blip
        Assert.False(task.IsCompleted, "a transient failure must still back off before the next poll");
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
    public void AnUnexpectedExceptionType_StaysAtErrorSeverity()
    {
        var (service, logger) = CreateService();
        var exception = new InvalidOperationException("something genuinely unexpected");

        InvokeHandlePollingErrorAsync(service, exception);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public void ConsecutiveTransientFailures_EscalateToErrorOnlyAfterTheDownThreshold()
    {
        var (service, logger) = CreateService();
        var start = DateTimeOffset.UtcNow;
        var exception = new RequestException("Exception during making request", new HttpRequestException());

        // Ten seconds apart matches the handler's own retry delay -- a realistic failure cadence.
        for (var i = 0; i < 11; i++)
        {
            InvokeHandleTransientPollingFailure(service, exception, start + TimeSpan.FromSeconds(10 * i));
        }

        // 100 seconds of failures so far: still under the two-minute threshold, so only Warnings.
        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);

        // This one crosses the two-minute mark.
        InvokeHandleTransientPollingFailure(service, exception, start + TimeSpan.FromSeconds(130));
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);

        // Still down afterwards must not log a second Error -- one alert per streak, not one per failure.
        InvokeHandleTransientPollingFailure(service, exception, start + TimeSpan.FromSeconds(140));
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
    }

    [Fact]
    public void RecoveringBetweenFailures_ResetsTheStreakSoASecondOutageEscalatesAgain()
    {
        var (service, logger) = CreateService();
        var start = DateTimeOffset.UtcNow;
        var exception = new RequestException("Exception during making request", new HttpRequestException());

        // A two-minute outage that escalates once...
        for (var i = 0; i <= 12; i++)
        {
            InvokeHandleTransientPollingFailure(service, exception, start + TimeSpan.FromSeconds(10 * i));
        }
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);

        // ...followed by a long gap, meaning updates were flowing again in between...
        var recovered = start + TimeSpan.FromMinutes(10);
        InvokeHandleTransientPollingFailure(service, exception, recovered);

        // ...so recovery itself must not immediately re-trip the alert.
        Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);

        // A second two-minute outage after recovery must be able to escalate on its own.
        for (var i = 1; i <= 12; i++)
        {
            InvokeHandleTransientPollingFailure(service, exception, recovered + TimeSpan.FromSeconds(10 * i));
        }
        Assert.Equal(2, logger.Entries.Count(e => e.Level == LogLevel.Error));
    }
}
