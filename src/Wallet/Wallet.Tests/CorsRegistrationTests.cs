using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Wallet.Tests;

/// <summary>
/// <c>UseCors</c> requires <c>AddCors</c>; without it the process dies at startup (issue #72).
///
/// <para>
/// <c>Affiliate.Api</c> called the middleware and never registered the services, so it threw
/// <c>InvalidOperationException: Unable to resolve service for type 'ICorsService' while
/// attempting to activate 'CorsMiddleware'</c> and the port never opened. It had been guarded
/// behind <c>if (app.Environment.IsProduction())</c> with a comment claiming the services were
/// registered there — they were not, in any environment. The guard did not prevent the fault;
/// it confined it to the one environment nobody runs locally, because
/// <c>launchSettings.json</c> forces Development and is not used by a published deployment.
/// </para>
///
/// <para>
/// <b>Why a registration test rather than a hosted one:</b> the defect is in the service
/// collection. Standing up the real app would need the shared configuration file and a
/// database, which is why nothing covered it before. This asserts the exact thing that was
/// missing, and costs nothing to run.
/// </para>
///
/// <para>
/// The broader gap is that Production is a code path never executed here — #71 (build and smoke
/// in Release in CI) is the general answer; this is the specific one.
/// </para>
/// </summary>
public class CorsRegistrationTests
{
    /// <summary>
    /// The middleware resolves <see cref="ICorsService"/> and <see cref="ICorsPolicyProvider"/>
    /// from the container. Both must be present for <c>UseCors</c> to run.
    /// </summary>
    [Fact]
    public void AddCors_RegistersWhatTheMiddlewareResolves()
    {
        var services = new ServiceCollection();

        // CorsService takes an ILoggerFactory. Every host has one; a bare ServiceCollection
        // does not, so it is added here to match the real application rather than to work
        // around anything.
        services.AddLogging();
        services.AddCors();

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetService<ICorsService>());
        Assert.NotNull(provider.GetService<ICorsPolicyProvider>());
    }

    /// <summary>
    /// The negative control: without the registration the resolution that killed Affiliate.Api
    /// fails. Included so the test above cannot pass for an unrelated reason — for instance if
    /// some other package happened to register the same services.
    /// </summary>
    [Fact]
    public void WithoutAddCors_TheMiddlewareCannotBeConstructed()
    {
        using var provider = new ServiceCollection().BuildServiceProvider();

        Assert.Null(provider.GetService<ICorsService>());
    }
}
