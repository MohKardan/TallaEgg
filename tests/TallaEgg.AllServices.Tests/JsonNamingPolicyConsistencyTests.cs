namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That the three deployed APIs agree on one JSON wire format (issue #229).
/// </summary>
/// <remarks>
/// Source-scanning, like <see cref="RuntimeVersionExposureTests"/> and
/// <see cref="ServiceHostPathAnchorTests"/>, and for the same reason: these are top-level
/// statements no test can build a container from. What it defends against is exactly how #229
/// happened — one service quietly setting a naming policy the other two do not, staying invisible
/// because every caller deserializes case-insensitively, and only becoming expensive once a
/// browser client (#97) has shipped against one of the two shapes.
/// </remarks>
public class JsonNamingPolicyConsistencyTests
{
    /// <summary>
    /// The three hosts that serve HTTP. <c>Affiliate.Api</c> and <c>TallaEgg.Api</c> are absent
    /// for the reason given in <see cref="RuntimeVersionExposureTests"/>: neither is deployed.
    /// </summary>
    public static TheoryData<string> DeployedApiEntryPoints =>
    [
        "src/User/Users.Api/Program.cs",
        "src/Wallet/Wallet.Api/Program.cs",
        "src/Order/Orders.Api/Program.cs",
    ];

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

    private static bool IsCode(string line) =>
        !line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    /// <summary>
    /// Leaving <c>PropertyNamingPolicy</c> alone is what makes all three answer in the ASP.NET
    /// Core default camelCase. Setting it to anything — including <c>null</c>, which is what
    /// #229 was — opts one service out of the shape the other two produce.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeployedApiEntryPoints))]
    public void NoDeployedApi_OverridesThePropertyNamingPolicy(string relativePath)
    {
        Assert.DoesNotContain(
            ReadLines(relativePath),
            line => IsCode(line) && line.Contains("PropertyNamingPolicy", StringComparison.Ordinal));
    }
}
