using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public interface IUsersApiClient
{
    Task<(bool isValid, string message)> ValidateInvitationCodeAsync(string invitationCode);
    Task<User?> RegisterUserAsync(long telegramId, string? username, string? firstName, string? lastName, string invitationCode);
    Task<User?> GetUserByTelegramIdAsync(long telegramId);
    Task<User?> UpdateUserPhoneAsync(long telegramId, string phoneNumber);
} 