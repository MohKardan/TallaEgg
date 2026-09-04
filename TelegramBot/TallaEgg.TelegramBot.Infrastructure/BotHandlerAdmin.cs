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

namespace TallaEgg.TelegramBot.Infrastructure
{
    public partial class BotHandler : IBotHandler
    {

        /// <summary>
        /// Whether the asset an admin named can carry credit, answering them specifically when it
        /// cannot. Shared by ش and د, which both act on the credit ledger and therefore both reject
        /// exactly the same input.
        ///
        /// <para>
        /// Three refusals, three different sentences, because they send the admin to three different
        /// places: an unrecognised word is a typo; a credit name ("اعتبار طلا") is a word the
        /// commands add for themselves; and Toman is a perfectly good asset that simply has no credit
        /// ledger, which no amount of re-spelling will fix. All three used to be "نوع شناسایی نشد",
        /// or in Toman's case not caught here at all — it reached the wallet and came back as the
        /// generic "خطا در بروزرسانی".
        /// </para>
        /// </summary>
        private async Task<string?> ResolveCreditableAssetAsync(long chatId, string typed)
        {
            var resolved = CurrenciesConstant.ResolveCurrencyCode(typed);

            if (resolved is null)
            {
                await _messenger.SendAsync(chatId,
                    string.Format(BotMsgs.MsgAdminInvalidCurrency, typed, CurrenciesConstant.GetCreditableNamesList()));
                return null;
            }

            if (CurrenciesConstant.IsCreditAsset(resolved))
            {
                await _messenger.SendAsync(chatId, string.Format(BotMsgs.MsgAdminCreditNameNotNeeded, typed));
                return null;
            }

            if (!CurrenciesConstant.HasCreditLedger(resolved))
            {
                await _messenger.SendAsync(chatId,
                    string.Format(BotMsgs.MsgAdminAssetHasNoCredit,
                        PersianFormat.Asset(resolved), CurrenciesConstant.GetCreditableNamesList()));
                return null;
            }

            return resolved;
        }

