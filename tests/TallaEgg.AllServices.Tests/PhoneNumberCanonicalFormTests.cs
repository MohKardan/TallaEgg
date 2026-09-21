using TallaEgg.AllServices.Tests.Fakes;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.ErrorHandling;
using TallaEgg.Core.Utilties;
using Users.Application;
using Users.Application.Mappers;
using Users.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A phone number has one stored form, and every caller — the bot, an operator command, a direct
/// API call — arrives at it through the same function (issue #307).
///
/// <para>
/// Until now the rule lived in the bot alone: <c>SharedPhoneNumber.ToLocal</c>, <c>internal</c> to
/// <c>TallaEgg.TelegramBot.Infrastructure</c>, applied on the registration path and nowhere else.
/// Everything else compared the raw string. So the same number written two ways was two different
/// accounts as far as storage was concerned, and the duplicate guard added in #303 passed over it:
/// each form had exactly one holder.
/// </para>
///
/// <para>
/// The stored form, decided by the owner on 2026-09-21: an Iranian number is stored in local form
/// (<c>09…</c>), and a number from anywhere else is stored as its digits with the country code and
/// no plus — which is what Telegram delivers. Foreign numbers are accepted; one real account on
/// this database already has one.
/// </para>
/// </summary>
public class PhoneNumberCanonicalFormTests
{
    private const string Stored = "09151198161";

    private sealed class InMemoryUserRepository(params User[] users) : IUserRepository
    {
        public List<User> Users { get; } = [.. users];
        public List<User> Updated { get; } = [];

        public Task<User?> GetByTelegramIdAsync(long telegramId) =>
            Task.FromResult(Users.FirstOrDefault(u => u.TelegramId == telegramId));

        /// <summary>Exact string comparison, because that is what the database does.</summary>
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

    private static (UserService service, InMemoryUserRepository repository) ServiceOver(params User[] users)
    {
        var repository = new InMemoryUserRepository(users);
        return (new UserService(repository, new UserMapper(), new NoHttpClientFactory(),
            new CapturingLogger<UserService>()), repository);
    }

    // ── the function itself ─────────────────────────────────────────────────────

    /// <summary>
    /// An Iranian number reaches local form from every shape it can arrive in: the plus-less form
    /// Telegram sends, the <c>+98</c> form, the <c>0098</c> an operator may dial, local form
    /// already, and any of them with the separators a person types.
    /// </summary>
    [Theory]
    [InlineData("989151198161", "09151198161")]
    [InlineData("+989151198161", "09151198161")]
    [InlineData("00989151198161", "09151198161")]
    [InlineData("09151198161", "09151198161")]
    [InlineData("+98 915 119 8161", "09151198161")]
    [InlineData("0915-119-8161", "09151198161")]
    public void Canonical_IranianNumber_ReachesLocalForm(string arrived, string expected) =>
        Assert.Equal(expected, PhoneNumbers.Canonical(arrived));

    /// <summary>
    /// A number from anywhere else keeps its country code and loses only the punctuation. 98 is
    /// Iran's country code and no other country's, so stripping it cannot corrupt a foreign
    /// number — country codes are prefix-free.
    /// </summary>
    [Theory]
    [InlineData("18085551234", "18085551234")]
    [InlineData("+1 808 555 1234", "18085551234")]
    [InlineData("+44 7700 900123", "447700900123")]
    [InlineData("0044 7700 900123", "447700900123")]
    public void Canonical_ForeignNumber_KeepsItsCountryCode(string arrived, string expected) =>
        Assert.Equal(expected, PhoneNumbers.Canonical(arrived));

