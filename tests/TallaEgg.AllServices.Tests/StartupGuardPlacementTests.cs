namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Where the startup guards sit in each <c>Program.cs</c>, which is behaviour even though it
/// looks like formatting (issue #205).
///
/// <para>
/// <c>ConfigurationGuard</c> exists so a missing key stops a service before it can serve a
/// request. That only holds if the guard actually runs during startup. Every service called
/// <c>RequireConnectionString</c> from inside the <c>AddDbContext</c> options delegate, which
/// does not run until <c>DbContextOptions&lt;T&gt;</c> is first resolved — so the guard fired at
/// startup only because the migration block a few lines later happened to resolve the context.
/// <c>Orders.Api</c> had the same shape inside an <c>AddHttpClient</c> configure delegate, where
/// nothing resolved it at all. Reorder or remove those incidental resolutions and a missing key
/// silently becomes a first-request failure.
/// </para>
///
/// <para>
/// Nothing else in this solution pins that. There is no host-level startup harness to hang the
/// assertion on, so moving a guarded read back inside a delegate would leave every other test
/// green. These two rules read the source instead, which is blunt but is the difference between
/// a property that is checked and one that is merely true today.
/// </para>
/// </summary>
public class StartupGuardPlacementTests
{
    public static TheoryData<string> ServiceEntryPoints =>
    [
        "src/User/Users.Api/Program.cs",
        "src/Wallet/Wallet.Api/Program.cs",
        "src/Order/Orders.Api/Program.cs",
        "src/Affiliate/Affiliate.Api/Program.cs",
        "src/TallaEgg/TallaEgg.Api/Program.cs",
    ];

    private const string GuardCall = "ConfigurationGuard.Require";

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

    private static bool IsGuardCall(string line) =>
        line.Contains(GuardCall, StringComparison.Ordinal)
        && !line.TrimStart().StartsWith("//", StringComparison.Ordinal);

    /// <summary>
    /// These files are top-level statements, so a guard that runs during startup is written at
    /// column zero. Indentation means it sits inside a lambda — a configure delegate — and runs
    /// whenever the container gets around to it.
    /// </summary>
    [Theory]
    [MemberData(nameof(ServiceEntryPoints))]
    public void EveryConfigurationGuardCall_RunsAtStartupRatherThanInsideAConfigureDelegate(string relativePath)
    {
        var indented = ReadLines(relativePath)
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(entry => IsGuardCall(entry.Line))
            .Where(entry => char.IsWhiteSpace(entry.Line[0]))
            .Select(entry => $"{relativePath}:{entry.Number}: {entry.Line.Trim()}")
            .ToList();

        Assert.Empty(indented);
    }

    /// <summary>Each service still reads at least one value through the guard.</summary>
    [Theory]
    [MemberData(nameof(ServiceEntryPoints))]
    public void EveryService_ReadsAtLeastOneValueThroughTheGuard(string relativePath)
    {
        Assert.Contains(ReadLines(relativePath), IsGuardCall);
    }

    /// <summary>
    /// A guard's message names the key and the file to edit, so it is written to be acted on by
    /// an operator with no debugger attached — and under <c>sc.exe</c> that operator has no
    /// console to read it in. Serilog has to be installed before the first guard can throw, or
    /// the one line that says what is wrong reaches nothing.
    /// </summary>
    [Theory]
    [MemberData(nameof(ServiceEntryPoints))]
    public void Serilog_IsInstalledBeforeTheFirstGuardCanThrow(string relativePath)
    {
        var lines = ReadLines(relativePath);

        var serilog = Array.FindIndex(lines, line =>
            line.Contains("builder.Host.UseSerilog()", StringComparison.Ordinal));
        var firstGuard = Array.FindIndex(lines, IsGuardCall);

        Assert.InRange(serilog, 0, firstGuard - 1);
    }
}
