using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Extensions.Telegram;
using TallaEgg.TelegramBot.Infrastructure.Messaging;
using Telegram.Bot.Types.ReplyMarkups;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Who sees which accounting buttons.
///
/// Quote history is the record of the prices the shop set, and for the admin it also shows the
/// margin on each one. That is the shop's business, not the customer's, so the button belongs
/// only on the admin keyboard.
///
/// <para>
/// This test exists because the removal was made once and silently did not apply: the edit was
/// a text substitution whose pattern no longer matched after an earlier change to the same
/// method, and nothing failed — not the build, not the tests. The customer kept seeing the
/// button until someone noticed in Telegram. A keyboard is behaviour; it should be asserted
/// like any other.
/// </para>
/// </summary>
public class AccountingMenuTests
{
    private const long ChatId = 12345;

    private static async Task<IReadOnlyList<string>> ButtonsOfAsync(Func<IBotMessenger, Task> sendMenu)
    {
        var messenger = new FakeBotMessenger();
        await sendMenu(messenger);

        var markup = Assert.IsType<ReplyKeyboardMarkup>(messenger.Sent.Single().ReplyMarkup);

        return markup.Keyboard.SelectMany(row => row).Select(b => b.Text).ToList();
    }

    [Fact]
    public async Task TheCustomerMenuDoesNotOfferQuoteHistory()
    {
        var buttons = await ButtonsOfAsync(m => m.SendAccountingMenuKeyboard(ChatId));

        Assert.DoesNotContain(BotBtns.BtnQuoteHistory, buttons);
    }

    [Fact]
    public async Task TheAdminMenuDoesOfferQuoteHistory()
    {
        var buttons = await ButtonsOfAsync(m => m.SendAccountingMenuKeyboardForAdmin(ChatId));

        Assert.Contains(BotBtns.BtnQuoteHistory, buttons);
    }

    /// <summary>Both audiences keep their own trade history and a way back.</summary>
    [Fact]
    public async Task BothMenusOfferTradeHistoryAndAWayBack()
    {
        foreach (var buttons in new[]
                 {
                     await ButtonsOfAsync(m => m.SendAccountingMenuKeyboard(ChatId)),
                     await ButtonsOfAsync(m => m.SendAccountingMenuKeyboardForAdmin(ChatId))
                 })
        {
            Assert.Contains(BotBtns.BtnTradeHistory, buttons);
            Assert.Contains(BotBtns.BtnMainMenu, buttons);
        }
    }

    /// <summary>
    /// The removed active-orders button must not come back. In the dealer model an order lives
    /// only for the instant of a fill, so the list was always empty.
    /// </summary>
    [Fact]
    public async Task NeitherMenuOffersActiveOrders()
    {
        foreach (var buttons in new[]
                 {
                     await ButtonsOfAsync(m => m.SendAccountingMenuKeyboard(ChatId)),
                     await ButtonsOfAsync(m => m.SendAccountingMenuKeyboardForAdmin(ChatId))
                 })
        {
            Assert.DoesNotContain(buttons, b => b.Contains("سفارشات فعال"));
        }
    }
}
