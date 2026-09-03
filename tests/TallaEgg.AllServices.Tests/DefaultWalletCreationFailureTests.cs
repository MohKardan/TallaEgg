using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using TallaEgg.AllServices.Tests.Fakes;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using Users.Application;
using Users.Application.Mappers;
using Users.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Registration creates a user's default wallets — Toman, MAUA, CREDIT_MAUA — by calling
/// Wallet.Api, and deliberately does not fail the registration when that call fails. Until
/// issue #206 it also did not record the failure: the exception path wrote to
/// <c>Console.WriteLine</c> while an <c>ILogger</c> was injected and in use two lines above,
/// and under <c>sc.exe</c> a Windows service has no console. The non-2xx path did not even
/// reach that — <c>return response.IsSuccessStatusCode</c> turned a 404 or a 500 into a
/// discarded <c>false</c> with no log line at all.
///
/// <para>
/// That mattered more after #190 removed the only other route to wallet creation. A
/// registration burst during a Wallet.Api restart committed users with no wallets, and nothing
/// anywhere said which ones: a missing wallet is created lazily the first time it is written to
/// (see <c>WalletLazyCreationTests</c>), so afterwards the wallet database cannot tell those
/// users apart from anyone else. The Error entries asserted here are their only trace.
/// </para>
///
/// <para>
/// The same issue moved the wallet endpoint from GET to POST — a safe verb must not create
/// rows — so the request the client sends is pinned here too.
/// </para>
/// </summary>
public class DefaultWalletCreationFailureTests
{
    /// <summary>Records what was asked of it, then answers however the test told it to.</summary>
    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(answer(request));
        }
    }

    /// <summary>Hands out one client over the handler the test supplied, like the real named client.</summary>
    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("http://wallet.test/") };
    }

    /// <summary>
    /// Only <c>CreateAsync</c> is exercised: <c>RegisterUserAsync(User)</c> stores the user and
    /// then creates its wallets, which is the whole path under test. The rest of the interface
    /// throws rather than returning a plausible-looking default, so a test that strays into it
    /// fails loudly instead of quietly passing.
    /// </summary>
    private sealed class OneUserRepository : IUserRepository
    {
        public List<User> Created { get; } = [];

        public Task<User> CreateAsync(User user)
        {
            Created.Add(user);
            return Task.FromResult(user);
        }

        public Task<User?> GetByTelegramIdAsync(long telegramId) => throw new NotSupportedException();
        public Task<User?> GetByPhoneNumberAsync(string phone) => throw new NotSupportedException();
        public Task<User> UpdateAsync(User user) => throw new NotSupportedException();
        public Task<bool> ExistsByTelegramIdAsync(long telegramId) => throw new NotSupportedException();
        public Task<PagedResult<UserDto>> GetAllAsync(string? q, int page, int size) => throw new NotSupportedException();
        public Task<User?> GetByIdAsync(Guid id) => throw new NotSupportedException();
        public Task<User?> UpdateUserRoleAsync(Guid id, UserRole role) => throw new NotSupportedException();
        public Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role) => throw new NotSupportedException();
        public Task<Affiliate.Core.Invitation?> GetInvitationByCodeAsync(string invitationCode) => throw new NotSupportedException();
        public Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode) => throw new NotSupportedException();
        public Task<Guid?> GetUserIdByPhonenumberAsync(string phoneNumber) => throw new NotSupportedException();
    }

    private static User AnyNewUser() => new()
    {
        Id = Guid.NewGuid(),
        TelegramId = 1,
        CreatedAt = DateTime.UtcNow,
        InvitationCode = "abcde"
    };

    private static (UserService service, OneUserRepository repository, CapturingLogger<UserService> logger)
        ServiceOver(HttpMessageHandler handler)
    {
        var repository = new OneUserRepository();
        var logger = new CapturingLogger<UserService>();
        var service = new UserService(repository, new UserMapper(), new SingleClientFactory(handler), logger);
        return (service, repository, logger);
    }

    private static HttpResponseMessage Answer(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// The transport failure — Wallet.Api down, connection refused. It reaches the catch, and
    /// the entry has to carry the exception itself, not just its message: the type is what
    /// tells an operator a refused connection from a timeout.
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_WalletServiceUnreachable_LogsErrorWithTheException()
    {
        var handler = new RecordingHandler(_ => throw new HttpRequestException("Connection refused"));
        var (service, _, logger) = ServiceOver(handler);

        await service.RegisterUserAsync(AnyNewUser());

        var error = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.IsType<HttpRequestException>(error.Exception);
    }

    /// <summary>
    /// A registration whose wallet creation fails still succeeds. That is the deliberate part
    /// of this behaviour and the part a future change is most likely to break by accident.
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_WalletServiceUnreachable_StillRegistersTheUser()
    {
        var handler = new RecordingHandler(_ => throw new HttpRequestException("Connection refused"));
        var (service, repository, _) = ServiceOver(handler);
        var user = AnyNewUser();

        var registered = await service.RegisterUserAsync(user);

        Assert.Equal(user.Id, registered.Id);
        Assert.Single(repository.Created);
    }

    /// <summary>
    /// The path that never reached the old catch at all. <c>return response.IsSuccessStatusCode</c>
    /// turned a refusal into a discarded <c>false</c>, so the likelier of the two failures was
    /// the one that logged nothing.
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_WalletServiceReturnsNon2xx_LogsErrorWithStatusAndBody()
    {
        const string reason = "کیف پول وجود ندارد";
        var handler = new RecordingHandler(_ => Answer(HttpStatusCode.BadRequest, $"{{\"success\":false,\"message\":\"{reason}\"}}"));
        var (service, _, logger) = ServiceOver(handler);

        await service.RegisterUserAsync(AnyNewUser());

        var error = Assert.Single(logger.Entries, e => e.Level == LogLevel.Error);
        Assert.Contains("400", error.Message);
        Assert.Contains(reason, error.Message);
    }

    /// <summary>A refused wallet creation must not take the registration down with it either.</summary>
    [Fact]
    public async Task RegisterUserAsync_WalletServiceReturnsNon2xx_StillRegistersTheUser()
    {
        var handler = new RecordingHandler(_ => Answer(HttpStatusCode.InternalServerError, "boom"));
        var (service, repository, _) = ServiceOver(handler);
        var user = AnyNewUser();

        var registered = await service.RegisterUserAsync(user);

        Assert.Equal(user.Id, registered.Id);
        Assert.Single(repository.Created);
    }

    /// <summary>
    /// The verb, from the calling side. Wallet creation is a POST since issue #206; a GET here
    /// would have to be matched by a GET route, which is the pairing that broke #190's endpoint.
    /// </summary>
    [Fact]
    public async Task RegisterUserAsync_CreatesDefaultWallets_WithPostNotGet()
    {
        var handler = new RecordingHandler(_ => Answer(HttpStatusCode.OK, "{\"success\":true}"));
        var (service, _, _) = ServiceOver(handler);
        var user = AnyNewUser();

        await service.RegisterUserAsync(user);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/api/wallet/create-default/{user.Id}", request.RequestUri?.AbsolutePath);
    }

    /// <summary>A wallet creation that works logs nothing at Error — otherwise the check is noise.</summary>
    [Fact]
    public async Task RegisterUserAsync_WalletServiceSucceeds_LogsNoError()
    {
        var handler = new RecordingHandler(_ => Answer(HttpStatusCode.OK, "{\"success\":true}"));
        var (service, _, logger) = ServiceOver(handler);

        await service.RegisterUserAsync(AnyNewUser());

        Assert.DoesNotContain(logger.Entries, e => e.Level == LogLevel.Error);
    }

    /// <summary>
    /// The verb, from the serving side. The two halves have to agree and only one of them is
    /// covered by the client test above; standing Wallet.Api up for real needs the shared
    /// configuration file and a database, so the mapping is asserted against its source. If the
    /// route ever moves, this assertion is meant to be updated, not deleted.
    /// </summary>
    [Fact]
    public void WalletApi_MapsDefaultWalletCreation_AsPostNotGet()
    {
        var program = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "src", "Wallet", "Wallet.Api", "Program.cs"));

        Assert.Contains("app.MapPost(\"/api/wallet/create-default/{userId}\"", program);
        Assert.DoesNotContain("app.MapGet(\"/api/wallet/create-default/{userId}\"", program);
    }

    /// <summary>
    /// Walks up from the test assembly to the directory holding <c>TallaEgg.sln</c>. The same
    /// anchor <c>ConfigurationPrecedenceTests</c> and <c>SolutionMembershipTests</c> use, and
    /// duplicated for the reason they duplicate it: sharing it would mean editing tests this
    /// change has no business touching.
    /// </summary>
    private static string FindRepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TallaEgg.sln")))
        {
            dir = dir.Parent;
        }

        Assert.NotNull(dir);

        return dir.FullName;
    }
}
