using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

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
        public static string ConvertToPersianDate(DateTime dateTime)
        {
            // تبدیل ساده به تاریخ شمسی - برای پیاده‌سازی کامل نیاز به کتابخانه PersianCalendar است
            var persianMonths = new[] { "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور", 
                                       "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
            
            // محاسبه تقریبی تاریخ شمسی
            var year = dateTime.Year - 621;
            var month = dateTime.Month;
            var day = dateTime.Day;
            
            // تنظیم ماه شمسی (تقریبی)
            if (month >= 3 && month <= 5) month = month - 2;
            else if (month >= 6 && month <= 8) month = month - 2;
            else if (month >= 9 && month <= 11) month = month - 2;
            else if (month == 12) month = 10;
            else if (month == 1) month = 11;
            else if (month == 2) month = 12;
            
            return $"{year:0000}/{month:00}/{day:00} {dateTime:HH:mm}";
        }
    }
}
