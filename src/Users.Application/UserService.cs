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
} 