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
        // BtnFutures حذف شد: بازار آتی وجود ندارد و هیچ handlerی برای این دکمه نبود.
        public const string BtnAccounting = "📊 حسابداری";
        public const string BtnHelp = "❓ راهنما";
        public const string BtnBack = "🔙 بازگشت";
        public const string BtnHistory = "📋 تاریخچه";
        public const string BtnOrderHistory = "📋 تاریخچه سفارشات";
        public const string BtnTradeHistory = "📊 تاریخچه معاملات";
        public const string BtnActiveOrders = "⚡ سفارشات فعال";
        public const string BtnWalletsBalance = "💵 موجودی";
        public const string BtnWallet = "💳 کیف پول";
        public const string BtnSharePhone = "📱 اشتراک‌گذاری شماره تلفن";
        public const string BtnConfirm = "✅ تایید";
        public const string BtnCancel = "❌ لغو";
        /// <summary>
        /// Place Order همان مفهوم Make Order را دارد و به معنای ثبت سفارش است
        /// </summary>
        public const string BtnSpotCreateOrder = "📝 ثبت سفارش نقدی";
        /// <summary>
        /// ثبت قیمت نقدی با ثبت سفارش نقدی هیچ فرقی نمیکند
        /// بخاطر اینکه برای مصطفی قابل درکتر باشه این اسمو روی دکمه ادمین نمایش میدم
        /// </summary>
        public const string BtnSpotSubmitPrice = "📝 ثبت قیمت نقدی";
        public const string BtnSpotMarket = "📈 بازار نقدی";
        public const string BtnSpotMarketBuy = "🛒 خرید نقدی";
        public const string BtnSpotMarketSell = "🛍️ فروش نقدی";
    }
    public static class BotMsgs
    {
        // ────────────────────────────────────────────────────────────────────────────
        // قواعد نوشتن پیام‌ها:
        // • تمام متن فارسی باشد؛ از کلمه یا نماد لاتین در متن استفاده نشود.
        // • هر عدد باید با PersianFormat قالب‌بندی شود (ارقام فارسی + محافظ راست‌به‌چپ)
        //   تا در تلگرام جابه‌جا نشود. عدد خام را مستقیم در پیام قرار ندهید.
        // • نماد معاملاتی با PersianFormat.Symbol به نام فارسی تبدیل شود («آبشده/تومان»).
        // • پیام باید بگوید «چه شد» و «حالا چه کار کنم»؛ از ابهام پرهیز شود.
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

        /// <summary>{0} = نام کاربر. حساب ثبت شده ولی مدیر هنوز تایید نکرده است.</summary>
        public const string MsgAccountNotApproved = "{0} عزیز، حساب کاربری شما هنوز فعال نشده است.\n\n" +
                                                    "حساب شما در انتظار تایید مدیر است؛ به‌محض تایید به شما اطلاع می‌دهیم.";

        public const string MsgSelectTradingType = "نوع معامله را انتخاب کنید:";

        public const string MsgSelectAsset = "دارایی مورد نظر را انتخاب کنید:";

        public const string MsgEnterAmount = "مقدار مورد نظر را وارد کنید:";

        /// <summary>
        /// درخواست قیمت برای طلای آبشده. واحد باید صریح باشد: قیمت «یک مثقال» به تومان.
        /// پیام قبلی («لطفا قیمت رو وارد کنید») نمی‌گفت قیمت چه چیزی و با چه واحدی.
        /// </summary>
        public const string MsgEnterPriceGold = "قیمت یک مثقال طلای آبشده را به تومان وارد کنید:\n\n" +
                                               "نمونه: ۷۹۰۰۰۰۰۰";

        /// <summary>درخواست قیمت برای سایر دارایی‌ها. {0} = نام فارسی دارایی</summary>
        public const string MsgEnterPrice = "قیمت هر واحد {0} را به تومان وارد کنید:";

        /// <summary>
        /// تایید سفارش. {0}=نماد فارسی، {1}=نوع سفارش (خرید/فروش)،
        /// {2}=مقدار همراه واحد، {3}=قیمت هر واحد، {4}=مبلغ کل
        /// همهٔ مقادیر باید از قبل با PersianFormat قالب‌بندی شده باشند.
        /// </summary>
        public const string MsgOrderConfirmation = "📋 تایید سفارش\n\n" +
                                                  "دارایی: {0}\n" +
                                                  "نوع سفارش: {1}\n" +
                                                  "مقدار: {2}\n" +
                                                  "قیمت هر واحد: {3} تومان\n" +
                                                  "مبلغ کل: {4} تومان\n\n" +
                                                  "آیا این سفارش را تایید می‌کنید؟";

        /// <summary>
        /// تایید سفارش طلای آبشده.
        /// قیمت «هر مثقال» همان عددی است که کاربر وارد کرده و قیمت «هر گرم» معادل
        /// محاسبه‌شدهٔ آن است. نمایش هر دو ضروری است، وگرنه کاربر عددی متفاوت از
        /// ورودی خود می‌بیند و گمان می‌کند اشتباه ثبت شده است.
        /// {0}=دارایی، {1}=نوع سفارش، {2}=مقدار با واحد،
        /// {3}=قیمت هر مثقال، {4}=قیمت هر گرم، {5}=مبلغ کل
        /// </summary>
        public const string MsgOrderConfirmationGold = "📋 تایید سفارش\n\n" +
                                                       "دارایی: {0}\n" +
                                                       "نوع سفارش: {1}\n" +
                                                       "مقدار: {2}\n" +
                                                       "قیمت هر مثقال: {3} تومان\n" +
                                                       "قیمت هر گرم: {4} تومان\n" +
                                                       "مبلغ کل: {5} تومان\n\n" +
                                                       "آیا این سفارش را تایید می‌کنید؟";

        /// <summary>{0} = توضیح دلیل، در صورت وجود</summary>
        public const string MsgInsufficientBalance = "❌ موجودی شما برای این سفارش کافی نیست.\n\n" +
                                                     "{0}\n\n" +
                                                     "برای افزایش موجودی یا اعتبار، با طلافروشی خود تماس بگیرید.";

        public const string MsgOrderSuccess = "✅ سفارش شما ثبت شد.\n\n" +
                                              "به‌محض انجام معامله، نتیجه به شما اطلاع داده می‌شود.\n" +
                                              "سفارش‌های در جریان را از «سفارشات فعال» ببینید.";

        /// <summary>{0} = دلیل خطا</summary>
        public const string MsgOrderFailed = "❌ سفارش شما ثبت نشد.\n\nدلیل: {0}\n\nلطفاً دوباره تلاش کنید.";

        /// <summary>
        /// قیمت‌های بازار. {0}=نماد فارسی، {1}=بهترین خرید، {2}=بهترین فروش، {3}=اختلاف
        /// همهٔ مقادیر از قبل قالب‌بندی‌شده.
        /// </summary>
        public const string MsgMarketPrices = "📊 قیمت‌های بازار\n\n" +
                                              "دارایی: {0}\n" +
                                              "بهترین قیمت خرید: {1} تومان\n" +
                                              "بهترین قیمت فروش: {2} تومان\n" +
                                              "اختلاف خرید و فروش: {3} تومان\n\n" +
                                              "عملیات مورد نظر را انتخاب کنید:";

        /// <summary>{0} = نام فارسی دارایی همراه واحد آن</summary>
        public const string MsgEnterQuantity = "مقدار {0} را وارد کنید:";

        // ── نمایش موجودی ────────────────────────────────────────────────────────────

        public const string MsgBalanceHeader = "💰 موجودی حساب شما\n\n";

        /// <summary>
        /// یک ردیف موجودی. {0}=نام فارسی دارایی، {1}=موجودی آزاد با واحد، {2}=مقدار درگیر سفارش با واحد
        /// </summary>
        public const string MsgBalanceRow = "▪️ {0}\n" +
                                           "   موجودی آزاد: {1}\n" +
                                           "   درگیر در سفارش: {2}\n";

        /// <summary>
        /// وقتی موجودی آزاد منفی است. در مدل اعتباری این حالت طبیعی است (کاربر با اعتبار
        /// معامله کرده) اما بدون توضیح، عدد منفی برای کاربر گیج‌کننده است.
        /// {0} = مبلغ بدهی با واحد
        /// </summary>
        public const string MsgBalanceDebtNote = "   ⚠️ بدهی شما: {0}\n";

        public const string MsgBalanceFooter = "\nبرای افزایش اعتبار با طلافروشی خود تماس بگیرید.";

        /// <summary>{0} = دلیل خطا</summary>
        public const string MsgActiveOrdersFailed = "❌ دریافت سفارش‌های فعال انجام نشد.\n\nدلیل: {0}";

        public const string MsgNoWallet = "برای شما هنوز حسابی ثبت نشده است.\n\n" +
                                          "برای فعال‌سازی حساب و دریافت اعتبار، با طلافروشی خود تماس بگیرید.";

        /// <summary>راهنمای کاربر عادی. راهنمای مدیر (MsgAdminHelp) در صورت نیاز به آن اضافه می‌شود.</summary>
        public const string MsgUserHelp = "❓ راهنما\n\n" +
                                         "📈 بازار نقدی: مشاهده قیمت‌ها و ثبت سفارش خرید یا فروش\n" +
                                         "📊 حسابداری: مشاهده موجودی، سفارش‌ها و تاریخچه معاملات\n" +
                                         "⚡ سفارشات فعال: سفارش‌های در جریان و امکان لغو آن‌ها\n\n" +
                                         "نکته: قیمت‌های بازار بر حسب «هر مثقال» و مقدار طلا بر حسب «گرم» است.\n\n";

        /// <summary>در انتهای راهنما اضافه می‌شود.</summary>
        public const string MsgSupportFooter = "برای افزایش موجودی یا هر سوال دیگر، با طلافروشی خود تماس بگیرید.";

        /// <summary>
        /// روش افزایش موجودی. در حال حاضر درگاه پرداخت وجود ندارد و شارژ حساب توسط
        /// طلافروشی انجام می‌شود؛ پیام قبلی شماره حساب و کارت نمونه (غیرواقعی) نشان
        /// می‌داد که گمراه‌کننده و خطرناک بود.
        /// </summary>
        public const string MsgChargeInfo = "💰 افزایش موجودی\n\n" +
                                           "افزایش موجودی و اعتبار حساب شما توسط طلافروشی انجام می‌شود.\n\n" +
                                           "برای شارژ حساب، با طلافروشی خود تماس بگیرید.\n" +
                                           "پس از تایید، موجودی شما در همین ربات به‌روز می‌شود.";

        /// <summary>
        /// بهترین قیمت‌های خرید و فروش بازار. {0}=بهترین خرید، {1}=بهترین فروش
        /// (هر دو قالب‌بندی‌شده و بر حسب «هر مثقال»).
        /// ذکر واحد «مثقال» ضروری است، چون قیمت‌ها در جای دیگر بر حسب «گرم» نمایش داده می‌شوند.
        /// </summary>
        public const string MsgBestPrices = "📊 بهترین قیمت‌های بازار (هر مثقال)\n\n" +
                                           "💰 خرید: {0}\n" +
                                           "💸 فروش: {1}";

        /// <summary>
        /// وقتی در آن سمت بازار هیچ سفارشی ثبت نشده باشد. نمایش «۰ تومان» گمراه‌کننده
        /// است، چون صفر یعنی قیمت صفر، نه نبودن قیمت.
        /// </summary>
        public const string MsgPriceNotAvailable = "فعلاً سفارشی ثبت نشده";

        /// <summary>آرگومان‌ها مثل MsgOrderConfirmation</summary>
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
                                          "نمونه: ش ۰۹۱۲۱۲۳۴۵۶۷ ۵۰۰۰۰۰ تومان\n\n" +
                                          "کسر از موجودی:\n" +
                                          "د [شمارهٔ تلفن] [مقدار] [نوع]\n" +
                                          "نمونه: د ۰۹۱۲۱۲۳۴۵۶۷ ۵۰۰۰۰۰ تومان\n\n" +
                                          "فهرست کاربران: ک [جستجو]\n" +
                                          "موجودی کاربر: م [شمارهٔ تلفن]\n" +
                                          "سفارش‌های فعال کاربر: س [شمارهٔ تلفن]\n\n" +
                                          "ثبت همزمان قیمت خرید و فروش (هر مثقال):\n" +
                                          "[قیمت خرید]-[قیمت فروش]\n" +
                                          "نمونه: ۷۹۰۰۰۰۰۰-۷۹۵۰۰۰۰۰\n\n" +
                                          "نکته: مقدارها را می‌توانید با ارقام فارسی یا انگلیسی وارد کنید.";

        // ── پیام‌های افزایش اعتبار و کسر موجودی (مدیر) ──────────────────────────────

        /// <summary>{0} = فهرست نام‌های فارسی ارزهای مجاز</summary>
        public const string MsgAdminChargeFormatError = "❌ قالب دستور درست نیست.\n\n" +
                                                        "قالب صحیح:\n" +
                                                        "ش [شمارهٔ تلفن] [مقدار] [نوع]\n\n" +
                                                        "نمونه: ش ۰۹۱۲۱۲۳۴۵۶۷ ۵۰۰۰۰۰ تومان\n\n" +
                                                        "نوع‌های مجاز: {0}";

        /// <summary>{0} = فهرست نام‌های فارسی ارزهای مجاز</summary>
        public const string MsgAdminDeductFormatError = "❌ قالب دستور درست نیست.\n\n" +
                                                        "قالب صحیح:\n" +
                                                        "د [شمارهٔ تلفن] [مقدار] [نوع]\n\n" +
                                                        "نمونه: د ۰۹۱۲۱۲۳۴۵۶۷ ۵۰۰۰۰۰ تومان\n\n" +
                                                        "نوع‌های مجاز: {0}";

        /// <summary>{0} = ورودی نامعتبر کاربر، {1} = فهرست نام‌های فارسی مجاز</summary>
        public const string MsgAdminInvalidCurrency = "❌ نوع «{0}» شناسایی نشد.\n\nنوع‌های مجاز: {1}";

        public const string MsgAdminUserNotFound = "❌ کاربری با این شمارهٔ تلفن پیدا نشد.";

        /// <summary>
        /// تاییدِ افزایش اعتبار برای مدیر. عمداً «اعتبار» گفته شده نه «موجودی»، چون
        /// شارژ مدیر به کیف پول اعتباری واریز می‌شود نه موجودی نقدی.
        /// {0}=نام فارسی دارایی، {1}=مقدار با واحد، {2}=شمارهٔ تلفن، {3}=اعتبار جدید با واحد
        /// </summary>
        public const string MsgAdminChargeDone = "✅ افزایش اعتبار انجام شد.\n\n" +
                                                 "دارایی: {0}\n" +
                                                 "مقدار افزودن: {1}\n" +
                                                 "کاربر: {2}\n" +
                                                 "اعتبار جدید: {3}";

        /// <summary>
        /// اطلاع‌رسانی افزایش اعتبار به خودِ کاربر.
        /// {0}=نام فارسی دارایی، {1}=مقدار با واحد، {2}=اعتبار جدید با واحد
        /// </summary>
        public const string MsgUserCreditIncreased = "✅ اعتبار حساب شما افزایش یافت.\n\n" +
                                                     "دارایی: {0}\n" +
                                                     "مقدار افزودن: {1}\n" +
                                                     "اعتبار جدید: {2}\n\n" +
                                                     "اکنون می‌توانید سفارش خرید یا فروش ثبت کنید.";

        /// <summary>
        /// تاییدِ کسر برای مدیر.
        /// {0}=نام فارسی دارایی، {1}=مقدار با واحد، {2}=شمارهٔ تلفن، {3}=موجودی جدید با واحد
        /// </summary>
        public const string MsgAdminDeductDone = "✅ کسر از موجودی انجام شد.\n\n" +
                                                 "دارایی: {0}\n" +
                                                 "مقدار کسر: {1}\n" +
                                                 "کاربر: {2}\n" +
                                                 "موجودی جدید: {3}";

        /// <summary>
        /// اطلاع‌رسانی کسر به خودِ کاربر. پیام قبلی اشتباهاً «شارژ» می‌گفت.
        /// {0}=نام فارسی دارایی، {1}=مقدار با واحد، {2}=موجودی جدید با واحد
        /// </summary>
        public const string MsgUserBalanceDeducted = "ℹ️ از موجودی حساب شما کسر شد.\n\n" +
                                                     "دارایی: {0}\n" +
                                                     "مقدار کسر: {1}\n" +
                                                     "موجودی جدید: {2}";

        /// <summary>{0} = دلیل خطا</summary>
        public const string MsgAdminOperationFailed = "❌ عملیات انجام نشد.\n\nدلیل: {0}";

        public const string MsgAdminProcessing = "⏳ در حال پردازش…";

        // ── تایید و رد کاربر ────────────────────────────────────────────────────────

        /// <summary>روی پیام درخواست ثبت‌نام، پس از تایید، افزوده می‌شود.</summary>
        public const string MsgAdminApprovedSuffix = "\n\n✅ این کاربر تایید شد.";

        /// <summary>روی پیام درخواست ثبت‌نام، پس از رد، افزوده می‌شود.</summary>
        public const string MsgAdminRejectedSuffix = "\n\n❌ این کاربر رد شد.";

        public const string MsgUserApproved = "🎉 حساب شما تایید شد.\n\n" +
                                              "اکنون می‌توانید قیمت‌های بازار را ببینید و سفارش خرید یا فروش ثبت کنید.\n" +
                                              "برای دریافت اعتبار، با طلافروشی خود تماس بگیرید.";

        public const string MsgUserRejected = "❌ درخواست ثبت‌نام شما تایید نشد.\n\n" +
                                              "برای اطلاع از دلیل، با طلافروشی خود تماس بگیرید.";

        /// <summary>{0} = تعداد سفارش لغوشده</summary>
        public const string MsgAdminPreviousPricesCancelled = "✅ {0} قیمت قبلی لغو شد.";

        /// <summary>{0} = دلیل خطا</summary>
        public const string MsgAdminCancelPreviousFailed = "⚠️ لغو قیمت‌های قبلی با خطا مواجه شد.\n\nدلیل: {0}";

        /// <summary>
        /// نتیجهٔ ثبت قیمت خرید و فروش توسط مدیر.
        /// قیمت‌های وارد‌شده بر حسب «هر مثقال» هستند و معادل «هر گرم» نیز نمایش داده
        /// می‌شود تا هیچ ابهامی در واحد باقی نماند.
        /// {0}=نام فارسی دارایی، {1}=وضعیت خرید، {2}=وضعیت فروش،
        /// {3}=قیمت خرید هر مثقال، {4}=قیمت خرید هر گرم،
        /// {5}=قیمت فروش هر مثقال، {6}=قیمت فروش هر گرم
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

        /// <summary>{0} = دلیل ناموفق بودن</summary>
        public const string MsgAdminOrderFailed = "❌ ثبت نشد — {0}";

        /// <summary>
        /// نتیجهٔ انتشار مظنه (issue #48).
        ///
        /// عمداً از «سفارش» حرفی نمی‌زند: انتشار مظنه دیگر سفارشی در دفتر نمی‌گذارد و
        /// وثیقه‌ای قفل نمی‌کند. متن قبلی «سفارش خرید ثبت شد» را می‌گفت، که همان ابهامی
        /// بود که مدیر را گیج می‌کرد — او «قیمت امروز» را اعلام می‌کرد، نه سفارش.
        ///
        /// جملهٔ آخر عمداً هست تا مدیر بداند چرا دیگر چیزی از موجودی‌اش قفل نمی‌شود.
        ///
        /// {0}=نام دارایی، {1}=قیمت خرید هر مثقال، {2}=قیمت خرید هر گرم،
        /// {3}=قیمت فروش هر مثقال، {4}=قیمت فروش هر گرم
        /// </summary>
        public const string MsgAdminQuotePublished = "📊 مظنه منتشر شد\n\n" +
                                                     "دارایی: {0}\n\n" +
                                                     "🟢 خرید شما — هر مثقال: {1} تومان\n" +
                                                     "        هر گرم: {2} تومان\n\n" +
                                                     "🔴 فروش شما — هر مثقال: {3} تومان\n" +
                                                     "        هر گرم: {4} تومان\n\n" +
                                                     "از این پس مشتریان روی همین قیمت‌ها معامله می‌کنند.\n" +
                                                     "هیچ مبلغی از موجودی شما قفل نشد؛ وثیقه فقط در لحظهٔ هر معامله و به اندازهٔ همان معامله درگیر می‌شود.";

        /// <summary>{0} = دلیل ناموفق بودن</summary>
        public const string MsgAdminQuoteFailed = "❌ انتشار مظنه انجام نشد.\n\nدلیل: {0}";

        /// <summary>{0} = دلیل خطا</summary>
        public const string MsgAdminPriceSubmitError = "❌ ثبت قیمت‌ها انجام نشد.\n\nدلیل: {0}";

        /// <summary>
        /// وقتی ربات بدون تغییر نسخه دوباره اجرا می‌شود (ری‌استارت معمولی).
        /// {0} = نسخه فعلی
        /// </summary>
        public const string MsgBotBackOnline = "✅ ربات دوباره در دسترس است.\n\n" +
                                               "نسخه: {0}\n" +
                                               "اگر مشکلی مشاهده کردید، لطفاً به ما اطلاع دهید 🙏";

        /// <summary>
        /// فقط وقتی نسخه واقعاً تغییر کرده باشد.
        /// {0} = نسخه جدید، {1} = فهرست خلاصه تغییرات (در صورت وجود، وگرنه خالی)
        /// </summary>
        public const string MsgBotUpdated = "🚀 ربات به نسخه جدید آپدیت شد!\n\n" +
                                            "نسخه فعلی: {0}\n" +
                                            "{1}" +
                                            "اگر پیشنهاد یا مشکلی داشتید، لطفاً به ما اطلاع دهید 🙏";
    }

    /// <summary>
    /// خلاصه تغییرات مهم هر نسخه، برای نمایش در پیام آپدیت.
    /// کلید باید دقیقاً با خروجی IVersionService.GetCurrentVersion یکی باشد (مثل "1.1.0").
    /// اگر برای نسخه‌ای کلیدی وجود نداشته باشد، پیام آپدیت بدون فهرست تغییرات ارسال می‌شود.
    /// هنگام بالا بردن AssemblyVersion، یک ورودی اینجا اضافه کنید.
    /// </summary>
    public static class ReleaseNotes
    {
        private static readonly Dictionary<string, string[]> Notes = new()
        {
            // مثال:
            // ["1.1.0"] = new[]
            // {
            //     "ثبت خودکار معاملات و به‌روزرسانی موجودی",
            //     "نمایش تاریخچه معاملات در منوی حسابداری",
            // },
        };

        /// <summary>
        /// فهرست تغییرات را به صورت متن آماده برای درج در پیام برمی‌گرداند.
        /// اگر برای این نسخه تغییری ثبت نشده باشد، رشته خالی برمی‌گردد.
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
        /// قبل این روی دکمه نقدی در منوی اصلی کلیک شده است
        /// BotTexts.BtnSpot
        /// دکمه شیشه ای خرید
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

        public const string AssetPrefix = "asset";
        public const string BackToMain = "back_to_main";
    }
}
