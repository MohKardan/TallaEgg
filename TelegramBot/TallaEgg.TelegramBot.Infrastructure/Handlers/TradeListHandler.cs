using TallaEgg.Core;
using System.Text;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Core.Utilties;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using Utils = TallaEgg.TelegramBot.Core.Utilties.Utils;

namespace TallaEgg.TelegramBot.Infrastructure.Handlers
{
    public static class TradeListHandler
    {
        public static InlineKeyboardMarkup? BuildPagingKeyboard(PagedResult<TradeHistoryDto> page, int currentPage, Guid userId)
        {
            var navButtons = new List<InlineKeyboardButton>();
            if (currentPage > 1)
                navButtons.Add(InlineKeyboardButton.WithCallbackData("⬅️ قبلی", $"trades_{userId}_{currentPage - 1}"));
            if (currentPage < page.TotalPages)
                navButtons.Add(InlineKeyboardButton.WithCallbackData("بعدی ➡️", $"trades_{userId}_{currentPage + 1}"));

            return navButtons.Any() ? new InlineKeyboardMarkup(navButtons) : null;
        }

        /// <summary>
        /// The user's trade list.
        /// </summary>
        /// <param name="viewerUserId">
        /// The user viewing the list. Required because buy-or-sell is not a property of the trade
        /// itself but of which side the viewer was on: one trade is a buy to one party and a sell to
        /// the other.
        /// </param>
        /// <param name="counterpartyPhones">
        /// Phone number per counterparty user id, for the rows where it should be shown.
        ///
        /// Only populated for the admin. The admin is one side of every trade, so without the
        /// other party named their history is a list of identical-looking rows and they cannot
        /// tell which customer any of them was with. A customer needs no such row: every trade
        /// they have is with the shop, so naming it adds nothing and would expose a number
        /// they have no reason to hold.
        ///
        /// Passed in rather than looked up here so this stays a pure builder that a test can
        /// call without a users service (issue #65).
        /// </param>
        public static async Task<string> BuildTradesListAsync(
            PagedResult<TradeHistoryDto> page, int currentPage, Guid viewerUserId,
            IReadOnlyDictionary<Guid, string>? counterpartyPhones = null)
        {
            if (page == null || !page.Items.Any())
            {
                return "هیچ معامله‌ای انجام نشده است.";
            }

            // Plain text: the previous version mixed HTML tags with Markdown markers and neither
            // rendered correctly.
            var sb = new StringBuilder();
            sb.AppendLine($"📊 معاملات شما — صفحهٔ {PersianFormat.Number(currentPage)} از {PersianFormat.Number(page.TotalPages)}");
            sb.AppendLine();

            foreach (var t in page.Items)
            {
                var baseAsset = t.Symbol.Split('/')[0];
                var unit = PersianFormat.Unit(baseAsset);
                var isGold = t.Symbol == CurrenciesConstant.MAUA_IRT;
                var displayPrice = isGold ? t.Price * CurrenciesConstant.GramsPerMesghal : t.Price;
                var priceLabel = isGold ? "قیمت هر مثقال" : "قیمت هر واحد";

                var isBuyer = t.BuyerUserId == viewerUserId;

                // Two independent signals for one fact: an explicit label at the top, and the
                // direction of the money at the bottom. Colour alone is not enough — a user who
                // cannot distinguish the colours, or who is skimming, has to be able to read it.
                var sideLabel = isBuyer ? "🟢 خرید" : "🔴 فروش";

                // A bare "total value" did not say whether the money left the user's account or
                // arrived in it. To a buyer the amount is paid; to a seller it is received.
                var amountLabel = isBuyer ? "پرداختی" : "دریافتی";

                sb.AppendLine($"📌 معاملهٔ {PersianFormat.Ltr(t.Id.ToString()[..8])}");
                sb.AppendLine(sideLabel);

                // Who the trade was with. The counterparty is whichever side the viewer is
                // not — derived from isBuyer rather than assumed, so it stays correct when
                // the viewer is on either side.
                var counterpartyId = isBuyer ? t.SellerUserId : t.BuyerUserId;
                if (counterpartyPhones is not null &&
                    counterpartyPhones.TryGetValue(counterpartyId, out var phone) &&
                    !string.IsNullOrWhiteSpace(phone))
                {
                    sb.AppendLine($"👤 طرف معامله: {PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone))}");
                }

                sb.AppendLine($"🏷️ دارایی: {PersianFormat.Symbol(t.Symbol)}");
                sb.AppendLine($"📊 مقدار: {PersianFormat.Amount(t.Quantity, baseAsset)} {unit}");
                sb.AppendLine($"💰 {priceLabel}: {PersianFormat.Number(displayPrice)} تومان");
                sb.AppendLine($"💵 {amountLabel}: {PersianFormat.Number(t.QuoteQuantity)} تومان");
                sb.AppendLine($"🕓 زمان: {PersianFormat.ToPersianDigits(TallaEgg.Core.Utilties.Utils.ConvertToPersianDate(t.CreatedAt))}");
                sb.AppendLine("➖➖➖➖➖➖➖➖➖");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}

