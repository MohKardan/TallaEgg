using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Enums.Wallet;
using TallaEgg.Core.Requests.Order;
using TallaEgg.Core.Utilties;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Utilties;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Extensions.Telegram;
using TallaEgg.TelegramBot.Infrastructure.Handlers;
using TallaEgg.TelegramBot.Infrastructure.Messages;
using TallaEgg.TelegramBot.Infrastructure.Messaging;
using Telegram.Bot;
using Telegram.Bot.Requests.Abstractions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using static System.Net.Mime.MediaTypeNames;
using static TallaEgg.TelegramBot.Infrastructure.Clients.OrderApiClient;

namespace TallaEgg.TelegramBot
{
    public partial class BotHandler : IBotHandler
    {

        private async Task<bool> HandleAdminCommandsAsync(long chatId, long telegramId, Message message, UserDto user)
        {
            var msgText = message.Text ?? "";
            msgText = msgText.ToLower().Trim();
            if (msgText.StartsWith("ش"))
            {
                // ش 09121234567 50000 دلاری
                // ش 09121234567 50000
                var regex = new Regex(@"^ش\s+(?<phone>\d{10,11})\s+(?<amount>\d+)(\s+(?<currency>\S+))?$",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var match = regex.Match(msgText);
                if (!match.Success)
                {
                    // بازگشت لازم است؛ بدون آن ادامهٔ کد روی match ناموفق اجرا می‌شد و خطا می‌داد.
                    await _messenger.SendAsync(message.Chat.Id,
                        string.Format(BotMsgs.MsgAdminChargeFormatError, CurrenciesConstant.GetPersianNamesList()));
                    return true;
                }

                var phone = match.Groups["phone"].Value;
                var amount = decimal.Parse(match.Groups["amount"].Value);

                // ورودی می‌تواند نام فارسی («تومان») یا کد («IRT») باشد.
                var currencyInput = match.Groups["currency"].Success
                    ? match.Groups["currency"].Value
                    : CurrenciesConstant.Maua; // مقدار پیش‌فرض
                var currency = CurrenciesConstant.ResolveCurrencyCode(currencyInput);

                if (currency is null)
                {
                    await _messenger.SendAsync(message.Chat.Id,
                        string.Format(BotMsgs.MsgAdminInvalidCurrency, currencyInput, CurrenciesConstant.GetPersianNamesList()));
                    return true;
                }

                var info = CurrenciesConstant.GetCurrencyInfo(currency);
                var userDto = await _usersApi.GetUserAsync(phone);
                if (userDto != null)
                {
                    var result = await _walletApi.DepositeAsync(new TallaEgg.Core.Requests.Wallet.WalletRequest
                    {
                        Asset = "CREDIT_" + currency, //فعلا ادمین شارژ کنه اعتباری شارژ میشه
                        Amount = amount,
                        UserId = userDto.Id
                    });
                    if (result.Success)
                    {

                        // نکته: شارژ مدیر به کیف پول «اعتباری» واریز می‌شود (CREDIT_)، پس
                        // پیام‌ها «اعتبار» می‌گویند نه «موجودی». پیام قبلی این دو را
                        // اشتباه گرفته بود. همچنین قالب Markdown با ParseMode.Html ناسازگار
                        // بود و ستاره و بک‌تیک عیناً نمایش داده می‌شدند؛ حالا متن ساده است.
                        var amountText = $"{PersianFormat.Amount(amount, currency)} {info.Unit}";
                        var newCreditText = $"{PersianFormat.Amount(result.Data.BalanceAfter, currency)} {info.Unit}";

                        await _messenger.SendAsync(
                           message.Chat.Id,
                           string.Format(BotMsgs.MsgAdminChargeDone,
                               info.PersianName,
                               amountText,
                               PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone)),
                               newCreditText));

                        await _messenger.SendAsync(
                           userDto.TelegramId,
                           string.Format(BotMsgs.MsgUserCreditIncreased,
                               info.PersianName,
                               amountText,
                               newCreditText));
                    }
                    else
                    {
                        await _messenger.SendAsync(message.Chat.Id,
                            string.Format(BotMsgs.MsgAdminOperationFailed, result.Message));
                    }
                }
                else
                {
                    await _messenger.SendAsync(message.Chat.Id, BotMsgs.MsgAdminUserNotFound);
                }

                return true;

            }

