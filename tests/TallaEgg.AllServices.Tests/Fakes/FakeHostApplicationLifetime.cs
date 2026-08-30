using Microsoft.Extensions.Hosting;

namespace TallaEgg.AllServices.Tests.Fakes;

/// <summary>Records whether shutdown was requested, without touching a real host.</summary>
public sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
{
    public CancellationToken ApplicationStarted => CancellationToken.None;
    public CancellationToken ApplicationStopping => CancellationToken.None;
    public CancellationToken ApplicationStopped => CancellationToken.None;

    public bool StopApplicationCalled { get; private set; }

    public void StopApplication() => StopApplicationCalled = true;
}
