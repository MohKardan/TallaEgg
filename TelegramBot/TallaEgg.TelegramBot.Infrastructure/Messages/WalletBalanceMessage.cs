using System.Text;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
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
/// about on the shop's side. It is also, with <paramref name="positions"/> below, where they
/// see whether that debt is buying them a winning position (issue #93 part B) — the same
/// screen answers "what do I have" and "what did it make or lose me", since a balance number
/// alone answers neither on its own.
/// </summary>
public static class WalletBalanceMessage
{
    public static string Build(IEnumerable<WalletDTO> wallets, PositionsResponseDto? positions = null)
    {
        var walletList = wallets as IReadOnlyCollection<WalletDTO> ?? wallets.ToList();
        var byAsset = walletList.ToDictionary(w => w.Asset, w => w, StringComparer.OrdinalIgnoreCase);
        var positionsBySymbol = positions?.Positions.ToDictionary(p => p.Symbol, p => p, StringComparer.OrdinalIgnoreCase)
            ?? new Dictionary<string, PositionDto>(StringComparer.OrdinalIgnoreCase);

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
            var symbol = CurrenciesConstant.AllTradingPairs
                .FirstOrDefault(p => string.Equals(p.BaseAsset, asset, StringComparison.OrdinalIgnoreCase))?.Symbol;
            var position = symbol is not null ? positionsBySymbol.GetValueOrDefault(symbol) : null;

            AppendSection(sb, wallet, creditWallet, position);
        }

        if (byAsset.TryGetValue(CurrenciesConstant.Toman, out var toman))
            AppendSection(sb, toman, creditWallet: null, position: null);

        if (positions is not null)
            sb.Append(FormatTotalPnl(positions.TotalPnl));

        sb.Append(BotMsgs.MsgBalanceFooter);
        return sb.ToString();
    }

    private static void AppendSection(StringBuilder sb, WalletDTO wallet, WalletDTO? creditWallet, PositionDto? position)
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

        AppendPositionLines(sb, position, code);

        sb.AppendLine();
    }

    /// <summary>
    /// Nothing here if the customer has never traded this symbol at all (no <see cref="PositionDto"/>)
    /// and has no closed history in it either — a symbol they only hold via a direct credit
    /// grant, say, has nothing to report yet.
    /// </summary>
    private static void AppendPositionLines(StringBuilder sb, PositionDto? position, string baseAssetCode)
    {
        if (position is null)
            return;

        var isGold = string.Equals(baseAssetCode, CurrenciesConstant.Maua, StringComparison.OrdinalIgnoreCase);

        if (position.Quantity != 0)
        {
            if (position.AverageCost.HasValue)
                sb.Append(string.Format(BotMsgs.MsgBalanceAverageCost, FormatTomanPerUnit(position.AverageCost.Value, isGold)));

            if (position.MarkPrice.HasValue)
            {
                var currentValue = position.Quantity * position.MarkPrice.Value;
                sb.Append(string.Format(BotMsgs.MsgBalanceCurrentValue, FormatToman(currentValue)));
            }

            if (position.UnrealizedPnl.HasValue)
                sb.Append(FormatPnlLine(position.UnrealizedPnl.Value, BotMsgs.MsgBalanceUnrealizedGain, BotMsgs.MsgBalanceUnrealizedLoss));
            else
                sb.Append(BotMsgs.MsgBalanceNoQuoteForUnrealized);
        }

        if (position.RealizedPnl != 0)
            sb.Append(FormatPnlLine(position.RealizedPnl, BotMsgs.MsgBalanceRealizedGain, BotMsgs.MsgBalanceRealizedLoss));
    }

    /// <summary>A gain and a loss never share a label — a bare signed number reads as an error the same way a raw negative balance does.</summary>
    private static string FormatPnlLine(decimal pnl, string gainTemplate, string lossTemplate) =>
        pnl >= 0
            ? string.Format(gainTemplate, FormatToman(pnl))
            : string.Format(lossTemplate, FormatToman(-pnl));

    private static string FormatTotalPnl(decimal totalPnl) =>
        totalPnl switch
        {
            0m => BotMsgs.MsgBalanceTotalPnlNone,
            > 0m => string.Format(BotMsgs.MsgBalanceTotalPnlGain, FormatToman(totalPnl)),
            _ => string.Format(BotMsgs.MsgBalanceTotalPnlLoss, FormatToman(-totalPnl))
        };

    private static string FormatToman(decimal amount) =>
        $"{PersianFormat.Amount(amount, CurrenciesConstant.Toman)} {PersianFormat.Unit(CurrenciesConstant.Toman)}";

    /// <summary>
    /// Gold is quoted internally per gram but shown per mesghal everywhere else a price
    /// appears in the bot (see <c>BestPricesMessage</c>, order confirmations, trade/quote
    /// history) — a per-unit price here must follow the same convention or it reads as a
    /// different, much lower price than the one the customer actually agreed to.
    /// </summary>
    private static string FormatTomanPerUnit(decimal pricePerUnit, bool isGold) =>
        FormatToman(isGold ? pricePerUnit * CurrenciesConstant.GramsPerMesghal : pricePerUnit);

    private static bool IsToman(string asset) =>
        string.Equals(asset, CurrenciesConstant.Toman, StringComparison.OrdinalIgnoreCase);

    private static bool IsCredit(string asset) =>
        asset.StartsWith("CREDIT_", StringComparison.OrdinalIgnoreCase);

    private static string BaseAssetOfCredit(string creditAsset) => creditAsset["CREDIT_".Length..];
}
