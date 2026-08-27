namespace TallaEgg.Core.Utilties
{
    /// <summary>
    /// Number and text formatting for the bot's Persian messages.
    ///
    /// It solves two problems:
    /// 1. Latin digits (123) inside Persian text are converted to Persian digits, so the message
    ///    reads as entirely Persian.
    /// 2. Bidirectional reordering: when a left-to-right run — a separated number, a card number, a
    ///    version string — sits inside a Persian sentence, the Unicode bidi algorithm reorders it
    ///    on screen. Isolating that run pins the display order.
    /// </summary>
    public static class PersianFormat
    {
        // These three characters are invisible in an editor. Each one's Unicode code point is given
        // in its doc comment and PersianDateTimeTests asserts the values, because damage from a bad
        // copy-paste in the source would otherwise be impossible to see.
        /// <summary>Right-to-Left Mark, U+200F.</summary>
        public const string Rlm = "‏";

        /// <summary>Left-to-Right Isolate, U+2066.</summary>
        public const string Lri = "⁦";

        /// <summary>Pop Directional Isolate, U+2069.</summary>
        public const string Pdi = "⁩";

        private const char ArabicIndicZero = '۰'; // ۰ فارسی

        /// <summary>Persian thousands separator, U+066C.</summary>
        private const char PersianThousandsSeparator = '٬';

        /// <summary>Persian decimal separator, U+066B.</summary>
        private const char PersianDecimalSeparator = '٫';

        /// <summary>
        /// Converts Latin digits to Persian digits. Every other character is left untouched.
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
        /// Formats a number with Persian thousands separators and Persian digits, isolated so it does
        /// not reorder inside Persian text.
        /// </summary>
        /// <param name="value">The numeric value.</param>
        /// <param name="decimals">Decimal places; zero by default, which suits toman amounts.</param>
        public static string Number(decimal value, int decimals = 0)
        {
            // Format with the standard Latin separators first, then map the characters to their
            // Persian equivalents. This is independent of the system culture.
            var formatted = value.ToString("N" + decimals.ToString(System.Globalization.CultureInfo.InvariantCulture),
                                           System.Globalization.CultureInfo.InvariantCulture);

            return Ltr(ToPersianDigits(Localize(formatted)));
        }

        /// <summary>
        /// Formats an asset amount with that asset's own number of decimals — toman has none, melted
        /// gold has two. Trailing zero decimals are dropped, so it reads "8 grams" rather than
        /// "8.00 grams".
        /// </summary>
        public static string Amount(decimal value, string assetCode)
        {
            var decimals = CurrenciesConstant.GetCurrencyInfo(assetCode)?.DecimalPlaces ?? 0;

            // A format using # in the decimal section hides trailing zeros, so the output is "8" and
            // "8.5" rather than "8.00" and "8.50".
            var pattern = decimals > 0
                ? "#,##0." + new string('#', decimals)
                : "#,##0";

            var formatted = value.ToString(pattern, System.Globalization.CultureInfo.InvariantCulture);
            return Ltr(ToPersianDigits(Localize(formatted)));
        }

        /// <summary>Replaces Latin separators with their Persian equivalents.</summary>
        private static string Localize(string formatted) =>
            formatted
                .Replace(",", PersianThousandsSeparator.ToString())
                .Replace(".", PersianDecimalSeparator.ToString());

        /// <summary>
        /// Wraps a left-to-right run — a number, a date, a version string — in a Unicode
        /// left-to-right isolate, so inside a Persian sentence it displays in exactly the order it
        /// was written.
        ///
        /// <para>
        /// <b>This used to surround the run with RLM on both sides, which was wrong.</b> RLM is a
        /// strong right-to-left character, so it made the run's interior right-to-left. For a lone
        /// number that made no difference, but as soon as the run held <b>two</b> groups of digits —
        /// a date and a time together — the space between them took right-to-left direction and the
        /// bidi algorithm <b>swapped the date and the time</b>, showing the user the time first.
        /// </para>
        ///
        /// <para>
        /// U+2066 (LRI) to U+2069 (PDI) is the standard answer to exactly this: the whole run becomes
        /// one isolated left-to-right unit, its internal order is preserved, and the direction of the
        /// surrounding text is unaffected. The behaviour is defined in the Unicode standard itself
        /// and does not depend on device or system language settings.
        /// </para>
        /// </summary>
        public static string Ltr(string? text) =>
            string.IsNullOrEmpty(text) ? string.Empty : $"{Lri}{text}{Pdi}";

        /// <summary>
        /// The trading pair's Persian display name, which keeps Latin symbols out of Persian text.
        /// </summary>
        public static string Symbol(string symbol) =>
            CurrenciesConstant.GetPersianSymbolName(symbol);

        /// <summary>An asset's Persian name.</summary>
        public static string Asset(string assetCode) =>
            CurrenciesConstant.GetPersianName(assetCode);

        /// <summary>An asset's display unit.</summary>
        public static string Unit(string assetCode) =>
            CurrenciesConstant.GetCurrencyInfo(assetCode)?.Unit ?? string.Empty;

        /// <summary>
        /// Displays a date and time the way an Iranian user expects it: <b>Jalali calendar, Tehran
        /// time (UTC+03:30), Persian digits</b>.
        ///
        /// <para>
        /// This is the only way a date should be displayed in a bot message. Previously each call
        /// site formatted its own, and all of them were Gregorian and in UTC — so a trade made at
        /// 13:22 Tehran time showed as 09:52, with a Gregorian date.
        /// </para>
        ///
        /// <para>
        /// <b>Storage is unchanged.</b> Everything in the database stays Gregorian and UTC, which is
        /// correct for sorting, comparison and reconciliation. This is a presentation concern only.
        /// </para>
        /// </summary>
        public static string DateTimeText(DateTime value) =>
            Ltr(ToPersianDigits(Utils.ConvertToPersianDate(value)));
    }
}
