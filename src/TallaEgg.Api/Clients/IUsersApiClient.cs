namespace TallaEgg.Api.Clients;

public interface IUsersApiClient
{
    Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode);
    Task<(bool isValid, string message)> ValidateInvitationCodeAsync(string invitationCode);
    Task<UserDto?> GetUserByTelegramIdAsync(long telegramId);
    Task<bool> UpdateUserPhoneAsync(long telegramId, string phoneNumber);
}

public record UserDto(Guid Id, long TelegramId, string? Username, string? FirstName, string? LastName);
