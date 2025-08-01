namespace Orders.Core;

public interface IUserRepository
{
    Task<User?> GetByTelegramIdAsync(long telegramId);
    Task<User?> GetByInvitationCodeAsync(string invitationCode);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task<bool> ExistsByTelegramIdAsync(long telegramId);
    Task<Invitation?> GetInvitationByCodeAsync(string code);
    Task<Invitation> CreateInvitationAsync(Invitation invitation);
    Task<Invitation> UpdateInvitationAsync(Invitation invitation);
} 