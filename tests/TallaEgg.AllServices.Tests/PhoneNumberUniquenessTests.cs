using Microsoft.Extensions.Logging;
using TallaEgg.AllServices.Tests.Fakes;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.ErrorHandling;
using Users.Application;
using Users.Application.Mappers;
using Users.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// One phone number, one account (issue #303).
///
/// <para>
/// Nothing used to stop a second account from claiming a number another account already held:
/// <c>UpdateUserPhoneAsync</c> assigned it, and <c>PhoneNumber</c> carried no unique index — it
/// has one since #307, filtered, and created only where the data allowed it. The
/// operator side then had no way to tell the two apart — it identifies customers by phone, and
/// the lookup returned whichever row the database handed back first. Ids are
/// <c>Guid.NewGuid()</c>, so which one that is, is not something the code decides.
/// </para>
///
/// <para>
/// So there are two rules here, and the second is not made redundant by the first: a duplicate
/// is refused when it is created, and a duplicate that exists anyway — a customer who deleted
/// their Telegram account and signed up again on the same number, or a row predating this
/// change — makes the lookup refuse to answer rather than guess.
/// </para>
/// </summary>
public class PhoneNumberUniquenessTests
{
    private const string SharedPhone = "09158527483";

    /// <summary>
    /// Holds the users the test set up. Everything the paths under test do not touch throws,
    /// so a test that strays fails loudly instead of quietly passing.
    /// </summary>
    private sealed class InMemoryUserRepository(params User[] users) : IUserRepository
    {
        public List<User> Users { get; } = [.. users];

        /// <summary>Every user handed to <c>UpdateAsync</c>, in order.</summary>
        public List<User> Updated { get; } = [];

        public Task<User?> GetByTelegramIdAsync(long telegramId) =>
            Task.FromResult(Users.FirstOrDefault(u => u.TelegramId == telegramId));

        public Task<IReadOnlyList<User>> GetAllByPhoneNumberAsync(string phoneNumber) =>
            Task.FromResult<IReadOnlyList<User>>([.. Users.Where(u => u.PhoneNumber == phoneNumber)]);

        public Task<User> UpdateAsync(User user)
        {
            Updated.Add(user);
            return Task.FromResult(user);
        }

