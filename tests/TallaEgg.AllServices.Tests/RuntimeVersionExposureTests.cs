namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That every deployed host actually exposes which build it is running (issue #218).
/// </summary>
/// <remarks>
/// These read the source, like <see cref="ServiceHostPathAnchorTests"/> and
/// <see cref="StartupGuardPlacementTests"/>, and for the same reason: the property is about how a
/// host is wired, and four of the five wirings are in top-level statements that no test can build
/// a container from. What they defend against is a sixth service being added, or one of these
/// lines being dropped in a merge — either of which brings back the question #218 exists to
/// answer, silently.
/// </remarks>
public class RuntimeVersionExposureTests
{
    /// <summary>
    /// The four hosts <c>install-services.ps1</c> installs — the ones that run somewhere nobody
    /// can read a file-properties dialog. <c>Affiliate.Api</c> and <c>TallaEgg.Api</c> are
    /// deliberately absent: neither is deployed (see #69), so neither has the question to answer.
    /// </summary>
    public static TheoryData<string> DeployedHostEntryPoints =>
    [
        "src/User/Users.Api/Program.cs",
        "src/Wallet/Wallet.Api/Program.cs",
        "src/Order/Orders.Api/Program.cs",
        "TelegramBot/TallaEgg.TelegramBot.Infrastructure/Program.cs",
    ];

    /// <summary>
    /// The three of those that serve HTTP. The bot is not among them — it has no endpoints, which
    /// is exactly why the startup log line above is the whole of its answer.
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
        !line.TrimStart().StartsWith("//", StringComparison.Ordinal)
        && !line.TrimStart().StartsWith("///", StringComparison.Ordinal);

    /// <summary>
    /// The log line is the only answer available for a service that throws during startup, which
    /// is when the question is asked most urgently — so it has to be reached before the
    /// configuration guards, and it is asserted here for every deployed host including the bot.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeployedHostEntryPoints))]
    public void EveryDeployedHost_LogsWhichBuildItIsAtStartup(string relativePath)
    {
        Assert.Contains(
            ReadLines(relativePath),
            line => IsCode(line) && line.Contains("StartupLogging.LogBuildVersion();", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(DeployedApiEntryPoints))]
    public void EveryDeployedApi_AnswersTheVersionOverHttp(string relativePath)
    {
        Assert.Contains(
            ReadLines(relativePath),
            line => IsCode(line) && line.Contains("MapGet(\"/version\"", StringComparison.Ordinal));
    }

    /// <summary>
    /// The endpoint carries no <c>AllowAnonymous</c>, so Production's fallback authorization
    /// policy applies and a caller needs the same <c>X-API-Key</c> as every other endpoint. That
    /// is a decision, not an omission: the commit hash names an exact line of a public repository.
    /// Adding it would be a one-word change with no visible effect outside Production, so it is
    /// pinned here rather than left to the comment beside it.
    /// </summary>
    [Theory]
    [MemberData(nameof(DeployedApiEntryPoints))]
    public void TheVersionEndpoint_IsNotOpenedToUnauthenticatedCallers(string relativePath)
    {
        var lines = ReadLines(relativePath);

        var endpoint = Array.FindIndex(
            lines,
            line => IsCode(line) && line.Contains("MapGet(\"/version\"", StringComparison.Ordinal));

        Assert.True(endpoint >= 0, $"{relativePath} does not map /version.");

        // The mapping and everything chained onto it, up to the statement's terminating ';'.
        var statement = new List<string>();
        for (var index = endpoint; index < lines.Length; index++)
        {
            statement.Add(lines[index]);

            if (lines[index].TrimEnd().EndsWith(';'))
            {
                break;
            }
        }

        Assert.DoesNotContain(statement, line => line.Contains("AllowAnonymous", StringComparison.Ordinal));
    }
}
