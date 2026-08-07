using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Responses.Order;
using TallaEgg.Core.Services;
using TallaEgg.TelegramBot.Infrastructure.Services;
using TallaEgg.TelegramBot;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace Wallet.Tests.Fakes;

/// <summary>
/// Records the order the bot placed, and answers with a published quote (issue #65).
///
/// Only the members a conversation actually reaches are implemented; the rest throw, so a
/// test that wanders into an unconfigured call fails loudly instead of quietly passing
/// against a default value.
/// </summary>
public sealed class FakeOrderApiClient : IOrderApiClient
{
    /// <summary>The quote returned for any symbol. Null means dealer mode is off.</summary>
    public QuoteDto? ActiveQuote { get; set; }

    public List<(Guid UserId, string Symbol, OrderSide Side, decimal Quantity)> AcceptedQuotes { get; } = [];
    public List<OrderDto> SubmittedOrders { get; } = [];

    /// <summary>What <c>AcceptQuoteAsync</c> reports back.</summary>
    public (bool Success, string Message) AcceptResult { get; set; } = (true, "ok");

    public Task<QuoteDto?> GetActiveQuoteAsync(string symbol) => Task.FromResult(ActiveQuote);

    /// <summary>Quotes returned by the history endpoint, newest first.</summary>
    public List<QuoteDto> QuoteHistory { get; } = [];

    public Task<PagedResult<QuoteDto>> GetQuoteHistoryAsync(string symbol, int pageNumber = 1, int pageSize = 5) =>
        Task.FromResult(new PagedResult<QuoteDto>
        {
            Items = QuoteHistory.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList(),
            TotalCount = QuoteHistory.Count,
            PageNumber = pageNumber,
            PageSize = pageSize
        });

    public Task<(bool success, string message)> AcceptQuoteAsync(
        Guid userId, string symbol, OrderSide side, decimal quantity)
    {
        AcceptedQuotes.Add((userId, symbol, side, quantity));
        return Task.FromResult(AcceptResult);
    }

    public Task<(bool success, string message)> SubmitOrderAsync(OrderDto order)
    {
        SubmittedOrders.Add(order);
        return Task.FromResult((true, "ok"));
    }

    public Task<ApiResponse<BestPricesDto>> GetBestPricesAsync(string symbol) =>
        Task.FromResult(ApiResponse<BestPricesDto>.Ok(new BestPricesDto(), "ok"));

    public Task<ApiResponse<PagedResult<OrderHistoryDto>>> GetUserOrdersAsync(Guid userId, int pageNumber = 1, int pageSize = 10) =>
        throw new NotSupportedException(nameof(GetUserOrdersAsync));
    public Task<ApiResponse<PagedResult<TradeHistoryDto>>> GetUserTradesAsync(Guid userId, int pageNumber = 1, int pageSize = 10) =>
        throw new NotSupportedException(nameof(GetUserTradesAsync));
    public Task<ApiResponse<List<OrderHistoryDto>>> GetUserActiveOrdersAsync(Guid userId) =>
        throw new NotSupportedException(nameof(GetUserActiveOrdersAsync));
    public Task<ApiResponse<List<OrderHistoryDto>>> GetAllActiveOrdersAsync() =>
        throw new NotSupportedException(nameof(GetAllActiveOrdersAsync));
    public Task<(bool success, string message)> CancelOrderAsync(Guid orderId) =>
        throw new NotSupportedException(nameof(CancelOrderAsync));
    public Task<(bool success, string message)> PublishQuoteAsync(
        string symbol, decimal buyPrice, decimal sellPrice, Guid publishedByUserId) =>
        throw new NotSupportedException(nameof(PublishQuoteAsync));
    public Task<(bool success, string message, int cancelledCount)> CancelAllUserActiveOrdersAsync(Guid userId, string? reason = null) =>
        throw new NotSupportedException(nameof(CancelAllUserActiveOrdersAsync));
    public Task<ApiResponse<bool>> NotifyMatchingEngineAsync(NotifyMatchingEngineRequest request) =>
        throw new NotSupportedException(nameof(NotifyMatchingEngineAsync));

    // ── مظنهٔ اتومات (issue #90) ─────────────────────────────────────────────────

    public AutoQuoteSettingsDto? AutoQuoteSettings { get; set; }

    public Task<AutoQuoteSettingsDto?> GetAutoQuoteSettingsAsync(string symbol) => Task.FromResult(AutoQuoteSettings);

    /// <summary>Every spread update asked for.</summary>
    public List<(string Symbol, decimal SpreadPercent, Guid UpdatedByUserId)> SpreadUpdates { get; } = [];
    public (bool Success, string Message) SpreadUpdateResult { get; set; } = (true, "اسپرد به‌روزرسانی شد.");

    public Task<(bool success, string message)> UpdateAutoQuoteSpreadAsync(string symbol, decimal spreadPercent, Guid updatedByUserId)
    {
        SpreadUpdates.Add((symbol, spreadPercent, updatedByUserId));
        return Task.FromResult(SpreadUpdateResult);
    }

    /// <summary>Every enable/disable toggle asked for.</summary>
    public List<(string Symbol, bool IsEnabled, Guid UpdatedByUserId)> EnabledToggles { get; } = [];
    public (bool Success, string Message) EnabledToggleResult { get; set; } = (true, "انجام شد.");

