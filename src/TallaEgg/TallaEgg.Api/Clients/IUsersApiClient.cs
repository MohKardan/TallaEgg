namespace TallaEgg.Api.Clients;

public interface IUsersApiClient
{
    Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode);
    Task<(bool isValid, string message)> ValidateInvitationCodeAsync(string invitationCode);
    Task<UserDto?> GetUserByTelegramIdAsync(long telegramId);
    Task<bool> UpdateUserPhoneAsync(long telegramId, string phoneNumber);
    Task<UserDto?> RegisterUserAsync(RegisterUserRequest request);
    Task<UserDto?> RegisterUserWithInvitationAsync(RegisterUserWithInvitationRequest request);
    Task<bool> UpdateUserStatusAsync(long telegramId, UserStatus status);
    Task<UserDto?> UpdateUserRoleAsync(Guid userId, UserRole newRole);
    Task<IEnumerable<UserDto>> GetUsersByRoleAsync(UserRole role);
    Task<bool> UserExistsAsync(long telegramId);
}

public record UserDto(Guid Id, long TelegramId, string? Username, string? FirstName, string? LastName, string? PhoneNumber, UserStatus Status, UserRole Role, DateTime CreatedAt, DateTime LastActiveAt, bool IsActive);
public record RegisterUserRequest(long TelegramId, string? Username, string? FirstName, string? LastName, string? InvitationCode = null);
public record RegisterUserWithInvitationRequest(UserDto User);
public record UpdateUserRoleRequest(Guid UserId, UserRole NewRole);

public enum UserStatus
{
    Pending,
    Active,
    Suspended,
    Banned
}

public enum UserRole
{
    RegularUser,
    Admin,
    Accountant,
    Moderator
}
