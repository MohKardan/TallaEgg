using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// What each host resolves its two startup paths against — the shared configuration file and the
/// Serilog file sink (issues #212 and #211).
///
/// <para>
/// Both were relative to the process working directory, and <c>sc.exe create</c> has no option to
/// set one, so the SCM handed every installed service <c>C:\Windows\System32</c>. The bot could
/// not start at all, and all four services wrote their logs into a Windows system directory. The
/// working directory is not something the deployment can influence; the content root and
/// <c>AppContext.BaseDirectory</c> both point at the binary's own folder under
/// <c>UseWindowsService()</c>, and are.
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
