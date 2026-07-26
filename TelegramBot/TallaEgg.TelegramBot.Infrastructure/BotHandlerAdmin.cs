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
                    await _botClient.SendMessage(message.Chat.Id,
                        "❌ فرمت دستور نادرست است." +
                        "\nمثال 1 : ش 09121234567 50000 IRR" +
                        "\nمثال 2 : ش 09121234567 50000 XAUM");
                }

                var phone = match.Groups["phone"].Value;
                var amount = decimal.Parse(match.Groups["amount"].Value);
                var currency = match.Groups["currency"].Success
                    ? match.Groups["currency"].Value
                    : CurrenciesConstant.Maua; // مقدار پیش‌فرض

                if(CurrenciesConstant.IsValidCurrency(currency) == false)
                {
                    await _botClient.SendMessage(message.Chat.Id,
                        "❌ ارز وارد شده معتبر نیست." +
                        "\nارزهای معتبر: " + string.Join(", ", CurrenciesConstant.GetAllCodes()));
                    return true;
                }

                        var info = CurrenciesConstant.GetCurrencyInfo(currency);
                string response = $"📌 دستورافزایش موجودی ثبت شد:\n" +
                                  $"👤 کاربر: {phone}\n" +
                                  $"💰 مبلغ: {amount}\n" +
                                  $"💵 نوع شارژ: {info.PersianName}";

                await _botClient.SendMessage(message.Chat.Id, response);
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

                        await _botClient.SendMessage(
                           message.Chat.Id,
                           $"💰 *شارژ کیف‌پول با موفقیت انجام شد.*\n\n" +
                           $"💳 دارایی: `{info.PersianName}`\n" +
                           $"💵 مبلغ شارژ: `{amount.ToString($"F{info.DecimalPlaces}")}` {info.Unit}\n" +
                           $"🆔 تلفن: `{phone}`\n\n" +
                           $"💵 موجودی جدید: `{result.Data.BalanceAfter.ToString($"F{info.DecimalPlaces}")} {info.Unit}`\n\n" +
                           $"✅ موجودی جدید شما در کیف‌پول به‌روزرسانی شد.", parseMode: ParseMode.Html
                       );
                                        await _botClient.SendMessage(
                           userDto.TelegramId,
                           $"💰 *شارژ کیف‌پول با موفقیت انجام شد.*\n\n" +
                           $"💳 دارایی: `{info.PersianName}`\n" +
                           $"💵 مبلغ شارژ: `{amount.ToString($"F{info.DecimalPlaces}")}` {info.Unit}\n" +
                           $"🆔 تلفن: `{phone}` \n" +
                           $"💵 موجودی جدید: `{result.Data.BalanceAfter.ToString($"F{info.DecimalPlaces}")} {info.Unit}`\n\n" +
                           $"✅ موجودی جدید شما در کیف‌پول به‌روزرسانی شد.", parseMode: ParseMode.Html
                       );
                    }
                    else
                    {
                        await _botClient.SendMessage(message.Chat.Id, result.Message);

                    }
                }
                else
                {
                    await _botClient.SendMessage(message.Chat.Id, "شماره تلفن معتبر نیست");

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
                    await _botClient.SendMessage(message.Chat.Id,
                        "❌ فرمت دستور نادرست است." +
                        "\nمثال 1 : د 09121234567 50000 IRR" +
                        "\nمثال 2 : د 09121234567 10 XAUM" +
                        "\nمثال 3 : د 09121234567 3000 MAUA" +
                        "\nمثال 4 : د 09121234567 500 TAIR");
                }

                var phone = match.Groups["phone"].Value;
                var amount = decimal.Parse(match.Groups["amount"].Value);
                var currency = match.Groups["currency"].Success
                    ? match.Groups["currency"].Value
                    : "ریالی"; // مقدار پیش‌فرض

                string response = $"📌 دستور کسر از موجودی ثبت شد:\n" +
                                  $"👤 کاربر: {phone}\n" +
                                  $"💰 مبلغ: {amount}\n" +
                                  $"💵 نوع شارژ: {currency}";

                await _botClient.SendMessage(message.Chat.Id, response);
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


                        await _botClient.SendMessage(
                               message.Chat.Id,
                               $"💰 *کسر از کیف‌پول با موفقیت انجام شد.*\n\n" +
                               $"💳 دارایی: `{currency}`\n" +
                               $"💵 مبلغ کسر : `{amount:N0}` ریال\n" +
                               $"🆔 تلفن: `{phone}`\n\n" +
                               $"💵 موجودی جدید: `{result.Data.BalanceAfter}`\n\n" +
                               $"✅ موجودی جدید شما در کیف‌پول به‌روزرسانی شد.", parseMode: ParseMode.Html
                           );
                                            await _botClient.SendMessage(
                               userDto.TelegramId,
                               $"💰 *شارژ کیف‌پول با موفقیت انجام شد.*\n\n" +
                               $"💳 دارایی: `{currency}`\n" +
                               $"💵 مبلغ کسر: `{amount:N0}` ریال\n" +
                               $"🆔 تلفن: `{phone}`\n\n" +
                               $"💵 موجودی جدید: `{result.Data.BalanceAfter}`\n\n" +
                               $"✅ موجودی جدید شما در کیف‌پول به‌روزرسانی شد.", parseMode: ParseMode.Html
                           );


                    }
                    else
                    {
                        await _botClient.SendMessage(message.Chat.Id, result.Message);

                    }
                }
                else
                {
                    await _botClient.SendMessage(message.Chat.Id, "شماره تلفن معتبر نیست");

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

                    await _botClient.SendMessage(
                        chatId: chatId,
                        text: text,
                        parseMode: ParseMode.MarkdownV2,
                        replyMarkup: UserListHandler.BuildPagingKeyboard(page.Data!, 1, q)
                    );
                }
                else await _botClient.SendMessage(chatId, page.Message);
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
                    await _botClient.SendMessage(chatId, "شماره تلفن پیدا نشد");
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
                    await ShowActiveOrders(chatId, useId.Value);
                }
                else
                {
                    await _botClient.SendMessage(chatId, "شماره تلفن پیدا نشد");
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
            //        await _botClient.SendMessage(chatId,
            //            "✅ اجباری بودن کد دعوت فعال شد.\n" +
            //            "کاربران جدید باید کد دعوت داشته باشند.");
            //        return true;

            //    case "/admin_referral_off":
            //        _requireReferralCode = false;
            //        await _botClient.SendMessage(chatId,
            //            "❌ اجباری بودن کد دعوت غیرفعال شد.\n" +
            //            $"کاربران جدید با کد پیش‌فرض '{_defaultReferralCode}' ثبت‌نام خواهند شد.");
            //        return true;

            //    case "/admin_referral_status":
            //        var status = _requireReferralCode ? "فعال" : "غیرفعال";
            //        await _botClient.SendMessage(chatId,
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
            await _botClient.EditMessageText(
                chatId: originalMsg.Chat.Id,
                messageId: originalMsg.MessageId,
                text: $"{originalMsg.Text}\n\n✅ توسط ادمین {adminTgId} تأیید شد.",
                replyMarkup: null);

            // اطلاع‌رسانی به کاربر
            await _botClient.SendMessage(telegramUserId, "درخواست شما تأیید شد\n حالا میتوانید از خدمات ما استفاده کنید.");
            await _telegramLogger.Notif<Message>($"کاربر تایید شد \n userId : {telegramUserId} adminId : {adminTgId}", originalMsg);
        }

        private async Task RejectUser(long telegramUserId, long adminTgId, Message originalMsg)
        {
            await _usersApi.UpdateUserStatusAsync(telegramUserId, TallaEgg.Core.Enums.User.UserStatus.Rejected);

            await _botClient.EditMessageText(
                chatId: originalMsg.Chat.Id,
                messageId: originalMsg.MessageId,
                text: $"{originalMsg.Text}\n\n❌ توسط ادمین {adminTgId} رد شد.",
                replyMarkup: null);

            // اطلاع‌رسانی به کاربر
            await _botClient.SendMessage(telegramUserId, "درخواست شما رد شد.");
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
                //await _botClient.SendMessage(chatId, "⏳ در حال کنسل سفارشات قبلی...");
                await _botClient.SendMessage(chatId, "⏳ در حال پردازش...");

                var cancelResults = await CancelUserActiveOrdersAsync(userId);
                if (cancelResults.CancelledCount > 0)
                {
                    await _botClient.SendMessage(chatId,
                        $"✅ {cancelResults.CancelledCount} قیمت قبلی کنسل شد");
                }
                else if (cancelResults.HasError)
                {
                    await _botClient.SendMessage(chatId,
                        $"⚠️ خطا در کنسل سفارشات قبلی: {cancelResults.ErrorMessage}");
                }

                // Create buy order
                var buyOrder = new OrderDto
                {
                    Asset = defaultAsset,
                    Amount = defaultAmount,
                    Price = buyPrice / 4.3318m, // Convert to grams for MAUA
                    UserId = userId,
                    Side = OrderSide.Buy,
                    Type = OrderType.Limit,
                    TradingType = TradingType.Spot
                };

                var (buySuccess, buyMessage) = await _orderApi.SubmitOrderAsync(buyOrder);

                // Create sell order
                var sellOrder = new OrderDto
                {
                    Asset = defaultAsset,
                    Amount = defaultAmount,
                    Price = sellPrice / 4.3318m, // Convert to grams for MAUA
                    UserId = userId,
                    Side = OrderSide.Sell,
                    Type = OrderType.Limit,
                    TradingType = TradingType.Spot
                };

                var (sellSuccess, sellMessage) = await _orderApi.SubmitOrderAsync(sellOrder);

                // Send result message
                var resultMessage = $"📊 نتیجه ثبت قیمت جدید:\n\n" +
                                  $"🟢 قیمت خرید {buyPrice:N0}: {(buySuccess ? "✅ موفق" : "❌ ناموفق - " + buyMessage)}\n" +
                                  $"🔴 قیمت فروش {sellPrice:N0}: {(sellSuccess ? "✅ موفق" : "❌ ناموفق - " + sellMessage)}\n\n" +
                                  $"📋 جزئیات:\n" +
                                  $"• نماد: طلای آبشده\n" +
                                  $"• قیمت خرید: {buyPrice:N0} تومان (هر گرم: {buyOrder.Price:N0})\n" +
                                  $"• قیمت فروش: {sellPrice:N0} تومان (هر گرم: {sellOrder.Price:N0})";

                await _botClient.SendMessage(chatId, resultMessage);
            }
            catch (Exception ex)
            {
                await _botClient.SendMessage(chatId, $"❌ خطا در ثبت سفارشات: {ex.Message}");
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