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
        /// Forces right-to-left presentation when the text contains Persian letters, using RLE and
        /// PDF to control direction.
        /// </summary>
        public static string AutoRtl(this string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return text;

            // Does the text contain Persian or Arabic characters?
            bool hasPersian = text.Any(c => c >= 0x0600 && c <= 0x06FF);

            // If so, wrap it in RLE ... PDF.
            if (hasPersian)
                return $"\u202B{text}\u202C";

            return text;
        }

        /// <summary>
        /// Reads an enum member's Persian text from its Description attribute.
        /// </summary>
        /// <param name="value">The enum value.</param>
        /// <returns>The Description text, or the enum member's name if it has none.</returns>
        public static string GetEnumDescription(Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
            return attribute?.Description ?? value.ToString();
        }

        /// <summary>
        /// Converts a Gregorian date to Jalali.
        /// </summary>
        /// <param name="dateTime">The Gregorian date.</param>
        /// <returns>The Jalali date, formatted yyyy/MM/dd HH:mm.</returns>
        /// <summary>
        /// Converts a UTC instant to a Tehran-local Jalali date and time, formatted yyyy/MM/dd HH:mm.
        ///
        /// The previous version was labelled "approximate" and was simply wrong: it subtracted 621
        /// from the year and shifted the month, but returned the <b>Gregorian day unchanged</b>.
        /// 27 July 2026, which is 5 Mordad 1405, was displayed as 1405/05/27 — so a user saw their
        /// own trade dated 22 days out.
        ///
        /// The old comment claimed a full implementation would need a PersianCalendar library. That
        /// was not true: PersianCalendar ships with .NET and needs no new dependency.
        /// </summary>
        public static string ConvertToPersianDate(DateTime dateTime)
        {
            var local = ToTehranTime(dateTime);
            var pc = new PersianCalendar();

            // All formatting goes through InvariantCulture.
            //
            // Without it every format would fall back to CurrentCulture, making the output depend on
            // the language settings of whatever machine runs the service. On this machine the
            // culture is en-US and it looked correct; on a server with fa-IR, that culture's default
            // calendar would apply and the fallback path below would print a Jalali year — a Jalali
            // date labelled as Gregorian. That is exactly the dependency that should not exist.
            try
            {
                var year = pc.GetYear(local);
                var month = pc.GetMonth(local);
                var day = pc.GetDayOfMonth(local);

                return string.Format(CultureInfo.InvariantCulture,
                    "{0:0000}/{1:00}/{2:00} {3:00}:{4:00}",
                    year, month, day, local.Hour, local.Minute);
            }
            catch (ArgumentOutOfRangeException)
            {
                // PersianCalendar throws for very old dates. A displayed date must never break a
                // message, so fall back to the Gregorian format.
                return local.ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
            }
        }

        /// <summary>
        /// Times are stored in UTC via DateTime.UtcNow, but an Iranian user expects Tehran time.
        /// Without this conversion a trade made at 13:22 Tehran time displayed as 09:52.
        ///
        /// The offset is a fixed +03:30 rather than a lookup in the operating system's time-zone database.
        ///
        /// Iran has had no daylight saving since 2022, so it is always UTC+03:30 and a fixed offset
        /// gives the right answer. Relying on the OS database instead carried three risks and no
        /// benefit: the zone id differs between Windows and Linux, a server may have no tz database
        /// at all in minimal container images, and an out-of-date database still applies the
        /// obsolete daylight-saving rule and shows the time an hour out.
        ///
        /// The result is that date display is identical on every machine and independent of server
        /// configuration, which matters for deployment.
        /// </summary>
        public static DateTime ToTehranTime(DateTime dateTime)
        {
            // Local times have already been converted; converting again would shift them wrongly.
            // Unspecified is treated as UTC, because every time in this system originates from
            // DateTime.UtcNow and loses its Kind on a round trip through the database.
            if (dateTime.Kind == DateTimeKind.Local)
                return dateTime;

            return dateTime.AddHours(3).AddMinutes(30);
        }

        /// <summary>Iran's fixed offset from UTC. Iran does not observe daylight saving.</summary>
        public static readonly TimeSpan TehranOffset = new(3, 30, 0);
    }
    /// <summary>
    /// Helpers for working out a wallet's type from its asset code.
    /// </summary>
    public static class AssetHelper
    {
        private const string CREDIT_PREFIX = "CREDIT_";
        private const string MARGIN_PREFIX = "MARGIN_";
        private const string SAVINGS_PREFIX = "SAVINGS_";

        /// <summary>
        /// Combines an asset and a wallet type into an asset string.
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
        /// Extracts the wallet type from an asset string.
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
        /// Whether the asset string denotes a credit wallet.
        /// </summary>
        public static bool IsCreditWallet(string assetKey) => assetKey.StartsWith(CREDIT_PREFIX);
        public static bool IsMarginWallet(string assetKey) => assetKey.StartsWith(MARGIN_PREFIX);
        public static bool IsSpotWallet(string assetKey) => !assetKey.Contains("_");
    }
}
