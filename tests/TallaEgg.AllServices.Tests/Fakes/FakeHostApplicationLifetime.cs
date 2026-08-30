using Microsoft.Extensions.Hosting;

namespace TallaEgg.AllServices.Tests.Fakes;

/// <summary>
/// Satisfies the <see cref="IHostApplicationLifetime"/> a service under test asks for, without
/// touching a real host. <see cref="StopApplicationCalled"/> is there for a test that exercises a
/// shutdown path; the polling-error tests only need the constructor to succeed.
/// </summary>
public sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => CancellationToken.None;
    public CancellationToken ApplicationStopped => CancellationToken.None;

    public bool StopApplicationCalled { get; private set; }

    public void StopApplication() => StopApplicationCalled = true;
}
