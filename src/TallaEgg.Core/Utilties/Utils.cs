using System;
using System.Collections.Generic;
using System.Linq;
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
    }
}
