using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByTelegramIdAsync(long telegramId);
    Task<User?> GetByIdAsync(Guid userId);
    Task<User?> GetByInvitationCodeAsync(string invitationCode);
    Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task<bool> ExistsByTelegramIdAsync(long telegramId);
    Task<(bool isValid, string message)> IsInvitationCodeValidAsync(string code);
    Task<Invitation?> GetInvitationByCodeAsync(string code);
    Task<Invitation> CreateInvitationAsync(Invitation invitation);
    Task<Invitation> UpdateInvitationAsync(Invitation invitation);
} 