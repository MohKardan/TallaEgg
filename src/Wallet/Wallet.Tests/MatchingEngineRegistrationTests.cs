using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application;
using Orders.Application.Services;

namespace Wallet.Tests;

/// <summary>
/// The matching engine must exist exactly once per process.
///
/// It used to be registered twice — <c>AddScoped&lt;IMatchingEngine&gt;</c> for injection into
/// OrderService, and <c>AddHostedService</c> for the background loop — which produced two
/// independent engines. Its <c>SemaphoreSlim</c> guard is an instance field, so each engine
/// held its own and the two never excluded each other: the background loop and the
/// order-placement path could match the same orders concurrently. Nothing underneath caught
/// it either (no row lock, no concurrency token), so a single order could fill twice, once
/// per engine, producing two Trades with different ids that both settle. See issue #53.
///
/// This is a wiring test rather than a timing test on purpose. The defect is in the
/// registration, and a test that tried to provoke the race would be slow, flaky, and would
/// still not pin down what must stay true.
/// </summary>
public class MatchingEngineRegistrationTests
{
    /// <summary>
    /// Calls the very same <c>AddMatchingEngine</c> that <c>Orders.Api/Program.cs</c> calls, so
    /// this test covers the production wiring rather than a copy of it that could drift.
    /// </summary>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(
            new ConfigurationBuilder().AddInMemoryCollection().Build());
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));

        services.AddMatchingEngine();

        return services.BuildServiceProvider();
    }

    [Fact]
    public void HostedInstanceAndInjectedInstance_AreTheSameObject()
    {
        using var provider = BuildProvider();

        var hosted = provider.GetServices<IHostedService>().OfType<MatchingEngineService>().Single();

        using var scope = provider.CreateScope();
        var injected = scope.ServiceProvider.GetRequiredService<IMatchingEngine>();

        Assert.Same(hosted, injected);
    }

    /// <summary>
    /// Two separate request scopes must not each get their own engine — that was the shape of
    /// the original bug, where N concurrent requests meant N semaphores.
    /// </summary>
    [Fact]
    public void SeparateScopes_ResolveTheSameEngine()
    {
        using var provider = BuildProvider();

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        var a = scopeA.ServiceProvider.GetRequiredService<IMatchingEngine>();
        var b = scopeB.ServiceProvider.GetRequiredService<IMatchingEngine>();

        Assert.Same(a, b);
    }

    /// <summary>
    /// The engine's lifetime belongs to the host. Exposing StopAsync on IMatchingEngine would
    /// let any consumer disable matching for the whole process now that the instance is shared.
    /// </summary>
    [Fact]
    public void IMatchingEngine_DoesNotExposeLifecycleControl()
    {
        var methods = typeof(IMatchingEngine).GetMethods().Select(m => m.Name).ToArray();

        Assert.DoesNotContain("StartAsync", methods);
        Assert.DoesNotContain("StopAsync", methods);
    }
}
