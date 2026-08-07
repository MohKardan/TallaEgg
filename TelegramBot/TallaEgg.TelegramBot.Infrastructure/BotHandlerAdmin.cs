using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Enums.User;
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


            if (msgText.StartsWith("ن "))
            {
                await HandleChangeRoleCommandAsync(chatId, msgText, user);
                return true;
            }

            if (msgText.StartsWith("ت "))
            {
                await HandleSetUserStatusCommandAsync(chatId, msgText, "ت", UserStatus.Approved);
                return true;
            }

            if (msgText.StartsWith("ر "))
            {
                await HandleSetUserStatusCommandAsync(chatId, msgText, "ر", UserStatus.Rejected);
                return true;
            }

            if (msgText.StartsWith("اسپرد "))
            {
                await HandleAutoQuoteSpreadCommandAsync(chatId, msgText, user);
                return true;
            }

            if (msgText.StartsWith("اتومات "))
            {
                await HandleAutoQuoteToggleCommandAsync(chatId, msgText, user);
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
        /// <c>ن [شمارهٔ تلفن] [نقش]</c> — changes a user's role.
        ///
        /// <para>
        /// The Users service has had <c>POST /api/user/update-role</c> from the start and nothing
        /// ever called it, so in practice a role could only be changed with a hand-written SQL
        /// UPDATE against the database. That is tolerable on a developer's machine and impossible
        /// on a server, and it is the reason a freshly deployed instance has no operator.
        /// </para>
        ///
        /// <para>
        /// <b>The user id is echoed back on success.</b> It is not decoration:
        /// <c>Matching:MarketMakerUserId</c> is a configuration value naming a specific row, and
        /// on a new database that row does not exist yet with any predictable id. Whoever is made
        /// the dealer has an id that was generated when they registered, and it has to be copied
        /// into the configuration by hand. Printing it here is the difference between one message
        /// and a database query.
        /// </para>
        /// </summary>
        private async Task HandleChangeRoleCommandAsync(long chatId, string msgText, UserDto actor)
        {
            // Same shape as the charge command: a phone number, then the argument.
            var match = Regex.Match(msgText.Trim(),
                @"^ن\s+(?<phone>\d{10,11})\s+(?<role>.+)$",
                RegexOptions.Compiled);

            if (!match.Success)
            {
                await _messenger.SendAsync(chatId,
                    string.Format(BotMsgs.MsgAdminRoleFormatError, UserRoleNames.AssignableList()));
                return;
            }

            var phone = match.Groups["phone"].Value;

            if (!UserRoleNames.TryParse(match.Groups["role"].Value, out var newRole))
            {
                await _messenger.SendAsync(chatId,
                    string.Format(BotMsgs.MsgAdminRoleUnknown,
                        match.Groups["role"].Value.Trim(),
                        UserRoleNames.AssignableList()));
                return;
            }

            var target = await _usersApi.GetUserAsync(phone);
            if (target is null)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAdminUserNotFound);
                return;
            }

            // Refusing self-change is what stops the only operator from locking everyone out of
            // the administrative side in one message. There is no undo from inside the bot: once
            // the last Admin becomes an ordinary user, the command that would restore them is no
            // longer reachable. The recovery path is OwnerTelegramIds, which needs a config edit
            // and a restart — worth avoiding for the sake of one refusal here.
            if (target.Id == actor.Id)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAdminRoleSelfChange);
                return;
            }

            if (target.Role == newRole)
            {
                await _messenger.SendAsync(chatId,
                    string.Format(BotMsgs.MsgAdminRoleUnchanged,
                        PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone)),
                        UserRoleNames.Display(newRole)));
                return;
            }

            var previousRole = target.Role;
            var (success, message) = await _usersApi.UpdateRoleAsync(target.Id, newRole);

            if (!success)
            {
                await _messenger.SendAsync(chatId, string.Format(BotMsgs.MsgAdminOperationFailed, message));
                return;
            }

            await _messenger.SendAsync(chatId,
                string.Format(BotMsgs.MsgAdminRoleChanged,
                    PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone)),
                    UserRoleNames.Display(previousRole),
                    UserRoleNames.Display(newRole),
                    PersianFormat.Ltr(target.Id.ToString())));

            // A privilege change is worth an audit line even though nothing reads it yet; when
            // someone asks later how an account became an operator, this is the only record.
            _logger.LogWarning("Role of {TargetUserId} ({Phone}) changed from {OldRole} to {NewRole} by {ActorUserId}.",
                target.Id, phone, previousRole, newRole, actor.Id);

            // Telling the person is not a courtesy: their menu changes on their next message and
            // an unexplained change of what the bot offers reads as a fault.
            if (target.TelegramId != 0)
            {
                await _messenger.SendAsync(target.TelegramId,
                    string.Format(BotMsgs.MsgUserRoleChanged, UserRoleNames.Display(newRole)));
            }
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

        /// <summary>
        /// <c>ت [شمارهٔ تلفن]</c> and <c>ر [شمارهٔ تلفن]</c> — approve or reject an account from
        /// inside the bot.
        ///
        /// <para>
        /// Until now the only way to approve anybody was an inline button delivered to the
        /// administrators of one hard-coded Telegram group (<c>Constants.GroupId</c>). That makes
        /// activation depend on a Telegram group rather than on the product: if the bot is not a
        /// member of that group — and a newly deployed bot is not — <c>GetChatAdministrators</c>
        /// throws, the exception is swallowed by the catch-all in <c>HandleMessageAsync</c>, and
        /// the account stays Pending with nobody informed. Every new account would be stuck, not
        /// only the first one.
        /// </para>
        ///
        /// <para>
        /// The button path still works and is untouched; this is a second route that depends on
        /// nothing outside the product.
        /// </para>
        /// </summary>
        private async Task HandleSetUserStatusCommandAsync(
            long chatId, string msgText, string prefix, UserStatus newStatus)
        {
            var match = Regex.Match(msgText.Trim(), $@"^{prefix}\s+(?<phone>\d{{10,11}})$", RegexOptions.Compiled);

            if (!match.Success)
            {
                await _messenger.SendAsync(chatId, string.Format(BotMsgs.MsgAdminStatusFormatError, prefix));
                return;
            }

            var phone = match.Groups["phone"].Value;
            var target = await _usersApi.GetUserAsync(phone);

            if (target is null)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAdminUserNotFound);
                return;
            }

            // The status endpoint is keyed on the Telegram id, so an account that has none
            // cannot be reached through it. The seeded administrator row is exactly that: it
            // exists to own the bootstrap invitation code and is not a person.
            if (target.TelegramId == 0)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAdminStatusNoTelegramAccount);
                return;
            }

            if (target.Status == newStatus)
            {
                await _messenger.SendAsync(chatId,
                    string.Format(BotMsgs.MsgAdminStatusUnchanged,
                        PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone)),
                        StatusName(newStatus)));
                return;
            }

            var result = await _usersApi.UpdateUserStatusAsync(target.TelegramId, newStatus);

            if (!result.Success)
            {
                await _messenger.SendAsync(chatId, string.Format(BotMsgs.MsgAdminOperationFailed, result.Message));
                return;
            }

            await _messenger.SendAsync(chatId,
                string.Format(BotMsgs.MsgAdminStatusChanged,
                    PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone)),
                    StatusName(newStatus)));

            await _messenger.SendAsync(target.TelegramId,
                newStatus == UserStatus.Approved ? BotMsgs.MsgUserApproved : BotMsgs.MsgUserRejected);
        }

        /// <summary>Only the two statuses these commands can set need a name.</summary>
        private static string StatusName(UserStatus status) =>
            status == UserStatus.Approved ? "تایید‌شده" : "رد‌شده";

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
        /// <c>اسپرد [درصد]</c> — sets the spread the automatic quote publisher applies around
        /// the fetched gold price for MAUA/IRT (issue #90). Does not itself turn auto-quote on;
        /// see <see cref="HandleAutoQuoteToggleCommandAsync"/>.
        /// </summary>
        private async Task HandleAutoQuoteSpreadCommandAsync(long chatId, string msgText, UserDto actor)
        {
            var match = Regex.Match(msgText.Trim(), @"^اسپرد\s+(?<percent>[\d.]+)$", RegexOptions.Compiled);

            if (!match.Success || !decimal.TryParse(match.Groups["percent"].Value, out var spreadPercent))
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAutoQuoteSpreadFormatError);
                return;
            }

            var (success, message) = await _orderApi.UpdateAutoQuoteSpreadAsync(
                CurrenciesConstant.MAUA_IRT, spreadPercent, actor.Id);

            await _messenger.SendAsync(chatId, success
                ? string.Format(BotMsgs.MsgAutoQuoteSpreadUpdated, PersianFormat.Number(spreadPercent, decimals: 2))
                : string.Format(BotMsgs.MsgAutoQuoteSpreadFailed, message));
        }

        /// <summary>
        /// <c>اتومات روشن</c> / <c>اتومات خاموش</c> — turns automatic quote publishing on or
        /// off for MAUA/IRT (issue #90). A manually published quote (<c>buyPrice-sellPrice</c>)
        /// always overrides the automatic one regardless of this setting.
        /// </summary>
        private async Task HandleAutoQuoteToggleCommandAsync(long chatId, string msgText, UserDto actor)
        {
            var trimmed = msgText.Trim();
            bool? enable = trimmed switch
            {
                "اتومات روشن" => true,
                "اتومات خاموش" => false,
                _ => null
            };

            if (enable is null)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAutoQuoteToggleFormatError);
                return;
            }

            var (success, message) = await _orderApi.SetAutoQuoteEnabledAsync(
                CurrenciesConstant.MAUA_IRT, enable.Value, actor.Id);

            await _messenger.SendAsync(chatId, success
                ? (enable.Value ? BotMsgs.MsgAutoQuoteEnabled : BotMsgs.MsgAutoQuoteDisabled)
                : string.Format(BotMsgs.MsgAutoQuoteToggleFailed, message));
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