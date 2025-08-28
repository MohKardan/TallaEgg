using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.TelegramBot.Core.Utilties
{
    public static class Utils
    {

        public static string EscapeMarkdown(string text)
        {
            if (string.IsNullOrEmpty(text))
                return "";
            return text
                .Replace("_", "\\_")
                .Replace("*", "\\*")
                .Replace("[", "\\[")
                .Replace("`", "\\`")
                .Replace("(", "\\(")
                .Replace(")", "\\)")
                .Replace("~", "\\~")
                .Replace(">", "\\>")
                .Replace("#", "\\#")
                .Replace("+", "\\+")
                .Replace("-", "\\-")
                .Replace("=", "\\=")
                .Replace("|", "\\|")
                .Replace("{", "\\{")
                .Replace("}", "\\}")
                .Replace(".", "\\.")
                .Replace("!", "\\!");
        }


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

        public static string EscapeHtml(string? str) =>
    string.IsNullOrEmpty(str) ? "-" :
    str.Replace("&", "&amp;")
       .Replace("<", "&lt;")
       .Replace(">", "&gt;");

        public static string UsernameLink(string? username) =>
            string.IsNullOrEmpty(username) ? "-" :
            $"<a href=\"https://t.me/{username}\">@{username}</a>";

    }
}
