using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

/// <summary>
/// The Users service as the Telegram bot uses it (issue #65).
///
/// Extracted so a conversation test can supply a known customer instead of reaching a
/// running Users API over HTTP. Covers what the bot actually calls, not the whole client:
/// a wider interface would be more to fake for no extra coverage, and the unused methods
/// remain available on the concrete class.
/// </summary>
public interface IUsersApiClient
{
    Task<UserDto?> GetUserAsync(long telegramId);
    Task<UserDto?> GetUserAsync(string phone);
    Task<ApiResponse<PagedResult<UserDto>>> GetUsersAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null);
    Task<(bool success, string message, Guid? userId)> RegisterUserAsync(
        long telegramId, string invitationCode, string? username, string? firstName, string? lastName);
    Task<ApiResponse<UserDto>> UpdatePhoneAsync(long telegramId, string phoneNumber);
    Task<ApiResponse<UserDto>> UpdateUserStatusAsync(long telegramId, UserStatus newStatus);
    Task<Guid?> GetUserIdByPhoneNumberAsync(string phonenumber);

    /// <summary>
    /// Looks a user up by their internal id; null when no such user exists.
    ///
    /// Needed because trades carry user ids, not phone numbers, so naming the other side of
    /// a trade in the admin's history requires this direction of lookup.
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(Guid userId);
}
