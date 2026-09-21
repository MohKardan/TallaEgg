using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;

namespace Users.Core;

public interface IUserRepository
{
    Task<User?> GetByTelegramIdAsync(long telegramId);
    /// <summary>
    /// Every account holding this number. It returns a list rather than one user because a
    /// caller that cannot see a second holder has no way to refuse the ambiguity — it would
    /// silently act on whichever row came back first (issue #303). Duplicates are rarer since
    /// #307 added a unique index, but that index is skipped on a database that already held
    /// duplicates when it ran, so more than one holder remains possible.
    /// </summary>
    Task<IReadOnlyList<User>> GetAllByPhoneNumberAsync(string phoneNumber);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task<bool> ExistsByTelegramIdAsync(long telegramId);
    Task<PagedResult<UserDto>> GetAllAsync(string? q, int page, int size);
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> UpdateUserRoleAsync(Guid id, UserRole role);
    Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role);
    Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode);
    Task<Guid?> GetUserIdByPhonenumberAsync(string phoneNumber);
}