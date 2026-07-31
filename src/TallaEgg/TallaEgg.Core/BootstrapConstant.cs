namespace TallaEgg.Core;

/// <summary>
/// The few fixed values a brand-new, empty database depends on.
///
/// <para>
/// They live here because each one has to be identical in two services that never call each
/// other, and until now each was written out twice by hand. Two hand-written copies that must
/// agree will eventually disagree, and this particular disagreement is silent: nothing fails
/// at build time, nothing fails on a database that already has data, and the fault only
/// appears on the very first run of a fresh deployment — the moment with the least margin to
/// debug it.
/// </para>
/// </summary>
public static class BootstrapConstant
{
    /// <summary>
    /// The invitation code that lets the first person register.
    ///
    /// <para>
    /// Registration refuses any code that does not already belong to a user
    /// (<c>UserService.RegisterUserAsync</c>), so on an empty database the only usable code is
    /// the one carried by the administrator row that <c>Users.Api</c> seeds at startup. The bot
    /// falls back to <c>BotSettings:DefaultReferralCode</c> when referral codes are not
    /// required, and that value has to be this one.
    /// </para>
    ///
    /// <para>
    /// It was <c>"ADMIN2024"</c> on the bot side and <c>"admin"</c> on the seed side. Nobody
    /// noticed, because every account in the existing database was created with a code
    /// belonging to a real user; the mismatch only bites when there are no real users yet —
    /// which is exactly a first deployment, where it stops registration entirely and therefore
    /// stops everything.
    /// </para>
    /// </summary>
    public const string RootInvitationCode = "admin";

    /// <summary>
    /// The id of the administrator row seeded by <c>Users.Api</c> at startup.
    ///
    /// <para>
    /// Note that this row cannot be signed in as: it is created with <c>TelegramId = 0</c>, so
    /// it exists to own <see cref="RootInvitationCode"/> and nothing else. Appointing a human
    /// operator is a separate step — see <c>BotSettings:OwnerTelegramIds</c>.
    /// </para>
    /// </summary>
    public static readonly Guid RootAdminUserId = Guid.Parse("5564f136-b9fb-4719-b4dc-b0833fa24761");
}