    public Task<(bool success, string message)> SetAutoQuoteEnabledAsync(string symbol, bool isEnabled, Guid updatedByUserId)
    {
        EnabledToggles.Add((symbol, isEnabled, updatedByUserId));
        return Task.FromResult(EnabledToggleResult);
    }
}

/// <summary>Answers with one known, approved customer.</summary>
public sealed class FakeUsersApiClient : IUsersApiClient
{
    public UserDto? User { get; set; }

    /// <summary>
    /// Users reachable by phone number. When empty, <see cref="User"/> answers every lookup,
    /// which is what the order-placing flows rely on; the role command needs two distinct
    /// people (the operator and the target) so it fills this instead.
    /// </summary>
    public Dictionary<string, UserDto> UsersByPhone { get; } = [];

    public Task<UserDto?> GetUserAsync(long telegramId) => Task.FromResult(User);

    public Task<UserDto?> GetUserAsync(string phone) => Task.FromResult(
        UsersByPhone.Count == 0
            ? User
            : UsersByPhone.TryGetValue(phone, out var found) ? found : null);

    /// <summary>Every role change asked for, and what the call was told to answer.</summary>
    public List<(Guid UserId, UserRole NewRole)> RoleChanges { get; } = [];
    public (bool Success, string Message) RoleChangeResult { get; set; } = (true, "نقش کاربر با موفقیت به‌روزرسانی شد.");

    public Task<(bool success, string message)> UpdateRoleAsync(Guid userId, UserRole newRole)
    {
        RoleChanges.Add((userId, newRole));
        return Task.FromResult(RoleChangeResult);
    }

    public Task<ApiResponse<PagedResult<UserDto>>> GetUsersAsync(int pageNumber = 1, int pageSize = 10, string? searchTerm = null) =>
        throw new NotSupportedException(nameof(GetUsersAsync));
    /// <summary>Every registration attempted, with the invitation code it was made under.</summary>
    public List<(long TelegramId, string InvitationCode)> Registrations { get; } = [];

    public Task<(bool success, string message, Guid? userId)> RegisterUserAsync(
        long telegramId, string invitationCode, string? username, string? firstName, string? lastName)
    {
        Registrations.Add((telegramId, invitationCode));
        return Task.FromResult((true, "ok", (Guid?)Guid.NewGuid()));
    }
    /// <summary>What <c>UpdatePhoneAsync</c> answers; null means it succeeds with <see cref="User"/>.</summary>
    public ApiResponse<UserDto>? PhoneUpdateResult { get; set; }

    public Task<ApiResponse<UserDto>> UpdatePhoneAsync(long telegramId, string phoneNumber) =>
        Task.FromResult(PhoneUpdateResult ?? ApiResponse<UserDto>.Ok(User!, "ok"));
    /// <summary>Every status change asked for, and what the call was told to answer.</summary>
    public List<(long TelegramId, UserStatus NewStatus)> StatusChanges { get; } = [];
    public ApiResponse<UserDto>? StatusChangeResult { get; set; }

    public Task<ApiResponse<UserDto>> UpdateUserStatusAsync(long telegramId, UserStatus newStatus)
    {
        StatusChanges.Add((telegramId, newStatus));
        return Task.FromResult(StatusChangeResult ?? ApiResponse<UserDto>.Ok(new UserDto(), "ok"));
    }
    public Task<Guid?> GetUserIdByPhoneNumberAsync(string phonenumber) =>
        throw new NotSupportedException(nameof(GetUserIdByPhoneNumberAsync));

    /// <summary>Users reachable by id, for resolving the other side of a trade.</summary>
    public Dictionary<Guid, UserDto> UsersById { get; } = [];

    public Task<UserDto?> GetUserByIdAsync(Guid userId) =>
        Task.FromResult(UsersById.TryGetValue(userId, out var found) ? found : null);

    /// <summary>What the operator lookup answers; tests set this to whoever should be notified.</summary>
    public List<long> OperatorTelegramIds { get; set; } = [];

    public Task<List<long>> GetOperatorTelegramIdsAsync() => Task.FromResult(OperatorTelegramIds);
}

public sealed class FakeAffiliateApiClient : IAffiliateApiClient
{
    public Task<(bool success, string message, Guid? invitationId)> UseInvitationAsync(string invitationCode, Guid usedByUserId) =>
        Task.FromResult((true, "ok", (Guid?)Guid.NewGuid()));
}

/// <summary>
/// Swallows diagnostics. These calls are observational: they must never influence what the
/// customer sees, and a test should not post to a real Telegram channel.
/// </summary>
public sealed class SilentTelegramLogger : ITelegramLogger
{
    public Task Notif(string message, string chatId = "", string parseMode = "") => Task.CompletedTask;
    public Task Notif<T>(string message, T dto, string chatId = "", string parseMode = "") => Task.CompletedTask;
    public Task LogAsync<T>(string message, T dto, string chatId = "", string parseMode = "") => Task.CompletedTask;
    public Task LogAsync(string log, string chatId = "") => Task.CompletedTask;
    public Task ErrorAsync(Exception ex, string message = "") => Task.CompletedTask;
}

public sealed class FakeVersionService : IVersionService
{
    public string GetCurrentVersion() => "1.0.0";
    public string GetLastAnnouncedVersion() => "1.0.0";
    public void SaveAnnouncedVersion(string version) { }
}
