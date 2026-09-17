namespace TallaEgg.TelegramBot.Infrastructure;

/// <summary>
/// Turns the phone number Telegram sends with a shared contact into the local form the rest of the
/// system stores and looks customers up by.
/// </summary>
internal static class SharedPhoneNumber
{
    /// <summary>
    /// Replaces a leading Iranian country code — <c>+98</c> or <c>98</c> — with <c>0</c>, once.
    /// Anything else is returned unchanged.
    /// </summary>
    /// <remarks>
    /// Only the prefix. This used to be <c>Replace("98", "0")</c> behind a <c>StartsWith</c> check,
    /// which rewrote every <c>98</c> in the number, so <c>989151198161</c> was stored as
    /// <c>0915110161</c> (issue #297). A number from another country is left as sent: whether to
    /// accept one is a separate decision, not something to settle by rewriting it.
    /// </remarks>
    public static string ToLocal(string phoneNumber)
    {
        if (phoneNumber.StartsWith("+98", StringComparison.Ordinal))
            return "0" + phoneNumber["+98".Length..];

        if (phoneNumber.StartsWith("98", StringComparison.Ordinal))
            return "0" + phoneNumber["98".Length..];

        return phoneNumber;
    }
}
