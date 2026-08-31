using Orders.Application.Services;

namespace TallaEgg.AllServices.Tests.Fakes;

/// <summary>
/// Grants the role to whoever asks. For tests that are about something other than leader
/// election and only need the background service to consider itself in charge (issue #160).
/// </summary>
public sealed class AlwaysLeaderLease : ILeaderLease
{
    public Task<LeaderLeaseResult> TryAcquireOrRenewAsync(string role, TimeSpan duration, CancellationToken ct = default) =>
        Task.FromResult(LeaderLeaseResult.Leader);

    public Task ReleaseAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>
/// Refuses the role and names someone else as the holder — a second instance, from the point of
/// view of the one under test.
/// </summary>
public sealed class HeldElsewhereLease : ILeaderLease
{
    public HeldElsewhereLease(string holder = "another-instance") => Holder = holder;

    public string Holder { get; }

    public Task<LeaderLeaseResult> TryAcquireOrRenewAsync(string role, TimeSpan duration, CancellationToken ct = default) =>
        Task.FromResult(LeaderLeaseResult.FollowerOf(Holder));

    public Task ReleaseAsync(string role, CancellationToken ct = default) => Task.CompletedTask;
}
