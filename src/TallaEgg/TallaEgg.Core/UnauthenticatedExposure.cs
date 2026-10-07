using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace TallaEgg.Core;

/// <summary>
/// Warns when a service runs without authentication on an address other machines can reach
/// (issue #332).
/// </summary>
/// <remarks>
/// Users, Wallet, Orders and Affiliate register API-key authentication only when the hosting
/// environment is Production. That is deliberate and correct, and the Production path has been
/// executed and seen to refuse a missing key. The trap is the other half: <c>dotnet run</c> applies
/// <c>launchSettings.json</c>, which sets Development and overrides an exported
/// <c>ASPNETCORE_ENVIRONMENT</c>, and nothing says so — <c>Hosting environment: Development</c> is
/// printed as information. Harmless while every service binds loopback; an open API the moment
/// someone widens the listen address for a second machine. This turns that one combination into a
/// warning in the log, and changes nothing else.
///
/// <para>
/// It checks the addresses the server actually bound, once it has started, rather than working them
/// out from configuration. Kestrel takes addresses from several places — the shared file's
/// <c>Urls</c>, <c>ASPNETCORE_URLS</c>, <c>--urls</c>, <c>ASPNETCORE_HTTP_PORTS</c>, a
/// <c>Kestrel:Endpoints</c> section — and a check that re-derived them would have to track every one.
/// A first version did, and in doing so blinded the source guard that protects the #181 fix.
/// </para>
/// </remarks>
public static class UnauthenticatedExposure
{
    /// <summary>
    /// Logs a warning once <paramref name="app"/> has started, if it is not Production and any address
    /// it bound is reachable from another machine. Call it after <c>builder.Build()</c>.
    /// </summary>
    public static void WarnOnceStartedIfExposed(WebApplication app) =>
        app.Lifetime.ApplicationStarted.Register(() =>
        {
            var exposed = ExposedUrls(app.Environment.IsProduction(), app.Urls);
            if (exposed.Count == 0)
                return;

            // Static Log, like the rest of StartupLogging, so it reaches the same sinks whatever
            // logging the host has wired by now.
            Log.Warning(
                "Hosting environment is {Environment}, so API-key authentication is NOT registered, yet " +
                "this service is listening on {ExposedUrls}, which other machines can reach. Anyone who can " +
                "reach that address can call every endpoint with no key. Run it as Production — the " +
                "installed service, or `dotnet run --no-launch-profile` with ASPNETCORE_ENVIRONMENT=" +
                "Production — or bind it to localhost (issue #332).",
                app.Environment.EnvironmentName, exposed);
        });

    /// <summary>
    /// The listen addresses reachable from another machine, when the environment is not Production;
    /// empty otherwise. A wildcard host (<c>*</c>, <c>+</c>, <c>0.0.0.0</c>, <c>[::]</c>) counts as
    /// reachable, and so does any name or address that is not <c>localhost</c>, a loopback IP, or a
    /// Unix socket or named pipe.
    /// </summary>
    public static IReadOnlyList<string> ExposedUrls(bool isProduction, IEnumerable<string> urls) =>
        isProduction
            ? []
            : urls.Where(u => !string.IsNullOrWhiteSpace(u) && !IsLocalOnly(u)).ToList();

    /// <summary>
    /// Whether a listen address can be reached only from this machine. Parsed by hand rather than
    /// with <see cref="Uri"/>, which rejects the <c>*</c> and <c>+</c> wildcards Kestrel accepts.
    /// </summary>
    private static bool IsLocalOnly(string url)
    {
        var rest = url.Trim();

        var scheme = rest.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
            rest = rest[(scheme + 3)..];

        // Kestrel's Unix-socket and named-pipe forms: http://unix:/path, http://pipe:/name.
        if (rest.StartsWith("unix:", StringComparison.OrdinalIgnoreCase) ||
            rest.StartsWith("pipe:", StringComparison.OrdinalIgnoreCase))
            return true;

        var path = rest.IndexOf('/');
        if (path >= 0)
            rest = rest[..path];

        string host;
        if (rest.StartsWith('['))
        {
            var end = rest.IndexOf(']');
            host = end > 0 ? rest[1..end] : rest;
        }
        else
        {
            var port = rest.LastIndexOf(':');
            host = port >= 0 ? rest[..port] : rest;
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!IPAddress.TryParse(host, out var address))
            return false;

        // IPAddress.IsLoopback knows only ::1 for IPv6, so ::ffff:127.0.0.1 has to be unwrapped.
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        return IPAddress.IsLoopback(address);
    }
}
