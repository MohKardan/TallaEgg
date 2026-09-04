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
    /// The mechanism the two rules below exist to protect against, demonstrated rather than
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

        var delegateStart = Array.FindIndex(lines, line =>
            line.Contains(".AddScheme<ApiKeyAuthenticationSchemeOptions", StringComparison.Ordinal));
        Assert.InRange(delegateStart, 0, lines.Length - 1);

        // The delegate is the argument of that call, so it ends where the call does: the first
        // line closing both, written "});" on its own.
        var delegateEnd = Array.FindIndex(lines, delegateStart, line =>
            line.Trim() == "});");
        Assert.InRange(delegateEnd, delegateStart, lines.Length - 1);

        var inside = lines[delegateStart..(delegateEnd + 1)]
            .Select((line, offset) => (Line: line, Number: delegateStart + offset + 1))
            .Where(entry => entry.Line.Contains(GuardCall, StringComparison.Ordinal))
            .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}")
            .ToList();

        Assert.Empty(inside);
    }

    /// <summary>
    /// And the read still happens. Deleting it would satisfy the rule above while putting the
    /// Production hole — a service authenticating against the local-dev placeholder — back.
    /// </summary>
    [Theory]
    [MemberData(nameof(ServiceEntryPoints))]
    public void EveryAuthenticatingService_StillRequiresTheKey(string relativePath)
    {
        Assert.Contains(ReadLines(relativePath), line =>
            line.Contains(GuardCall, StringComparison.Ordinal)
            && !line.TrimStart().StartsWith("//", StringComparison.Ordinal));
    }
}
