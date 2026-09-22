using System.Text.RegularExpressions;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Handlers;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The admin help, the user list and the command dispatch describe the same set of commands
/// (issue #293).
///
/// <para>
/// All three had drifted apart. The help called «س» the customer's active orders when it had been
/// repointed at their trade history; «نماد فعال/غیرفعال» was handled and documented nowhere; and
/// «ف» was offered in the user list as «سفارشات باز» while no dispatch for it has ever existed —
/// <c>git log -S 'StartsWith("ف'</c> returns nothing across the whole history. It was advertised
/// on 2025-09-10 by a commit whose own message says «تست نشده», and an admin who typed it got the
/// main menu, silently, for over a year.
/// </para>
///
/// <para>
/// <c>UserHelpMatchesTheMenuTests.TheAdminHelpNamesEveryOperatorCommand</c> already guards the
/// help — with eleven commands listed by hand, and «نماد» is not among them. That is why this was
/// missed, and it is the same shape as #292: a guard aimed by hand covers only what someone
/// remembered to aim it at. So all three sets here are derived, not listed: the dispatch is read
/// from the handler's source, the help is parsed from the constant, and the user list is parsed
/// from output the renderer actually produced.
/// </para>
///
/// <para>
/// Reading source in a test is established here — <c>NoOutOfProcessLoggingTests</c>,
/// <c>ClientRequestWireContractTests</c> and <c>JsonNamingPolicyConsistencyTests</c> all do it. It
/// is the only way to enumerate a dispatch written as a chain of <c>StartsWith</c>, short of
/// restructuring the dispatch, which this issue did not ask for.
/// </para>
/// </summary>
public class AdminHelpMatchesTheCommandsTests
{
    private const long ChatId = 12345;

    /// <summary>
    /// The dispatch reads <c>msgText.StartsWith("…")</c>, one line per command. The trailing space
    /// several of them carry ("م ", "س ") is part of the guard against matching a longer word, not
    /// part of the command, so it is trimmed away here.
    /// </summary>
    private static readonly Regex DispatchPrefix = new(@"msgText\.StartsWith\(""(?<cmd>[^""]+)""\)", RegexOptions.Compiled);

    /// <summary>
    /// A command as the help writes it: the word, then a bracketed argument — «م [شمارهٔ تلفن]»,
    /// «ک [جستجو]» — on its own line or after a colon. The optional second word is
    /// «نماد فعال [نماد]», whose command is the first word and whose second is its mode. Without
    /// allowing it that command parses out of the help as nothing, and escapes the check below in
    /// silence.
    /// </summary>
    private static readonly Regex HelpCommand = new(@"(?:^|\n|:\s*)(?<cmd>[\u0600-\u06FF]+)(?:\s+[\u0600-\u06FF]+)?\s+\[", RegexOptions.Compiled);

    /// <summary>A command as the user list offers it: `x 09123456789` in a Markdown code span.</summary>
    private static readonly Regex ListedCommand = new(@"`(?<cmd>[\u0600-\u06FF]+)\s+[\d\\]+`", RegexOptions.Compiled);

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

    /// <summary>Every command the dispatch actually answers.</summary>
    private static HashSet<string> HandledCommands()
    {
        var source = File.ReadAllText(Path.Combine(
            RepositoryRoot(), "TelegramBot", "TallaEgg.TelegramBot.Infrastructure", "BotHandlerAdmin.cs"));

        return DispatchPrefix.Matches(source)
            .Select(m => m.Groups["cmd"].Value.Trim())
            .ToHashSet(StringComparer.Ordinal);
    }

