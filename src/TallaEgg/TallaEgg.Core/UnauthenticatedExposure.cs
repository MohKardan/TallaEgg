using System.Net;
using Serilog;

namespace TallaEgg.Core;

/// <summary>
/// Warns when a service runs without authentication on an address other machines can reach
/// (issue #332).
/// </summary>
/// <remarks>
/// Users, Wallet and Orders register API-key authentication only when the hosting environment is
/// Production. That is deliberate and correct, and the Production path has been executed and seen to
/// refuse a missing key. The trap is the other half: <c>dotnet run</c> applies
/// <c>launchSettings.json</c>, which sets Development and overrides an exported
/// <c>ASPNETCORE_ENVIRONMENT</c>, and nothing says so — <c>Hosting environment: Development</c> is
/// printed as information. Harmless while every service binds loopback; an open API the moment
/// someone widens <c>Urls</c> for a second machine. This turns that one combination into a warning
/// in the log, and changes nothing else.
/// </remarks>
public static class UnauthenticatedExposure
{
    /// <summary>
    /// The listen addresses reachable from another machine, when the environment is not Production;
    /// empty otherwise. A wildcard host (<c>*</c>, <c>+</c>, <c>0.0.0.0</c>, <c>[::]</c>) counts as
    /// reachable, and so does any name or address that is not <c>localhost</c> or a loopback IP.
    /// </summary>
    public static IReadOnlyList<string> ExposedUrls(bool isProduction, IEnumerable<string> urls) =>
        isProduction
            ? []
            : urls.Where(u => !string.IsNullOrWhiteSpace(u) && !IsLoopback(u)).ToList();

    /// <summary>
    /// Logs a warning naming the exposed addresses, if there are any. Through Serilog's static
    /// <see cref="Log"/>, like the rest of <see cref="StartupLogging"/>: it runs before the host exists.
    /// </summary>
    public static void WarnIfExposed(string environmentName, bool isProduction, IEnumerable<string> urls)
    {
        var exposed = ExposedUrls(isProduction, urls);
        if (exposed.Count == 0)
            return;

        Log.Warning(
            "Hosting environment is {Environment}, so API-key authentication is NOT registered, yet this " +
            "service listens on {ExposedUrls}, which other machines can reach. Anyone who can reach that " +
            "address can call every endpoint with no key. Run it as Production — the installed service, or " +
            "`dotnet run --no-launch-profile` with ASPNETCORE_ENVIRONMENT=Production — or bind it to " +
            "localhost (issue #332).",
            environmentName, exposed);
    }

    /// <summary>
    /// Whether a listen address names this machine only. Parsed by hand rather than with
    /// <see cref="Uri"/>, which rejects the <c>*</c> and <c>+</c> wildcards Kestrel accepts.
    /// </summary>
    internal static bool IsLoopback(string url)
    {
        var rest = url.Trim();

        var scheme = rest.IndexOf("://", StringComparison.Ordinal);
        if (scheme >= 0)
            rest = rest[(scheme + 3)..];

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

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }
}
