using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;

namespace TallaEgg.TelegramBot.Application.Services;

public class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<(bool isValid, string message, Invitation? invitation)> ValidateInvitationCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return (false, "کد دعوت وارد نشده است.", null);
        }

        var invitation = await _userRepository.GetInvitationByCodeAsync(code);
        if (invitation == null)
        {
            return (false, "کد دعوت نامعتبر است.", null);
        }

        if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
        {
            return (false, "کد دعوت منقضی شده است.", null);
        }

        if (invitation.MaxUses > 0 && invitation.UsedCount >= invitation.MaxUses)
        {
            return (false, "کد دعوت به حداکثر تعداد استفاده رسیده است.", null);
        }

        return (true, "کد دعوت معتبر است.", invitation);
    }

    public async Task<User> RegisterUserAsync(long telegramId, string? username, string? firstName, string? lastName, string invitationCode)
    {
        var invitation = await _userRepository.GetInvitationByCodeAsync(invitationCode);
        if (invitation == null)
        {
            throw new InvalidOperationException("کد دعوت نامعتبر است.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            TelegramId = telegramId,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            InvitedByUserId = invitation.CreatedByUserId,
            InvitationCode = invitationCode,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            IsActive = true,
            Status = UserStatus.Pending
        };

        // Update invitation usage count
        invitation.UsedCount++;
        await _userRepository.UpdateInvitationAsync(invitation);

        return await _userRepository.CreateAsync(user);
    }

    public async Task<User?> GetUserByTelegramIdAsync(long telegramId)
    {
        return await _userRepository.GetByTelegramIdAsync(telegramId);
    }

    public async Task<User> UpdateUserPhoneAsync(long telegramId, string phoneNumber)
    {
        var user = await _userRepository.GetByTelegramIdAsync(telegramId);
        if (user == null)
        {
            throw new InvalidOperationException("کاربر یافت نشد.");
        }

        user.PhoneNumber = phoneNumber;
        user.LastActiveAt = DateTime.UtcNow;
        user.Status = UserStatus.Active; // فعال کردن کاربر پس از ثبت شماره تلفن

        return await _userRepository.UpdateAsync(user);
    }

    public async Task<bool> UserExistsAsync(long telegramId)
    {
        return await _userRepository.ExistsByTelegramIdAsync(telegramId);
    }

    public async Task<User> CreateRootUserAsync()
    {
        var rootUser = new User
        {
            Id = Guid.NewGuid(),
            TelegramId = 123456789,
            Username = "admin",
            FirstName = "مدیر",
            LastName = "سیستم",
            Role = UserRole.Root,
            Status = UserStatus.Active,
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            InvitationCode = "ROOT2024"
        };

        return await _userRepository.CreateAsync(rootUser);
    }

    public async Task<string> GenerateInvitationCodeAsync(Guid userId)
    {
        var code = $"INV{DateTime.Now:yyyyMMdd}{Random.Shared.Next(1000, 9999)}";
        var user = await _userRepository.GetByIdAsync(userId);
        if (user != null)
        {
            user.InvitationCodeGenerated = code;
            await _userRepository.UpdateAsync(user);
        }
        return code;
    }

    public async Task<bool> IsUserAdminAsync(long telegramId)
    {
        var user = await _userRepository.GetByTelegramIdAsync(telegramId);
        return user?.Role == UserRole.Admin || user?.Role == UserRole.Root;
    }

    public async Task<bool> IsUserRootAsync(long telegramId)
    {
        var user = await _userRepository.GetByTelegramIdAsync(telegramId);
        return user?.Role == UserRole.Root;
    }
} 