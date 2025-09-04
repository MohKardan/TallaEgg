using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
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
                    : "ریالی"; // مقدار پیش‌فرض

                string response = $"📌 دستورافزایش موجودی ثبت شد:\n" +
                                  $"👤 کاربر: {phone}\n" +
                                  $"💰 مبلغ: {amount}\n" +
                                  $"💵 نوع شارژ: {currency}";

                await _botClient.SendMessage(message.Chat.Id, response);
                var userDto = await _usersApi.GetUserAsync(phone);
                if (userDto != null)
                {
                    var result = await _walletApi.DepositeAsync(new TallaEgg.Core.Requests.Wallet.WalletRequest
                    {
                        Asset = currency,
                        Amount = amount,
                        UserId = userDto.Id
                    });
                    if (result.Success)
                    {


                        await _botClient.SendMessage(
           message.Chat.Id,
           $"💰 *شارژ کیف‌پول با موفقیت انجام شد.*\n\n" +
           $"💳 دارایی: `{currency}`\n" +
           $"💵 مبلغ شارژ: `{amount:N0}` ریال\n" +
           $"🆔 تلفن: `{phone}`\n\n" +
           $"💵 موجودی جدید: `{result.Data.BalanceAfter}`\n\n" +
           $"✅ موجودی جدید شما در کیف‌پول به‌روزرسانی شد.", parseMode: ParseMode.Html
       );
                        await _botClient.SendMessage(
           userDto.TelegramId,
           $"💰 *شارژ کیف‌پول با موفقیت انجام شد.*\n\n" +
           $"💳 دارایی: `{currency}`\n" +
           $"💵 مبلغ شارژ: `{amount:N0}` ریال\n" +
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
                    await ShowTradeHistory(chatId, useId.Value);
                }
                else
                {
                    await _botClient.SendMessage(chatId, "شماره تلفن پیدا نشد");
                }
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
        }
    }
}