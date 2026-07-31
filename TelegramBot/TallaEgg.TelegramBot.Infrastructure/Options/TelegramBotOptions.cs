namespace TallaEgg.TelegramBot.Infrastructure.Options;

public class TelegramBotOptions
{
    public string? TelegramBotToken { get; set; }
    public string? OrderApiUrl { get; set; }
    public string? UsersApiUrl { get; set; }
    public string? AffiliateApiUrl { get; set; }
    public string? WalletApiUrl { get; set; }
    public BotSettingsOptions BotSettings { get; set; } = new();
}

public class BotSettingsOptions
{
    public bool RequireReferralCode { get; set; }

    /// <summary>
    /// The code used to register when <see cref="RequireReferralCode"/> is off.
    ///
    /// Defaults to <see cref="TallaEgg.Core.BootstrapConstant.RootInvitationCode"/> because
    /// registration rejects a code that belongs to no user, and on an empty database the seeded
    /// administrator's code is the only one that exists.
    /// </summary>
    public string DefaultReferralCode { get; set; } = TallaEgg.Core.BootstrapConstant.RootInvitationCode;

    /// <summary>
    /// Telegram ids treated as operators regardless of the role stored against them.
    ///
    /// <para>
    /// <b>This exists to break a deadlock, not as a convenience.</b> Administrative commands
    /// are only reached when the caller's stored role is Admin, and the only way to obtain
    /// that role is an administrative command. On the database that exists today the deadlock
    /// is invisible because an Admin row was created by hand months ago. On an empty database
    /// — which is what a new deployment starts with — the first person to register is an
    /// ordinary user, nobody is an Admin, and no one can ever become one. The seeded SuperAdmin
    /// does not help: it is created with <c>TelegramId = 0</c>, so no human can sign in as it.
    /// </para>
    ///
    /// <para>
    /// Identity therefore has to come from somewhere the database cannot invalidate. An id
    /// listed here is an operator even against an empty <c>Users</c> table, which is what makes
    /// the first role assignment possible. It is also the recovery path if the last Admin is
    /// demoted by mistake.
    /// </para>
    ///
    /// <para>
    /// Keep the list short — one or two people. Membership cannot be revoked from inside the
    /// product; it needs a configuration change and a restart.
    /// </para>
    /// </summary>
    public long[] OwnerTelegramIds { get; set; } = [];
}
