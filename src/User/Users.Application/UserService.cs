using Affiliate.Core;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Utilties;
using Users.Application.Mappers;
using Users.Core;
using TallaEgg.Core.ErrorHandling;

namespace Users.Application;

public class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly UserMapper _userMapper;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UserService> _logger;

    public UserService(IUserRepository userRepository, UserMapper userMapper, IHttpClientFactory httpClientFactory, ILogger<UserService> logger)
    {
        _userRepository = userRepository;
        _userMapper = userMapper;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<UserDto> RegisterUserAsync(long telegramId,string invitationCode, string? username, string? firstName, string? lastName)
    {
        var createdByUserId = await GetUserIdByInvitationCode(invitationCode);
        if (createdByUserId == null) throw new BusinessRuleException("کد دعوت معتبر نیست.");
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
            Status = UserStatus.Pending,
            Role = UserRole.RegularUser,
            CreatedByUserId =  createdByUserId,
            InvitationCode = Utils.GenerateSecureRandomString(5), 
        };

        await _userRepository.CreateAsync(user);
        
        // Create the user's default wallets.
        await CreateDefaultWalletsAsync(user.Id);
        
        return _userMapper.Map(user);
    }

    public async Task<UserDto?> GetUserByTelegramIdAsync(long telegramId)
    {
        var user = await _userRepository.GetByTelegramIdAsync(telegramId);
        return _userMapper.Map(user);
    }

    public async Task<UserDto?> GetUserByPhoneNumberAsync(string phone)
    {
        var user = await _userRepository.GetByPhoneNumberAsync(phone);
        return _userMapper.Map(user);
    }

    public async Task<PagedResult<UserDto>> GetUsersAsync(string? q,int page,int size)
    {
        var users = await _userRepository.GetAllAsync(q,page,size);
       return users;
    }

    public async Task<UserDto> UpdateUserPhoneAsync(long telegramId, string phoneNumber)
    {
        var user = await _userRepository.GetByTelegramIdAsync(telegramId);
        if (user == null)
        {
            throw new BusinessRuleException("کاربر یافت نشد.");
        }

        user.PhoneNumber = phoneNumber;
        user.LastActiveAt = DateTime.UtcNow;
         await _userRepository.UpdateAsync(user);
        return _userMapper.Map(user);
    }

    public async Task<bool> UserExistsAsync(long telegramId)
    {
        return await _userRepository.ExistsByTelegramIdAsync(telegramId);
    }

    public async Task<UserDto> UpdateUserStatusAsync(long telegramId, UserStatus status)
    {
        var user = await _userRepository.GetByTelegramIdAsync(telegramId);
        if (user == null)
        {
            throw new BusinessRuleException("کاربر یافت نشد.");
        }

        user.Status = status;
        user.LastActiveAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user);
        return _userMapper.Map(user);
    }

    public async Task<Guid?> GetUserIdByInvitationCode(string invitationCode)
    {
        if (string.IsNullOrWhiteSpace(invitationCode))
            return null;

        // Look in the Users table first.
        var id = await _userRepository.GetUserIdByInvitationCodeAsync(invitationCode);
        if (id != null)
        {
            return id;
        }

        return null;
    }

    public async Task<Guid?> GetUserIdByPhoneNumber(string phoneNumber)
    {
        if (string.IsNullOrWhiteSpace(phoneNumber))
            return null;

        // Look in the Users table first.
        var id = await _userRepository.GetUserIdByPhonenumberAsync(phoneNumber);
        if (id != null)
        {
            return id;
        }

        return null;
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

    public async Task<User> RegisterUserAsync(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        var createdUser = await _userRepository.CreateAsync(user);
        
        // Create the user's default wallets.
        await CreateDefaultWalletsAsync(createdUser.Id);
        
        return createdUser;
    }

    public async Task<User?> UpdateUserRoleAsync(Guid userId, UserRole newRole)
    {
        return await _userRepository.UpdateUserRoleAsync(userId, newRole);
    }

    public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
    {
        return await _userRepository.GetUsersByRoleAsync(role);
    }

    public UserRole ParseUserRole(string roleString)
    {
        if (Enum.TryParse<UserRole>(roleString, true, out var role))
            return role;
        return UserRole.User; // Default.
    }

    /// <summary>
    /// Returns a user by id.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <returns>The user as a DTO, or null if there is no such user.</returns>
    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        return user != null ? _userMapper.Map(user) : null;
    }

    /// <summary>
    /// Creates the default wallets for a new user.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <returns>Whether it succeeded.</returns>
    private async Task<bool> CreateDefaultWalletsAsync(Guid userId)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient("WalletAPI");
            _logger.Log(LogLevel.Information,"درخواست برای ساخت کیف پول های پیش فرض" + " " + httpClient.BaseAddress + $"api/wallet/create-default/{userId}");
            
            var response = await httpClient.GetAsync($"api/wallet/create-default/{userId}");
            
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            // Log the error but don't fail user creation
            Console.WriteLine($"خطا در ایجاد کیف پول‌های پیش‌فرض برای کاربر {userId}: {ex.Message}");
            return false;
        }
    }
}