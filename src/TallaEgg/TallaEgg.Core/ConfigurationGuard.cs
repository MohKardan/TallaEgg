using Microsoft.Extensions.Configuration;

namespace TallaEgg.Core;

/// <summary>
/// Reads configuration values that a service cannot start without, and fails loudly when one
/// is missing.
/// </summary>
public static class ConfigurationGuard
{
    /// <summary>The file every service reads its settings from, named in the failure message.</summary>
    private const string SHARED_CONFIG_FILE_NAME = "appsettings.global.json";

    /// <summary>
    /// Returns the named connection string, or throws naming what is missing and where it belongs.
    /// </summary>
    /// <remarks>
    /// Each service previously fell back to a hardcoded literal:
    /// <code>
    /// GetConnectionString("OrdersDb") ?? "Server=localhost;Database=TallaEggOrders;..."
    /// </code>
    /// That turns a configuration mistake into a connectivity error. A missing key, a renamed
    /// key, or a config file the host cannot locate all surfaced as
    /// <c>"A network-related or instance-specific error has occurred... Server is not found"</c>,
    /// which points at the database rather than at the setting that is actually absent — the
    /// exact confusion recorded in issue #68.
    ///
    /// On a host that <i>does</i> answer at <c>localhost</c> it is worse than confusing: the
    /// service starts, migrates, and reads and writes a different database than intended, with
    /// nothing in the logs to say so.
    ///
    /// Throwing at startup keeps a misconfigured service from ever reaching a request.
    /// </remarks>
    /// <param name="configuration">Configuration the service was built with.</param>
    /// <param name="name">Connection string name, e.g. <c>OrdersDb</c>.</param>
    /// <exception cref="InvalidOperationException">The value is absent, empty, or whitespace.</exception>
    public static string RequireConnectionString(IConfiguration configuration, string name)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var value = configuration.GetConnectionString(name);

        // Whitespace counts as missing. A key present but blank is a half-finished edit, and
        // letting it through only moves the failure to the first query.
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Connection string '{name}' is missing. Add it under \"ConnectionStrings\" in " +
                $"{SHARED_CONFIG_FILE_NAME}. The service will not start without it.");
        }

        return value;
    }

    /// <summary>
    /// Returns the named configuration value as an absolute http(s) URI, or throws naming what
    /// is wrong and where it belongs.
    /// </summary>
    /// <remarks>
    /// The same reasoning as <see cref="RequireConnectionString"/>, applied to the address of
    /// another service. A fallback literal there is worse than no value at all: the service
    /// starts and then dials a host nobody configured — issue #190 found
    /// <c>GetValue&lt;string&gt;("WalletApiUrl") ?? "https://localhost:60932/"</c> pointing at a
    /// port this system has never used, kept alive only by configuration always supplying the
    /// real one.
    ///
    /// It returns a <see cref="Uri"/> rather than the string so that the parse happens here,
    /// at startup, and not wherever the caller eventually constructs one. A value that is
    /// present but not a usable address — a bare port, a host with no scheme, a leftover
    /// placeholder — is a configuration mistake in exactly the way an absent one is, and both
    /// have to stop the service before it accepts a request. <c>HttpClient</c> would otherwise
    /// raise it on the first call, far from the cause.
    /// </remarks>
    /// <param name="configuration">Configuration the service was built with.</param>
    /// <param name="key">Configuration key, e.g. <c>WalletApiUrl</c>.</param>
    /// <exception cref="InvalidOperationException">
    /// The value is absent, empty, whitespace, or not an absolute http(s) URI.
    /// </exception>
    public static Uri RequireUri(IConfiguration configuration, string key)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var value = configuration[key];

        // Whitespace counts as missing, for the same reason it does for a connection string.
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' is missing. Add it under this service's own " +
                $"section, Services:{{ApplicationName}}, in {SHARED_CONFIG_FILE_NAME}. The " +
                $"service will not start without it.");
        }

        // Uri.TryCreate alone is not enough: "localhost:60933" parses as absolute with the
        // scheme "localhost", and HttpClient rejects it only when a request is finally made.
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' is not an absolute http(s) URL: '{value}'. Fix it " +
                $"under this service's own section, Services:{{ApplicationName}}, in " +
                $"{SHARED_CONFIG_FILE_NAME}. The service will not start without a usable address.");
        }

        return uri;
    }
}
