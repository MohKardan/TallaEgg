using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IUserService
{
    Task<(bool isValid, string message, Invitation? invitation)> ValidateInvitationCodeAsync(string code);
    Task<User> RegisterUserAsync(long telegramId, string? username, string? firstName, string? lastName, string invitationCode);
    Task<User?> GetUserByTelegramIdAsync(long telegramId);
    Task<User> UpdateUserPhoneAsync(long telegramId, string phoneNumber);
    Task<bool> UserExistsAsync(long telegramId);
} 