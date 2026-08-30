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
///
/// <para>
/// That last property is what <see cref="IsToolingScratch"/> protects. "On disk" means in the
/// repository, not merely in the folder: a project under a dot-directory is invisible to CI, so
/// counting it here would fail on one developer's machine and nowhere else — and a test that only
/// fails for the person who ran it is one people learn to skip.
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
    ///
    /// <para>
    /// Empty on purpose. The one entry it ever held, <c>TelegramBot/TestRunner</c>, lasted a
    /// single change before the project was deleted.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, string> DeliberatelyExcluded = new();

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
    /// build output and tooling scratch.
    /// </summary>
    private static List<string> ProjectsOnDisk(string root)
    {
        return Directory
            .EnumerateFiles(root, "*.csproj", SearchOption.AllDirectories)
            .Select(f => Path.GetRelativePath(root, f).Replace('\\', '/'))
            .Where(p => !p.Contains("/bin/", StringComparison.Ordinal)
                     && !p.Contains("/obj/", StringComparison.Ordinal)
                     && !IsToolingScratch(p))
            .ToList();
    }

    /// <summary>
    /// Whether a path sits inside a dot-directory — <c>.audit-work/</c>, <c>.vs/</c>,
    /// <c>.github/</c>, and whatever the next tool creates.
    ///
    /// <para>
    /// A project under one of those is not a stray project. It is tooling scratch, kept out of
    /// the repository, so nothing is rotting unbuilt and the solution has no business holding it.
    /// Counting it fails the test on the one machine that happens to have the folder while CI
    /// stays green — and a test that only fails for the person who ran it is one people learn to
    /// skip. This is not hypothetical either: a disposable reproduction harness written under
    /// <c>.audit-work/</c> during an audit turned this red locally and nowhere else.
    /// </para>
    ///
    /// <para>
    /// <b>Why the leading dot and not <c>git check-ignore</c>:</b> asking git would honour
    /// whatever <c>.gitignore</c> says next, which is the more general rule — but it needs a
    /// <c>git</c> executable the test host can actually start. On the Windows machine this repo
    /// is developed on, git lives inside Git Bash and is not on the Windows <c>PATH</c>, so the
    /// subprocess fails and the filter silently does nothing precisely where it is needed. A rule
    /// that works identically everywhere beats a more general one that quietly stops working.
    /// </para>
    ///
    /// <para>
    /// The narrowness is deliberate. An untracked project in an ordinary folder still fails the
    /// test — that one is a real stray, committed or about to be. Only the dot-prefix convention,
    /// which every tool in this repository already follows, is treated as "not source".
    /// </para>
    /// </summary>
    private static bool IsToolingScratch(string relativePath) =>
        relativePath.Split('/').Any(segment => segment.StartsWith('.'));

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