            if (msgText.StartsWith("د"))
            {
                // ش 09121234567 50000 دلاری
                // ش 09121234567 50000
                var regex = new Regex(@"^د\s+(?<phone>\d{10,11})\s+(?<amount>\d+)(\s+(?<currency>\S+))?$",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var match = regex.Match(msgText);
                if (!match.Success)
                {
                    // بازگشت لازم است؛ بدون آن ادامهٔ کد روی match ناموفق اجرا می‌شد و خطا می‌داد.
                    await _messenger.SendAsync(message.Chat.Id,
                        string.Format(BotMsgs.MsgAdminDeductFormatError, CurrenciesConstant.GetPersianNamesList()));
                    return true;
                }

                var phone = match.Groups["phone"].Value;
                var amount = decimal.Parse(match.Groups["amount"].Value);

                // ورودی می‌تواند نام فارسی («تومان») یا کد («IRT») باشد.
                // پیش‌فرض قبلی رشتهٔ فارسی «ریالی» بود که هیچ‌وقت با کد دارایی تطبیق
                // نمی‌کرد و باعث می‌شد کسر روی کیف پول ناموجود انجام شود.
                var currencyInput = match.Groups["currency"].Success
                    ? match.Groups["currency"].Value
                    : CurrenciesConstant.Toman; // مقدار پیش‌فرض
                var currency = CurrenciesConstant.ResolveCurrencyCode(currencyInput);

                if (currency is null)
                {
                    await _messenger.SendAsync(message.Chat.Id,
                        string.Format(BotMsgs.MsgAdminInvalidCurrency, currencyInput, CurrenciesConstant.GetPersianNamesList()));
                    return true;
                }

                var info = CurrenciesConstant.GetCurrencyInfo(currency);
                var userDto = await _usersApi.GetUserAsync(phone);
                if (userDto != null)
                {
                    var result = await _walletApi.WithdrawalAsync(new TallaEgg.Core.Requests.Wallet.WalletRequest
                    {
                        Asset = currency,
                        Amount = amount,
                        UserId = userDto.Id
                    });
                    if (result.Success)
                    {


                        // پیام قبلیِ ارسالی به کاربر اشتباهاً «شارژ کیف‌پول» می‌گفت، در حالی
                        // که مبلغ کسر شده بود. همچنین واحد به‌صورت ثابت «ریال» نوشته شده بود
                        // بدون توجه به دارایی، و کد لاتین دارایی نمایش داده می‌شد.
                        var deductAmountText = $"{PersianFormat.Amount(amount, currency)} {info.Unit}";
                        var newBalanceText = $"{PersianFormat.Amount(result.Data.BalanceAfter, currency)} {info.Unit}";

                        await _messenger.SendAsync(
                               message.Chat.Id,
                               string.Format(BotMsgs.MsgAdminDeductDone,
                                   info.PersianName,
                                   deductAmountText,
                                   PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone)),
                                   newBalanceText));

                        await _messenger.SendAsync(
                               userDto.TelegramId,
                               string.Format(BotMsgs.MsgUserBalanceDeducted,
                                   info.PersianName,
                                   deductAmountText,
                                   newBalanceText));


                    }
                    else
                    {
                        await _messenger.SendAsync(message.Chat.Id,
                            string.Format(BotMsgs.MsgAdminOperationFailed, result.Message));
                    }
                }
                else
                {
                    await _messenger.SendAsync(message.Chat.Id, BotMsgs.MsgAdminUserNotFound);
                }

