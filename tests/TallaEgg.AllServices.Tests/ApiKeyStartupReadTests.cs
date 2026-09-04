using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Production's API key is read while the host is being built, not while a request is being
/// served (issue #214).
///
/// <para>
/// All four authenticating services called <c>APIKeyConstant.RequireTallaEggApiKey()</c> from
/// inside the <c>AddScheme</c> options delegate. That delegate is an options configuration
/// action: it runs the first time the scheme's options are resolved, which is inside
/// <c>AuthenticationHandler&lt;T&gt;.InitializeAsync</c> — on a request. With
/// <c>TALLAEGG_API_KEY</c> unset the service started, bound its port, reported healthy to
/// <c>sc.exe</c>, and answered 500 to everything.
/// </para>
///
/// <para>
/// Not a security hole — the request failed closed. The cost was diagnosis, and
/// <c>README.md</c> had promised the opposite ("each one throws at startup if it is missing")
/// since the guard was introduced.
/// </para>
/// </summary>
public class ApiKeyStartupReadTests
{
    private static readonly string[] AuthenticatingServices =
    [
        "src/User/Users.Api/Program.cs",
        "src/Wallet/Wallet.Api/Program.cs",
        "src/Order/Orders.Api/Program.cs",
        "src/Affiliate/Affiliate.Api/Program.cs",
    ];

    public static TheoryData<string> ServiceEntryPoints => [.. AuthenticatingServices];

    private const string GuardCall = "APIKeyConstant.RequireTallaEggApiKey()";
    private const string SchemeOptions = "ApiKeyAuthenticationSchemeOptions";

    /// <summary>The scheme's key, assigned from a bare local rather than an expression.</summary>
    private static readonly Regex ApiKeyAssignment =
        new(@"^\s*options\.ApiKey\s*=\s*(?<local>[A-Za-z_][A-Za-z0-9_]*)\s*;\s*$", RegexOptions.Compiled);

    /// <summary>That local, read from the guard at startup.</summary>
    private static Regex GuardedLocal(string name) =>
        new($@"^\s*var\s+{Regex.Escape(name)}\s*=\s*APIKeyConstant\.RequireTallaEggApiKey\(\)\s*;\s*$");

