using System.Text;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Utilties;

namespace TallaEgg.TelegramBot.Infrastructure.Messages;

/// <summary>
/// Builds the customer's balance screen (issue #65), grouped per symbol rather than as a
/// flat wallet list (issue #93 part A) — a customer asking "what's my wallet" means what
/// they hold of each tradable asset, with that asset's own credit ceiling alongside it, not
/// an undifferentiated row per underlying wallet record (which would show "اعتبار آبشده" as
/// its own unrelated-looking section instead of as a line under "آبشده").
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
        var walletList = wallets as IReadOnlyCollection<WalletDTO> ?? wallets.ToList();
        var byAsset = walletList.ToDictionary(w => w.Asset, w => w, StringComparer.OrdinalIgnoreCase);

        var sb = new StringBuilder();
        sb.Append(BotMsgs.MsgBalanceHeader);

        // Every base asset the customer has any footprint in — either they hold or have
        // traded it directly, or an admin extended credit against it even if they never
        // did. A credit-only customer must still see that credit; dropping it here would
        // hide real spending power. Symbols first, Toman (cash) last: "my wallet" means
        // what they hold of each tradable asset, not their spending cash.
        var baseAssets = walletList
            .Select(w => w.Asset)
            .Where(a => !IsToman(a) && !IsCredit(a))
            .Concat(walletList.Select(w => w.Asset).Where(IsCredit).Select(BaseAssetOfCredit))
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var asset in baseAssets)
        {
            var wallet = byAsset.GetValueOrDefault(asset) ?? new WalletDTO { Asset = asset };
            var creditWallet = byAsset.GetValueOrDefault(CurrenciesConstant.CreditAssetFor(asset));
            AppendSection(sb, wallet, creditWallet);
        }

        if (byAsset.TryGetValue(CurrenciesConstant.Toman, out var toman))
            AppendSection(sb, toman, creditWallet: null);

        sb.Append(BotMsgs.MsgBalanceFooter);
        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, WalletDTO wallet, WalletDTO? creditWallet)
    {
        var code = wallet.Asset;
        var unit = PersianFormat.Unit(code);

        // The Persian asset name, never the latin code: the customer chose "طلای آبشده",
        // not "MAUA".
        sb.Append(string.Format(BotMsgs.MsgBalanceRow,
            PersianFormat.Asset(code),
            $"{PersianFormat.Amount(wallet.Balance, code)} {unit}",
            $"{PersianFormat.Amount(wallet.LockedBalance, code)} {unit}"));

        if (creditWallet is { Balance: > 0 })
            sb.Append(string.Format(BotMsgs.MsgBalanceCreditLine,
                $"{PersianFormat.Amount(creditWallet.Balance, code)} {unit}"));

        if (wallet.Balance < 0)
        {
            // Negated so the customer reads "you owe 5,000,000", not "-5,000,000".
            sb.Append(string.Format(BotMsgs.MsgBalanceDebtNote,
                $"{PersianFormat.Amount(-wallet.Balance, code)} {unit}"));
        }

        sb.AppendLine();
    }

    private static bool IsToman(string asset) =>
        string.Equals(asset, CurrenciesConstant.Toman, StringComparison.OrdinalIgnoreCase);

    private static bool IsCredit(string asset) =>
        asset.StartsWith("CREDIT_", StringComparison.OrdinalIgnoreCase);

    private static string BaseAssetOfCredit(string creditAsset) => creditAsset["CREDIT_".Length..];
}
