using Xunit;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The systemd unit files under <c>scripts/linux/systemd/</c> are the Linux counterpart of the
/// service definitions <c>install-services.ps1</c> creates with <c>sc.exe</c> (issue #349), so
/// the properties that keep the Windows deployment honest get the same guard here.
///
/// <para>
/// These read the repository, like <see cref="ServiceHostPathAnchorTests"/>, and for the same
/// reason: the machines this suite runs on have no systemd, so nothing here can exercise a unit.
/// What they can do is catch the drift a redeploy introduces — a renamed publish folder, a unit
/// moved to root, an API key pasted into a world-readable file, a <c>Requires=</c> on a database
/// unit a remote-database host will never have — while it is still a review comment instead of a
/// service that will not start.
/// </para>
/// </summary>
public class LinuxSystemdUnitTests
{
    /// <summary>
    /// The deployment root the unit files are written against. Both the unit files and
    /// <c>install-services.sh</c> hard-code it, mirroring <c>C:\TallaEgg</c> on the Windows side;
    /// the test pins the two together so they cannot drift apart silently.
    /// </summary>
    private const string InstallRoot = "/opt/tallaegg";

    /// <summary>
    /// One row per deployed service, in dependency order: the unit's file name, the publish
    /// folder <c>publish-all.sh</c> writes it to (the same folder names the Windows scripts
    /// use), and the entry-point assembly <c>ExecStart</c> has to run.
    /// </summary>
    public static TheoryData<string, string, string> DeployedServices => new()
    {
        { "tallaegg-wallet.service", "Wallet.Api", "Wallet.Api.dll" },
        { "tallaegg-users.service", "Users.Api", "Users.Api.dll" },
        { "tallaegg-orders.service", "Orders.Api", "Orders.Api.dll" },
        { "tallaegg-bot.service", "Bot", "TallaEgg.TelegramBot.Infrastructure.dll" },
    };

    /// <summary>
    /// The unit file names alone, for the checks that care about no other column.
    /// </summary>
    public static TheoryData<string> UnitFileNames()
    {
        var data = new TheoryData<string>();
        foreach (var row in DeployedServices)
        {
            data.Add((string)row[0]);
        }

        return data;
    }

