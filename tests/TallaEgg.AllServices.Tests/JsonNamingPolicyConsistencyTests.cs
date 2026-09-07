namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That the three deployed APIs agree on one JSON wire format for their responses (issue #229).
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
        var root = Path.Combine(RepoRoot(), relativeRoot);
        Assert.True(Directory.Exists(root), $"{relativeRoot} does not exist; update JsonWiringRoots.");

        var offenders = new List<string>();

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

                foreach (var token in NamingPolicyOverrides)
                {
                    if (lines[index].Contains(token, StringComparison.Ordinal))
                    {
                        offenders.Add(
                            $"{Path.GetRelativePath(RepoRoot(), file)}:{index + 1}: {lines[index].Trim()}");
                    }
                }
            }
        }

        Assert.True(
            offenders.Count == 0,
            "A deployed API overrides the JSON response naming policy, which is how #229 happened. "
                + "Leave it at the framework default so all three services answer in one shape:"
                + Environment.NewLine
                + string.Join(Environment.NewLine, offenders));
    }
}
