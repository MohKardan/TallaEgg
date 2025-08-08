using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Core.Interfaces;

public interface IUserService
{
    Task<(bool isValid, string message, Invitation? invitation)> ValidateInvitationCodeAsync(string code);
    Task<(bool isValid, string message)> IsInvitationCodeValidAsync(string code);
    Task<User> RegisterUserAsync(long telegramId, string? username, string? firstName, string? lastName, string invitationCode);
    Task<User?> GetUserByTelegramIdAsync(long telegramId);
    Task<User> UpdateUserPhoneAsync(long telegramId, string phoneNumber);
    Task<bool> UserExistsAsync(long telegramId);
    Task<User> CreateRootUserAsync();
    Task<string> GenerateInvitationCodeAsync(Guid userId);
    Task<bool> IsUserAdminAsync(long telegramId);
    Task<bool> IsUserRootAsync(long telegramId);
    Task<(bool success, decimal balance)> GetUserBalanceAsync(long telegramId, string asset);
} 