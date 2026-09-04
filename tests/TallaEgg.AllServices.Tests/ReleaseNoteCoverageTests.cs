using System.Text.RegularExpressions;
using TallaEgg.TelegramBot.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That the version in <c>Directory.Build.props</c> has an entry in <see cref="ReleaseNotes"/>.
/// </summary>
/// <remarks>
/// Raising <c>VersionPrefix</c> is what makes <c>BotHandler.NotifyUpdateToAllUsers</c> treat the
/// next startup as an update and broadcast to every registered user. With no entry for the new
/// number, <see cref="ReleaseNotes.GetSummaryFor"/> returns an empty string and all of them get
/// an "updated" push with no changelog in it — which is what the class's own doc comment tells
/// the next person to avoid.
///
/// <para>
/// That was caught by review on #219 and would not have been caught by anything else: the bump
/// and the missing entry are in different files, the build does not connect them, and nothing
/// fails until real users are already being messaged. It is a two-line omission with an
/// unrecallable consequence, so it is asserted rather than remembered.
/// </para>
/// </remarks>
public class ReleaseNoteCoverageTests
{
    [Fact]
    public void TheCurrentVersion_HasSomethingToTellUsersAboutIt()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TallaEgg.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        var props = File.ReadAllText(Path.Combine(directory.FullName, "Directory.Build.props"));
        var match = Regex.Match(props, @"<VersionPrefix>(?<version>[^<]+)</VersionPrefix>");

        Assert.True(match.Success, "Directory.Build.props declares no VersionPrefix.");

        // IVersionService.GetCurrentVersion builds its key the same way — "Major.Minor.Build" —
        // so the entry has to be keyed by exactly the string VersionPrefix holds.
        var version = match.Groups["version"].Value.Trim();

        Assert.False(
            string.IsNullOrEmpty(ReleaseNotes.GetSummaryFor(version)),
            $"Version {version} has no entry in ReleaseNotes, so deploying it would send every "
            + "registered user an update message with an empty changelog.");
    }
}
