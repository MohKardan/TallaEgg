using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Services;

namespace Orders.Application;

/// <summary>
/// Registers the matching engine in DI.
///
/// Deliberately a shared method so <c>Orders.Api</c> and the tests take exactly the same path. If
/// the registration were written directly in <c>Program.cs</c>, a test could only assert against a
/// copy of it, and would stay green if <c>Program.cs</c> later changed — losing precisely what it
/// exists to protect.
/// </summary>
public static class MatchingEngineRegistration
{
    /// <summary>
    /// Registers the matching engine as <b>a single instance</b>, exposed both as
    /// <see cref="IMatchingEngine"/> for injection and as <c>IHostedService</c> for the background
    /// loop.
    ///
    /// It used to be registered twice, producing two independent engines with two separate
    /// <see cref="SemaphoreSlim"/> instances — so the semaphore written to prevent concurrent
    /// processing protected nothing at all (issue #53).
    ///
    /// Singleton is safe because <see cref="MatchingEngineService"/> creates its own scope through
    /// <c>IServiceScopeFactory</c> for every database access. A scoped service such as
    /// <c>OrderService</c> may depend on a singleton; the reverse is not allowed.
    /// </summary>
    public static IServiceCollection AddMatchingEngine(this IServiceCollection services)
    {
        services.AddSingleton<MatchingEngineService>();
        services.AddSingleton<IMatchingEngine>(sp => sp.GetRequiredService<MatchingEngineService>());
        services.AddHostedService(sp => sp.GetRequiredService<MatchingEngineService>());

        return services;
    }
}