                return true;

            }






            if (msgText.StartsWith("ک"))
            {
                var msgSplit = msgText.Split(" ");
                string? q = null;
                if (msgSplit.Length > 1) q = msgSplit[1];
                var page = await _usersApi.GetUsersAsync(pageNumber: 1, pageSize: 5, q);
                if (page.Success)
                {
                    var text = await UserListHandler.BuildUsersListAsync(page.Data!, 1, q);

                    await _messenger.SendAsync(
                        chatId: chatId,
                        text: text,
                        parseMode: ParseMode.MarkdownV2,
                        replyMarkup: UserListHandler.BuildPagingKeyboard(page.Data!, 1, q)
                    );
                }
                else await _messenger.SendAsync(chatId, page.Message);
                return true;
            }
            if (msgText.StartsWith("م "))
            {
                var msgSplit = msgText.Split(" ");
                string phone = "";
                if (msgSplit.Length > 1) phone = msgSplit[1];
                var useId = await _usersApi.GetUserIdByPhoneNumberAsync(phone);
                if (useId.HasValue)
                {
                    await ShowWalletsBalance(chatId, useId.Value);
                }
                else
                {
                    await _messenger.SendAsync(chatId, "شماره تلفن پیدا نشد");
                }
                return true;
            }
            if (msgText.StartsWith("س "))
            {
                var msgSplit = msgText.Split(" ");
                string phone = "";
                if (msgSplit.Length > 1) phone = msgSplit[1];
                var useId = await _usersApi.GetUserIdByPhoneNumberAsync(phone);
                if (useId.HasValue)
                {
                    // Was "show this customer's active orders". In the dealer model an order
                    // exists only for the instant of a fill, so that list was always empty and
                    // the command answered nothing. Their completed trades are what the admin
                    // is actually looking for when they type a customer's number.
                    await ShowCustomerTradeHistoryAsync(chatId, useId.Value, phone);
                }
                else
                {
                    await _messenger.SendAsync(chatId, "شماره تلفن پیدا نشد");
                }
                return true;
            }


            //دستور ثبت قیمت جفتی برای ادمین    
            // Handle price pair format: buyPrice-sellPrice (e.g., 8523690-8529630)
            var pricePairRegex = new Regex(@"^(\d+)-(\d+)$", RegexOptions.Compiled);
            var pricePairMatch = pricePairRegex.Match(msgText);
            if (pricePairMatch.Success)
            {
                var buyPrice = decimal.Parse(pricePairMatch.Groups[1].Value);
                var sellPrice = decimal.Parse(pricePairMatch.Groups[2].Value);

                await HandlePricePairOrdersAsync(chatId, user.Id, buyPrice, sellPrice);
                return true;
            }

            return false;

            //switch (msgText.ToLower())
            //{
            //    case "/admin_referral_on":
            //        _requireReferralCode = true;
            //        await _messenger.SendAsync(chatId,
            //            "✅ اجباری بودن کد دعوت فعال شد.\n" +
            //            "کاربران جدید باید کد دعوت داشته باشند.");
            //        return true;

            //    case "/admin_referral_off":
            //        _requireReferralCode = false;
            //        await _messenger.SendAsync(chatId,
            //            "❌ اجباری بودن کد دعوت غیرفعال شد.\n" +
            //            $"کاربران جدید با کد پیش‌فرض '{_defaultReferralCode}' ثبت‌نام خواهند شد.");
            //        return true;

            //    case "/admin_referral_status":
            //        var status = _requireReferralCode ? "فعال" : "غیرفعال";
            //        await _messenger.SendAsync(chatId,
            //            $"📊 وضعیت فعلی:\n" +
            //            $"اجباری بودن کد دعوت: {status}\n" +
            //            $"کد پیش‌فرض: {_defaultReferralCode}\n\n" +
            //            $"دستورات مدیریتی:\n" +
            //            $"/admin_referral_on - فعال کردن اجباری بودن کد دعوت\n" +
            //            $"/admin_referral_off - غیرفعال کردن اجباری بودن کد دعوت\n" +
            //            $"/admin_referral_status - نمایش وضعیت فعلی");
            //        return true;

