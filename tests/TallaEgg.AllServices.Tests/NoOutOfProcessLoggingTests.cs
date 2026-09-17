using System.Text.RegularExpressions;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The services report through their own logs and nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// The bot used to carry a second reporting path next to Serilog: <c>TelegramLoggerService</c>,
/// which posted every incoming customer message — contact cards with phone numbers included — plus
/// user approvals and exceptions, together with a hardcoded bot token, to a notification relay on
/// infrastructure outside the project's control. The token sat in <c>Program.cs</c> as a literal
/// because it had no settings key.
/// </para>
/// <para>
/// That path is gone. Every host writes to Serilog (console and a rolling file, see
/// <c>StartupLogging</c>), and that is the one place diagnostics go. These tests stop the path
/// coming back: no bot token in tracked source, no relay host, no second logger abstraction.
/// </para>
/// </remarks>
public class NoOutOfProcessLoggingTests
{
    // Telegram's token format: the bot's numeric id, a colon, then 35 characters starting "AA".
    private static readonly Regex BotToken = new(@"(?<![0-9])[0-9]{8,10}:AA[A-Za-z0-9_-]{33}(?![A-Za-z0-9_-])");

    private static readonly string[] ForbiddenMarkers =
    [
        "workers.dev",
        "ITelegramLogger",
        "TelegramLoggerService",
    ];

    // Source a build or a deployment reads. Generated output and this test's own file are excluded:
    // the markers above appear here by necessity.
    private static readonly string[] ScannedExtensions = [".cs", ".json", ".csproj", ".config", ".ps1", ".yml", ".yaml"];

    [Fact]
    public void NoTrackedSource_ContainsATelegramBotToken()
    {
        var offenders = SourceFiles()
            .Where(f => BotToken.IsMatch(File.ReadAllText(f.FullPath)))
            .Select(f => f.Relative)
            .ToList();

        Assert.True(offenders.Count == 0,
            "A Telegram bot token is in source. Tokens belong in config/appsettings.global.json, which is not tracked:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void NoTrackedSource_ReportsToATelegramRelayOrASecondLogger()
    {
        var offenders = SourceFiles()
            .SelectMany(f => ForbiddenMarkers
                .Where(marker => File.ReadAllText(f.FullPath).Contains(marker, StringComparison.Ordinal))
                .Select(marker => $"{f.Relative}: {marker}"))
            .ToList();

        Assert.True(offenders.Count == 0,
            "Diagnostics go to ILogger (Serilog) only; this reintroduces an out-of-process reporting path:"
            + Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    /// <summary>
    /// A sweep that reaches nothing passes vacuously. It must at least see the bot's host and the
    /// services' logging setup, which is where a reporting path would be wired.
    /// </summary>
    [Fact]
    public void TheSweep_ReachesTheFilesWhereLoggingIsWired()
    {
        var reached = SourceFiles().Select(f => f.Relative).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("TelegramBot/TallaEgg.TelegramBot.Infrastructure/Program.cs", reached);
        Assert.Contains("TelegramBot/TallaEgg.TelegramBot.Infrastructure/BotHandler.cs", reached);
        Assert.Contains("src/TallaEgg/TallaEgg.Core/StartupLogging.cs", reached);
        Assert.True(reached.Count > 300, $"Expected the whole source tree, reached {reached.Count} files.");
    }

    private static IEnumerable<(string FullPath, string Relative)> SourceFiles()
    {
        var root = RepositoryRoot();
        var self = Path.GetFullPath(Path.Combine(root, "tests", "TallaEgg.AllServices.Tests", nameof(NoOutOfProcessLoggingTests) + ".cs"));

        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(path => ScannedExtensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            .Where(path => !IsExcluded(Path.GetRelativePath(root, path)))
            .Where(path => !string.Equals(Path.GetFullPath(path), self, StringComparison.OrdinalIgnoreCase))
            .Select(path => (path, Path.GetRelativePath(root, path).Replace('\\', '/')));
    }

    private static bool IsExcluded(string relative)
    {
        var segments = relative.Replace('\\', '/').Split('/');
        // Build output, VCS and local tooling, and the untracked live config.
        return segments.Any(s => s is "bin" or "obj" or ".git" or ".vs" or "node_modules" or ".claude" or ".audit-work")
            || relative.Replace('\\', '/') == "config/appsettings.global.json";
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TallaEgg.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return directory.FullName;
    }
}