        private async Task<bool> HandleAdminCommandsAsync(long chatId, long telegramId, Message message, UserDto user)
        {
            var msgText = message.Text ?? "";
            msgText = msgText.ToLower().Trim();
            if (msgText.StartsWith("ش"))
            {
                // Accepted forms of the top-up command, with the currency given as a code, omitted,
                // or written as a multi-word Persian name.
                var regex = new Regex(@"^ش\s+(?<phone>\d{10,11})\s+(?<amount>\d+)(\s+(?<currency>.+?))?\s*$",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var match = regex.Match(msgText);
                if (!match.Success)
                {
                    // The return is required; without it the code below ran on a failed match and threw.
                    await _messenger.SendAsync(message.Chat.Id,
                        string.Format(BotMsgs.MsgAdminChargeFormatError, CurrenciesConstant.GetCreditableNamesList()));
                    return true;
                }

                var phone = match.Groups["phone"].Value;
                var amount = decimal.Parse(match.Groups["amount"].Value);

                // The input may be a Persian name or a currency code.
                var currencyInput = match.Groups["currency"].Success
                    ? match.Groups["currency"].Value
                    : CurrenciesConstant.Maua; // مقدار پیش‌فرض
                var currency = await ResolveCreditableAssetAsync(message.Chat.Id, currencyInput);
                if (currency is null) return true;

                var userDto = await _usersApi.GetUserAsync(phone);
                if (userDto != null)
                {
                    var creditAsset = CurrenciesConstant.CreditAssetFor(currency);

                    var result = await _walletApi.DepositeAsync(new TallaEgg.Core.Requests.Wallet.WalletRequest
                    {
                        Asset = creditAsset, // شارژ ادمین همیشه اعتباری‌ست
                        Amount = amount,
                        UserId = userDto.Id,

                        // Without this the column was always NULL in production and the same charge
                        // sent twice credited twice (issue #157). The realistic duplicate is an admin
                        // re-sending after a reply that never arrived, so the key comes from what they
                        // typed rather than from the message.
                        ReferenceId = AdminAdjustmentKey.ForDeposit(userDto.Id, creditAsset, amount, DateTime.UtcNow)
                    });
                    if (result.Success)
                    {

                        // An admin top-up goes into the CREDIT_ wallet, so the messages say "credit"
                        // rather than "balance"; the previous message confused the two. Markdown
                        // formatting also clashed with ParseMode.Html and rendered the asterisks and
                        // backticks literally, so the text is now plain.
                        var amountText = $"{PersianFormat.Amount(amount, currency)} {PersianFormat.Unit(currency)}";
                        var newCreditText = $"{PersianFormat.Amount(result.Data?.BalanceAfter ?? 0m, currency)} {PersianFormat.Unit(currency)}";

                        // A deduplicated repeat reports success, because the charge did happen — on the
                        // earlier send, with nothing moving on this one. Telling the customer their
                        // credit rose again would be a lie about their own money, so only the admin is
                        // told, and told that it was a repeat (issue #157).
                        var alreadyApplied = result.Data?.WasAlreadyApplied ?? false;

                        // The repeat reports the original transaction, so its BalanceAfter is the credit
                        // as it stood at that earlier moment. Anything charged or deducted since is
                        // missing from it, which is why the repeat quotes CurrentBalance instead.
                        //
                        // Falling back to BalanceAfter, not to zero: a wallet that predates
                        // CurrentBalance sends no such field, and the services restart independently.
                        // The stale figure is the thing being fixed here, but telling an admin their
                        // customer holds nothing would be the worse of the two answers.
                        var creditNow = result.Data?.CurrentBalance ?? result.Data?.BalanceAfter ?? 0m;
                        var creditNowText = alreadyApplied
                            ? $"{PersianFormat.Amount(creditNow, currency)} {PersianFormat.Unit(currency)}"
                            : newCreditText;

                        await _messenger.SendAsync(
                           message.Chat.Id,
                           string.Format(alreadyApplied ? BotMsgs.MsgAdminChargeAlreadyApplied : BotMsgs.MsgAdminChargeDone,
                               PersianFormat.Asset(currency),
                               amountText,
                               PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone)),
                               creditNowText));

                        if (!alreadyApplied)
                        {
                            await _messenger.SendAsync(
                               userDto.TelegramId,
                               string.Format(BotMsgs.MsgUserCreditIncreased,
                                   PersianFormat.Asset(currency),
                                   amountText,
                                   newCreditText));
                        }
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
                // Accepted forms of the deduction command, with the currency given as a code,
                // omitted, or written as a multi-word Persian name.
                var regex = new Regex(@"^د\s+(?<phone>\d{10,11})\s+(?<amount>\d+)(\s+(?<currency>.+?))?\s*$",
                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
                var match = regex.Match(msgText);
                if (!match.Success)
                {
                    // The return is required; without it the code below ran on a failed match and threw.
                    await _messenger.SendAsync(message.Chat.Id,
                        string.Format(BotMsgs.MsgAdminDeductFormatError, CurrenciesConstant.GetCreditableNamesList()));
                    return true;
                }

                var phone = match.Groups["phone"].Value;
                var amount = decimal.Parse(match.Groups["amount"].Value);

                // The input may be a Persian name or a currency code.
                // The old default was a Persian word that never matched any asset code, so the
                // deduction ran against a wallet that did not exist.
                //
                // Gold, matching the charge command. The default used to be Toman, so the two
                // commands disagreed about what an omitted asset meant as well as about which
                // ledger they touched.
                var currencyInput = match.Groups["currency"].Success
                    ? match.Groups["currency"].Value
                    : CurrenciesConstant.Maua; // مقدار پیش‌فرض
                var currency = await ResolveCreditableAssetAsync(message.Chat.Id, currencyInput);
                if (currency is null) return true;

                var userDto = await _usersApi.GetUserAsync(phone);
                if (userDto != null)
                {
                    // The credit ledger, exactly as the charge command writes to it. These two
                    // commands used to disagree: ش credited CREDIT_<X> while د debited plain <X>,
                    // from the same Persian word and with the same help text and examples. An admin
                    // undoing a mistaken top-up with the obvious symmetric command therefore left the
                    // credit untouched and took the customer's real position instead — one mistake
                    // becoming two, under a ✅ confirmation (issue #36 carries the evidence).
                    //
                    // The bot has never been able to add to a spot balance: this is the only place it
                    // moves money, and the charge command has always meant credit. So a deduction that
                    // could reach a spot balance was the asymmetry, not a capability being removed —
                    // nothing here has a counterpart that adds. Spot balances move by trading, or
                    // through the wallet API directly.
                    var creditAsset = CurrenciesConstant.CreditAssetFor(currency);

                    var result = await _walletApi.WithdrawalAsync(new TallaEgg.Core.Requests.Wallet.WalletRequest
                    {
                        Asset = creditAsset,
                        Amount = amount,
                        UserId = userDto.Id,

                        // Same deduplication as the charge command above (issue #157).
                        ReferenceId = AdminAdjustmentKey.ForWithdrawal(userDto.Id, creditAsset, amount, DateTime.UtcNow)
                    });
                    if (result.Success)
                    {


                        // The message sent to the user used to say the wallet had been topped up
                        // when the amount had in fact been deducted. It also hard-coded the currency
                        // unit regardless of the asset, and displayed the asset's Latin code.
                        var deductAmountText = $"{PersianFormat.Amount(amount, currency)} {PersianFormat.Unit(currency)}";
                        var newBalanceText = $"{PersianFormat.Amount(result.Data?.BalanceAfter ?? 0m, currency)} {PersianFormat.Unit(currency)}";

                        // Same as the charge command above: a deduplicated repeat moved nothing, so the
                        // customer is not told a second deduction happened (issue #157).
                        var alreadyApplied = result.Data?.WasAlreadyApplied ?? false;

                        // Same reason as the charge command above, including the fallback: on a repeat
                        // the figure that means "what this customer holds" is CurrentBalance, and a
                        // wallet too old to send it must not turn into a reported balance of zero.
                        var balanceNow = result.Data?.CurrentBalance ?? result.Data?.BalanceAfter ?? 0m;
                        var balanceNowText = alreadyApplied
                            ? $"{PersianFormat.Amount(balanceNow, currency)} {PersianFormat.Unit(currency)}"
                            : newBalanceText;

                        await _messenger.SendAsync(
                               message.Chat.Id,
                               string.Format(alreadyApplied ? BotMsgs.MsgAdminDeductAlreadyApplied : BotMsgs.MsgAdminDeductDone,
                                   PersianFormat.Asset(currency),
                                   deductAmountText,
                                   PersianFormat.Ltr(PersianFormat.ToPersianDigits(phone)),
                                   balanceNowText));

                        if (!alreadyApplied)
                        {
                            await _messenger.SendAsync(
                                   userDto.TelegramId,
                                   string.Format(BotMsgs.MsgUserBalanceDeducted,
                                       PersianFormat.Asset(currency),
                                       deductAmountText,
                                       newBalanceText));
                        }


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
                else await _messenger.SendAsync(chatId, page.Message ?? BotMsgs.MsgUnexpectedError);
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

            if (msgText.StartsWith("نماد "))
            {
                await HandleSymbolActiveCommandAsync(chatId, msgText, user);
                return true;
            }

            // The admin's paired-price command.
            // Handle price pair format: buyPrice-sellPrice, with an optional trailing symbol
            // keyword (e.g., 8523690-8529630 یا 8523690-8529630 سکه). No keyword means MAUA/IRT
            // (issue: multi-symbol quoting, see the coin/Bitcoin work in this conversation).
            var pricePairRegex = new Regex(@"^(?<buy>\d+)-(?<sell>\d+)(?:\s+(?<symbol>\S+))?$", RegexOptions.Compiled);
            var pricePairMatch = pricePairRegex.Match(msgText.Trim());
            if (pricePairMatch.Success)
            {
                var symbol = ResolveAdminQuoteSymbol(
                    pricePairMatch.Groups["symbol"].Success ? pricePairMatch.Groups["symbol"].Value : null);

                if (symbol is null)
                {
                    await _messenger.SendAsync(chatId, BotMsgs.MsgAdminUnknownQuoteSymbol);
                    return true;
                }

                var buyPrice = decimal.Parse(pricePairMatch.Groups["buy"].Value);
                var sellPrice = decimal.Parse(pricePairMatch.Groups["sell"].Value);

                await HandlePricePairOrdersAsync(chatId, user.Id, symbol, buyPrice, sellPrice);
                return true;
            }

            return false;
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
        /// <b>The user id is echoed back on success.</b> It is not decoration: Wallet and Orders key
        /// every user-scoped endpoint by this internal Guid and neither of them accepts a phone
        /// number, while no other message the bot sends shows it. Printing it here is the difference
        /// between one message and a database query the next time the account has to be looked at.
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
        /// Checks only whether the user is an admin of the Telegram group.
        /// 
        /// </summary>
        /// <param name="user"></param>
        /// <returns></returns>
        private async Task<bool> IsTelegramAdmin(UserDto user)
        {
            var ids = await _botClient.GetAdminUserIdsAsync(Constants.GroupId);
            return ids.Contains(user.TelegramId);
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

            // Edit the admin's message.
            await _messenger.EditTextAsync(
                chatId: originalMsg.Chat.Id,
                messageId: originalMsg.MessageId,
                text: originalMsg.Text + BotMsgs.MsgAdminApprovedSuffix,
                replyMarkup: null);

            // Notify the user.
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

            // Notify the user.
            await _messenger.SendAsync(telegramUserId, BotMsgs.MsgUserRejected);
            await _telegramLogger.Notif<Message>($"کاربر رد شد \n userId : {telegramUserId} adminId : {adminTgId}", originalMsg);

        }

        /// <summary>
        /// Resolves the optional trailing symbol keyword on the auto-quote, manual-quote, and
        /// active/inactive commands to a trading-pair symbol — delegates to
        /// <see cref="CurrenciesConstant.ResolveSymbolByAlias"/> so a symbol added purely via
        /// config (with its own <c>Aliases</c> entry) is recognised here with no code change.
        /// </summary>
        private static string? ResolveAdminQuoteSymbol(string? keyword) =>
            CurrenciesConstant.ResolveSymbolByAlias(keyword);

        /// <summary>
        /// <c>اسپرد [درصد]</c> / <c>اسپرد [درصد] [نماد]</c> — sets the spread the automatic
        /// quote publisher applies around the fetched reference price for a symbol (issue #90).
        /// No symbol keyword means MAUA/IRT. Does not itself turn auto-quote on; see
        /// <see cref="HandleAutoQuoteToggleCommandAsync"/>.
        /// </summary>
        private async Task HandleAutoQuoteSpreadCommandAsync(long chatId, string msgText, UserDto actor)
        {
            var match = Regex.Match(msgText.Trim(), @"^اسپرد\s+(?<percent>[\d.]+)(?:\s+(?<symbol>\S+))?$", RegexOptions.Compiled);

            if (!match.Success || !decimal.TryParse(match.Groups["percent"].Value, out var spreadPercent))
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAutoQuoteSpreadFormatError);
                return;
            }

            var symbol = ResolveAdminQuoteSymbol(match.Groups["symbol"].Success ? match.Groups["symbol"].Value : null);
            if (symbol is null)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAdminUnknownQuoteSymbol);
                return;
            }

            var (success, message) = await _orderApi.UpdateAutoQuoteSpreadAsync(
                symbol, spreadPercent, actor.Id);

            var symbolName = PersianFormat.Symbol(symbol);

            await _messenger.SendAsync(chatId, success
                ? string.Format(BotMsgs.MsgAutoQuoteSpreadUpdated, symbolName, PersianFormat.Number(spreadPercent, decimals: 2))
                : string.Format(BotMsgs.MsgAutoQuoteSpreadFailed, symbolName, message));
        }

        /// <summary>
        /// <c>اتومات روشن</c> / <c>اتومات خاموش</c> (+ optional trailing symbol keyword) — turns
        /// automatic quote publishing on or off for a symbol (issue #90). No symbol keyword
        /// means MAUA/IRT. A manually published quote (<c>buyPrice-sellPrice</c>) always
        /// overrides the automatic one regardless of this setting.
        /// </summary>
        private async Task HandleAutoQuoteToggleCommandAsync(long chatId, string msgText, UserDto actor)
        {
            var match = Regex.Match(msgText.Trim(), @"^اتومات\s+(?<state>روشن|خاموش)(?:\s+(?<symbol>\S+))?$", RegexOptions.Compiled);

            if (!match.Success)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAutoQuoteToggleFormatError);
                return;
            }

            var enable = match.Groups["state"].Value == "روشن";

            var symbol = ResolveAdminQuoteSymbol(match.Groups["symbol"].Success ? match.Groups["symbol"].Value : null);
            if (symbol is null)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAdminUnknownQuoteSymbol);
                return;
            }

