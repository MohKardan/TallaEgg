using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.DTOs;

namespace TallaEgg.Core.RateLimiting;

/// <summary>
/// Configuration for the platform's HTTP rate limiter (issue #279).
/// Bound from the <c>RateLimiting</c> configuration section; missing keys fall back to generous
/// defaults so existing loopback bot traffic and tests are unaffected while internet-facing
/// deployments can tighten limits without a rebuild.
/// </summary>
public sealed class RateLimitingOptions
{
    public const string SectionName = "RateLimiting";
    public const int DefaultPermitLimit = 300;
    public const int DefaultWindowSeconds = 60;
    public const int DefaultQueueLimit = 0;

    /// <summary>
    /// Enables or disables HTTP rate limiting. Defaults to <c>true</c>.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of requests allowed per partition within a single window. Defaults to 300.
    /// </summary>
    public int PermitLimit { get; set; } = DefaultPermitLimit;

    /// <summary>
    /// Fixed window duration in seconds. Defaults to 60.
    /// </summary>
    public int WindowSeconds { get; set; } = DefaultWindowSeconds;

    /// <summary>
    /// Maximum number of requests to queue once the permit limit is reached. Defaults to 0
    /// (reject excess bursts immediately with HTTP 429).
    /// </summary>
    public int QueueLimit { get; set; } = DefaultQueueLimit;

    internal RateLimitingOptions Normalize() => new()
    {
        Enabled = Enabled,
        PermitLimit = PermitLimit > 0 ? PermitLimit : DefaultPermitLimit,
        WindowSeconds = WindowSeconds > 0 ? WindowSeconds : DefaultWindowSeconds,
        QueueLimit = QueueLimit >= 0 ? QueueLimit : DefaultQueueLimit
    };
}

/// <summary>
/// Registers and applies ASP.NET Core's built-in rate limiting middleware consistently across
/// Users.Api, Wallet.Api and Orders.Api (issue #279).
/// </summary>
public static class RateLimitingExtensions
{
    private const string TOO_MANY_REQUESTS_MESSAGE =
        "تعداد درخواست‌ها بیش از حد مجاز است. لطفاً کمی بعد دوباره تلاش کنید.";

    private const string SERVICE_CLIENT_ID = "api-client";
    private const string USER_ID_HEADER = "X-User-Id";
    private const string FORWARDED_FOR_HEADER = "X-Forwarded-For";
    private const string LOOPBACK_FALLBACK = "loopback";

    /// <summary>
    /// Configures global partitioned fixed-window rate limiting from the <c>RateLimiting</c>
    /// configuration section. Rejections answer <c>429 Too Many Requests</c> with a
    /// <c>Retry-After</c> header and the platform's standard <see cref="ApiResponse{T}"/> JSON
    /// envelope.
    /// </summary>
    public static IServiceCollection AddTallaEggRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var bound = new RateLimitingOptions();
        configuration.GetSection(RateLimitingOptions.SectionName).Bind(bound);
        var options = bound.Normalize();

        services.AddSingleton(options);

        services.AddRateLimiter(limiterOptions =>
        {
            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            limiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;

                var retryAfterSeconds = options.WindowSeconds;
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                {
                    retryAfterSeconds = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds));
                }

                context.HttpContext.Response.Headers.RetryAfter =
                    retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

                var logger = context.HttpContext.RequestServices
                    .GetService<ILoggerFactory>()
                    ?.CreateLogger("TallaEgg.Core.RateLimiting");
                logger?.LogWarning(
                    "Rate limit exceeded. Method={Method} Path={Path} RetryAfterSeconds={RetryAfterSeconds}",
                    context.HttpContext.Request.Method,
                    context.HttpContext.Request.Path,
                    retryAfterSeconds);

                // Every API refusal on this platform uses ApiResponse<T> rather than ProblemDetails
                // so clients deserialize one consistent shape (see OrderRequestContractTests).
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponse<object>.Fail(TOO_MANY_REQUESTS_MESSAGE),
                    cancellationToken);
            };

            limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                if (!options.Enabled)
                {
                    return RateLimitPartition.GetNoLimiter("disabled");
                }

                var partitionKey = ResolvePartitionKey(httpContext);

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = options.PermitLimit,
                        Window = TimeSpan.FromSeconds(options.WindowSeconds),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = options.QueueLimit,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    /// <summary>
    /// Adds the rate limiting middleware to the request pipeline when enabled. Must be paired with
    /// <see cref="AddTallaEggRateLimiting"/>; omitting the registration fails at startup rather
    /// than silently leaving endpoints unthrottled.
    /// </summary>
    public static IApplicationBuilder UseTallaEggRateLimiting(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = app.ApplicationServices.GetRequiredService<RateLimitingOptions>();
        if (!options.Enabled)
        {
            return app;
        }

        app.UseRateLimiter();
        return app;
    }

    /// <summary>
    /// Resolves the rate-limit partition key for a request: per authenticated user where the
    /// caller carries a distinct user identity, and per client IP otherwise.
    /// </summary>
    /// <remarks>
    /// An unauthenticated <c>X-User-Id</c> header is deliberately ignored so an untrusted client
    /// cannot rotate header values to bypass limits. Only an authenticated trusted service caller
    /// (the shared <c>api-client</c> principal issued by <see cref="ApiKeyAuthenticationHandler"/>)
    /// may supply <c>X-User-Id</c> to partition on behalf of an end user.
    /// </remarks>
    internal static string ResolvePartitionKey(HttpContext context)
    {
        if (context.User?.Identity?.IsAuthenticated == true)
        {
            var claimId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? context.User.FindFirstValue("sub");

            if (!string.IsNullOrWhiteSpace(claimId)
                && !string.Equals(claimId, SERVICE_CLIENT_ID, StringComparison.OrdinalIgnoreCase))
            {
                return $"user:{claimId.Trim()}";
            }

            if (context.Request.Headers.TryGetValue(USER_ID_HEADER, out var forwardedUserId))
            {
                var userId = forwardedUserId.ToString().Trim();
                if (!string.IsNullOrEmpty(userId))
                {
                    return $"user:{userId}";
                }
            }
        }

        return $"ip:{ResolveClientIp(context)}";
    }

    private static string ResolveClientIp(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(FORWARDED_FOR_HEADER, out var forwardedFor))
        {
            var firstIp = forwardedFor.ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();

            if (!string.IsNullOrWhiteSpace(firstIp))
            {
                return firstIp;
            }
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString();
        return string.IsNullOrWhiteSpace(remoteIp) ? LOOPBACK_FALLBACK : remoteIp;
    }
}