    private static string ReadUnit(string fileName)
    {
        var directory = FindRepoRoot();
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory.FullName, "scripts", "linux", "systemd", fileName));
    }

    private static string[] ReadUnitLines(string fileName) => ReadUnit(fileName).Split('\n');

    private static DirectoryInfo? FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TallaEgg.sln")))
        {
            directory = directory.Parent;
        }

        return directory;
    }

    [Theory]
    [MemberData(nameof(DeployedServices))]
    public void EveryDeployedService_HasAUnitFileRunningItsPublishedOutput(string unit, string folder, string dll)
    {
        var lines = ReadUnitLines(unit);

        var workingDirectory = lines.SingleOrDefault(line =>
            line.TrimStart().StartsWith("WorkingDirectory=", StringComparison.Ordinal));
        Assert.Equal($"WorkingDirectory={InstallRoot}/publish/{folder}", workingDirectory?.TrimEnd('\r'));

        var execStart = lines.SingleOrDefault(line =>
            line.TrimStart().StartsWith("ExecStart=", StringComparison.Ordinal));
        Assert.Equal($"ExecStart=/usr/bin/dotnet {InstallRoot}/publish/{folder}/{dll}", execStart?.TrimEnd('\r'));
    }

    [Theory]
    [MemberData(nameof(UnitFileNames))]
    public void EveryUnit_SupervisesCrashRecoveryAndRunsUnprivileged(string unit)
    {
        var lines = ReadUnitLines(unit);

        Assert.Contains(lines, line => line.TrimStart().StartsWith("Restart=always", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.TrimStart().StartsWith("RestartSec=10s", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.Trim() == "User=tallaegg");
        Assert.Contains(lines, line => line.Trim() == "Group=tallaegg");
        Assert.DoesNotContain(lines, line => line.Trim().StartsWith("User=root", StringComparison.Ordinal));

        // Type=notify is what makes UseSystemd() active — without it systemd never sets
        // NOTIFY_SOCKET, the host never reports readiness, and a process that dies during
        // construction looks "active" to the service manager.
        Assert.Contains(lines, line => line.Trim() == "Type=notify");
    }

    [Theory]
    [MemberData(nameof(UnitFileNames))]
    public void EveryUnit_TakesTheApiKeyFromTheRootOwnedEnvironmentFile(string unit)
    {
        var lines = ReadUnitLines(unit);

        // install-services.sh writes this file root-owned, mode 600. A unit file in
        // /etc/systemd/system is world-readable, so the key must never appear in one.
        Assert.Contains(lines, line =>
            line.TrimStart().StartsWith("EnvironmentFile=", StringComparison.Ordinal)
            && line.Contains($"{InstallRoot}/config/tallaegg.env", StringComparison.Ordinal));

        Assert.DoesNotContain(lines, line => line.Contains("TALLAEGG_API_KEY", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(UnitFileNames))]
    public void EveryUnit_PinsTheProductionEnvironment(string unit)
    {
        var lines = ReadUnitLines(unit);

        Assert.Contains(lines, line =>
            line.Trim() == "Environment=ASPNETCORE_ENVIRONMENT=Production");
    }

    /// <summary>
    /// Every unit starts after the things it calls, through soft dependencies only. The three
    /// APIs also order themselves after the local SQL Server unit when there is one; the bot,
    /// which opens no database, does not name it at all. A missing unit under
    /// <c>Wants=/After=</c> is ignored with a warning — the remote-database case — where
    /// <c>Requires=</c> would refuse every start.
    /// </summary>
    [Theory]
    [MemberData(nameof(UnitFileNames))]
    public void EveryUnit_StartsAfterWhatItDependsOn_ThroughSoftDependenciesOnly(string unit)
    {
        var lines = ReadUnitLines(unit);
        var after = lines.Where(line => line.TrimStart().StartsWith("After=", StringComparison.Ordinal))
            .Select(line => line.Trim()).ToList();
        var wants = lines.Where(line => line.TrimStart().StartsWith("Wants=", StringComparison.Ordinal))
            .Select(line => line.Trim()).ToList();

        // No hard requirements anywhere: the deployment survives a host without a local SQL
        // Server, and stopping one service does not cascade into the others.
        Assert.DoesNotContain(lines, line => line.TrimStart().StartsWith("Requires=", StringComparison.Ordinal));

        if (unit == "tallaegg-bot.service")
        {
            Assert.Contains(after, line => line.Contains("tallaegg-wallet.service", StringComparison.Ordinal));
            Assert.Contains(after, line => line.Contains("tallaegg-users.service", StringComparison.Ordinal));
            Assert.Contains(after, line => line.Contains("tallaegg-orders.service", StringComparison.Ordinal));
            // The bot opens no database of its own; naming a SQL Server unit here would drag a
            // dependency it does not have onto hosts that run one.
            Assert.DoesNotContain(after.Concat(wants), line => line.Contains("mssql-server", StringComparison.Ordinal));
        }
        else
        {
            Assert.Contains(after, line => line.Contains("mssql-server.service", StringComparison.Ordinal));
            Assert.Contains(wants, line => line.Contains("mssql-server.service", StringComparison.Ordinal));
        }

        if (unit is "tallaegg-users.service" or "tallaegg-orders.service")
        {
            Assert.Contains(after, line => line.Contains("tallaegg-wallet.service", StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// Exactly the four deployed services have units here — a fifth project must not grow a
    /// unit by accident. <c>Affiliate.Api</c> and <c>TallaEgg.Api</c> are not deployed (see #69).
    /// </summary>
    [Fact]
    public void UnitDirectory_ContainsExactlyTheFourDeployedUnits()
    {
        var directory = FindRepoRoot();
        Assert.NotNull(directory);
        var units = Directory.GetFiles(Path.Combine(directory.FullName, "scripts", "linux", "systemd"), "*.service")
            .Select(file => Path.GetFileName(file) ?? "")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        var expected = DeployedServices.Select(row => (string)row[0])
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();
        Assert.Equal(expected, units);
    }
}