            var (success, message) = await _orderApi.SetAutoQuoteEnabledAsync(symbol, enable, actor.Id);

            var symbolName = PersianFormat.Symbol(symbol);

            await _messenger.SendAsync(chatId, success
                ? string.Format(enable ? BotMsgs.MsgAutoQuoteEnabled : BotMsgs.MsgAutoQuoteDisabled, symbolName)
                : string.Format(BotMsgs.MsgAutoQuoteToggleFailed, symbolName, message));
        }

        /// <summary>
        /// <c>نماد فعال</c> / <c>نماد غیرفعال</c> (+ optional trailing symbol keyword) — turns a
        /// symbol tradable or not: shown in the customer's symbol picker, eligible for
        /// auto-quote, usable for a manual quote. No symbol keyword means MAUA/IRT, the same
        /// convention as <see cref="HandleAutoQuoteToggleCommandAsync"/>. Independent of
        /// auto-quote's own on/off switch — a symbol can be tradable with only manual quotes.
        /// </summary>
        private async Task HandleSymbolActiveCommandAsync(long chatId, string msgText, UserDto actor)
        {
            var match = Regex.Match(msgText.Trim(), @"^نماد\s+(?<state>فعال|غیرفعال)(?:\s+(?<symbol>\S+))?$", RegexOptions.Compiled);

            if (!match.Success)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgSymbolActiveFormatError);
                return;
            }

            var makeActive = match.Groups["state"].Value == "فعال";

            var symbol = ResolveAdminQuoteSymbol(match.Groups["symbol"].Success ? match.Groups["symbol"].Value : null);
            if (symbol is null)
            {
                await _messenger.SendAsync(chatId, BotMsgs.MsgAdminUnknownQuoteSymbol);
                return;
            }

            var (success, message) = await _orderApi.SetSymbolActiveAsync(symbol, makeActive, actor.Id);

            var symbolName = PersianFormat.Symbol(symbol);

            await _messenger.SendAsync(chatId, success
                ? string.Format(makeActive ? BotMsgs.MsgSymbolActivated : BotMsgs.MsgSymbolDeactivated, symbolName)
                : string.Format(BotMsgs.MsgSymbolActiveFailed, symbolName, message));
        }

        /// <summary>
        /// Handles the admin's paired-price submission.
        /// </summary>
        /// <param name="chatId">Telegram chat id to send the reply to.</param>
        /// <param name="userId">User id in our system.</param>
        /// <param name="asset">Trading pair symbol, for example MAUA/IRT.</param>
        /// <param name="buyPrice">Buy price entered by the admin.</param>
        /// <param name="sellPrice">Sell price entered by the admin.</param>
        /// <returns>A task representing the operation.</returns>
        /// <remarks>
        /// Cancels the user's active orders, converts the entered prices for gold by dividing by
        /// 4.3318 while leaving other symbols unconverted, creates a buy order at the lower price and
        /// a sell order at the higher one, each for a default 1000 units, and reports the outcome to
        /// the admin.
        /// </remarks>
        private async Task HandlePricePairOrdersAsync(long chatId, Guid userId, string asset, decimal buyPrice, decimal sellPrice)
        {
            // No local catch here (issue #99) — publish failure already comes back as
            // (published: false, publishMessage) below and is shown to the admin there. The
            // catch that used to wrap this method only ever fired for something genuinely
            // unexpected, sent the admin ex.Message verbatim, and logged nowhere. Letting it
            // bubble reaches TelegramBotHostedService.HandleUpdateAsync's catch, which does
            // both.

            // First, cancel all existing active orders for this user
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
            var quote = QuoteMessage.Prepare(asset, buyPrice, sellPrice);

            var (published, publishMessage, pending) = await _orderApi.PublishQuoteAsync(
                asset, quote.BuyPricePerGram, quote.SellPricePerGram, userId);

            if (pending is not null)
            {
                // Too far from the current quote to publish unasked. The band applies to a price
                // an admin typed as much as to one the feed produced — a mistyped zero does the
                // same damage, and a manual quote is tradeable the moment it lands (issue #158).
                // The admin is right here, so the question goes straight back to them.
                var (text, keyboard) = PendingQuoteMessage.Build(pending, DateTime.UtcNow);
                await _messenger.SendAsync(chatId, text, keyboard);
            }
            else if (published)
            {
                await _messenger.SendAsync(chatId, quote.Text);
            }
            else
            {
                await _messenger.SendAsync(chatId,
                    string.Format(BotMsgs.MsgAdminQuoteFailed, publishMessage));
            }
        }

        /// <summary>
        /// Answers a quote the plausibility band is holding (issue #158).
        ///
        /// <para>
        /// The proposal id travels in the callback data rather than in conversation state,
        /// because the message can sit in Telegram for minutes, several symbols can be waiting at
        /// once, and any admin may be the one who answers — none of which a per-conversation
        /// "current pending quote" would survive.
        /// </para>
        ///
        /// <para>
        /// "Already answered" and "expired" both come back as refusals with a sentence written for
        /// the admin, and both are ordinary: two admins can press at once, and a button in an old
        /// message is exactly what the expiry window exists to stop.
        /// </para>
        /// </summary>
        private async Task HandlePendingQuoteDecisionAsync(long chatId, Guid adminUserId, string callbackData)
        {
            var separator = callbackData.IndexOf(':');
            if (separator < 0 || !Guid.TryParse(callbackData[(separator + 1)..], out var pendingQuoteId))
            {
                _logger.LogWarning("Unparseable pending-quote callback: {CallbackData}", callbackData);
                return;
            }

            var approving = callbackData.StartsWith(InlineCallBackData.approve_quote, StringComparison.Ordinal);

            // Read before answering: once resolved it is no longer in the awaiting list, and the
            // confirmation needs its symbol and prices. A null here is not fatal — the server's own
            // sentence is still shown — so a failed read costs detail, not the answer.
            var pending = (await _orderApi.GetPendingQuotesAsync())
                ?.FirstOrDefault(p => p.Id == pendingQuoteId);

            var (success, message) = approving
                ? await _orderApi.ApprovePendingQuoteAsync(pendingQuoteId, adminUserId)
                : await _orderApi.RejectPendingQuoteAsync(pendingQuoteId, adminUserId);

            if (!success)
            {
                await _messenger.SendAsync(chatId, string.Format(BotMsgs.MsgAdminQuoteResolveFailed, message));
                return;
            }

            // The server's sentence says what happened but not to which symbol or at what price,
            // and an admin may have several questions open at once. The confirmation is rebuilt
            // from the proposal so it names both — in the same units the question used.
            var answered = pending is null
                ? message
                : approving
                    ? PendingQuoteMessage.Approved(pending)
                    : PendingQuoteMessage.Rejected(pending);

            await _messenger.SendAsync(chatId, answered);
        }

        /// <summary>
        /// Cancels all of a user's active orders.
        /// </summary>
        /// <param name="userId">The user whose orders should be cancelled.</param>
        /// <returns>The outcome, including how many orders were cancelled and any error.</returns>
        /// <remarks>
        /// Calls the cancel-active-orders endpoint, records the cancellation reason as an admin
        /// replacing the orders, and returns the count together with success or failure.
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
                _logger.LogError(ex, "Unexpected error while cancelling a user's active orders.");
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