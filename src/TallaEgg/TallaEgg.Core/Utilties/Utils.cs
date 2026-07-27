using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;   // PersianCalendar — بخشی از دات‌نت، بدون وابستگی خارجی
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TallaEgg.Core.Enums.Wallet;

namespace TallaEgg.Core.Utilties
{
    public static class Utils
    {
        public static string GenerateSecureRandomString(int length)
        {
            const string alphanumericCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ" + // Allowed characters
                                                  "abcdefghijklmnopqrstuvwxyz" +
                                                  "0123456789";
            var characterArray = alphanumericCharacters.ToCharArray(); // Convert to char array
            var bytes = new byte[length * 4]; // Use 4 bytes for each char to reduce bias
            var result = new char[length];

            using (var cryptoProvider = RandomNumberGenerator.Create()) // Use the recommended Create method
            {
                cryptoProvider.GetBytes(bytes); // Fill bytes with cryptographically strong random data
            }

            for (int i = 0; i < length; i++)
            {
                uint value = BitConverter.ToUInt32(bytes, i * 4); // Convert 4 bytes to an unsigned integer
                result[i] = characterArray[value % (uint)characterArray.Length]; // Select character using modulo operator
            }

            return new string(result);
        }

        public static string ConvertPersianDigitsToEnglish(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var persianDigits = new[] { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };
            var arabicDigits = new[] { '٠', '١', '٢', '٣', '٤', '٥', '٦', '٧', '٨', '٩' };

            for (int i = 0; i < 10; i++)
            {
                input = input.Replace(persianDigits[i], (char)('0' + i));
                input = input.Replace(arabicDigits[i], (char)('0' + i));
            }

            return input;
        }

        /// <summary>
        /// اگر متن شامل حروف فارسی باشد، آن را راست‌چین می‌کند.
        /// از RLE و PDF برای کنترل جهت متن استفاده می‌شود.
        /// </summary>
        public static string AutoRtl(this string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // چک می‌کنیم آیا متن شامل کاراکترهای فارسی/عربی هست یا نه
            bool hasPersian = text.Any(c => c >= 0x0600 && c <= 0x06FF);

            // اگر فارسی بود، متن را داخل RLE...PDF قرار می‌دهیم
            if (hasPersian)
                return $"\u202B{text}\u202C";

            return text;
        }

        /// <summary>
        /// دریافت متن فارسی از Description attribute یک enum
        /// </summary>
        /// <param name="value">مقدار enum</param>
        /// <returns>متن فارسی از Description attribute یا نام enum در صورت عدم وجود</returns>
        public static string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }

        /// <summary>
        /// تبدیل تاریخ میلادی به شمسی (تقریبی)
        /// </summary>
        /// <param name="dateTime">تاریخ میلادی</param>
        /// <returns>تاریخ شمسی به فرمت yyyy/MM/dd HH:mm</returns>
        /// <summary>
        /// تبدیل زمان UTC به تاریخ و ساعت شمسیِ تهران، با قالب yyyy/MM/dd HH:mm.
        ///
        /// نسخهٔ قبلی «تقریبی» بود و در عمل غلط: سال را ۶۲۱ واحد کم می‌کرد، ماه را جابه‌جا
        /// می‌کرد، اما **روزِ میلادی را دست‌نخورده** برمی‌گرداند. مثلاً ۲۷ ژوئیهٔ ۲۰۲۶ که
        /// معادل ۵ مرداد ۱۴۰۵ است، ۱۴۰۵/۰۵/۲۷ نمایش داده می‌شد — یعنی کاربر تاریخ معاملهٔ
        /// خودش را ۲۲ روز اشتباه می‌دید.
        ///
        /// کامنت قبلی می‌گفت «برای پیاده‌سازی کامل نیاز به کتابخانه PersianCalendar است»؛
        /// این درست نبود — PersianCalendar بخشی از خود دات‌نت است و هیچ وابستگی جدیدی
        /// لازم ندارد.
        /// </summary>
        public static string ConvertToPersianDate(DateTime dateTime)
        {
            var local = ToTehranTime(dateTime);
            var pc = new PersianCalendar();

            // PersianCalendar در محدودهٔ تاریخ‌های خیلی قدیمی استثنا می‌دهد. تاریخ نمایشی
            // هرگز نباید باعث شکست یک پیام شود، پس در آن حالت به قالب میلادی برمی‌گردیم.
            try
            {
                return $"{pc.GetYear(local):0000}/{pc.GetMonth(local):00}/{pc.GetDayOfMonth(local):00} {local:HH:mm}";
            }
            catch (ArgumentOutOfRangeException)
            {
                return local.ToString("yyyy/MM/dd HH:mm");
            }
        }

