using System.Reflection;
using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// How a build's <c>InformationalVersion</c> is split into the version and the commit hash
/// reported by <c>GET /version</c> and by the startup log line (issue #218).
/// </summary>
/// <remarks>
/// The strings below are the four shapes a real build produces: with build metadata (inside a git
/// checkout), without it (outside one), with the attribute absent altogether, and with nothing to
/// read at all.
/// </remarks>
public class BuildVersionTests
{
    [Fact]
    public void Parse_SeparatesTheCommitHashFromTheVersion()
    {
        var build = BuildVersion.Parse("1.1.0+ff95e00a327536efa53e2af247b661ba9be5f744", new Version(1, 1, 0, 0));

        Assert.Equal("1.1.0", build.Version);
        Assert.Equal("ff95e00a327536efa53e2af247b661ba9be5f744", build.CommitHash);
    }

    /// <summary>
    /// A pre-release label is part of the version, not of the metadata: the split is at the first
    /// '+', so the '-' before it must not move it.
    /// </summary>
    [Fact]
    public void Parse_KeepsAPreReleaseLabelWithTheVersion()
    {
        var build = BuildVersion.Parse("1.2.0-beta.1+ff95e00", new Version(1, 2, 0, 0));

        Assert.Equal("1.2.0-beta.1", build.Version);
        Assert.Equal("ff95e00", build.CommitHash);
    }

    /// <summary>
    /// A build made outside a git checkout — a published tree, or a source archive — carries no
    /// metadata, and the whole string is the version.
    /// </summary>
    [Fact]
    public void Parse_ReportsNoCommitWhenTheBuildCarriedNoMetadata()
    {
        var build = BuildVersion.Parse("1.1.0", new Version(1, 1, 0, 0));

        Assert.Equal("1.1.0", build.Version);
        Assert.Null(build.CommitHash);
    }

    [Fact]
    public void Parse_ReportsNoCommitWhenTheMetadataIsEmpty()
    {
        var build = BuildVersion.Parse("1.1.0+", new Version(1, 1, 0, 0));

        Assert.Equal("1.1.0", build.Version);
        Assert.Null(build.CommitHash);
    }

    /// <summary>
    /// <c>AssemblyVersion</c> is always present, so a missing attribute still has an answer — the
    /// same three numbers with a fourth appended.
    /// </summary>
    [Fact]
    public void Parse_FallsBackToTheAssemblyVersionWhenTheAttributeIsAbsent()
    {
        var build = BuildVersion.Parse(null, new Version(1, 1, 0, 0));

        Assert.Equal("1.1.0.0", build.Version);
        Assert.Null(build.CommitHash);
    }

    [Fact]
    public void Parse_ReportsUnknownWhenThereIsNothingToRead()
    {
        var build = BuildVersion.Parse(null, null);

        Assert.Equal("unknown", build.Version);
        Assert.Null(build.CommitHash);
    }

    /// <summary>
    /// Reads a real assembly rather than a fabricated string, which is what proves the attribute
    /// is actually there to read. It asserts the shape and not the number: the version moves with
    /// every release, and a test that has to be edited to cut one would be edited without thought.
    /// </summary>
    [Fact]
    public void Read_ReportsTheVersionStampedIntoABuiltAssembly()
    {
        var build = BuildVersion.Read(typeof(BuildVersion).Assembly);

        Assert.True(Version.TryParse(build.Version, out _), $"'{build.Version}' is not a version number.");
    }

    /// <summary>
    /// <see cref="BuildVersion.Current"/> is what both callers use, and it is computed from
    /// <see cref="Assembly.GetEntryAssembly"/> — which under a test runner is the runner, not this
    /// assembly. The fall-back is what has to hold here.
    /// </summary>
    [Fact]
    public void Current_HasAnAnswerEvenWhenTheEntryAssemblyIsNotOurs()
    {
        Assert.False(string.IsNullOrWhiteSpace(BuildVersion.Current.Version));
    }
}
