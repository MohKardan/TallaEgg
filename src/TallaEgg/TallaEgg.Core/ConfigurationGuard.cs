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
}
