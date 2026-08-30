namespace TallaEgg.Core.Utilties;

/// <summary>
/// The deduplication key an admin's manual top-up or deduction carries into the wallet service,
/// so that sending the same command twice credits the customer once (issue #157).
///
/// <para>
/// <b>Why a key derived from content.</b> The realistic duplicate is a lost response: the wallet
/// commits, the reply never reaches the bot, the admin sees no confirmation and sends the command
/// again. That re-send is a new Telegram message, so anything derived from the message id would
/// differ and deduplicate nothing. What is identical between the two is what the admin typed —
/// this customer, this asset, this amount.
/// </para>
///
/// <para>
/// <b>Why a time bucket.</b> Content alone cannot tell a re-send from a genuine second top-up of
/// the same amount to the same customer next week. Time separates them, and the product owner set
/// the boundary at <see cref="WindowMinutes"/> minutes: two identical charges closer together than
/// that are one charge sent twice. The timestamp is therefore rounded down to a bucket of that
/// length, so both sends of a re-send land on the same key.
/// </para>
///
/// <para>
/// <b>What the bucket costs.</b> A bucket is not a sliding window. Two sends that straddle a
/// bucket boundary — the first at 12:09:58, the retry at 12:10:03 with a five-minute bucket — get
/// different keys and the duplicate is not caught. The exposure is the retry gap divided by the
/// window, so a retry a few seconds apart escapes a few percent of the time. A true sliding window
/// would need the wallet to know this key's internal structure and query by prefix, trading a
/// clean contract for that last few percent. Worth revisiting if duplicates are ever seen in
/// practice; the failure mode is the one that already exists today, not a new one.
/// </para>
/// </summary>
public static class AdminAdjustmentKey
{
    /// <summary>
    /// How close together two identical admin adjustments have to be to count as one sent twice.
    /// A business number, set by the product owner for issue #157 against the lost-response case:
    /// an admin who gets no confirmation re-sends within seconds, well inside five minutes, while
    /// two genuinely separate top-ups of exactly the same amount to the same customer that close
    /// together are rare.
    /// </summary>
    public const int WindowMinutes = 5;

    /// <summary>The key for an admin top-up. See <see cref="For"/>.</summary>
    public static string ForDeposit(Guid userId, string asset, decimal amount, DateTime utcNow) =>
        For("deposit", userId, asset, amount, utcNow);

    /// <summary>The key for an admin deduction. See <see cref="For"/>.</summary>
    public static string ForWithdrawal(Guid userId, string asset, decimal amount, DateTime utcNow) =>
        For("withdrawal", userId, asset, amount, utcNow);

    /// <summary>
    /// Builds the key. Deposits and withdrawals are namespaced apart so that topping a customer up
    /// and deducting the same amount inside one window are never mistaken for each other.
    ///
    /// <para>
    /// The amount is normalised before it is formatted: <c>100</c> and <c>100.00</c> are the same
    /// charge and have to produce the same key, but <see cref="decimal"/> keeps trailing zeros and
    /// would render them differently.
    /// </para>
    /// </summary>
    private static string For(string operation, Guid userId, string asset, decimal amount, DateTime utcNow)
    {
        var bucket = utcNow.Ticks / TimeSpan.FromMinutes(WindowMinutes).Ticks;

        return string.Create(
            System.Globalization.CultureInfo.InvariantCulture,
            $"admin-{operation}:{userId:N}:{asset.Trim().ToUpperInvariant()}:{Normalize(amount)}:{bucket}");
    }

    /// <summary>Strips trailing zeros so 100 and 100.00 render identically.</summary>
    private static decimal Normalize(decimal amount) => amount / 1.000000000000000000000000000000000m;
}