    /// <summary>
    /// Persian and Arabic-Indic digits reach the same form as their ASCII spelling. The bot
    /// converts digits in message text, but a contact card's number and a direct API call do not
    /// go through that, and <c>char.IsDigit</c> accepts the whole Unicode Nd category — so these
    /// would otherwise survive the strip, miss the ASCII prefix comparison, and be stored as a
    /// row no ASCII spelling of the same number could ever match, one the unique index would
    /// accept because it is a different string.
    /// </summary>
    [Theory]
    [InlineData("۰۹۱۵۱۱۹۸۱۶۱", "09151198161")]
    [InlineData("۹۸۹۱۵۱۱۹۸۱۶۱", "09151198161")]
    [InlineData("+۹۸ ۹۱۵ ۱۱۹ ۸۱۶۱", "09151198161")]
    [InlineData("٠٩١٥١١٩٨١٦١", "09151198161")]
    public void Canonical_PersianOrArabicDigits_ReachTheSameForm(string arrived, string expected) =>
        Assert.Equal(expected, PhoneNumbers.Canonical(arrived));

    /// <summary>Nothing to canonicalise is not an error; the caller decides what absent means.</summary>
    [Fact]
    public void Canonical_Nothing_StaysNothing()
    {
        Assert.Null(PhoneNumbers.Canonical(null));
        Assert.Equal(string.Empty, PhoneNumbers.Canonical(string.Empty));
    }

    // ── the lookup every operator command goes through ──────────────────────────

    /// <summary>
    /// The operator types the number the way they have it written down. It has to find the
    /// customer whichever form that is, or ش/د/م/س/ن/ت/ر answer "no such customer" for an account
    /// that is plainly there.
    /// </summary>
    [Theory]
    [InlineData("09151198161")]
    [InlineData("+989151198161")]
    [InlineData("989151198161")]
    [InlineData("0915 119 8161")]
    public async Task GetUserByPhoneNumberAsync_AnyWrittenForm_FindsTheAccount(string typedByOperator)
    {
        var (service, _) = ServiceOver(Person(1001, Stored));

        var found = await service.GetUserByPhoneNumberAsync(typedByOperator);

        Assert.NotNull(found);
        Assert.Equal(1001, found!.TelegramId);
    }

    // ── the uniqueness rule, which the raw comparison let through ───────────────

    /// <summary>
    /// The bypass: the same number in another form was a different string, so the duplicate check
    /// found one holder of each and let the second account claim it. Two rows, one real number,
    /// and #303's ambiguity guard blind to both because neither form is held twice.
    /// </summary>
    [Fact]
    public async Task UpdateUserPhoneAsync_SameNumberWrittenDifferently_IsStillRefused()
    {
        var (service, repository) = ServiceOver(Person(1001, Stored), Person(2002, phone: null));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.UpdateUserPhoneAsync(telegramId: 2002, "+989151198161"));

        Assert.Empty(repository.Updated);
    }

    /// <summary>What is written is the stored form, not the form the caller happened to send.</summary>
    [Fact]
    public async Task UpdateUserPhoneAsync_IranianNumber_StoresLocalForm()
    {
        var (service, repository) = ServiceOver(Person(2002, phone: null));

        await service.UpdateUserPhoneAsync(telegramId: 2002, "989151198161");

        Assert.Equal(Stored, Assert.Single(repository.Updated).PhoneNumber);
    }

    /// <summary>
    /// Foreign numbers are accepted (owner decision, 2026-09-21) and stored as delivered: digits,
    /// country code, no plus.
    /// </summary>
    [Fact]
    public async Task UpdateUserPhoneAsync_ForeignNumber_IsAcceptedInInternationalForm()
    {
        var (service, repository) = ServiceOver(Person(2002, phone: null));

        await service.UpdateUserPhoneAsync(telegramId: 2002, "+1 808 555 1234");

        Assert.Equal("18085551234", Assert.Single(repository.Updated).PhoneNumber);
    }

    /// <summary>
    /// A customer re-sharing their own number, in a different form than the one already stored,
    /// is not somebody else claiming it.
    /// </summary>
    [Fact]
    public async Task UpdateUserPhoneAsync_TheirOwnNumberInAnotherForm_IsNotARefusal()
    {
        var (service, repository) = ServiceOver(Person(1001, Stored));

        await service.UpdateUserPhoneAsync(telegramId: 1001, "+98 915 119 8161");

        Assert.Equal(Stored, Assert.Single(repository.Updated).PhoneNumber);
    }
}
