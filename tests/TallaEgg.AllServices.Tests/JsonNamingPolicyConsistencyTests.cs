namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That the platform keeps one JSON wire format: the three deployed APIs agree on it for their
/// responses (issue #229), and no type overrides it a field at a time (issue #235).
/// </summary>
/// <remarks>
/// Source-scanning, like <see cref="RuntimeVersionExposureTests"/> and
/// <see cref="ServiceHostPathAnchorTests"/>, and for the same reason: these are top-level
/// statements no test can build a container from.
///
/// It sweeps whole project trees rather than the three <c>Program.cs</c> files, and looks for
/// every spelling of the override rather than the one #229 happened to use. A guard that only
/// reads <c>Program.cs</c> would go quiet the moment this wiring is hoisted into a shared helper
/// — which is this repository's established habit for cross-service startup concerns
/// (<c>Cors/</c>, <c>ErrorHandling/</c>, <c>StartupLogging</c>), so <c>TallaEgg.Core</c> is swept
/// too. Passing vacuously after a refactor is the failure mode a guard like this is most prone
/// to, and refactoring is one of the two cases the guard exists for.
/// </remarks>
public class JsonNamingPolicyConsistencyTests
{
    /// <summary>
    /// The three hosts that serve HTTP, plus the shared kernel any common startup helper would
    /// live in. <c>Affiliate.Api</c> and <c>TallaEgg.Api</c> are absent for the reason given in
    /// <see cref="RuntimeVersionExposureTests"/>: neither is deployed.
    /// </summary>
    public static TheoryData<string> JsonWiringRoots =>
    [
        "src/User/Users.Api",
        "src/Wallet/Wallet.Api",
        "src/Order/Orders.Api",
        "src/TallaEgg/TallaEgg.Core",
    ];

    /// <summary>
    /// Where a <c>[JsonPropertyName]</c> can reach the wire. That is a wider question than where
    /// the naming policy is wired: an attribute travels with the type, so it matters wherever a
    /// serialized type is <em>declared</em>, not only where a host is configured. The bot declares
    /// request and response types of its own (<c>NotifyMatchingEngineRequest</c>,
    /// <c>InvitationDto</c>), so its two project trees are swept alongside the APIs and the shared
    /// kernel that holds most DTOs.
    /// </summary>
    public static TheoryData<string> SerializedTypeRoots =>
    [
        "src/User/Users.Api",
        "src/Wallet/Wallet.Api",
        "src/Order/Orders.Api",
        "src/TallaEgg/TallaEgg.Core",
        "TelegramBot/TallaEgg.TelegramBot.Core",
        "TelegramBot/TallaEgg.TelegramBot.Infrastructure",
    ];

    /// <summary>
    /// Wire names pinned against an external contract. Adding to this list is a contract decision:
    /// it opts a field out of the platform's camelCase default permanently.
    /// </summary>
    /// <remarks>
    /// Empty is the honest starting state. Issue #235 removed the only four the repository ever
    /// had, and all four were mistakes — two values crossed into four fields on
    /// <c>POST /api/orders</c>. The platform has no external consumer that dictates a spelling, so
    /// until one exists this list has nothing to hold.
    /// </remarks>
    private static readonly string[] AllowedPropertyNameOverrides = [];

    /// <summary>
    /// Every way an ASP.NET Core host can move its responses off the default camelCase.
    /// <c>PropertyNamingPolicy</c> covers <c>ConfigureHttpJsonOptions</c> and
    /// <c>Configure&lt;JsonOptions&gt;</c> alike; the other two swap the serializer wholesale and
    /// would take the naming policy with them without ever naming it.
    /// </summary>
    private static readonly string[] NamingPolicyOverrides =
    [
        "PropertyNamingPolicy",
        "AddJsonOptions",
        "AddNewtonsoftJson",
    ];

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TallaEgg.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }

    private static bool IsCode(string line) =>
        !line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    /// <summary>
    /// Every line of real source under <paramref name="relativeRoot"/> holding any of
    /// <paramref name="tokens"/>, rendered as <c>path:line: text</c>.
    /// </summary>
    private static List<string> ScanForTokens(string relativeRoot, params string[] tokens)
    {
        var root = Path.Combine(RepoRoot(), relativeRoot);
        Assert.True(Directory.Exists(root), $"{relativeRoot} does not exist; update the root list.");

        var hits = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            // Build output carries copies of source-generated and referenced code.
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (var index = 0; index < lines.Length; index++)
            {
                if (!IsCode(lines[index]))
                {
                    continue;
                }

                if (tokens.Any(token => lines[index].Contains(token, StringComparison.Ordinal)))
                {
                    hits.Add($"{Path.GetRelativePath(RepoRoot(), file)}:{index + 1}: {lines[index].Trim()}");
                }
            }
        }

        return hits;
    }

    /// <summary>
    /// Leaving the naming policy alone is what makes all three answer in the ASP.NET Core default
    /// camelCase. Setting it to anything — including <c>null</c>, which is what #229 was — opts one
    /// service out of the shape the other two produce, invisibly, because every current caller
    /// deserializes case-insensitively. It only becomes expensive once a browser client (#97) has
    /// shipped against one of the two shapes.
    /// </summary>
    [Theory]
    [MemberData(nameof(JsonWiringRoots))]
    public void ResponseNamingPolicy_InAnyDeployedApi_IsLeftAtTheFrameworkDefault(string relativeRoot)
    {
        var offenders = ScanForTokens(relativeRoot, NamingPolicyOverrides);

        Assert.True(
            offenders.Count == 0,
            "A deployed API overrides the JSON response naming policy, which is how #229 happened. "
                + "Leave it at the framework default so all three services answer in one shape:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// The other half of the same rule. Leaving the naming policy alone settles the shape of a
    /// whole service; <c>[JsonPropertyName]</c> then reopens it one field at a time, and it beats
    /// the policy in both directions — which is why #229's fix left #235's four crossed names
    /// byte-for-byte unchanged and could never have caught them.
    /// </summary>
    /// <remarks>
    /// The attribute has a legitimate use, so this is a gate rather than a ban: a name listed in
    /// <see cref="AllowedPropertyNameOverrides"/> passes. Putting one there is the reviewed,
    /// deliberate act that the four names #235 removed never went through.
    /// </remarks>
    [Theory]
    [MemberData(nameof(SerializedTypeRoots))]
    public void WireNameOverrides_OnAnySerializedType_AreAbsentOrAllowlisted(string relativeRoot)
    {
        var offenders = ScanForTokens(relativeRoot, "JsonPropertyName")
            .Where(hit => !AllowedPropertyNameOverrides.Any(
                allowed => hit.Contains($"\"{allowed}\"", StringComparison.Ordinal)))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A [JsonPropertyName] pins a wire name against the platform's camelCase default, which "
                + "is how #235 happened. Remove it, or — if an external contract genuinely requires "
                + "that exact spelling — add the name to AllowedPropertyNameOverrides together with "
                + "the reason, having checked that no other property already carries the same value:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }
}
