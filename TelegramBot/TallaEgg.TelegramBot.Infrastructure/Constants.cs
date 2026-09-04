using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.TelegramBot.Infrastructure
{
    public class Constants
    {
        public const long GroupId = -1002988196234;
        public const string DeveloperChatId = "-4777000333";
        public const string SupportErrorMessage = "مشکلی پیش آمده لطفا با پشتیبانی تماس بگیرید.";
    }
    public static class BotBtns
    {
        public const string BtnMainMenu = "💰 منوی اصلی";
        public const string BtnSpot = "💰 نقدی";
        // BtnFutures was removed: there is no futures market and no handler existed for the button.
        public const string BtnAccounting = "📊 حسابداری";
        public const string BtnHelp = "❓ راهنما";
        public const string BtnBack = "🔙 بازگشت";
        public const string BtnHistory = "📋 تاریخچه";

        /// <summary>
        /// Quote history replaced order history in the accounting menu. In the dealer model
        /// an order exists only for the instant of a fill, so "my orders" showed a list that
        /// was either empty or made of rows already completed — nothing a customer could act
        /// on. The published prices are the thing with a history worth reading.
        /// </summary>
        public const string BtnQuoteHistory = "📋 تاریخچه مظنه‌ها";

        public const string BtnTradeHistory = "📊 تاریخچه معاملات";
        public const string BtnWalletsBalance = "💵 موجودی";
        public const string BtnWallet = "💳 کیف پول";
        public const string BtnSharePhone = "📱 اشتراک‌گذاری شماره تلفن";
        public const string BtnConfirm = "✅ تایید";
        public const string BtnCancel = "❌ لغو";

        /// <summary>Answers on a quote the band is holding — deliberately worded as a judgement about the price.</summary>
        public const string BtnApproveQuote = "✅ قیمت درست است، منتشر کن";
        public const string BtnRejectQuote = "❌ منتشر نکن";
        /// <summary>
        /// "Place Order" and "Make Order" mean the same thing here: submitting an order.
        /// </summary>
        public const string BtnSpotCreateOrder = "📝 ثبت سفارش نقدی";
        /// <summary>
        /// The admin publishes a quote by typing "buyPrice-sellPrice" (e.g. 71000000-80000000).
        /// The old label, "ثبت قیمت نقدی", described submitting an order — which is no longer
        /// what happens: since #48 nothing is placed in a book and no collateral is locked.
        /// </summary>
        public const string BtnSpotSubmitPrice = "💹 اعلام مظنه";

        /// <summary>
        /// What the customer actually does here is ask for today's price and trade on it.
        /// The old label, "بازار نقدی" (spot market), described an order book they never see:
        /// they do not place a resting order and never enter a price of their own.
        /// </summary>
        public const string BtnSpotMarket = "💹 دریافت مظنه";
        public const string BtnSpotMarketBuy = "🛒 خرید نقدی";
        public const string BtnSpotMarketSell = "🛍️ فروش نقدی";
    }
    public static class BotMsgs
    {
        // ────────────────────────────────────────────────────────────────────────────
        // Rules for writing these messages:
        // - All text is Persian; no Latin word or symbol appears in it.
        // - Every number goes through PersianFormat, which gives Persian digits and a
        //   right-to-left guard so Telegram does not reorder it. Never interpolate a raw number.
        // - Trading symbols go through PersianFormat.Symbol to become Persian names.
        // - A message says what happened and what to do next. Avoid ambiguity.
        // ────────────────────────────────────────────────────────────────────────────

        public const string MsgEnterInvite = "برای شروع، کد معرف خود را وارد کنید.\n\n" +
                                            "اگر کد معرف دارید، آن را همراه دستور شروع بفرستید.\n" +
                                            "در غیر این صورت، از طلافروشی خود کد معرف بگیرید.";

        public const string MsgPhoneRequest = "برای استفاده از خدمات، شماره تلفن خود را به اشتراک بگذارید.\n\n" +
                                              "روی دکمهٔ زیر بزنید تا شماره‌تان ثبت شود.";

        public const string MsgWelcome = "🎉 خوش آمدید!\n\n" +
                                         "ثبت‌نام شما انجام شد.\n" +
                                         "برای تکمیل، شماره تلفن خود را با دکمهٔ زیر به اشتراک بگذارید.";

        public const string MsgPhoneSuccess = "✅ شماره تلفن شما ثبت شد.\n\n" +
                                              "حساب شما در انتظار تایید مدیر است.\n" +
                                              "به‌محض تایید، به شما اطلاع می‌دهیم و می‌توانید معامله کنید.";

        public const string MsgMainMenu = "🎯 منوی اصلی\n\nیکی از گزینه‌های زیر را انتخاب کنید:";

        /// <summary>{0} = the user's name. The account is registered but an admin has not approved it yet.</summary>
        public const string MsgAccountNotApproved = "{0} عزیز، حساب کاربری شما هنوز فعال نشده است.\n\n" +
                                                    "حساب شما در انتظار تایید مدیر است؛ به‌محض تایید به شما اطلاع می‌دهیم.";

        public const string MsgSelectTradingType = "نوع معامله را انتخاب کنید:";

        public const string MsgSelectAsset = "دارایی مورد نظر را انتخاب کنید:";

        public const string MsgEnterAmount = "مقدار مورد نظر را وارد کنید:";

        /// <summary>
        /// Price prompt for melted gold. The unit has to be explicit: the price of one mesghal in
        /// toman. The previous message asked for "the price" without saying of what, or in what unit.
        /// </summary>
        public const string MsgEnterPriceGold = "قیمت یک مثقال طلای آبشده را به تومان وارد کنید:\n\n" +
                                               "نمونه: ۷۹۰۰۰۰۰۰";

        /// <summary>Price prompt for other assets. {0} = the asset's Persian name.</summary>
        public const string MsgEnterPrice = "قیمت هر واحد {0} را به تومان وارد کنید:";

        /// <summary>
        /// Order confirmation. Icons and the separator before the total match the executed-trade
        /// message and the trade history, so the same fact keeps the same shape everywhere the
        /// customer meets it. The total sits below the rule because it is the number they check
        /// before tapping "confirm".
        ///
        /// {0} = Persian symbol; {1} = side with its colour icon; {2} = quantity with unit;
        /// {3} = price per unit; {4} = total amount.
        /// Every value must already have been formatted through PersianFormat.
        /// </summary>
        public const string MsgOrderConfirmation = "📋 تأیید سفارش\n\n" +
                                                  "🏷️ دارایی: {0}\n" +
                                                  "{1}\n" +
                                                  "📊 مقدار: {2}\n" +
                                                  "💰 قیمت هر واحد: {3} تومان\n" +
                                                  "➖➖➖➖➖➖➖➖➖\n" +
                                                  "💵 مبلغ کل: {4} تومان\n\n" +
                                                  "آیا این سفارش را تأیید می‌کنید؟";

        /// <summary>
        /// Order confirmation for melted gold.
        /// The per-mesghal price is the number the user typed; the per-gram price is its computed
        /// equivalent. Both must be shown, or the user sees a figure different from their own input
        /// and assumes the order was recorded wrongly.
        /// {0} = asset; {1} = side with its colour icon; {2} = quantity with unit;
        /// {3} = price per mesghal; {4} = price per gram; {5} = total amount.
        /// </summary>
        public const string MsgOrderConfirmationGold = "📋 تأیید سفارش\n\n" +
                                                       "🏷️ دارایی: {0}\n" +
                                                       "{1}\n" +
                                                       "📊 مقدار: {2}\n" +
                                                       "💰 قیمت هر مثقال: {3} تومان\n" +
                                                       "⚖️ قیمت هر گرم: {4} تومان\n" +
                                                       "➖➖➖➖➖➖➖➖➖\n" +
                                                       "💵 مبلغ کل: {5} تومان\n\n" +
                                                       "آیا این سفارش را تأیید می‌کنید؟";

        /// <summary>{0} = the reason, if there is one.</summary>
        public const string MsgInsufficientBalance = "❌ موجودی شما برای این سفارش کافی نیست.\n\n" +
                                                     "{0}\n\n" +
                                                     "برای افزایش موجودی یا اعتبار، با طلافروشی خود تماس بگیرید.";

        public const string MsgOrderSuccess = "✅ سفارش شما ثبت شد.\n\n" +
                                              "به‌محض انجام معامله، نتیجه به شما اطلاع داده می‌شود.\n" +
                                              "سفارش‌های در جریان را از «سفارشات فعال» ببینید.";

        /// <summary>
        /// The outcome of a trade that actually executed. Sent after the order-placed message.
        ///
        /// Why two messages and not one: the first says the request was accepted, the second says
        /// the trade executed. In dealer mode those coincide, but in an order book they may be hours
        /// apart — and the user must see both in the same shape, or they learn that "placed" means
        /// "executed", which in an order book is not true.
        ///
        /// The total is deliberately included: the user should know what they paid or received
        /// without doing the arithmetic.
        ///
        /// The icons match those used for the same facts in the trade history
        /// (<c>TradeListHandler</c>) on purpose: the same thing should look the same
        /// wherever the customer meets it. The separator before the total is there because
        /// the total is the one number the customer checks — it is what left or entered
        /// their account, and it should not have to be found among the others.
        ///
        /// {0} = side with its colour icon; {1} = Persian symbol; {2} = quantity with unit;
        /// {3} = price label; {4} = price; {5} = "paid" or "received"; {6} = total amount.
        /// </summary>
        public const string MsgTradeExecuted = "✅ معاملهٔ شما انجام شد\n\n" +
                                               "{0}\n" +
                                               "🏷️ دارایی: {1}\n" +
                                               "📊 مقدار: {2}\n" +
                                               "💰 {3}: {4} تومان\n" +
                                               "➖➖➖➖➖➖➖➖➖\n" +
                                               "💵 {5}: {6} تومان\n\n" +
                                               "جزئیات را از «📊 تاریخچه معاملات» ببینید.";

        /// <summary>{0} = the error reason.</summary>
        public const string MsgOrderFailed = "❌ سفارش شما ثبت نشد.\n\nدلیل: {0}\n\nلطفاً دوباره تلاش کنید.";

        /// <summary>
        /// Market prices. {0} = Persian symbol; {1} = best bid; {2} = best ask; {3} = spread.
        /// All values are pre-formatted.
        /// </summary>
        public const string MsgMarketPrices = "📊 قیمت‌های بازار\n\n" +
                                              "دارایی: {0}\n" +
                                              "بهترین قیمت خرید: {1} تومان\n" +
                                              "بهترین قیمت فروش: {2} تومان\n" +
                                              "اختلاف خرید و فروش: {3} تومان\n\n" +
                                              "عملیات مورد نظر را انتخاب کنید:";

        /// <summary>{0} = the asset's Persian name together with its unit.</summary>
        public const string MsgEnterQuantity = "مقدار {0} را وارد کنید:";

        /// <summary>
        /// Shown when no quote has been published for the chosen symbol. Continuing — asking for a
        /// quantity — would only reach a guaranteed failure at the next step, since there is no price
        /// to place an order at. Stop here, not after taking the quantity from the user.
        /// </summary>
        public const string MsgNoQuoteForSymbol = "در حال حاضر قیمتی برای این نماد منتشر نشده. لطفاً کمی بعد دوباره تلاش کنید.";

        // ── Balance display ─────────────────────────────────────────────────────────

        public const string MsgBalanceHeader = "💰 موجودی حساب شما\n\n";

        /// <summary>
        /// One balance row. {0} = the asset's Persian name; {1} = free balance with unit;
        /// {2} = amount tied up in orders, with unit.
        /// </summary>
        public const string MsgBalanceRow = "▪️ {0}\n" +
                                           "   موجودی آزاد: {1}\n" +
                                           "   درگیر در سفارش: {2}\n";

        /// <summary>
        /// Shown when the free balance is negative. Under the credit model that is normal — the user
        /// traded on credit — but without an explanation a negative number confuses them.
        /// {0} = the debt amount with its unit.
        /// </summary>
        public const string MsgBalanceDebtNote = "   ⚠️ بدهی شما: {0}\n";

        /// <summary>
        /// The symbol's credit, where an admin has granted any — including when the asset's own
        /// wallet does not exist yet, which is the case for a user given credit who has not traded
        /// that symbol. {0} = the credit amount with its unit.
        /// </summary>
        public const string MsgBalanceCreditLine = "   💳 اعتبار: {0}\n";

        // ── Profit and loss (issue #93) ─────────────────────────────────────────────
        // Only shown when the position is open (Quantity != 0) or there is realised profit or loss.
        // A symbol the user has never traded shows none of these lines.

        /// <summary>{0} = average buy price, with unit, in toman.</summary>
        public const string MsgBalanceAverageCost = "   میانگین قیمت خرید: {0}\n";

        /// <summary>{0} = the position's current value, with unit, in toman.</summary>
        public const string MsgBalanceCurrentValue = "   ارزش فعلی: {0}\n";

        /// <summary>Unrealised profit or loss on an open position, valued at the admin's buy price. {0} = the amount with its unit.</summary>
        public const string MsgBalanceUnrealizedGain = "   📈 سود تحقق‌نیافته: {0}\n";
        public const string MsgBalanceUnrealizedLoss = "   📉 زیان تحقق‌نیافته: {0}\n";

        /// <summary>Realised profit or loss from closed trades. {0} = the amount with its unit.</summary>
        public const string MsgBalanceRealizedGain = "   ✅ سود تحقق‌یافته: {0}\n";
        public const string MsgBalanceRealizedLoss = "   ❌ زیان تحقق‌یافته: {0}\n";

        /// <summary>Shown when no quote has been published to value an open position against.</summary>
        public const string MsgBalanceNoQuoteForUnrealized = "   سود/زیان تحقق‌نیافته: قیمتی برای این نماد منتشر نشده\n";

        /// <summary>{0} = total profit or loss, realised plus unrealised, across every symbol, with unit.</summary>
        public const string MsgBalanceTotalPnlGain = "\n📊 مجموع سود و زیان شما: 📈 {0} سود\n";
        public const string MsgBalanceTotalPnlLoss = "\n📊 مجموع سود و زیان شما: 📉 {0} زیان\n";
        public const string MsgBalanceTotalPnlNone = "\n📊 مجموع سود و زیان شما: بدون تغییر\n";

        public const string MsgBalanceFooter = "\nبرای افزایش اعتبار با طلافروشی خود تماس بگیرید.";

        /// <summary>{0} = the error reason.</summary>
        public const string MsgActiveOrdersFailed = "❌ دریافت سفارش‌های فعال انجام نشد.\n\nدلیل: {0}";

        public const string MsgNoWallet = "برای شما هنوز حسابی ثبت نشده است.\n\n" +
                                          "برای فعال‌سازی حساب و دریافت اعتبار، با طلافروشی خود تماس بگیرید.";

        /// <summary>Help text for an ordinary user. The admin help, MsgAdminHelp, replaces it where needed.</summary>
        /// <summary>
        /// Help text for an ordinary user.
        ///
        /// <para>
        /// The buttons are composed from their own constants, not from hand-typed text. The previous
        /// version spelled all three names out separately and drifted from reality when the menu
        /// changed: one button had been renamed and another removed entirely, while the help still
        /// advertised both. Users went looking for a button that did not exist.
        /// </para>
        ///
        /// <para>
        /// <c>UserHelpMatchesTheMenuTests</c> asserts this, because the compiler does not check text
        /// inside a string, and that silence is why the breakage survived for months.
        /// </para>
        /// </summary>
        public const string MsgUserHelp = "❓ راهنما\n\n" +
                                         BotBtns.BtnSpotMarket + ": مظنهٔ روز را می‌گیرید و روی همان قیمت خرید یا فروش می‌کنید\n" +
                                         BotBtns.BtnAccounting + ": موجودی و تاریخچهٔ معاملات شما\n\n" +
                                         "نکته: قیمت‌ها بر حسب «هر مثقال» و مقدار طلا بر حسب «گرم» است.\n\n";

        /// <summary>
        /// Main-menu help for an admin. It <b>replaces</b> <see cref="MsgUserHelp"/> rather than
        /// being appended to it.
        ///
        /// An admin's menu has different buttons from an ordinary user's: they publish a quote
        /// rather than request one, and their accounting menu includes quote history, not just
        /// balances. <c>MsgUserHelp</c> used to be shown to admins too, describing a button that is
        /// not on their menu at all.
        /// </summary>
        public const string MsgAdminMainHelp = "❓ راهنما\n\n" +
                                              BotBtns.BtnSpotSubmitPrice + ": آخرین مظنهٔ منتشرشده را نشان می‌دهد\n" +
                                              BotBtns.BtnAccounting + ": تاریخچهٔ معاملات و مظنه‌های منتشرشده\n\n" +
                                              "نکته: قیمت‌ها بر حسب «هر مثقال» و مقدار طلا بر حسب «گرم» است.\n\n";

        /// <summary>Appended to the end of the help text.</summary>
        public const string MsgSupportFooter = "برای افزایش موجودی یا هر سوال دیگر، با طلافروشی خود تماس بگیرید.";

        /// <summary>
        /// How to add funds. There is no payment gateway today and top-ups are performed by the gold
        /// shop; the previous message displayed a sample account and card number, which was both
        /// misleading and dangerous.
        /// </summary>
        public const string MsgChargeInfo = "💰 افزایش موجودی\n\n" +
                                           "افزایش موجودی و اعتبار حساب شما توسط طلافروشی انجام می‌شود.\n\n" +
                                           "برای شارژ حساب، با طلافروشی خود تماس بگیرید.\n" +
                                           "پس از تایید، موجودی شما در همین ربات به‌روز می‌شود.";

        /// <summary>
        /// The market's best bid and ask. {0} = the display unit (mesghal for gold, otherwise the
        /// symbol's own base unit); {1} = best bid; {2} = best ask.
        /// Naming the unit is essential, because gold prices are shown per gram elsewhere and the
        /// other symbols do not share a unit.
        /// </summary>
        public const string MsgBestPrices = "📊 بهترین قیمت‌های بازار (هر {0})\n\n" +
                                           "💰 خرید: {1}\n" +
                                           "💸 فروش: {2}";

        /// <summary>
        /// Shown when that side of the market has no orders. Displaying zero would mislead, since a
        /// zero reads as a price of zero rather than the absence of one.
        /// </summary>
        public const string MsgPriceNotAvailable = "فعلاً سفارشی ثبت نشده";

        /// <summary>Same arguments as MsgOrderConfirmation.</summary>
        public const string MsgMarketOrderConfirmation = "📋 تایید سفارش بازار\n\n" +
                                                          "دارایی: {0}\n" +
                                                          "نوع سفارش: {1}\n" +
                                                          "مقدار: {2}\n" +
                                                          "قیمت هر واحد: {3} تومان\n" +
                                                          "مبلغ کل: {4} تومان\n\n" +
                                                          "آیا این سفارش را تایید می‌کنید؟";
        
        public const string MsgAdminHelp = "🔧 دستورهای مدیریت\n\n" +
                                          "افزایش اعتبار:\n" +
                                          "ش [شمارهٔ تلفن] [مقدار] [نوع]\n" +
                                          "نمونه: ش ۰۹۱۲۱۲۳۴۵۶۷ ۱۰۰ آبشده\n\n" +
                                          "کسر از اعتبار (معکوس ش):\n" +
                                          "د [شمارهٔ تلفن] [مقدار] [نوع]\n" +
                                          "نمونه: د ۰۹۱۲۱۲۳۴۵۶۷ ۱۰۰ آبشده\n" +
                                          "(هر دو روی اعتبار کار می‌کنند؛ بدون ذکر نوع، آبشده)\n\n" +
                                          "فهرست کاربران: ک [جستجو]\n" +
                                          "موجودی کاربر: م [شمارهٔ تلفن]\n" +
                                          "سفارش‌های فعال کاربر: س [شمارهٔ تلفن]\n\n" +
                                          "تغییر سطح دسترسی:\n" +
                                          "ن [شمارهٔ تلفن] [نقش]\n" +
                                          "نمونه: ن ۰۹۱۲۱۲۳۴۵۶۷ مدیر\n" +
                                          "نقش‌ها: کاربر عادی (۰)، حسابدار (۱)، مدیر (۲)، مدیر ارشد (۳)\n\n" +
                                          "تایید حساب کاربر: ت [شمارهٔ تلفن]\n" +
                                          "رد حساب کاربر: ر [شمارهٔ تلفن]\n\n" +
                                          "ثبت همزمان قیمت خرید و فروش (هر مثقال):\n" +
                                          "[قیمت خرید]-[قیمت فروش]\n" +
                                          "نمونه: ۷۹۰۰۰۰۰۰-۷۹۵۰۰۰۰۰\n\n" +
                                          "مظنهٔ اتومات:\n" +
                                          "تنظیم اسپرد: اسپرد [درصد]\n" +
                                          "نمونه: اسپرد ۰.۵\n" +
                                          "روشن/خاموش: اتومات روشن یا اتومات خاموش\n\n" +
                                          "نکته: مقدارها را می‌توانید با ارقام فارسی یا انگلیسی وارد کنید.";

        // ── Credit top-up and balance deduction messages (admin) ────────────────────

        /// <summary>{0} = the list of permitted currency names in Persian.</summary>
        public const string MsgAdminChargeFormatError = "❌ قالب دستور درست نیست.\n\n" +
                                                        "این دستور به اعتبار کاربر اضافه می‌کند.\n\n" +
                                                        "قالب صحیح:\n" +
                                                        "ش [شمارهٔ تلفن] [مقدار] [نوع]\n\n" +
                                                        "نمونه: ش ۰۹۱۲۱۲۳۴۵۶۷ ۱۰۰ آبشده\n" +
                                                        "نمونه: ش ۰۹۱۲۱۲۳۴۵۶۷ ۱۰۰ سکه\n\n" +
                                                        "اگر نوع را ننویسید، آبشده در نظر گرفته می‌شود.\n" +
                                                        "نوع‌های مجاز: {0}\n" +
                                                        "(به‌جای نام کامل، کلیدواژهٔ کوتاه هم می‌پذیرد: سکه، بیت)";

        /// <summary>{0} = the list of permitted currency names in Persian.</summary>
        public const string MsgAdminDeductFormatError = "❌ قالب دستور درست نیست.\n\n" +
                                                        "این دستور از اعتبار کاربر کم می‌کند (معکوس دستور ش).\n\n" +
                                                        "قالب صحیح:\n" +
                                                        "د [شمارهٔ تلفن] [مقدار] [نوع]\n\n" +
                                                        "نمونه: د ۰۹۱۲۱۲۳۴۵۶۷ ۱۰۰ آبشده\n" +
                                                        "نمونه: د ۰۹۱۲۱۲۳۴۵۶۷ ۱۰۰ سکه\n\n" +
                                                        "اگر نوع را ننویسید، آبشده در نظر گرفته می‌شود.\n" +
                                                        "نوع‌های مجاز: {0}\n" +
                                                        "(به‌جای نام کامل، کلیدواژهٔ کوتاه هم می‌پذیرد: سکه، بیت)";

        /// <summary>
        /// For an asset that exists but has no credit ledger — today that is Toman, which is a quote
        /// currency, and credit ledgers are minted per tradable base asset only. Deliberately not
        /// <see cref="MsgAdminInvalidCurrency"/>: Toman is a perfectly good asset name, and telling
        /// the admin it was not recognised would send them hunting for a spelling mistake that is
        /// not there. Before this, the command built CREDIT_IRT and failed at the wallet instead,
        /// surfacing as the generic "خطا در بروزرسانی".
        /// {0} = the asset's Persian name; {1} = the assets that do have a credit ledger.
        /// </summary>
        public const string MsgAdminAssetHasNoCredit = "❌ «{0}» دفتر اعتبار ندارد.\n\n" +
                                                       "دستورهای ش و د فقط روی اعتبار کار می‌کنند، و اعتبار فقط برای دارایی‌های قابل معامله تعریف می‌شود.\n\n" +
                                                       "دارایی‌های مجاز: {1}";

        /// <summary>
        /// For a name that resolves to a credit ledger, which both commands add for themselves.
        /// Separate from "not recognised" because the admin typed something meaningful — and it used
        /// to be the only spelling that could reduce a credit line, so it is worth answering kindly.
        /// {0} = what they typed.
        /// </summary>
        public const string MsgAdminCreditNameNotNeeded = "ℹ️ لازم نیست «{0}» را بنویسید.\n\n" +
                                                          "هر دو دستور ش و د همیشه روی اعتبار کار می‌کنند.\n" +
                                                          "فقط نام دارایی را بنویسید — مثلاً «آبشده» یا «سکه».";

        /// <summary>{0} = the user's invalid input; {1} = the list of permitted Persian names.</summary>
        public const string MsgAdminInvalidCurrency = "❌ نوع «{0}» شناسایی نشد.\n\n" +
                                                       "نوع‌های مجاز: {1}\n" +
                                                       "(به‌جای نام کامل، کلیدواژهٔ کوتاه هم می‌پذیرد: سکه، بیت)";

        public const string MsgAdminUserNotFound = "❌ کاربری با این شمارهٔ تلفن پیدا نشد.";

        // ── Role change messages (admin) ────────────────────────────────────────────

        /// <summary>{0} = the permitted roles, each with its number.</summary>
        public const string MsgAdminRoleFormatError = "❌ قالب دستور درست نیست.\n\n" +
                                                       "قالب صحیح:\n" +
                                                       "ن [شمارهٔ تلفن] [نقش]\n\n" +
                                                       "نمونه: ن ۰۹۱۲۱۲۳۴۵۶۷ مدیر\n\n" +
                                                       "نقش‌های مجاز: {0}";

        /// <summary>{0} = the user's invalid input; {1} = the permitted roles.</summary>
        public const string MsgAdminRoleUnknown = "❌ نقش «{0}» شناسایی نشد.\n\nنقش‌های مجاز: {1}";

        /// <summary>
        /// Shown when an admin tries to change their own role. The reason for refusing is explained
        /// in the code: if the only admin demotes themselves, the command that would restore them is
        /// no longer available to anyone.
        /// </summary>
        public const string MsgAdminRoleSelfChange = "❌ نقش خودتان را نمی‌توانید تغییر دهید.\n\n" +
                                                      "اگر این کار ممکن بود، یک اشتباه می‌توانست دسترسی مدیریتی را " +
                                                      "به‌کلی از بین ببرد و بازگرداندن آن فقط با تغییر فایل پیکربندی و " +
                                                      "راه‌اندازی دوبارهٔ ربات ممکن می‌شد.";

        /// <summary>{0} = phone number; {1} = current role.</summary>
        public const string MsgAdminRoleUnchanged = "ℹ️ تغییری لازم نبود.\n\n" +
                                                     "کاربر: {0}\n" +
                                                     "نقش فعلی: {1}";

        /// <summary>
        /// Role-change confirmation shown to the admin. Sent with <c>ParseMode.Html</c>.
        /// {0} = phone number; {1} = previous role; {2} = new role; {3} = user id.
        ///
        /// The user id is shown deliberately. Every user-scoped endpoint in Wallet and Orders is keyed
        /// by this internal Guid — balance, transactions, orders, trades, positions — and none of them
        /// accepts a phone number, so it is what an operator needs next if they want to look at the
        /// account they just changed. It is obtainable elsewhere — Users exposes
        /// <c>GET /api/user/getUserIdByPhoneNumber/{phone}</c>, and the role change is logged with it
        /// — so this line saves a round trip rather than being the only source.
        ///
        /// It is a <c>code</c> entity so Telegram offers tap-to-copy, and copies the bare Guid. The
        /// bidi isolate is deliberately <b>outside</b> that entity: within it, U+2066 and U+2069
        /// would be copied along with the id, and every endpoint above would then reject it as a
        /// malformed Guid — with nothing visible in the pasted text to explain why.
        /// </summary>
        public const string MsgAdminRoleChanged = "✅ سطح دسترسی تغییر کرد.\n\n" +
                                                   "👤 کاربر: {0}\n" +
                                                   "🔻 نقش پیشین: {1}\n" +
                                                   "🔺 نقش تازه: {2}\n" +
                                                   "➖➖➖\n" +
                                                   "🆔 شناسهٔ کاربر:\n" +
                                                   TallaEgg.Core.Utilties.PersianFormat.Lri +
                                                   "<code>{3}</code>" +
                                                   TallaEgg.Core.Utilties.PersianFormat.Pdi;

        // ── User approval and rejection messages (admin) ────────────────────────────

        /// <summary>{0} = the command letter, "ت" (approve) or "ر" (reject).</summary>
        public const string MsgAdminStatusFormatError = "❌ قالب دستور درست نیست.\n\n" +
                                                         "قالب صحیح:\n" +
                                                         "{0} [شمارهٔ تلفن]\n\n" +
                                                         "ت ۰۹۱۲۱۲۳۴۵۶۷ ← تایید حساب\n" +
                                                         "ر ۰۹۱۲۱۲۳۴۵۶۷ ← رد حساب";

        /// <summary>{0} = phone number; {1} = new status.</summary>
        public const string MsgAdminStatusChanged = "✅ وضعیت حساب تغییر کرد.\n\n" +
                                                     "👤 کاربر: {0}\n" +
                                                     "🔖 وضعیت: {1}";

        /// <summary>{0} = phone number; {1} = current status.</summary>
        public const string MsgAdminStatusUnchanged = "ℹ️ تغییری لازم نبود.\n\n" +
                                                       "کاربر: {0}\n" +
                                                       "وضعیت فعلی: {1}";

        /// <summary>
        /// An account with no Telegram account cannot be changed this way, because this service works
        /// by Telegram id. The only such account is the seeded super-admin, which is not a person and
        /// exists only to hold the initial invitation code.
        /// </summary>
        public const string MsgAdminStatusNoTelegramAccount =
            "❌ این حساب به تلگرام متصل نیست و وضعیتش از این راه قابل تغییر نیست.";

        /// <summary>Reply when a user presses a button they are not permitted to use.</summary>
        public const string MsgNotAuthorized = "شما اجازهٔ این کار را ندارید.";

        /// <summary>
        /// Reply when a button is pressed on a message Telegram no longer sends us — an inline
        /// message, or anything older than 48 hours. Every callback branch edits or deletes that
        /// message, so there is nothing to act on and the customer needs to start again.
        /// </summary>
        public const string MsgCallbackMessageGone =
            "این پیام قدیمی است. لطفاً از منوی اصلی دوباره شروع کنید.";

        /// <summary>
        /// The generic reply when handling a user's message stops on an unexpected error with no
        /// specific response from the handler (issue #99). This is what keeps a raw exception message
        /// from ever reaching a user.
        /// </summary>
        public const string MsgUnexpectedError = "❌ مشکلی پیش آمد.\n\nلطفاً دوباره تلاش کنید. اگر ادامه داشت، به طلافروشی خود اطلاع دهید.";

        /// <summary>
        /// Shown when a message arrives from someone who has not registered yet.
        ///
        /// "/start" is written plainly and without markup so Telegram turns it into a tappable
        /// command. The previous text described the command in prose, which the user could not tap
        /// and had to guess at.
        /// </summary>
        public const string MsgAccountNotFound = "حساب شما پیدا نشد.\n\n" +
                                                 "برای ثبت‌نام /start را بفرستید.";

        // ── Membership request card, sent to admins ────────────────────────────────

        /// <summary>
        /// The card sent to admins to approve or reject a new user.
        ///
        /// Every label is Persian. It used to be half Persian and half English, which displayed as a
        /// jumble: inside right-to-left text, a left-to-right label breaks the line's direction.
        ///
        /// {0} = full name; {1} = phone number; {2} = username; {3} = Telegram id;
        /// {4} = registration date.
        /// </summary>
        public const string MsgMembershipRequest = "📌 درخواست عضویت جدید\n\n" +
                                                    "👤 نام: {0}\n" +
                                                    "📞 شمارهٔ تلفن: {1}\n" +
                                                    "🔖 نام کاربری: {2}\n" +
                                                    "🆔 شناسهٔ تلگرام: {3}\n" +
                                                    "📅 تاریخ ثبت‌نام: {4}";

        /// <summary>Notifies the user of their own role change. {0} = the new role.</summary>
        public const string MsgUserRoleChanged = "ℹ️ سطح دسترسی حساب شما تغییر کرد.\n\n" +
                                                  "نقش تازه: {0}\n\n" +
                                                  "منوی ربات از پیام بعدی مطابق همین نقش نمایش داده می‌شود.";

        /// <summary>
        /// Credit top-up confirmation shown to the admin. It deliberately says "credit" rather than
        /// "balance", because an admin top-up goes into the credit wallet, not the spot balance.
        /// {0} = the asset's Persian name; {1} = amount with unit; {2} = phone number;
        /// {3} = the new credit with unit.
        /// </summary>
        public const string MsgAdminChargeDone = "✅ افزایش اعتبار انجام شد.\n\n" +
                                                 "دارایی: {0}\n" +
                                                 "مقدار افزودن: {1}\n" +
                                                 "کاربر: {2}\n" +
                                                 "اعتبار جدید: {3}";

        /// <summary>
        /// Notifies the user of their own credit top-up.
        /// {0} = the asset's Persian name; {1} = amount with unit; {2} = the new credit with unit.
        /// </summary>
        public const string MsgUserCreditIncreased = "✅ اعتبار حساب شما افزایش یافت.\n\n" +
                                                     "دارایی: {0}\n" +
                                                     "مقدار افزودن: {1}\n" +
                                                     "اعتبار جدید: {2}\n\n" +
                                                     "اکنون می‌توانید سفارش خرید یا فروش ثبت کنید.";

        /// <summary>
        /// Deduction confirmation shown to the admin. Says "credit" throughout, because that is
        /// what the command now reduces — it used to debit the spot balance while its counterpart
        /// credited the credit ledger, and the wording followed the old behaviour.
        /// {0} = the asset's Persian name; {1} = amount with unit; {2} = phone number;
        /// {3} = the new credit with unit.
        /// </summary>
        public const string MsgAdminDeductDone = "✅ کسر از اعتبار انجام شد.\n\n" +
                                                 "دارایی: {0}\n" +
                                                 "مقدار کسر: {1}\n" +
                                                 "کاربر: {2}\n" +
                                                 "اعتبار جدید: {3}";

        /// <summary>
        /// Notifies the user that their credit was reduced. The message named the balance while the
        /// command debited the spot wallet; it names the credit now, because that is what moved.
        /// {0} = the asset's Persian name; {1} = amount with unit; {2} = the new credit with unit.
        /// </summary>
        public const string MsgUserBalanceDeducted = "ℹ️ اعتبار حساب شما کاهش یافت.\n\n" +
                                                     "دارایی: {0}\n" +
                                                     "مقدار کسر: {1}\n" +
                                                     "اعتبار جدید: {2}";

        /// <summary>
        /// Shown to the admin when the same top-up was already recorded, so this send changed
        /// nothing. Deliberately not an error: the charge they wanted did happen, and the figures
        /// are the ones it produced. Sent instead of MsgAdminChargeDone, and the customer gets no
        /// notification at all, because no money moved this time (issue #157).
        ///
        /// <para>
        /// {3} is the credit the wallet holds <b>now</b>, which is not the same number as the one
        /// the original top-up left behind: anything that happened in between is in it. An admin
        /// re-sends precisely when they are unsure what has taken effect, so this is the worst
        /// moment to hand them a figure whose label says "now" when it means "then".
        /// </para>
        /// {0} = the asset's Persian name; {1} = amount with unit; {2} = phone number;
        /// {3} = the credit the wallet holds now, with unit.
        /// </summary>
        public const string MsgAdminChargeAlreadyApplied = "ℹ️ این شارژ پیش‌تر ثبت شده بود و دوباره اعمال نشد.\n\n" +
                                                          "دارایی: {0}\n" +
                                                          "مقدار: {1}\n" +
                                                          "کاربر: {2}\n" +
                                                          "اعتبار کنونی کاربر: {3}\n\n" +
                                                          "اگر قصد داشتید بار دوم هم شارژ کنید، چند دقیقه دیگر دوباره تلاش کنید.";

        /// <summary>
        /// The deduction counterpart of <see cref="MsgAdminChargeAlreadyApplied"/>, and {3} carries
        /// the same meaning: the balance the wallet holds now, not the one that deduction produced.
        /// {0} = the asset's Persian name; {1} = amount with unit; {2} = phone number;
        /// {3} = the balance the wallet holds now, with unit.
        /// </summary>
        public const string MsgAdminDeductAlreadyApplied = "ℹ️ این کسر اعتبار پیش‌تر ثبت شده بود و دوباره اعمال نشد.\n\n" +
                                                          "دارایی: {0}\n" +
                                                          "مقدار: {1}\n" +
                                                          "کاربر: {2}\n" +
                                                          "اعتبار کنونی کاربر: {3}\n\n" +
                                                          "اگر قصد داشتید بار دوم هم کسر کنید، چند دقیقه دیگر دوباره تلاش کنید.";

        /// <summary>{0} = the error reason.</summary>
        public const string MsgAdminOperationFailed = "❌ عملیات انجام نشد.\n\nدلیل: {0}";

        public const string MsgAdminProcessing = "⏳ در حال پردازش…";

        // ── Quotes held by the plausibility band (issue #158) ──────────────────────

        /// <summary>
        /// Asks an admin about a gold price too far from the current quote to publish unattended.
        ///
        /// <para>
        /// Both units, because the admin typed mesghal and the system stores grams. The first
        /// version of this message showed only the per-gram figure, so an admin who had typed
        /// 333,502,239 was asked to confirm 76,989,297.52 — a number they had never seen, in a
        /// message asking them to judge whether it was right.
        /// </para>
        ///
        /// <para>
        /// The prices are in toman; the unit is what they are <em>per</em>. Saying "۷۶٬۹۸۹٬۲۹۷ گرم"
        /// as that first version did states a weight, not a price, and is the same confusion
        /// issue #48 removed from the quote flow.
        /// </para>
        /// {0} = source, in Persian; {1} = the asset's Persian name;
        /// {2} = proposed buy per mesghal; {3} = proposed buy per gram;
        /// {4} = proposed sell per mesghal; {5} = proposed sell per gram;
        /// {6} = previous mid per mesghal; {7} = previous mid per gram;
        /// {8} = deviation percent; {9} = the band percent; {10} = minutes until it expires.
        /// </summary>
        public const string MsgAdminQuoteNeedsApprovalGold =
            "⚠️ مظنهٔ {0} با قیمت فعلی اختلاف زیادی دارد و منتشر نشد.\n\n" +
            "🏷️ دارایی: {1}\n\n" +
            "🟢 شما می‌خرید (مشتری می‌فروشد)\n" +
            "       هر مثقال: {2} تومان\n" +
            "       هر گرم: {3} تومان\n\n" +
            "🔴 شما می‌فروشید (مشتری می‌خرد)\n" +
            "       هر مثقال: {4} تومان\n" +
            "       هر گرم: {5} تومان\n\n" +
            "➖➖➖➖➖➖➖➖➖\n" +
            "میانگین قبلی: هر مثقال {6} تومان / هر گرم {7} تومان\n" +
            "اختلاف: {8}٪ (حد مجاز: {9}٪)\n\n" +
            "اگر این قیمت درست است تأیید کنید. تا آن زمان مظنهٔ قبلی برقرار است.\n" +
            "این درخواست تا {10} دقیقهٔ دیگر معتبر است.";

        /// <summary>
        /// The same question for a symbol with only one unit — a coin, a Bitcoin. There is no
        /// mesghal duality for those: the price the admin types is already per traded unit.
        /// {0} = source; {1} = the asset's Persian name; {2} = proposed buy; {3} = proposed sell;
        /// {4} = previous mid; {5} = the unit these are per; {6} = deviation percent;
        /// {7} = the band percent; {8} = minutes until it expires.
        /// </summary>
        public const string MsgAdminQuoteNeedsApprovalSimple =
            "⚠️ مظنهٔ {0} با قیمت فعلی اختلاف زیادی دارد و منتشر نشد.\n\n" +
            "🏷️ دارایی: {1}\n\n" +
            "🟢 شما می‌خرید (مشتری می‌فروشد): {2} تومان به ازای هر {5}\n" +
            "🔴 شما می‌فروشید (مشتری می‌خرد): {3} تومان به ازای هر {5}\n\n" +
            "➖➖➖➖➖➖➖➖➖\n" +
            "میانگین قبلی: {4} تومان به ازای هر {5}\n" +
            "اختلاف: {6}٪ (حد مجاز: {7}٪)\n\n" +
            "اگر این قیمت درست است تأیید کنید. تا آن زمان مظنهٔ قبلی برقرار است.\n" +
            "این درخواست تا {8} دقیقهٔ دیگر معتبر است.";

        /// <summary>Shown in place of a previous mid on a symbol that has never had a quote.</summary>
        public const string MsgNoPreviousQuote = "—";

        public const string MsgQuoteSourceAuto = "خودکار";
        public const string MsgQuoteSourceManual = "دستی";

        /// <summary>
        /// Confirms an approval, naming the symbol and the prices in both units — the same shape the
        /// question used. Several questions can be open at once, and the server's own sentence
        /// identifies none of them.
        /// {0} = the asset's Persian name; {1} = buy per mesghal; {2} = buy per gram;
        /// {3} = sell per mesghal; {4} = sell per gram.
        /// </summary>
        public const string MsgAdminQuoteApprovedGold = "✅ مظنه تأیید و منتشر شد.\n\n" +
                                                       "🏷️ دارایی: {0}\n\n" +
                                                       "🟢 شما می‌خرید\n" +
                                                       "       هر مثقال: {1} تومان\n" +
                                                       "       هر گرم: {2} تومان\n\n" +
                                                       "🔴 شما می‌فروشید\n" +
                                                       "       هر مثقال: {3} تومان\n" +
                                                       "       هر گرم: {4} تومان\n\n" +
                                                       "از این پس مشتریان روی همین قیمت‌ها معامله می‌کنند.";

        /// <summary>
        /// The single-unit counterpart, for a coin or a Bitcoin.
        /// {0} = the asset's Persian name; {1} = buy; {2} = sell; {3} = the unit these are per.
        /// </summary>
        public const string MsgAdminQuoteApprovedSimple = "✅ مظنه تأیید و منتشر شد.\n\n" +
                                                         "🏷️ دارایی: {0}\n\n" +
                                                         "🟢 شما می‌خرید: {1} تومان به ازای هر {3}\n" +
                                                         "🔴 شما می‌فروشید: {2} تومان به ازای هر {3}\n\n" +
                                                         "از این پس مشتریان روی همین قیمت‌ها معامله می‌کنند.";

        /// <summary>{0} = the asset's Persian name.</summary>
        public const string MsgAdminQuoteRejected = "❌ مظنهٔ {0} منتشر نشد و مظنهٔ قبلی برقرار ماند.";

        /// <summary>
        /// Shown when the button no longer works: somebody else answered first, or the price sat
        /// long enough to go stale. {0} = the reason, from the server.
        /// </summary>
        public const string MsgAdminQuoteResolveFailed = "ℹ️ این درخواست دیگر قابل پاسخ نیست.\n\n{0}";

        // ── User approval and rejection ─────────────────────────────────────────────

        /// <summary>Appended to the registration request message once approved.</summary>
        public const string MsgAdminApprovedSuffix = "\n\n✅ این کاربر تایید شد.";

        /// <summary>Appended to the registration request message once rejected.</summary>
        public const string MsgAdminRejectedSuffix = "\n\n❌ این کاربر رد شد.";

        public const string MsgUserApproved = "🎉 حساب شما تایید شد.\n\n" +
                                              "اکنون می‌توانید قیمت‌های بازار را ببینید و سفارش خرید یا فروش ثبت کنید.\n" +
                                              "برای دریافت اعتبار، با طلافروشی خود تماس بگیرید.";

        public const string MsgUserRejected = "❌ درخواست ثبت‌نام شما تایید نشد.\n\n" +
                                              "برای اطلاع از دلیل، با طلافروشی خود تماس بگیرید.";

        /// <summary>{0} = how many orders were cancelled.</summary>
        public const string MsgAdminPreviousPricesCancelled = "✅ {0} قیمت قبلی لغو شد.";

        /// <summary>{0} = the error reason.</summary>
        public const string MsgAdminCancelPreviousFailed = "⚠️ لغو قیمت‌های قبلی با خطا مواجه شد.\n\nدلیل: {0}";

        /// <summary>
        /// Result of an admin submitting buy and sell prices.
        /// The prices entered are per mesghal, and the per-gram equivalent is shown as well so no
        /// ambiguity about the unit remains.
        /// {0} = the asset's Persian name; {1} = buy status; {2} = sell status;
        /// {3} = buy price per mesghal; {4} = buy price per gram;
        /// {5} = sell price per mesghal; {6} = sell price per gram.
        /// </summary>
        public const string MsgAdminPriceSubmitResult = "📊 نتیجهٔ ثبت قیمت\n\n" +
                                                        "دارایی: {0}\n\n" +
                                                        "🟢 سفارش خرید: {1}\n" +
                                                        "🔴 سفارش فروش: {2}\n\n" +
                                                        "📋 قیمت‌های ثبت‌شده:\n" +
                                                        "خرید — هر مثقال: {3} تومان\n" +
                                                        "خرید — هر گرم: {4} تومان\n" +
                                                        "فروش — هر مثقال: {5} تومان\n" +
                                                        "فروش — هر گرم: {6} تومان";

        public const string MsgAdminOrderOk = "✅ ثبت شد";

        /// <summary>{0} = why it failed.</summary>
        public const string MsgAdminOrderFailed = "❌ ثبت نشد — {0}";

        /// <summary>
        /// Result of publishing a quote (issue #48).
        ///
        /// It deliberately never says "order": publishing a quote no longer places anything in the
        /// book or locks collateral. The previous text announced that a buy order had been placed,
        /// which was the exact ambiguity that confused admins — they were announcing today's price,
        /// not placing an order.
        ///
        /// Each side names <b>both</b> parties — "you buy (the customer sells)". The previous
        /// wording was "خرید شما" alone, which is ambiguous to the person reading it: an admin
        /// setting prices can just as easily read "your buy" as "the price your customers buy
        /// at", and those are the two opposite numbers. Naming both parties on the same line
        /// removes the reading entirely rather than relying on the admin to hold the
        /// convention in their head.
        ///
        /// The per-gram figures are marked as derived, because the admin typed only the
        /// per-mesghal ones and should not wonder where the others came from.
        ///
        /// The margin line is included because it is the number a price-setter actually
        /// decides, and it is the one they cannot compute at a glance from the other four.
        ///
        /// {0} = asset name; {1} = buy price per mesghal; {2} = buy price per gram;
        /// {3} = sell price per mesghal; {4} = sell price per gram; {5} = margin per mesghal.
        /// </summary>
        public const string MsgAdminQuotePublished = "📊 مظنه منتشر شد\n\n" +
                                                     "🏷️ دارایی: {0}\n\n" +
                                                     "🟢 شما می‌خرید (مشتری می‌فروشد)\n" +
                                                     "       هر مثقال: {1} تومان\n" +
                                                     "       هر گرم: {2} تومان\n\n" +
                                                     "🔴 شما می‌فروشید (مشتری می‌خرد)\n" +
                                                     "       هر مثقال: {3} تومان\n" +
                                                     "       هر گرم: {4} تومان\n\n" +
                                                     "➖➖➖➖➖➖➖➖➖\n" +
                                                     "📈 حاشیهٔ شما: {5} تومان در هر مثقال\n\n" +
                                                     "از این پس مشتریان روی همین قیمت‌ها معامله می‌کنند.";

        /// <summary>
        /// Same confirmation as <see cref="MsgAdminQuotePublished"/>, for a symbol with no
        /// mesghal/gram duality (the coin and Bitcoin quoted with the addition of these — issue
        /// tracked in the conversation, not a numbered GitHub issue). The admin's number is
        /// already the per-unit price, so there is nothing to derive and show twice.
        ///
        /// {0} = asset name; {1} = buy price; {2} = sell price; {3} = margin; {4} = unit.
        /// </summary>
        public const string MsgAdminQuotePublishedSimple = "📊 مظنه منتشر شد\n\n" +
                                                     "🏷️ دارایی: {0}\n\n" +
                                                     "🟢 شما می‌خرید (مشتری می‌فروشد): {1} تومان به ازای هر {4}\n" +
                                                     "🔴 شما می‌فروشید (مشتری می‌خرد): {2} تومان به ازای هر {4}\n\n" +
                                                     "➖➖➖➖➖➖➖➖➖\n" +
                                                     "📈 حاشیهٔ شما: {3} تومان به ازای هر {4}\n\n" +
                                                     "از این پس مشتریان روی همین قیمت‌ها معامله می‌کنند.";

        /// <summary>{0} = why it failed.</summary>
        public const string MsgAdminQuoteFailed = "❌ انتشار مظنه انجام نشد.\n\nدلیل: {0}";

        // ── Automatic quotes (issue #90) ────────────────────────────────────────

        public const string MsgAutoQuoteSpreadFormatError = "❌ قالب دستور درست نیست.\n\n" +
                                                             "قالب صحیح:\n" +
                                                             "اسپرد [درصد] [نماد اختیاری]\n\n" +
                                                             "نمونه: اسپرد 0.5\n" +
                                                             "نمونه: اسپرد 0.5 سکه";

        /// <summary>{0} = the symbol's Persian name; {1} = the new spread percentage.</summary>
        public const string MsgAutoQuoteSpreadUpdated = "✅ اسپرد مظنهٔ اتومات {0} روی {1}٪ تنظیم شد.";

        /// <summary>{0} = the symbol's Persian name; {1} = the error reason.</summary>
        public const string MsgAutoQuoteSpreadFailed = "❌ تنظیم اسپرد مظنهٔ اتومات {0} انجام نشد.\n\nدلیل: {1}";

        public const string MsgAutoQuoteToggleFormatError = "❌ قالب دستور درست نیست.\n\n" +
                                                             "قالب صحیح:\n" +
                                                             "اتومات روشن\n" +
                                                             "اتومات خاموش\n\n" +
                                                             "توجه: هر نماد تنظیم اتومات جدا دارد — بدون کلیدواژه یعنی فقط آبشده.\n" +
                                                             "با نماد دیگر: اتومات روشن سکه";

        /// <summary>{0} = the symbol's Persian name.</summary>
        public const string MsgAutoQuoteEnabled = "✅ مظنهٔ اتومات {0} روشن شد.";

        /// <summary>{0} = the symbol's Persian name.</summary>
        public const string MsgAutoQuoteDisabled = "⏸️ مظنهٔ اتومات {0} خاموش شد.";

        /// <summary>{0} = the symbol's Persian name; {1} = the error reason.</summary>
        public const string MsgAutoQuoteToggleFailed = "❌ روشن/خاموش‌کردن مظنهٔ اتومات {0} انجام نشد.\n\nدلیل: {1}";

        /// <summary>
        /// Shown when the symbol written after a spread, automatic-quote, symbol or paired-price
        /// command is not recognised.
        /// </summary>
        public const string MsgAdminUnknownQuoteSymbol = "❌ این نماد شناخته‌شده نیست.\n\n" +
                                                          "نمادهای معتبر: (خالی = آبشده)، سکه، بیت";

        // ── Symbol enable/disable ───────────────────────────────────────────────

        public const string MsgSymbolActiveFormatError = "❌ قالب دستور درست نیست.\n\n" +
                                                          "قالب صحیح:\n" +
                                                          "نماد فعال\n" +
                                                          "نماد غیرفعال\n\n" +
                                                          "توجه: بدون کلیدواژه یعنی فقط آبشده.\n" +
                                                          "با نماد دیگر: نماد فعال سکه";

        /// <summary>{0} = the symbol's Persian name.</summary>
        public const string MsgSymbolActivated = "✅ نماد {0} فعال شد و برای مشتریان قابل‌معامله است.";

        /// <summary>{0} = the symbol's Persian name.</summary>
        public const string MsgSymbolDeactivated = "⏸️ نماد {0} غیرفعال شد.";

        /// <summary>{0} = the symbol's Persian name; {1} = the error reason.</summary>
        public const string MsgSymbolActiveFailed = "❌ فعال/غیرفعال‌کردن نماد {0} انجام نشد.\n\nدلیل: {1}";

        /// <summary>
        /// Shown when the bot restarts without a version change, which is the ordinary case.
        /// {0} = the current version.
        /// </summary>
        public const string MsgBotBackOnline = "✅ ربات دوباره در دسترس است.\n\n" +
                                               "نسخه: {0}\n" +
                                               "اگر مشکلی مشاهده کردید، لطفاً به ما اطلاع دهید 🙏";

        /// <summary>
        /// Only when the version has genuinely changed.
        /// {0} = the new version; {1} = the changelog summary, or empty if there is none.
        /// </summary>
        public const string MsgBotUpdated = "🚀 ربات به نسخه جدید آپدیت شد!\n\n" +
                                            "نسخه فعلی: {0}\n" +
                                            "{1}" +
                                            "اگر پیشنهاد یا مشکلی داشتید، لطفاً به ما اطلاع دهید 🙏";
    }

    /// <summary>
    /// A summary of each version's notable changes, for the update message.
    /// The key must match IVersionService.GetCurrentVersion exactly, for example "1.1.0".
    /// A version with no entry here sends the update message without a changelog.
    /// Add an entry here whenever VersionPrefix (Directory.Build.props) is raised.
    /// </summary>
    public static class ReleaseNotes
    {
        private static readonly Dictionary<string, string[]> Notes = new()
        {
            ["1.2.0"] = new[]
            {
                "ساخت مطمئن‌تر کیف‌پول‌ها هنگام ثبت‌نام",
                "رفع چند خطای داخلی در ارتباط میان سرویس‌ها",
            },
            ["1.1.0"] = new[]
            {
                "امکان معامله سکه تمام بهار آزادی و بیت‌کوین، علاوه بر طلای آبشده",
                "نمایش صحیح‌تر تاریخچه معاملات (نوع خرید/فروش و تاریخ)",
                "پایداری بیشتر در ثبت و تسویه معاملات",
            },
        };

        /// <summary>
        /// Returns the changelog as text ready to interpolate into the message, or an empty string
        /// if nothing is recorded for this version.
        /// </summary>
        public static string GetSummaryFor(string version)
        {
            if (!Notes.TryGetValue(version, out var lines) || lines.Length == 0)
                return string.Empty;

            return "\n🔹 مهم‌ترین تغییرات:\n" +
                   string.Join("\n", lines.Select(line => $"• {line}")) +
                   "\n\n";
        }
    }
    public static class InlineCallBackData
    {
        /// <summary>
        /// Reached after the spot button on the main menu has been pressed.
        /// BotTexts.BtnSpot
        /// The inline buy button.
        /// </summary>
        public const string buy_spot = "buy_spot";
        public const string sell_spot = "sell_spot";
        public const string trading_spot = "trading_spot";
        public const string trading_futures = "trading_futures";
        public const string order_buy = "order_buy";
        public const string order_sell = "order_sell";
        public const string confirm_order = "confirm_order";
        public const string cancel_order = "cancel_order";
        public const string charge_card = "charge_card";
        public const string charge_bank = "charge_bank";
        public const string back_to_main = "back_to_main";
        
        public const string confirm_market_order = "confirm_market_order";

        /// <summary>
        /// Answering a quote the plausibility band held back (issue #158). The pending quote's id
        /// is appended after the colon, because the message may sit in Telegram for minutes and
        /// several symbols can be waiting at once — the button has to say which one it answers.
        /// </summary>
        public const string approve_quote = "approve_quote";
        public const string reject_quote = "reject_quote";

        public const string AssetPrefix = "asset";
        public const string BackToMain = "back_to_main";
    }
}
