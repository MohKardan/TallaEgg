using System.Text;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Utilties;

namespace TallaEgg.TelegramBot.Infrastructure.Messages;

/// <summary>
/// Builds the customer's balance screen (issue #65).
///
/// Extracted because it is the only message in the bot that has a branch in it: a negative
/// available balance is normal under the credit model — the customer traded on credit — but
/// a bare minus sign reads as an error, so the debt is shown as a positive number under an
/// explicit label. Getting the sign flip wrong shows a customer who owes 5,000,000 that
/// they owe -5,000,000, or that they owe nothing at all.
///
/// This is also the only place a customer can see their debt, which is the gap issue #61 is
/// about on the shop's side.
/// </summary>
public static class WalletBalanceMessage
{
    public static string Build(IEnumerable<WalletDTO> wallets)
    {
        var sb = new StringBuilder();
        sb.Append(BotMsgs.MsgBalanceHeader);

        foreach (var wallet in wallets)
        {
            var code = wallet.Asset;
            var unit = PersianFormat.Unit(code);

            // The Persian asset name, never the latin code: the customer chose "طلای آبشده",
            // not "MAUA".
            sb.Append(string.Format(BotMsgs.MsgBalanceRow,
                PersianFormat.Asset(code),
                $"{PersianFormat.Amount(wallet.Balance, code)} {unit}",
                $"{PersianFormat.Amount(wallet.LockedBalance, code)} {unit}"));

            if (wallet.Balance < 0)
            {
                // Negated so the customer reads "you owe 5,000,000", not "-5,000,000".
                sb.Append(string.Format(BotMsgs.MsgBalanceDebtNote,
                    $"{PersianFormat.Amount(-wallet.Balance, code)} {unit}"));
            }

            sb.AppendLine();
        }

        sb.Append(BotMsgs.MsgBalanceFooter);
        return sb.ToString();
    }
}
