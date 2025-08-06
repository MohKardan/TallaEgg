using Orders.Core;

namespace Orders.Application;

public static class UserMessages
{
    public const string InviteNotEntered = "کد دعوت وارد نشده است.";
    public const string InviteInvalid = "کد دعوت نامعتبر است.";
    public const string InviteExpired = "کد دعوت منقضی شده است.";
    public const string InviteMaxUsed = "کد دعوت به حداکثر تعداد استفاده رسیده است.";
    public const string InviteValid = "کد دعوت معتبر است.";
    public const string UserNotFound = "کاربر یافت نشد.";
}

public class UserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<(bool isValid, string message, Invitation? invitation)> ValidateInvitationCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (false, UserMessages.InviteNotEntered, null);

        var invitation = await _userRepository.GetInvitationByCodeAsync(code);
        if (invitation == null)
            return (false, UserMessages.InviteInvalid, null);

        if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
            return (false, UserMessages.InviteExpired, null);

        if (invitation.MaxUses > 0 && invitation.UsedCount >= invitation.MaxUses)
            return (false, UserMessages.InviteMaxUsed, null);

        return (true, UserMessages.InviteValid, invitation);
    }

    public async Task<User> RegisterUserAsync(long telegramId, string? username, string? firstName, string? lastName, string invitationCode)
    {
        var createdByUserId = await _userRepository.GetUserIdByInvitationCode(invitationCode);
        if (createdByUserId == null)
            throw new InvalidOperationException(UserMessages.InviteInvalid);

        var user = new User
        {
            Id = Guid.NewGuid(),
            TelegramId = telegramId,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            InvitedByUserId = createdByUserId,
            InvitationCode = invitationCode,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            IsActive = false // بعد از ارسال شماره تلفن فعال میشود
        };

        return await _userRepository.CreateAsync(user);
    }

    public async Task<User> RegisterUserAsync(User user)
    {
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
            throw new InvalidOperationException(UserMessages.UserNotFound);

        user.PhoneNumber = phoneNumber;
        user.LastActiveAt = DateTime.UtcNow;

        return await _userRepository.UpdateAsync(user);
    }

    public async Task<bool> UserExistsAsync(long telegramId)
    {
        return await _userRepository.ExistsByTelegramIdAsync(telegramId);
    }

    public async Task<Guid?> GetUserIdByInvitationCode(string invitationCode)
    {
        // فرض بر این است که UserRepository متدی با همین نام دارد
        return await _userRepository.GetUserIdByInvitationCode(invitationCode);
    }
}