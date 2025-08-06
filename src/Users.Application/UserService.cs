using Affiliate.Core;
using Users.Core;

namespace Users.Application;

public class UserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<User> RegisterUserAsync(long telegramId, string? username, string? firstName, string? lastName)
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            TelegramId = telegramId,
            Username = username,
            FirstName = firstName,
            LastName = lastName,
            CreatedAt = DateTime.UtcNow,
            LastActiveAt = DateTime.UtcNow,
            IsActive = true,
            Status = UserStatus.Pending
        };

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

    public async Task<User> UpdateUserStatusAsync(long telegramId, UserStatus status)
    {
        var user = await _userRepository.GetByTelegramIdAsync(telegramId);
        if (user == null)
        {
            throw new InvalidOperationException("کاربر یافت نشد.");
        }

        user.Status = status;
        user.LastActiveAt = DateTime.UtcNow;

        return await _userRepository.UpdateAsync(user);
    }

    public async Task<object?> GetUserIdByInvitationCode(string invitationCode)
    {
        throw new NotImplementedException();
    }

    public async Task<(bool isValid, string message, Invitation? invitation)> ValidateInvitationCodeAsync(string invitationCode)
    {
        if (string.IsNullOrWhiteSpace(invitationCode))
            return (false, "کد دعوت وارد نشده است.", null);

        var invitation = await _userRepository.GetInvitationByCodeAsync(invitationCode);
        if (invitation == null)
            return (false, "کد دعوت نامعتبر است.", null);

        if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
            return (false, "کد دعوت منقضی شده است.", null);

        if (invitation.MaxUses > 0 && invitation.UsedCount >= invitation.MaxUses)
            return (false, "کد دعوت به حداکثر تعداد استفاده رسیده است.", null);

        return (true, "کد دعوت معتبر است.", invitation);
    }

    public async Task RegisterUserAsync(global::Orders.Core.User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        throw new NotImplementedException();
    }

    public async Task<User> RegisterUserAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return await _userRepository.CreateAsync(user);
    }

    // اگر جایی در این فایل یا پروژه متدی دارید که آرگومان دوم آن باید از نوع Users.Core.UserRole باشد، 
    // باید مقدار رشته را به Enum تبدیل کنید. مثال:
    public Users.Core.UserRole ParseUserRole(string roleString)
    {
        if (Enum.TryParse<Users.Core.UserRole>(roleString, true, out var role))
            return role;
        return Users.Core.UserRole.User; // مقدار پیش‌فرض
    }

    // سپس هنگام فراخوانی متد، به جای رشته، مقدار Enum را ارسال کنید:
    // var userRole = ParseUserRole(roleString);
    // someMethod(userId, userRole);

    // اطمینان حاصل کنید که IUserRepository متد زیر را دارد:
    // Task<Invitation?> GetInvitationByCodeAsync(string invitationCode);

    // اگر ندارد، باید به اینترفیس IUserRepository اضافه شود و در کلاس پیاده‌سازی آن نیز نوشته شود.
    // مثال برای اینترفیس:
    /*
    public interface IUserRepository
    {
        // ...existing code...
        Task<Invitation?> GetInvitationByCodeAsync(string invitationCode);
        // ...existing code...
    }
    */

    // سپس در کلاس UserRepository پیاده‌سازی کنید:
    /*
    public async Task<Invitation?> GetInvitationByCodeAsync(string invitationCode)
    {
        // منطق دریافت دعوت‌نامه از دیتابیس
        // return await dbContext.Invitations.FirstOrDefaultAsync(x => x.Code == invitationCode);
    }
    */
}