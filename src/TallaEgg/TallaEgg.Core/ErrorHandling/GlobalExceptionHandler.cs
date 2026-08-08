using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.DTOs;

namespace TallaEgg.Core.ErrorHandling;

/// <summary>
/// Single point where every unhandled exception in an API service is logged and turned into a
/// response. Keeps error shape and logging consistent across services instead of each endpoint
/// doing its own catch/log/return. See issue #88.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;

        _logger.LogError(
            exception,
            "Unhandled exception. TraceId={TraceId} Method={Method} Path={Path}",
            traceId,
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.Headers["X-Trace-Id"] = traceId;

        await httpContext.Response.WriteAsJsonAsync(
            ApiResponse<object>.Error("خطای داخلی سرور"),
            cancellationToken);

        return true;
    }
}
