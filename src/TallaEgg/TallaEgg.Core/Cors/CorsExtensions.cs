using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace TallaEgg.Core.Cors;

/// <summary>
/// Restricts CORS to a configured whitelist instead of <c>AllowAnyOrigin</c>, the same way in
/// every API service, so each Program.cs only needs one call instead of repeating the policy.
/// See issue #31 / audit finding C-9.
///
/// <para>
/// An empty or missing <c>Cors:AllowedOrigins</c> allows no cross-origin browser request —
/// the safe default, since none of these services expect a browser client today (they are
/// called only by the bot, over loopback).
/// </para>
/// </summary>
public static class CorsExtensions
{
    private const string PolicyName = "TallaEggWhitelist";

    public static IServiceCollection AddTallaEggCors(this IServiceCollection services, IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        services.AddCors(options =>
        {
            options.AddPolicy(PolicyName, policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins).AllowAnyMethod().AllowAnyHeader();
                }
            });
        });

        return services;
    }

    public static IApplicationBuilder UseTallaEggCors(this IApplicationBuilder app)
    {
        app.UseCors(PolicyName);
        return app;
    }
}