            //    default:
            //        return false; // Not an admin command, continue with normal processing
            //}
        }

        /// <summary>
        /// با این فقط چک میکنیم ببینیم تو گروه تلگرام ادمین هست یا نه
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        private async Task<bool> IsTelegramAdmin(UserDto user)
        {
            var ids = await _botClient.GetAdminUserIdsAsync(Constants.GroupId);
            return ids.Contains(user.TelegramId);
            //  Check if user has admin status or is a known admin Telegram ID
            // var adminTelegramIds = new[] { 123456789L }; // Add actual admin Telegram IDs here
            //return user.Status?.ToLower().Contains("admin") == true ||
            //       user.Status?.ToLower().Contains("root") == true ||
            //       adminTelegramIds.Contains(user.TelegramId);

            return false;
        }

        private async Task ApproveUser(long telegramUserId, long adminTgId, Message originalMsg)
        {
            await _usersApi.UpdateUserStatusAsync(telegramUserId, TallaEgg.Core.Enums.User.UserStatus.Approved);

            // ویرایش پیام ادمین
            await _messenger.EditTextAsync(
                chatId: originalMsg.Chat.Id,
                messageId: originalMsg.MessageId,
                text: originalMsg.Text + BotMsgs.MsgAdminApprovedSuffix,
                replyMarkup: null);

            // اطلاع‌رسانی به کاربر
            await _messenger.SendAsync(telegramUserId, BotMsgs.MsgUserApproved);
            await _telegramLogger.Notif<Message>($"کاربر تایید شد \n userId : {telegramUserId} adminId : {adminTgId}", originalMsg);
        }

        private async Task RejectUser(long telegramUserId, long adminTgId, Message originalMsg)
        {
            await _usersApi.UpdateUserStatusAsync(telegramUserId, TallaEgg.Core.Enums.User.UserStatus.Rejected);

            await _messenger.EditTextAsync(
                chatId: originalMsg.Chat.Id,
                messageId: originalMsg.MessageId,
                text: originalMsg.Text + BotMsgs.MsgAdminRejectedSuffix,
                replyMarkup: null);

            // اطلاع‌رسانی به کاربر
            await _messenger.SendAsync(telegramUserId, BotMsgs.MsgUserRejected);
            await _telegramLogger.Notif<Message>($"کاربر رد شد \n userId : {telegramUserId} adminId : {adminTgId}", originalMsg);

        }

        /// <summary>
        /// پردازش سفارشات جفت قیمت برای ادمین
        /// </summary>
        /// <param name="chatId">شناسه چت تلگرام برای ارسال پیام</param>
        /// <param name="userId">شناسه کاربر در سیستم</param>
        /// <param name="buyPrice">قیمت خرید وارد شده توسط ادمین</param>
        /// <param name="sellPrice">قیمت فروش وارد شده توسط ادمین</param>
        /// <returns>Task که عملیات async را نشان می‌دهد</returns>
        /// <remarks>
        /// این تابع:
        /// 1. ابتدا تمام سفارشات فعال کاربر را کنسل می‌کند
        /// 2. قیمت‌های ورودی را برای طلا (تقسیم بر 4.3318) تنظیم می‌کند
        /// 3. یک سفارش خرید با قیمت پایین‌تر و 1000 واحد پیش‌فرض ایجاد می‌کند
        /// 4. یک سفارش فروش با قیمت بالاتر و 1000 واحد پیش‌فرض ایجاد می‌کند
        /// 5. نتیجه عملیات را به ادمین گزارش می‌دهد
        /// </remarks>
        private async Task HandlePricePairOrdersAsync(long chatId, Guid userId, decimal buyPrice, decimal sellPrice)
        {
            try
            {
                const string defaultAsset = CurrenciesConstant.MAUA_IRT; // Default asset for admin price pair orders
                const decimal defaultAmount = 1000m;    // Default amount

                // First, cancel all existing active orders for this user
                //await _messenger.SendAsync(chatId, "⏳ در حال کنسل سفارشات قبلی...");
                await _messenger.SendAsync(chatId, BotMsgs.MsgAdminProcessing);

                var cancelResults = await CancelUserActiveOrdersAsync(userId);
                if (cancelResults.CancelledCount > 0)
                {
                    await _messenger.SendAsync(chatId,
                        string.Format(BotMsgs.MsgAdminPreviousPricesCancelled,
                            PersianFormat.Number(cancelResults.CancelledCount)));
                }
                else if (cancelResults.HasError)
                {
                    await _messenger.SendAsync(chatId,
                        string.Format(BotMsgs.MsgAdminCancelPreviousFailed, cancelResults.ErrorMessage));
                }

                // A quote is published — no more pair of 1000-gram orders (issue #48).
                //
                // The 1000 was arbitrary and locked roughly 19 billion toman and 1000 grams
                // of the admin's collateral purely to announce a price. Publishing a quote
                // locks nothing; orders are created only when a customer trades, for exactly
                // the requested quantity, and are consumed in the same moment.
                //
                // Conversion and confirmation text come from one call, so the price
                // published and the price shown cannot drift apart (issue #65).
                var quote = QuoteMessage.Prepare(defaultAsset, buyPrice, sellPrice);

                var (published, publishMessage) = await _orderApi.PublishQuoteAsync(
                    defaultAsset, quote.BuyPricePerGram, quote.SellPricePerGram, userId);

                if (published)
                {
                    await _messenger.SendAsync(chatId, quote.Text);
                }
                else
                {
                    await _messenger.SendAsync(chatId,
                        string.Format(BotMsgs.MsgAdminQuoteFailed, publishMessage));
                }
            }
            catch (Exception ex)
            {
                await _messenger.SendAsync(chatId,
                    string.Format(BotMsgs.MsgAdminPriceSubmitError, ex.Message));
            }
        }

        /// <summary>
        /// کنسل کردن تمام سفارشات فعال یک کاربر
        /// </summary>
        /// <param name="userId">شناسه کاربر که سفارشاتش باید کنسل شوند</param>
        /// <returns>نتیجه عملیات کنسل شامل تعداد سفارشات کنسل شده و وضعیت خطا</returns>
        /// <remarks>
        /// این تابع:
        /// 1. از API endpoint مخصوص کنسل سفارشات فعال استفاده می‌کند
        /// 2. دلیل کنسل را "کنسل شده توسط ادمین برای ثبت سفارش جدید" ثبت می‌کند
        /// 3. تعداد سفارشات کنسل شده و وضعیت موفقیت/خطا را برمی‌گرداند
        /// </remarks>
        private async Task<CancelOrdersResult> CancelUserActiveOrdersAsync(Guid userId)
        {
            try
            {
                // Use the new API endpoint to cancel all active orders for the user
                var (success, message, cancelledCount) = await _orderApi.CancelAllUserActiveOrdersAsync(userId, "کنسل شده توسط ادمین برای ثبت سفارش جدید");

                return new CancelOrdersResult
                {
                    CancelledCount = cancelledCount,
                    HasError = !success,
                    ErrorMessage = success ? null : message
                };
            }
            catch (Exception ex)
            {
                return new CancelOrdersResult
                {
                    HasError = true,
                    ErrorMessage = ex.Message
                };
            }
        }
    }

    public class CancelOrdersResult
    {
        public int CancelledCount { get; set; }
        public bool HasError { get; set; }
        public string? ErrorMessage { get; set; }
    }
}