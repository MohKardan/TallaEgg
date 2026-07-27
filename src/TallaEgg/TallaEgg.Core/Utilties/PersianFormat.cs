namespace TallaEgg.Core.Utilties
{
    /// <summary>
    /// قالب‌بندی اعداد و متن برای پیام‌های فارسی ربات.
    ///
    /// دو مسئله را حل می‌کند:
    /// ۱. ارقام لاتین (123) در متن فارسی → به ارقام فارسی (۱۲۳) تبدیل می‌شوند تا پیام
    ///    کاملاً فارسی باشد.
    /// ۲. به‌هم‌ریختگی راست‌به‌چپ: وقتی یک قطعه متن چپ‌به‌راست (عدد با جداکننده،
    ///    شماره کارت، شماره نسخه) داخل جملهٔ فارسی قرار می‌گیرد، الگوریتم دوسویهٔ
    ///    یونیکد ترتیب نمایش را جابه‌جا می‌کند. با قرار دادن نشانگر RLM دور آن قطعه،
    ///    نمایش تثبیت می‌شود.
    /// </summary>
    public static class PersianFormat
    {
        /// <summary>نشانگر راست‌به‌چپ (Right-to-Left Mark) — جهت متن را تثبیت می‌کند.</summary>
        public const string Rlm = "‏";

        private const char ArabicIndicZero = '۰'; // ۰ فارسی

        /// <summary>جداکنندهٔ هزارگان فارسی (U+066C).</summary>
        private const char PersianThousandsSeparator = '٬';

        /// <summary>جداکنندهٔ اعشار فارسی (U+066B).</summary>
        private const char PersianDecimalSeparator = '٫';

        /// <summary>
        /// ارقام لاتین را به ارقام فارسی تبدیل می‌کند. بقیهٔ نویسه‌ها دست‌نخورده می‌مانند.
        /// </summary>
        public static string ToPersianDigits(string? text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            var chars = text.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
            {
                if (chars[i] >= '0' && chars[i] <= '9')
                    chars[i] = (char)(ArabicIndicZero + (chars[i] - '0'));
            }

            return new string(chars);
        }

        /// <summary>
        /// عدد را با جداکنندهٔ هزارگان فارسی و ارقام فارسی قالب‌بندی می‌کند و با نشانگر
        /// راست‌به‌چپ محافظت می‌کند تا در متن فارسی به هم نریزد.
        /// </summary>
        /// <param name="value">مقدار عددی</param>
        /// <param name="decimals">تعداد رقم اعشار (پیش‌فرض صفر — مناسب مبالغ تومانی)</param>
        public static string Number(decimal value, int decimals = 0)
        {
            // ابتدا با قالب استاندارد (جداکننده لاتین) قالب‌بندی می‌شود، سپس نویسه‌ها
            // به معادل فارسی نگاشت می‌شوند. این کار مستقل از Culture سیستم است.
            var formatted = value.ToString("N" + decimals.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                           System.Globalization.CultureInfo.InvariantCulture);

            return Ltr(ToPersianDigits(Localize(formatted)));
        }

        /// <summary>
        /// مقدار دارایی را با تعداد اعشار مناسب همان دارایی قالب‌بندی می‌کند
        /// (مثلاً تومان بدون اعشار، آبشده با دو رقم اعشار).
        /// اعشار اضافی صفر حذف می‌شود تا «۸ گرم» به‌جای «۸٬۰۰ گرم» نمایش داده شود.
        /// </summary>
        public static string Amount(decimal value, string assetCode)
        {
            var decimals = CurrenciesConstant.GetCurrencyInfo(assetCode)?.DecimalPlaces ?? 0;

            // قالب با # در بخش اعشار، صفرهای انتهایی را نمایش نمی‌دهد؛ پس «۸ گرم» و
            // «۸٫۵ گرم» به‌جای «۸٫۰۰» و «۸٫۵۰» نمایش داده می‌شوند.
            var pattern = decimals > 0
                ? "#,##0." + new string('#', decimals)
                : "#,##0";

            var formatted = value.ToString(pattern, System.Globalization.CultureInfo.InvariantCulture);
            return Ltr(ToPersianDigits(Localize(formatted)));
        }

        /// <summary>جداکننده‌های لاتین را با معادل فارسی جایگزین می‌کند.</summary>
        private static string Localize(string formatted) =>
            formatted
                .Replace(",", PersianThousandsSeparator.ToString())
                .Replace(".", PersianDecimalSeparator.ToString());

        /// <summary>
        /// قطعه‌متن چپ‌به‌راست (عدد، شماره نسخه، شماره کارت) را با نشانگر راست‌به‌چپ
        /// در دو طرف احاطه می‌کند تا داخل جملهٔ فارسی جای درست خود نمایش داده شود.
        /// </summary>
        public static string Ltr(string? text) =>
            string.IsNullOrEmpty(text) ? string.Empty : $"{Rlm}{text}{Rlm}";

        /// <summary>
        /// نام فارسی جفت معاملاتی برای نمایش به کاربر (مثل «آبشده/تومان»).
        /// جلوی نمایش نماد لاتین در متن فارسی را می‌گیرد.
        /// </summary>
        public static string Symbol(string symbol) =>
            CurrenciesConstant.GetPersianSymbolName(symbol);

        /// <summary>نام فارسی یک دارایی (مثل «آبشده» یا «تومان»).</summary>
        public static string Asset(string assetCode) =>
            CurrenciesConstant.GetPersianName(assetCode);

        /// <summary>واحد نمایش یک دارایی (مثل «گرم» یا «تومان»).</summary>
        public static string Unit(string assetCode) =>
            CurrenciesConstant.GetCurrencyInfo(assetCode)?.Unit ?? string.Empty;

        /// <summary>
        /// تاریخ و ساعت را به قالب فارسی‌خوان تبدیل می‌کند (ارقام فارسی، محافظت‌شده).
        /// </summary>
        public static string DateTimeText(DateTime value) =>
            Ltr(ToPersianDigits(value.ToString("yyyy/MM/dd HH:mm",
                System.Globalization.CultureInfo.InvariantCulture)));
    }
}
