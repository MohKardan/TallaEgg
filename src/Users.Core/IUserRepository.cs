using Affiliate.Core;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;

namespace Users.Core;

public interface IUserRepository
{
    Task<User?> GetByTelegramIdAsync(long telegramId);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task<bool> ExistsByTelegramIdAsync(long telegramId);
    Task<IEnumerable<User>> GetAllAsync();
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> UpdateUserRoleAsync(Guid id, UserRole role);
    Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role);
    Task<Invitation?> GetInvitationByCodeAsync(string invitationCode);
    Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode);
    Task<Guid?> GetUserIdByPhonenumberAsync(string phoneNumber);
}