    private static HashSet<string> CommandsInTheHelp() =>
        HelpCommand.Matches(BotMsgs.MsgAdminHelp)
            .Select(m => m.Groups["cmd"].Value)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>
    /// Rendered, not read from source: what the list offers is what it printed, and the escaping
    /// it applies to a phone number is part of that.
    /// </summary>
    private static async Task<HashSet<string>> CommandsOfferedByTheUserListAsync()
    {
        var page = new PagedResult<UserDto>
        {
            Items =
            [
                new UserDto
                {
                    Id = Guid.NewGuid(),
                    TelegramId = 555_001,
                    FirstName = "مشتری",
                    PhoneNumber = "09121234567",
                    Status = UserStatus.Approved,
                    Role = UserRole.User,
                    CreatedAt = DateTime.UtcNow
                }
            ],
            TotalCount = 1,
            PageNumber = 1,
            PageSize = 5
        };

        var text = await UserListHandler.BuildUsersListAsync(page, 1, null);

        return ListedCommand.Matches(text)
            .Select(m => m.Groups["cmd"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    // ── the three directions ────────────────────────────────────────────────────

    /// <summary>
    /// «ف» is the reason this test exists: offered to every admin who listed users, answered by
    /// nothing, for over a year.
    /// </summary>
    [Fact]
    public async Task EveryCommandTheUserListOffersIsHandled()
    {
        var handled = HandledCommands();
        var offered = await CommandsOfferedByTheUserListAsync();

        var unhandled = offered.Where(c => !handled.Contains(c)).ToList();

        Assert.True(unhandled.Count == 0,
            "the user list offers commands the bot does not handle: " + string.Join("، ", unhandled));
    }

    [Fact]
    public void EveryCommandTheHelpDocumentsIsHandled()
    {
        var handled = HandledCommands();

        var unhandled = CommandsInTheHelp().Where(c => !handled.Contains(c)).ToList();

        Assert.True(unhandled.Count == 0,
            "the admin help documents commands the bot does not handle: " + string.Join("، ", unhandled));
    }

    /// <summary>
    /// The direction that caught «نماد»: handled, working, and invisible to the only documentation
    /// an operator has.
    /// </summary>
    [Fact]
    public void EveryHandledCommandIsInTheHelp()
    {
        var help = BotMsgs.MsgAdminHelp;

        var undocumented = HandledCommands()
            .Where(c => !help.Contains(c, StringComparison.Ordinal))
            .ToList();

        Assert.True(undocumented.Count == 0,
            "the bot handles commands the admin help never mentions: " + string.Join("، ", undocumented));
    }

    // ── and the three sets are not empty ────────────────────────────────────────

    /// <summary>
    /// Every check above passes on an empty set. Each source is pinned so a regex that silently
    /// stopped matching — or a file that moved — turns the guard red instead of green-and-blind.
    /// Eleven dispatch prefixes, eight documented commands and three offered by the user list at
    /// the time of writing; the floors sit below those so ordinary additions do not trip them.
    /// </summary>
    [Fact]
    public async Task AllThreeSetsWereActuallyRead()
    {
        var handled = HandledCommands();
        var documented = CommandsInTheHelp();
        var offered = await CommandsOfferedByTheUserListAsync();

        Assert.True(handled.Count >= 10, $"only {handled.Count} dispatch prefixes were found");
        Assert.True(documented.Count >= 6, $"only {documented.Count} commands were parsed out of the help");
        Assert.True(offered.Count >= 2, $"only {offered.Count} commands were parsed out of the user list");

        // The ones every reading must contain, so a regex that matched something unrelated still fails.
        Assert.Contains("نماد", handled);
        Assert.Contains("م", documented);
        Assert.Contains("س", offered);

        // «نماد» specifically, out of the help: it is the one command written as two words, and the
        // first version of the help parser could not see that shape. Parsing it as nothing would
        // have left EveryCommandTheHelpDocumentsIsHandled silently skipping it.
        Assert.Contains("نماد", documented);
    }

    // ── what the help says «س» does ─────────────────────────────────────────────

    /// <summary>
    /// The set checks cannot see this: «س» is handled and documented either way. What changed is
    /// what it does — active orders became the customer's completed trades, because in the dealer
    /// model an order exists only for the instant of a fill, so that list was always empty.
    /// </summary>
    [Fact]
    public void TheHelpDescribesTheCustomerCommandAsTradesRatherThanActiveOrders()
    {
        Assert.DoesNotContain("سفارش‌های فعال کاربر", BotMsgs.MsgAdminHelp, StringComparison.Ordinal);
    }
}