        /// <summary>
        /// زمان‌ها در دیتابیس UTC ذخیره می‌شوند (DateTime.UtcNow)، ولی کاربر ایرانی انتظار
        /// ساعت تهران را دارد. بدون این تبدیل، معامله‌ای که ساعت ۱۳:۲۲ به وقت تهران انجام
        /// شده، ۰۹:۵۲ نمایش داده می‌شد.
        ///
        /// شناسهٔ منطقهٔ زمانی روی ویندوز و لینوکس متفاوت است، پس هر دو امتحان می‌شوند.
        /// اگر هیچ‌کدام موجود نبود، افست ثابت +۳:۳۰ استفاده می‌شود؛ ایران از سال ۱۴۰۱
        /// ساعت تابستانی ندارد، پس این افست ثابت امروز درست است.
        /// </summary>
        private static DateTime ToTehranTime(DateTime dateTime)
        {
            // فقط زمان‌های UTC تبدیل می‌شوند. اگر فراخوانی مقدار Local یا Unspecified بدهد،
            // تبدیل دوباره باعث جابه‌جایی اشتباه می‌شد.
            if (dateTime.Kind == DateTimeKind.Local)
                return dateTime;

            var utc = DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);

            foreach (var id in new[] { "Iran Standard Time", "Asia/Tehran" })
            {
                try
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(id));
                }
                catch (TimeZoneNotFoundException) { }
                catch (InvalidTimeZoneException) { }
            }

            return utc.AddHours(3).AddMinutes(30);
        }
    }
    /// <summary>
    /// برای تشخیص نوع کیف پول اینجوری خیلی راحت تره
    /// </summary>
    public static class AssetHelper
    {
        private const string CREDIT_PREFIX = "CREDIT_";
        private const string MARGIN_PREFIX = "MARGIN_";
        private const string SAVINGS_PREFIX = "SAVINGS_";

        /// <summary>
        /// تبدیل Asset + WalletType به Asset string
        /// </summary>
        public static string CreateAssetKey(string baseAsset, WalletType walletType)
        {
            return walletType switch
            {
                WalletType.Credit => $"{CREDIT_PREFIX}{baseAsset}",
                WalletType.Margin => $"{MARGIN_PREFIX}{baseAsset}",
                WalletType.Savings => $"{SAVINGS_PREFIX}{baseAsset}",
                WalletType.Spot => baseAsset,
                _ => baseAsset
            };
        }

        /// <summary>
        /// استخراج نوع کیف پول از Asset string
        /// </summary>
        public static (string baseAsset, WalletType walletType) ParseAssetKey(string assetKey)
        {
            if (assetKey.StartsWith(CREDIT_PREFIX))
                return (assetKey.Substring(CREDIT_PREFIX.Length), WalletType.Credit);

            if (assetKey.StartsWith(MARGIN_PREFIX))
                return (assetKey.Substring(MARGIN_PREFIX.Length), WalletType.Margin);

            if (assetKey.StartsWith(SAVINGS_PREFIX))
                return (assetKey.Substring(SAVINGS_PREFIX.Length), WalletType.Savings);

            return (assetKey, WalletType.Spot);
        }

        /// <summary>
        /// بررسی نوع کیف پول
        /// </summary>
        public static bool IsCreditWallet(string assetKey) => assetKey.StartsWith(CREDIT_PREFIX);
        public static bool IsMarginWallet(string assetKey) => assetKey.StartsWith(MARGIN_PREFIX);
        public static bool IsSpotWallet(string assetKey) => !assetKey.Contains("_");
    }
}
