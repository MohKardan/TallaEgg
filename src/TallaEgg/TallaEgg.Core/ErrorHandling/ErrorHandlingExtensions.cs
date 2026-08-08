using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace TallaEgg.Core.ErrorHandling;

/// <summary>
/// Wires up <see cref="GlobalExceptionHandler"/> plus per-request logging the same way in every
/// API service, so each Program.cs only needs one call instead of repeating the registration.
/// </summary>
public static class ErrorHandlingExtensions
{
    public static IServiceCollection AddTallaEggErrorHandling(this IServiceCollection services)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();

        // UseExceptionHandler() refuses to start without a fallback configured, even though
        // GlobalExceptionHandler always handles every exception itself — this is never actually
        // reached, but its absence is a hard startup failure.
        services.AddProblemDetails();

        return services;
    }

    public static IApplicationBuilder UseTallaEggErrorHandling(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseSerilogRequestLogging();

        return app;
    }
}
