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
    /// Everyone holding one role. Added so the bot can ask every admin about a quote the
    /// plausibility band is holding (issue #158) — the alternative, paging the whole user list and
    /// filtering, reads every customer to find a handful of operators.
    /// </summary>
    Task<IReadOnlyList<UserDto>> GetUsersByRoleAsync(UserRole role);

    /// <summary>
    /// Looks a user up by their internal id; null when no such user exists.
    ///
    /// Needed because trades carry user ids, not phone numbers, so naming the other side of
    /// a trade in the admin's history requires this direction of lookup.
    /// </summary>
    Task<UserDto?> GetUserByIdAsync(Guid userId);

    /// <summary>
    /// Changes a user's role. The Users service has exposed this since the beginning; nothing
    /// reached it, so roles could only be changed with a hand-written SQL UPDATE.
    ///
    /// That is why the bot needs it: a freshly created database contains no operator at all,
    /// and without a way to grant one from inside the product the whole dealer flow is
    /// unreachable on a new server.
    /// </summary>
    Task<(bool success, string message)> UpdateRoleAsync(Guid userId, UserRole newRole);

    /// <summary>
    /// Telegram ids of every current Admin and SuperAdmin.
    ///
    /// Needed to decide who gets the new-registration approve/reject card. That used to be
    /// answered by asking Telegram who administers one hard-coded group — a question that has
    /// nothing to do with who the product actually recognizes as an operator, and throws
    /// outright when the bot is not a member of that group. This asks the Users service
    /// instead, which is the same source of truth the "ت"/"ر" commands already trust.
    /// </summary>
    Task<List<long>> GetOperatorTelegramIdsAsync();
}