        public Task<User> CreateAsync(User user) => throw new NotSupportedException();
        public Task<bool> ExistsByTelegramIdAsync(long telegramId) => throw new NotSupportedException();
        public Task<PagedResult<UserDto>> GetAllAsync(string? q, int page, int size) => throw new NotSupportedException();
        public Task<User?> GetByIdAsync(Guid id) => throw new NotSupportedException();
        public Task<User?> UpdateUserRoleAsync(Guid id, UserRole role) => throw new NotSupportedException();
        public Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role) => throw new NotSupportedException();
        public Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode) => throw new NotSupportedException();
        public Task<Guid?> GetUserIdByPhonenumberAsync(string phoneNumber) => throw new NotSupportedException();
    }

    /// <summary>Neither path under test calls Wallet.Api; reaching for a client is a test bug.</summary>
    private sealed class NoHttpClientFactory : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => throw new NotSupportedException(name);
    }

    private static User Person(long telegramId, string? phone) => new()
    {
        Id = Guid.NewGuid(),
        TelegramId = telegramId,
        FirstName = "کاربر",
        PhoneNumber = phone,
        CreatedAt = DateTime.UtcNow,
        InvitationCode = "abcde"
    };

    private static (UserService service, InMemoryUserRepository repository, CapturingLogger<UserService> logger)
        ServiceOver(params User[] users)
    {
        var repository = new InMemoryUserRepository(users);
        var logger = new CapturingLogger<UserService>();
        return (new UserService(repository, new UserMapper(), new NoHttpClientFactory(), logger), repository, logger);
    }

    // ── a number another account already holds ──────────────────────────────────

    [Fact]
    public async Task UpdateUserPhoneAsync_NumberHeldByAnotherAccount_Refuses()
    {
        var (service, _, _) = ServiceOver(Person(1001, SharedPhone), Person(2002, phone: null));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateUserPhoneAsync(telegramId: 2002, SharedPhone));
    }

    /// <summary>
    /// The refusal is read by the customer in the bot, so it has to tell them what to do. They
    /// cannot resolve this themselves — by construction, the number is on somebody else's
    /// account — so the only useful instruction is to contact support.
    /// </summary>
    [Fact]
    public async Task UpdateUserPhoneAsync_NumberHeldByAnotherAccount_TellsThemToContactSupport()
    {
        var (service, _, _) = ServiceOver(Person(1001, SharedPhone), Person(2002, phone: null));

        var refusal = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateUserPhoneAsync(telegramId: 2002, SharedPhone));

        Assert.Contains("پشتیبانی", refusal.Message);
    }

    /// <summary>Refused means nothing was written, not written and then complained about.</summary>
    [Fact]
    public async Task UpdateUserPhoneAsync_NumberHeldByAnotherAccount_StoresNothing()
    {
        var (service, repository, _) = ServiceOver(Person(1001, SharedPhone), Person(2002, phone: null));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateUserPhoneAsync(telegramId: 2002, SharedPhone));

        Assert.Empty(repository.Updated);
    }

    /// <summary>
    /// Traceable: support is going to be told "it says contact you", and needs to find the
    /// attempt. The log line carries both accounts' ids — never the number itself, which is a
    /// customer's personal data and does not belong in a log file.
    /// </summary>
    [Fact]
    public async Task UpdateUserPhoneAsync_NumberHeldByAnotherAccount_LogsBothAccounts()
    {
        var holder = Person(1001, SharedPhone);
        var (service, _, logger) = ServiceOver(holder, Person(2002, phone: null));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateUserPhoneAsync(telegramId: 2002, SharedPhone));

        var warning = Assert.Single(logger.Entries, e => e.Level == LogLevel.Warning);
        Assert.Contains("2002", warning.Message);
        Assert.Contains(holder.Id.ToString(), warning.Message);
        Assert.DoesNotContain(SharedPhone, warning.Message);
    }

    /// <summary>
    /// Re-sending the number already on your own account is not a duplicate. The bot asks for a
    /// contact again whenever the stored number is empty, and a retry after a failed update
    /// lands here.
    /// </summary>
    [Fact]
    public async Task UpdateUserPhoneAsync_TheirOwnNumberAgain_IsStored()
    {
        var (service, repository, _) = ServiceOver(Person(1001, SharedPhone));

        await service.UpdateUserPhoneAsync(telegramId: 1001, SharedPhone);

        Assert.Equal(SharedPhone, Assert.Single(repository.Updated).PhoneNumber);
    }

    [Fact]
    public async Task UpdateUserPhoneAsync_NumberNobodyHolds_IsStored()
    {
        var (service, repository, _) = ServiceOver(Person(2002, phone: null));

        await service.UpdateUserPhoneAsync(telegramId: 2002, SharedPhone);

        Assert.Equal(SharedPhone, Assert.Single(repository.Updated).PhoneNumber);
    }

    // ── a duplicate that exists anyway ──────────────────────────────────────────

    /// <summary>
    /// The operator commands — charge, deduct, role, approve, reject, balances, trades — all
    /// resolve a customer through this. Answering one of two accounts means a credit charge can
    /// land on the wrong one, so it must answer neither.
    /// </summary>
    [Fact]
    public async Task GetUserByPhoneNumberAsync_MoreThanOneAccount_Refuses()
    {
        var (service, _, _) = ServiceOver(Person(1001, SharedPhone), Person(2002, SharedPhone));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.GetUserByPhoneNumberAsync(SharedPhone));
    }

    [Fact]
    public async Task GetUserByPhoneNumberAsync_OneAccount_AnswersIt()
    {
        var (service, _, _) = ServiceOver(Person(1001, SharedPhone), Person(2002, phone: null));

        var found = await service.GetUserByPhoneNumberAsync(SharedPhone);

        Assert.Equal(1001, found?.TelegramId);
    }

    [Fact]
    public async Task GetUserByPhoneNumberAsync_NobodyHoldsIt_AnswersNull()
    {
        var (service, _, _) = ServiceOver(Person(1001, phone: null));

        Assert.Null(await service.GetUserByPhoneNumberAsync(SharedPhone));
    }
}
