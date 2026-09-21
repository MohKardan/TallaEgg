using System.Text;

namespace TallaEgg.Core.Utilties;

/// <summary>
/// The one form a phone number is stored and looked up in.
/// </summary>
/// <remarks>
/// <para>
/// An Iranian number is stored in local form — <c>09151198161</c> — because that is what the
/// operators type and what every row already in the database holds. A number from anywhere else
/// is stored as its digits with the country code and no plus — <c>18085551234</c> — which is what
/// Telegram delivers and what the one foreign account on this database already has. Foreign
/// numbers are accepted; the owner settled that on 2026-09-21.
/// </para>
/// <para>
/// This lives in Core rather than in the bot on purpose. The rule used to be
/// <c>SharedPhoneNumber.ToLocal</c>, <c>internal</c> to the bot's infrastructure and applied on
/// the registration path alone, so an operator command, a direct API call and (soon) the web
/// client each compared the raw string instead. The same number written two ways was two rows,
/// and the duplicate guard from #303 passed over both: each form had exactly one holder
/// (issue #307).
/// </para>
/// <para>
/// Stripping a leading <c>98</c> cannot corrupt a foreign number. Country codes are prefix-free
/// and 98 is Iran's, so no other country's international number begins with it. What this does
/// not handle is a foreign number typed in its own national form — <c>07700900123</c> for the UK —
/// which is indistinguishable from an Iranian local number. Telegram always delivers the
/// international form, so that shape only arrives when a person types it by hand.
/// </para>
/// </remarks>
public static class PhoneNumbers
{
    private const string IranCountryCode = "98";

    /// <summary>
    /// Returns <paramref name="phoneNumber"/> in the form it is stored and compared in.
    /// <c>null</c> and empty are handed back unchanged: whether an absent number is acceptable
    /// is the caller's rule, not this one's.
    /// </summary>
    public static string? Canonical(string? phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return phoneNumber;

        // Persian and Arabic-Indic digits first. The bot converts them in message text, but a
        // number can also arrive through the API — from an operator tool, or the web client of
        // #97 — and char.IsDigit accepts the whole Unicode Nd category, so ۰۹۱۵… would survive
        // the strip below, miss the ASCII prefix comparisons, and be stored as a row no ASCII
        // spelling of the same number can ever match. The unique index would accept it happily:
        // it is a different string.
        phoneNumber = Utils.ConvertPersianDigitsToEnglish(phoneNumber);

        // Everything that is not a digit is punctuation a person added: spaces, dashes,
        // parentheses, and the plus, whose meaning is carried by the country code that follows it.
        var digits = new StringBuilder(phoneNumber.Length);
        foreach (var character in phoneNumber)
        {
            if (character is >= '0' and <= '9')
                digits.Append(character);
        }

        if (digits.Length == 0)
            return phoneNumber.Trim();

        var number = digits.ToString();

        // The international dialling prefix. "00" is never the start of an Iranian local number,
        // which begins 09, nor of a country code.
        if (number.StartsWith("00", StringComparison.Ordinal))
            number = number["00".Length..];

        // Only the prefix, never every occurrence: 989151198161 is 09151198161, not 0915110161.
        // That was #297, and it is the reason this is a comparison and not a Replace.
        if (number.StartsWith(IranCountryCode, StringComparison.Ordinal))
            return "0" + number[IranCountryCode.Length..];

        return number;
    }
}
