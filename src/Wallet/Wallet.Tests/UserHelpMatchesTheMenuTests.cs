using TallaEgg.TelegramBot.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// The help text has to name the buttons that are actually on the menu.
///
/// <para>
/// It drifted. «📈 بازار نقدی» was renamed to «💹 دریافت مظنه» and «⚡ سفارشات فعال» was
/// removed from the product entirely, but the help kept advertising both — so a customer
/// following it went looking for two buttons, one of which had a different name and one of
/// which no longer existed.
/// </para>
///
/// <para>
/// Nothing caught it because a button name inside a string literal is invisible to the
/// compiler: renaming the constant changed every keyboard and left the help untouched. The
/// help is now built from the same constants the keyboards use, and these tests assert the
/// two stay in step.
/// </para>
/// </summary>
public class UserHelpMatchesTheMenuTests
{
    /// <summary>
    /// Ordinal throughout — a culture-sensitive comparison ignores the invisible formatting
    /// characters these strings carry, so a mismatch could pass unnoticed.
    /// </summary>
    private static bool Mentions(string text, string button) =>
        text.Contains(button, StringComparison.Ordinal);

    // ── every button on the menu is explained ───────────────────────────────────

    [Fact]
    public void TheHelpNamesTheQuoteButton()
    {
        Assert.True(Mentions(BotMsgs.MsgUserHelp, BotBtns.BtnSpotMarket),
            $"help must name the quote button ({BotBtns.BtnSpotMarket})");
    }

    [Fact]
    public void TheHelpNamesTheAccountingButton()
    {
        Assert.True(Mentions(BotMsgs.MsgUserHelp, BotBtns.BtnAccounting),
            $"help must name the accounting button ({BotBtns.BtnAccounting})");
    }

    // ── and nothing that is no longer there ─────────────────────────────────────

    /// <summary>
    /// The two strings that were actually wrong. Pinned by literal rather than by constant,
    /// because the constants are gone — that is the point: a literal is the only way to assert
    /// a name has left the product.
    /// </summary>
    [Theory]
    [InlineData("بازار نقدی")]      // renamed to «دریافت مظنه»
    [InlineData("سفارشات فعال")]    // button removed from the product
    public void TheHelpDoesNotAdvertiseAButtonThatIsGone(string removed)
    {
        Assert.False(Mentions(BotMsgs.MsgUserHelp, removed),
            $"help still advertises «{removed}», which is not on the menu");
    }

    /// <summary>
    /// The customer's accounting menu holds trade history only — the quote-history button is
    /// for operators. Promising customers their "orders" sends them looking for a list they
    /// cannot reach.
    /// </summary>
    [Fact]
    public void TheHelpDoesNotPromiseCustomersAnOrderList()
    {
        Assert.False(Mentions(BotMsgs.MsgUserHelp, "سفارش‌ها"),
            "the customer's accounting menu has trade history only, not an order list");
    }

    // ── the operator's own main-menu help, same rule ────────────────────────────

    /// <summary>
    /// The operator's menu has «💹 اعلام مظنه», not the customer's «💹 دریافت مظنه» — before
    /// this, an operator's help described a button that was not on their own keyboard.
    /// </summary>
    [Fact]
    public void TheAdminMainHelpNamesTheAnnounceQuoteButton()
    {
        Assert.True(Mentions(BotMsgs.MsgAdminMainHelp, BotBtns.BtnSpotSubmitPrice),
            $"admin help must name the announce-quote button ({BotBtns.BtnSpotSubmitPrice})");
    }

    [Fact]
    public void TheAdminMainHelpNamesTheAccountingButton()
    {
        Assert.True(Mentions(BotMsgs.MsgAdminMainHelp, BotBtns.BtnAccounting),
            $"admin help must name the accounting button ({BotBtns.BtnAccounting})");
    }

    /// <summary>The customer's quote button is not on the operator's keyboard at all.</summary>
    [Fact]
    public void TheAdminMainHelpDoesNotNameTheCustomerQuoteButton()
    {
        Assert.False(Mentions(BotMsgs.MsgAdminMainHelp, BotBtns.BtnSpotMarket),
            $"admin help must not advertise the customer's quote button ({BotBtns.BtnSpotMarket}), which is not on the operator's menu");
    }

    // ── the operator help, same rule ────────────────────────────────────────────

    /// <summary>
    /// Each operator command is a single letter followed by a space, so the help is the only
    /// place an operator can learn they exist. All eight must appear.
    /// </summary>
    [Theory]
    [InlineData("ت [")]   // approve
    [InlineData("ر [")]   // reject
    [InlineData("ن [")]   // change role
    [InlineData("ش [")]   // credit
    [InlineData("د [")]   // debit
    [InlineData("م [")]   // balances
    [InlineData("س [")]   // open orders
    [InlineData("ک [")]   // list users
    [InlineData("اسپرد [")]     // auto-quote spread
    [InlineData("اتومات روشن")] // auto-quote on
    [InlineData("اتومات خاموش")] // auto-quote off
    public void TheAdminHelpNamesEveryOperatorCommand(string command)
    {
        Assert.True(Mentions(BotMsgs.MsgAdminHelp, command),
            $"admin help must document the «{command.Trim(' ', '[')}» command");
    }
}