    private static string[] ReadLines(string relativePath)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TallaEgg.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return File.ReadAllLines(Path.Combine(directory.FullName, relativePath));
    }

    /// <summary>
    /// The <c>AddScheme</c> call's line range: from the call to the <c>});</c> that closes both
    /// it and the delegate it is passed.
    /// </summary>
    private static (int Start, int End) AddSchemeCall(string[] lines, string relativePath)
    {
        var start = Array.FindIndex(lines, line =>
            line.Contains($".AddScheme<{SchemeOptions}", StringComparison.Ordinal));
        Assert.True(start >= 0, $"{relativePath} registers no {SchemeOptions} scheme.");

        var end = Array.FindIndex(lines, start, line => line.Trim() == "});");
        Assert.True(end >= start, $"{relativePath}: could not find the end of the AddScheme call.");

        return (start, end);
    }

    /// <summary>
    /// The mechanism the source rules below exist to protect against, demonstrated rather than
    /// asserted from the shape of the source: an <c>AddScheme</c> configure delegate does not
    /// run when the container is built. Nothing about registering it, or about resolving the
    /// provider, forces the read — only asking for the options does.
    /// </summary>
    [Fact]
    public void AnAddSchemeConfigureDelegate_DoesNotRunUntilTheOptionsAreResolved()
    {
        var ran = false;

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthentication("ApiKey")
            .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options =>
            {
                ran = true;
                options.ApiKey = "whatever the guard would have returned";
            });

        var provider = services.BuildServiceProvider();
        Assert.False(ran);

        var monitor = provider.GetRequiredService<IOptionsMonitor<ApiKeyAuthenticationSchemeOptions>>();
        Assert.False(ran);

        _ = monitor.Get("ApiKey");
        Assert.True(ran);
    }

    /// <summary>
    /// Source inspection, for the reason <see cref="StartupGuardPlacementTests"/> gives: there is
    /// no host-level startup harness to hang a behavioural assertion on, and moving the read back
    /// inside the delegate would leave every other test green.
    ///
    /// <para>
    /// This rule is narrower than that file's column-zero one, and deliberately so. The read
    /// belongs inside <c>if (builder.Environment.IsProduction())</c> — outside Production no
    /// service registers API-key authentication, and requiring the variable there would stop
    /// every clone and CI job that has no reason to hold it — so it is legitimately indented and
    /// the blunt rule cannot express it. What has to hold is only that the read is not inside the
    /// <c>AddScheme</c> delegate, which is what this checks.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(ServiceEntryPoints))]
    public void TheApiKeyIsNotRead_InsideTheAddSchemeConfigureDelegate(string relativePath)
    {
        var lines = ReadLines(relativePath);
        var (start, end) = AddSchemeCall(lines, relativePath);

        var inside = lines[start..(end + 1)]
            .Select((line, offset) => (Line: line, Number: start + offset + 1))
            .Where(entry => entry.Line.Contains(GuardCall, StringComparison.Ordinal))
            .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}")
            .ToList();

        Assert.Empty(inside);
    }

    /// <summary>
    /// The key the scheme ends up holding is the one the guard returned, read before the
    /// registration that consumes it.
    ///
    /// <para>
    /// Asserting only that the file mentions the guard somewhere is not enough, and was not:
    /// <c>Users.Api</c> calls it a second time further down, for the API key its outgoing wallet
    /// client sends. That call satisfied a whole-file search on its own, so the Production auth
    /// read could have been swapped for <c>APIKeyConstant.TallaEggApiKey</c> — the local-dev
    /// placeholder, which is exactly the hole the guard exists to close — with every test still
    /// green.
    /// </para>
    ///
    /// <para>
    /// So this follows the value instead: the delegate must assign a bare local, and that local
    /// must have been assigned from the guard above the <c>AddScheme</c> call. An expression in
    /// the assignment fails the first half; a different source for the local fails the second.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(ServiceEntryPoints))]
    public void TheSchemesKey_ComesFromTheGuardReadAboveTheRegistration(string relativePath)
    {
        var lines = ReadLines(relativePath);
        var (start, end) = AddSchemeCall(lines, relativePath);

        var assignment = lines[start..(end + 1)]
            .Select(line => ApiKeyAssignment.Match(line))
            .FirstOrDefault(match => match.Success);

        Assert.True(assignment is not null,
            $"{relativePath}: the AddScheme delegate does not assign options.ApiKey from a local. " +
            "It has to, or the value cannot have been read at startup.");

        var local = assignment!.Groups["local"].Value;
        var guarded = GuardedLocal(local);

        var readAt = Array.FindIndex(lines, 0, start, line => guarded.IsMatch(line));
        Assert.True(readAt >= 0,
            $"{relativePath}: options.ApiKey is assigned from '{local}', but no " +
            $"'var {local} = {GuardCall};' appears above the AddScheme call.");
    }

    /// <summary>
    /// One place configures the scheme's options, and it is the delegate the rules above check.
    ///
    /// <para>
    /// Without this, the read could be moved into a
    /// <c>PostConfigure&lt;ApiKeyAuthenticationSchemeOptions&gt;</c> further down the file and be
    /// exactly as lazy as before — a different options configuration action, run at the same
    /// moment, out of reach of a rule that only looks between <c>AddScheme</c> and its closing
    /// brace.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(ServiceEntryPoints))]
    public void TheSchemesOptions_AreConfiguredInExactlyOnePlace(string relativePath)
    {
        var configuring = ReadLines(relativePath)
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => entry.Line.Contains(SchemeOptions, StringComparison.Ordinal)
                            && !entry.Line.TrimStart().StartsWith("//", StringComparison.Ordinal))
            .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}")
            .ToList();

        var registration = Assert.Single(configuring);
        Assert.Contains($".AddScheme<{SchemeOptions}", registration, StringComparison.Ordinal);
    }
}
