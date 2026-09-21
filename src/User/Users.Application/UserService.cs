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
        
        return _userMapper.MapRequired(user);
    }

    public async Task<UserDto?> GetUserByTelegramIdAsync(long telegramId)
    {
        var user = await _userRepository.GetByTelegramIdAsync(telegramId);
        return _userMapper.Map(user);
    }

    /// <summary>
    /// The operator side of the bot identifies a customer by phone number — charging credit,
    /// deducting it, changing a role, approving, rejecting. When two accounts hold the number,
    /// answering one of them is a coin toss the caller cannot see, so this refuses instead.
    /// Refusing costs the operator a puzzled moment; guessing costs a customer's credit (#303).
    /// </summary>
    public async Task<UserDto?> GetUserByPhoneNumberAsync(string phone)
    {
        // The operator types the number the way they have it written down. Storage holds one
        // form, so the question has to be asked in that form or an account plainly there answers
        // "no such customer" (issue #307).
        var holders = await _userRepository.GetAllByPhoneNumberAsync(PhoneNumbers.Canonical(phone)!);

        if (holders.Count > 1)
        {
            _logger.LogWarning(
                "Phone number lookup refused: {Count} accounts hold it — {UserIds}.",
                holders.Count,
                string.Join(", ", holders.Select(h => h.Id)));

            throw new BusinessRuleException(
                "این شماره روی بیش از یک حساب ثبت شده است و تا اصلاح آن، دستور روی هیچ حسابی اجرا نمی‌شود. " +
                "لطفاً با پشتیبانی تماس بگیرید.");
        }

        return _userMapper.Map(holders.SingleOrDefault());
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

        // One number, one account. Storage enforces this too since #307 — a filtered unique
        // index on PhoneNumber — but the rule stays here as well, because the index can only
        // answer with a constraint violation and this can say something the customer can act on.
        // The index is also created only when the data allowed it, so it may be absent on a
        // database that still holds duplicates.
        //
        // The customer cannot fix this themselves: by construction the number is on an account
        // that is not theirs. Support can, which is why the message sends them there and the log
        // line names both accounts. The number itself is deliberately absent from the log; it is
        // personal data, and the two ids are what an operator needs anyway (issue #303).
        // Canonical before the comparison and before the write, so "the same number" is one
        // string rather than however many ways it can be written. Without it the duplicate check
        // below finds one holder of each form and lets the second account claim it, and #303's
        // ambiguity guard sees nothing either (issue #307).
        phoneNumber = PhoneNumbers.Canonical(phoneNumber)!;

        var otherHolder = (await _userRepository.GetAllByPhoneNumberAsync(phoneNumber))
            .FirstOrDefault(holder => holder.Id != user.Id);

        if (otherHolder is not null)
        {
            _logger.LogWarning(
                "Phone number update refused for Telegram id {TelegramId}: already registered to user {ExistingUserId}.",
                telegramId,
                otherHolder.Id);

            throw new BusinessRuleException(
                "این شماره تلفن قبلاً برای حساب دیگری ثبت شده است.\n" +
                $"لطفاً با پشتیبانی تماس بگیرید و کد پیگیری {telegramId} را اعلام کنید.");
        }

        user.PhoneNumber = phoneNumber;
        user.LastActiveAt = DateTime.UtcNow;
         await _userRepository.UpdateAsync(user);
        return _userMapper.MapRequired(user);
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
        return _userMapper.MapRequired(user);
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

        // Same form as every other phone lookup. This one still answers an arbitrary holder
        // when a number is on more than one account — its endpoint returns a bare Guid with
        // nowhere to carry a refusal — and no bot command calls it any more (issue #307).
        var id = await _userRepository.GetUserIdByPhonenumberAsync(PhoneNumbers.Canonical(phoneNumber)!);
        if (id != null)
        {
            return id;
        }

        return null;
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
    /// <remarks>
    /// A failure here deliberately does not fail the registration: a user should not be turned
    /// away because Wallet.Api happens to be restarting. It does have to be recorded, though.
    /// Until issue #206 it was not — failures went to <c>Console.WriteLine</c>, and under
    /// <c>sc.exe</c> a Windows service has no console, so a registration burst during a
    /// Wallet.Api restart committed users with no wallets and said so nowhere. These Error
    /// entries are the only trace such a user leaves: a missing wallet is created lazily the
    /// first time it is written to, so afterwards nothing in the wallet database distinguishes
    /// them from anyone else.
    /// </remarks>
    /// <param name="userId">User id.</param>
    private async Task CreateDefaultWalletsAsync(Guid userId)
    {
        try
        {
            using var httpClient = _httpClientFactory.CreateClient("WalletAPI");
            _logger.Log(LogLevel.Information,"درخواست برای ساخت کیف پول های پیش فرض" + " " + httpClient.BaseAddress + $"api/wallet/create-default/{userId}");

            using var response = await httpClient.PostAsync($"api/wallet/create-default/{userId}", null);

            if (!response.IsSuccessStatusCode)
            {
                // A non-2xx never reaches the catch below — it is a completed request, not an
                // exception — and it is the likelier of the two failures, so it needs its own
                // Error entry. The body carries whatever reason the wallet service gave.
                var body = Truncated(await response.Content.ReadAsStringAsync());
                _logger.LogError(
                    "Default wallet creation for user {UserId} was refused: HTTP {StatusCode}. Response: {ResponseBody}. Registration continues; this user's default wallets may be missing or incomplete.",
                    userId, (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // Stays broad on purpose: nothing is rethrown, so narrowing it would turn some
            // failures into failed registrations, which is exactly what the remarks above say
            // must not happen. The exception is logged, so its type is visible in the log
            // rather than flattened away here.
            _logger.LogError(ex,
                "Default wallet creation for user {UserId} failed. Registration continues; this user's default wallets may be missing or incomplete.",
                userId);
        }
    }

    /// <summary>
    /// Caps a response body before it is logged. An intermediary answering with an HTML error
    /// page would otherwise be copied whole into the rolling log once per registration, and a
    /// registration burst is exactly when this logging fires.
    /// </summary>
    private static string Truncated(string body) =>
        body.Length <= MaxLoggedResponseBody
            ? body
            : string.Concat(body.AsSpan(0, MaxLoggedResponseBody), "… (truncated)");

    private const int MaxLoggedResponseBody = 500;
}