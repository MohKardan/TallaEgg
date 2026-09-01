using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.TelegramBot.Simulator;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The simulator's <c>DataReset</c> must not delete anything while the previous run's
/// settlements are still queued: the wallets it removes are the ones those settlements need,
/// and they then fail permanently (issues #184, #175).
///
/// <para>
/// These tests drive the drain loop directly rather than a database. The loop takes its queue
/// depth and its pause as delegates for exactly that reason, so what is asserted here is the
/// decision it makes — keep waiting, stop waiting, or give up — with no timing in it. The SQL
/// that produces the real count is one <c>SELECT COUNT(*)</c> and is exercised by running the
/// simulator.
/// </para>
/// </summary>
public class SimulatorOutboxDrainTests
{
    private static Func<CancellationToken, Task<int>> CountsOf(params int[] counts)
    {
        var index = 0;
        return _ => Task.FromResult(counts[Math.Min(index++, counts.Length - 1)]);
    }

    private static Task Immediately(CancellationToken _) => Task.CompletedTask;

    [Fact]
    public async Task WaitForOutboxToDrainAsync_QueueAlreadyEmpty_DoesNotPoll()
    {
        var polls = 0;

        await DataReset.WaitForOutboxToDrainAsync(
            _ => Task.FromResult(0),
            _ => { polls++; return Task.CompletedTask; },
            maxPolls: 10,
            NullLogger.Instance,
            CancellationToken.None);

        // The common case is a queue that is already empty, and it must cost nothing: the reset
        // runs at the start of every simulation, including the first one on a clean database.
        Assert.Equal(0, polls);
    }

    [Fact]
    public async Task WaitForOutboxToDrainAsync_QueueDrains_ReturnsOnceEmpty()
    {
        await DataReset.WaitForOutboxToDrainAsync(
            CountsOf(12, 7, 3, 0),
            Immediately,
            maxPolls: 10,
            NullLogger.Instance,
            CancellationToken.None);
    }

    [Fact]
    public async Task WaitForOutboxToDrainAsync_QueueStopsShrinking_StillWaitsUntilEmpty()
    {
        // A queue that sits at the same depth for a while is normal, not stuck: messages that
        // exhausted an attempt wait out an exponential backoff before the processor sees them
        // again. Giving up on a count that has not moved would abandon a run that was going to
        // settle perfectly well a few seconds later.
        await DataReset.WaitForOutboxToDrainAsync(
            CountsOf(4, 4, 4, 4, 4, 0),
            Immediately,
            maxPolls: 10,
            NullLogger.Instance,
            CancellationToken.None);
    }

    [Fact]
    public async Task WaitForOutboxToDrainAsync_QueueNeverDrains_ThrowsAndDeletesNothing()
    {
        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            DataReset.WaitForOutboxToDrainAsync(
                CountsOf(5),
                Immediately,
                maxPolls: 3,
                NullLogger.Instance,
                CancellationToken.None));

        // Throwing is the point: the reset is the first thing a run does, so failing here stops
        // the run before a single row is deleted. The message has to say where to look, or the
        // next person sees a simulator that will not start and no reason why.
        Assert.Contains("5 Pending", exception.Message);
        Assert.Contains("Nothing was deleted", exception.Message);
        Assert.Contains("/api/outbox/unsettled", exception.Message);
    }

    [Fact]
    public async Task WaitForOutboxToDrainAsync_Cancelled_Propagates()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            DataReset.WaitForOutboxToDrainAsync(
                CountsOf(3),
                token => Task.FromCanceled(token),
                maxPolls: 10,
                NullLogger.Instance,
                cts.Token));
    }
}
