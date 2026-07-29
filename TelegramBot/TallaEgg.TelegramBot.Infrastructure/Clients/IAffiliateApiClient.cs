namespace TallaEgg.TelegramBot;

/// <summary>
/// The Affiliate service as the Telegram bot uses it (issue #65).
///
/// Only the invitation redemption is here, because that is the only call the bot makes
/// during a conversation.
/// </summary>
public interface IAffiliateApiClient
{
    Task<(bool success, string message, Guid? invitationId)> UseInvitationAsync(
        string invitationCode, Guid usedByUserId);
}
