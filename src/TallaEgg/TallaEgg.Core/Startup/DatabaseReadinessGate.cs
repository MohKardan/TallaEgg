using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TallaEgg.Core.DTOs;

namespace TallaEgg.Core.Startup;

/// <summary>
/// Refuses requests until the database migration has succeeded (issue #230).
/// </summary>
public static class DatabaseReadinessGate
{
    /// <summary>
    /// Shown to a caller, so Persian like every other message this platform returns.
    /// </summary>
    private const string NOT_READY_MESSAGE = "سرویس هنوز آماده نیست: مهاجرت پایگاه داده کامل نشده است. لطفاً چند لحظه دیگر دوباره تلاش کنید.";

    /// <summary>
    /// Seconds to suggest before retrying. The migration backoff starts at five seconds and
    /// grows, so this is a hint rather than a promise.
    /// </summary>
    private const string RETRY_AFTER_SECONDS = "10";

    /// <summary>
    /// Answers <c>503 Service Unavailable</c> to every request until
    /// <see cref="DatabaseReadiness"/> opens, so the service never serves against a schema that
    /// has not been migrated yet.
    /// </summary>
    /// <remarks>
    /// Since the migration moved off the pre-start path, the host reports <i>Running</i> before
    /// the database has necessarily answered. Something has to say what happens in that window,
    /// and the alternative — serving — turns a startup delay into whatever error the missing
    /// schema happens to raise, reported as a 500 with a stack trace in the log.
    ///
    /// <para>
    /// Register it directly after the error handler, ahead of authentication, CORS, Swagger and
    /// the endpoints: none of those can do anything useful without the database either, and a
    /// caller learning that a service is still starting is not information worth protecting.
    /// </para>
    ///
    /// <para>
    /// <c>GET /version</c> is exempt. Answering "which build is this?" while a service is
    /// degraded is the whole reason it exists (issue #218), and it reads nothing from the
    /// database.
    /// </para>
    /// </remarks>
    public static IApplicationBuilder UseDatabaseReadinessGate(this IApplicationBuilder app)
    {
        // Resolved once here rather than per request, and deliberately not optional: a service
        // that gates on readiness without registering the migration host would refuse every
        // request forever, so it should fail at startup instead.
        var readiness = app.ApplicationServices.GetRequiredService<DatabaseReadiness>();

        return app.Use(async (context, next) =>
        {
            if (readiness.IsReady || IsExempt(context.Request.Path))
            {
                await next(context);
                return;
            }

            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = RETRY_AFTER_SECONDS;
            await context.Response.WriteAsJsonAsync(
                ApiResponse<object>.Fail(NOT_READY_MESSAGE),
                context.RequestAborted);
        });
    }

    private static bool IsExempt(PathString path) =>
        path.StartsWithSegments("/version", StringComparison.OrdinalIgnoreCase);
}
