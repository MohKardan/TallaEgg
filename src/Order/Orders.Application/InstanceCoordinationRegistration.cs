using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Services;

namespace Orders.Application;

/// <summary>
/// Registers what the background services need to coordinate with other instances (issue #160).
///
/// A shared method for the same reason <see cref="MatchingEngineRegistration"/> is one: the
/// matching engine and the outbox processor both take these dependencies, so a test that built
/// its own copy of the registration would keep passing after <c>Program.cs</c> changed.
/// </summary>
public static class InstanceCoordinationRegistration
{
    /// <summary>
    /// Both are singletons. The identity must be, or one process would answer to several names and
    /// could not renew its own leases; the lease implementation must be, because the hosted
    /// services that consume it are singletons themselves and open their own scopes per call.
    /// </summary>
    public static IServiceCollection AddInstanceCoordination(this IServiceCollection services)
    {
        services.AddSingleton<InstanceIdentity>();
        services.AddSingleton<ILeaderLease, DatabaseLeaderLease>();

        return services;
    }
}
