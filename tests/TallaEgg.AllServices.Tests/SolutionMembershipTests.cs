using System.Text.RegularExpressions;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Every project on disk must be in <c>TallaEgg.sln</c>, or be listed here as a deliberate
/// exclusion.
///
/// <para>
/// A project outside the solution is invisible: <c>dotnet build TallaEgg.sln</c> never compiles
/// it, <c>dotnet test</c> never runs it, and CI — which builds the solution — cannot report on
/// it. It rots without anyone being told.
/// </para>
///
/// <para>
/// This is not hypothetical. It is what happened to the wallet test project: it sat outside the
/// build for roughly a year, its assertions silently uncompiled, and was eventually mistaken for
/// dead code and nearly deleted (#117). The fix then was to move it in and make the CI step name
/// the solution rather than one project path. This test is the general form of that fix — the
/// next stray project fails a test instead of waiting a year to be noticed.
/// </para>
///
/// <para>
/// <b>Why a test and not a CI step:</b> the check needs no network, no database and no
/// configuration, and it runs in milliseconds. A developer finds out before pushing rather than
/// after. It also fails locally in the same way it fails in CI, which a workflow-only check does
/// not.
/// </para>
/// </summary>
public class SolutionMembershipTests
{
    /// <summary>
    /// Projects knowingly left out of the solution, each with the reason it is out.
    ///
    /// <para>
    /// An entry here is a statement that someone looked and decided, which is the whole
    /// difference between this and the situation in #117. Entries are expected to be removed,
    /// not accumulated — <see cref="ExcludedProjects_AllStillExist"/> makes sure a stale one
    /// cannot sit here after the project itself is gone.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> DeliberatelyExcluded = new()
    {
        ["TelegramBot/TestRunner/TestRunner.csproj"] =
            "A 2025 attempt at a bot simulator that never grew. It has no ProjectReference to " +
            "any TallaEgg project, so its MockBotHandler is a reimplementation of the bot inside " +
            "its own file and its assertions can only ever confirm what that mock was written to " +
            "return. TallaEgg.TelegramBot.Simulator (#101) does the same job against the real " +
            "BotHandler, and the four flows it scripts are covered here against real code. " +
            "Removal is an open decision; until it is made, the exclusion is at least visible.",
    };

    /// <summary>
    /// Fails naming every project that is on disk, not in the solution, and not excluded above.
    /// </summary>
    [Fact]
    public void EveryProjectOnDisk_IsInTheSolution()
    {
        var root = FindRepositoryRoot();
        var inSolution = ProjectsInSolution(root);
        var onDisk = ProjectsOnDisk(root);

        var stray = onDisk
            .Where(p => !inSolution.Contains(p) && !DeliberatelyExcluded.ContainsKey(p))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(stray.Count == 0,
            "These projects are on disk but not in TallaEgg.sln, so nothing builds or tests " +
            "them:" + Environment.NewLine +
            string.Join(Environment.NewLine, stray.Select(p => "  " + p)) + Environment.NewLine +
            "Add each to the solution, or — if it is genuinely meant to stay out — add it to " +
            $"{nameof(DeliberatelyExcluded)} with the reason.");
    }

    /// <summary>
    /// The exclusion list must not outlive what it excludes, or it quietly becomes a licence for
    /// the next stray project that happens to share a path.
    /// </summary>
    [Fact]
    public void ExcludedProjects_AllStillExist()
    {
        var root = FindRepositoryRoot();

        var gone = DeliberatelyExcluded.Keys
            .Where(p => !File.Exists(Path.Combine(root, p.Replace('/', Path.DirectorySeparatorChar))))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(gone.Count == 0,
            $"{nameof(DeliberatelyExcluded)} names projects that no longer exist. Remove them:" +
            Environment.NewLine +
            string.Join(Environment.NewLine, gone.Select(p => "  " + p)));
    }

    /// <summary>
    /// The solution must not reference a project file that has been deleted or moved — that
    /// breaks <c>dotnet build</c> for everyone, and it is the same class of drift in the other
    /// direction.
    /// </summary>
    [Fact]
    public void EveryProjectInTheSolution_ExistsOnDisk()
    {
        var root = FindRepositoryRoot();

        var missing = ProjectsInSolution(root)
            .Where(p => !File.Exists(Path.Combine(root, p.Replace('/', Path.DirectorySeparatorChar))))
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        Assert.True(missing.Count == 0,
            "TallaEgg.sln references project files that do not exist:" + Environment.NewLine +
            string.Join(Environment.NewLine, missing.Select(p => "  " + p)));
    }

    /// <summary>
    /// Repo-relative, forward-slashed paths of every project the solution lists.
    /// </summary>
    private static HashSet<string> ProjectsInSolution(string root)
    {
        var text = File.ReadAllText(Path.Combine(root, "TallaEgg.sln"));

        // Solution project lines end with the project path in the third quoted field:
        //   Project("{GUID}") = "Name", "src\Foo\Foo.csproj", "{GUID}"
        var paths = Regex.Matches(text, "\"([^\"]+\\.csproj)\"")
            .Select(m => m.Groups[1].Value.Replace('\\', '/'));

        return new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Repo-relative, forward-slashed paths of every project file in the working tree, ignoring
    /// build output.
    /// </summary>
    private static List<string> ProjectsOnDisk(string root)
    {
        return Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(p => !p.Contains("/bin/", StringComparison.Ordinal)
                     && !p.Contains("/obj/", StringComparison.Ordinal))
            .ToList();
    }

    /// <summary>
    /// Walks up from the test assembly until the directory holding <c>TallaEgg.sln</c> is found.
    /// The assembly sits several levels down in <c>bin</c>, and the depth differs between
    /// configurations, so the solution file itself is the anchor rather than a fixed number of
    /// <c>..</c> segments.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TallaEgg.sln")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null,
            "Could not find TallaEgg.sln above " + AppContext.BaseDirectory);

        return dir!.FullName;
    }
}
