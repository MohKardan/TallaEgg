using System.Reflection;
using System.Text.RegularExpressions;
using TallaEgg.TelegramBot.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// No message may send a customer looking for a button that is not on a menu (issue #292).
///
/// <para>
/// <c>MsgOrderSuccess</c> told customers to check «سفارشات فعال». That button was deleted on
/// 2026-07-30 by the commit that made one quote fill produce one trade (#74/#75), and the message
/// went on naming it for fifty-three days. A label inside a string literal is invisible to the
/// compiler, so deleting the constant broke nothing that anything could notice.
/// </para>
///
/// <para>
/// <c>UserHelpMatchesTheMenuTests</c> already guards exactly this, and even names «سفارشات فعال»
/// as a label that has left the product — but it is anchored on three constants chosen by hand
/// (<c>MsgUserHelp</c>, <c>MsgAdminMainHelp</c>, <c>MsgAdminHelp</c>), so a fourth constant with
/// the same defect walked straight past it. That is the gap this sweep closes: it reads every
/// message constant there is, and it keeps working when someone adds the next one.
/// </para>
///
/// <para>
/// The rule is inverted deliberately. Rather than listing labels that have been removed — which
/// only ever catches what somebody remembered to add — anything a message quotes in «…» has to be
/// a button that exists <b>today</b>, or one of the handful of things below that are quoted for
/// emphasis and were never buttons. Delete a button from <see cref="BotBtns"/> and every message
/// still naming it fails on the spot, with no list to maintain.
/// </para>
/// </summary>
public class BotMessagesNameRealButtonsTests
{
    /// <summary>
    /// Guillemets are the bot's quoting convention throughout. The inner pattern excludes the
    /// closing mark so a message with several quotes yields several segments rather than one that
    /// swallows everything between the first and last.
    /// </summary>
    private static readonly Regex Quoted = new("«([^»]{1,40})»", RegexOptions.Compiled);

    /// <summary>
    /// Quoted for emphasis, never a menu label:
    /// <list type="bullet">
    ///   <item>«هر مثقال» and «گرم» are units of price and weight.</item>
    ///   <item>«آبشده» and «سکه» are the asset names an operator types in a command.</item>
    ///   <item>«{0}» is a format placeholder — the operator's own unrecognised input, echoed back
    ///         in a refusal, so its content is whatever they typed and never a button.</item>
    /// </list>
    /// A new entry here is a claim that something is not a button. Adding one to silence a failure
    /// about a name that <i>was</i> a button is how this guard would be defeated.
    /// </summary>
    private static readonly HashSet<string> NotMenuLabels = new(StringComparer.Ordinal)
    {
        "{0}", "هر مثقال", "گرم", "آبشده", "سکه"
    };

    /// <summary>Ordinal: these strings carry invisible formatting characters that a culture-sensitive comparison would fold away.</summary>
    private static HashSet<string> ButtonLabels() =>
        new(typeof(BotBtns)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(f => f.IsLiteral && f.FieldType == typeof(string))
                .Select(f => (string)f.GetRawConstantValue()!),
            StringComparer.Ordinal);

    private static List<(string Name, string Text)> Messages() =>
        typeof(BotMsgs)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (f.Name, (string)f.GetRawConstantValue()!))
            .ToList();

    // ── the guard ───────────────────────────────────────────────────────────────

    [Fact]
    public void NoMessageNamesAButtonThatIsNotOnAMenu()
    {
        var buttons = ButtonLabels();

        var offenders = Messages()
            .SelectMany(m => Quoted.Matches(m.Text).Select(match => (m.Name, Label: match.Groups[1].Value)))
            .Where(x => !buttons.Contains(x.Label) && !NotMenuLabels.Contains(x.Label))
            .Select(x => $"{x.Name} names «{x.Label}»")
            .Distinct()
            .ToList();

        Assert.True(offenders.Count == 0,
            "these messages quote a label that is not a button on any menu:\n  " +
            string.Join("\n  ", offenders));
    }

    // ── and the guard is looking at something ───────────────────────────────────

    /// <summary>
    /// A sweep that reaches nothing passes forever. Both halves are pinned: the constants read,
    /// and the quoted segments actually extracted from them — a regex that silently stopped
    /// matching would leave the check above green with nothing in it.
    /// </summary>
    [Fact]
    public void TheSweepReadsEveryMessageConstantAndFindsTheQuotedLabelsInThem()
    {
        var messages = Messages();
        var quoted = messages.SelectMany(m => Quoted.Matches(m.Text).Select(x => x.Groups[1].Value)).ToList();

        // 111 message constants and 10 quoted segments at the time of writing. The floors are
        // deliberately below those: this asserts the sweep still reaches the bulk of them, not
        // that nobody may add or remove a message.
        Assert.True(messages.Count >= 100, $"only {messages.Count} message constants were read");
        Assert.True(quoted.Count >= 5, $"only {quoted.Count} quoted labels were found in them");
    }

    /// <summary>
    /// And it recognises a real button when it sees one, so the check above can fail rather than
    /// passing because nothing it extracts ever resembles a menu label.
    /// </summary>
    [Fact]
    public void TheSweepMatchesAQuotedLabelAgainstTheButtonItNames()
    {
        var buttons = ButtonLabels();

        var quoted = Messages()
            .SelectMany(m => Quoted.Matches(m.Text).Select(x => x.Groups[1].Value))
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(BotBtns.BtnTradeHistory, quoted);
        Assert.Contains(BotBtns.BtnTradeHistory, buttons);
    }

    // ── the message this issue is about ─────────────────────────────────────────

    /// <summary>
    /// Named directly as well as swept, because the sweep would keep passing if someone dropped
    /// the navigation line rather than correcting it — and then a customer who has just traded is
    /// told nothing about where to see it.
    /// </summary>
    [Fact]
    public void TheOrderSuccessMessagePointsAtTheTradeHistoryButton()
    {
        Assert.Contains(BotBtns.BtnTradeHistory, BotMsgs.MsgOrderSuccess, StringComparison.Ordinal);
        Assert.Contains(BotBtns.BtnAccounting, BotMsgs.MsgOrderSuccess, StringComparison.Ordinal);
    }

    /// <summary>
    /// The second half of #292: on the quote path the trade has already executed and
    /// «✅ معاملهٔ شما انجام شد» arrives immediately after, so promising a future notification
    /// describes a wait that is already over.
    /// </summary>
    [Fact]
    public void TheOrderSuccessMessageDoesNotPromiseANotificationThatHasAlreadyArrived()
    {
        Assert.DoesNotContain("به‌محض انجام معامله", BotMsgs.MsgOrderSuccess, StringComparison.Ordinal);
    }
}
