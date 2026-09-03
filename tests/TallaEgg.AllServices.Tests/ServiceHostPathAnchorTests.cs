using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// What each host resolves its two startup paths against — the shared configuration file and the
/// Serilog file sink (issues #212 and #211).
///
/// <para>
/// Both were relative to the process working directory, and <c>sc.exe create</c> has no option to
/// set one, so the SCM handed every installed service <c>C:\Windows\System32</c>. The bot could
/// not start at all, and all four services wrote their logs into a Windows system directory. A
/// deployment cannot influence the working directory at all; it controls where the binary sits,
/// which is what <see cref="AppContext.BaseDirectory"/> reports and what
/// <c>UseWindowsService()</c> points the content root at.
/// </para>
///
/// <para>
/// These read the source, like <see cref="StartupGuardPlacementTests"/>, and for the same reason:
/// the property is about how a host is wired before it exists, so there is nothing to resolve
/// from a container and no host-level harness to hang an assertion on. A run under
/// <c>dotnet test</c> gets the working directory it wants either way, which is exactly why this
/// class of bug survives a green suite.
/// </para>
/// </summary>
public class ServiceHostPathAnchorTests
{
    /// <summary>
    /// The six programs that build a host. <c>TallaEgg.TelegramBot.Simulator</c> is deliberately
    /// not among them: it is a CLI tool run by hand from a developer's clone and is never
    /// installed as a service, so resolving its configuration from the working directory is
    /// correct there rather than drift.
    /// </summary>
    public static TheoryData<string> HostEntryPoints =>
    [
        "src/User/Users.Api/Program.cs",
        "src/Wallet/Wallet.Api/Program.cs",
        "src/Order/Orders.Api/Program.cs",
        "src/Affiliate/Affiliate.Api/Program.cs",
        "src/TallaEgg/TallaEgg.Api/Program.cs",
        "TelegramBot/TallaEgg.TelegramBot.Infrastructure/Program.cs",
    ];

    /// <summary>
    /// The four hosts <c>install-services.ps1</c> actually installs. <c>Affiliate.Api</c> and
    /// <c>TallaEgg.Api</c> are not among them and do not call <c>UseWindowsService()</c> — see
    /// #69: nothing calls <c>TallaEgg.Api</c>, and <c>Affiliate.Api</c> ships no migrations.
    /// </summary>
    public static TheoryData<string> DeployedHostEntryPoints =>
    [
        "src/User/Users.Api/Program.cs",
        "src/Wallet/Wallet.Api/Program.cs",
        "src/Order/Orders.Api/Program.cs",
        "TelegramBot/TallaEgg.TelegramBot.Infrastructure/Program.cs",
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
    /// A sink path given as a bare literal is relative, and Serilog resolves it against the
    /// working directory. Going through <see cref="StartupLogging.LogFilePath"/> is what makes it
    /// absolute, so the check is that no host passes <c>WriteTo.File</c> a string of its own.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostEntryPoints))]
    public void EveryLogSinkPath_IsAnchoredOnTheBinaryFolderRatherThanTheWorkingDirectory(string relativePath)
    {
        var lines = ReadLines(relativePath);

        var relativeSinks = lines
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => IsCode(entry.Line))
            .Where(entry => entry.Line.Contains("WriteTo.File(\"", StringComparison.Ordinal))
            .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}")
            .ToList();

        Assert.Empty(relativeSinks);
        Assert.Contains(lines, line => IsCode(line) && line.Contains("StartupLogging.LogFilePath(", StringComparison.Ordinal));
    }

    /// <summary>
    /// The shared configuration is found by walking up looking for a <c>config</c> folder. What
    /// the walk starts from is the whole issue: the content root follows the binary under
    /// <c>UseWindowsService()</c>, the working directory does not.
    /// </summary>
    [Theory]
    [MemberData(nameof(HostEntryPoints))]
    public void NoHost_ResolvesSharedConfigurationFromTheWorkingDirectory(string relativePath)
    {
        var lines = ReadLines(relativePath);

        var workingDirectoryReads = lines
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => IsCode(entry.Line))
            .Where(entry => entry.Line.Contains("GetCurrentDirectory()", StringComparison.Ordinal))
            .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}")
            .ToList();

        Assert.Empty(workingDirectoryReads);
        Assert.Contains(lines, line => IsCode(line) && line.Contains("ContentRootPath", StringComparison.Ordinal));
    }

    /// <summary>
    /// Reading the content root is only half of it. <c>ContentRootPath</c> defaults to the working
    /// directory; the one thing that moves it to the binary's folder is <c>UseWindowsService()</c>,
    /// which does so only inside a real SCM session. Delete that call from a deployed host and
    /// issue #212 comes straight back — the content root falls back to <c>C:\Windows\System32</c>,
    /// the walk up finds no <c>config\</c>, and the host throws inside <c>HostBuilder.Build()</c>
    /// — while every other assertion in this class stays green, because they only check what the
    /// walk starts from.
    ///
    /// <para>
    /// Blunt, like the rest of this file: it checks the call is written, not that it took effect.
    /// Nothing here can check the latter, since it is a no-op outside a service session, which is
    /// the whole difficulty with this class of bug.
    /// </para>
    /// </summary>
    [Theory]
    [MemberData(nameof(DeployedHostEntryPoints))]
    public void EveryDeployedHost_CallsUseWindowsServiceSoTheContentRootFollowsTheBinary(string relativePath)
    {
        Assert.Contains(
            ReadLines(relativePath),
            line => IsCode(line) && line.Contains("UseWindowsService()", StringComparison.Ordinal));
    }

    /// <summary>
    /// The one part of this that is behaviour rather than source shape.
    /// </summary>
    [Fact]
    public void LogFilePath_ReturnsAnAbsolutePathBesideTheBinary()
    {
        var path = StartupLogging.LogFilePath("example-.log");

        Assert.True(Path.IsPathRooted(path));
        Assert.Equal(
            Path.Combine(AppContext.BaseDirectory, "logs"),
            Path.GetDirectoryName(path));
        Assert.Equal("example-.log", Path.GetFileName(path));
    }
}
