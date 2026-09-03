using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// A rejected wallet operation has to reach the admin with the reason the wallet gave.
///
/// <para>
/// The endpoints answer a refusal with 400 and the <c>BusinessRuleException</c> message in the
/// body — Persian, written for the person reading it, which is that exception type's entire
/// contract. The client discarded it and substituted "خطا در بروزرسانی", so an admin who asked to
/// deduct more credit than a customer holds was told the system had failed rather than that the
/// wallet had refused them.
/// </para>
///
/// <para>
/// Found by driving the real bot: deducting 100 against a credit of 50 produced
/// "❌ عملیات انجام نشد. دلیل: خطا در بروزرسانی", while the wallet had in fact answered
/// "مقدار کسر از حساب بیشتر از حد مجاز است". It matters more now that the deduction command is
/// the way credit is reduced: asking for more than exists is an ordinary mistake, not an exotic one.
/// </para>
/// </summary>
public class WalletApiClientFailureMessageTests
{
    private const string TheWalletsReason = "مقدار کسر از حساب بیشتر از حد مجاز است";

    /// <summary>Answers every request with one canned response, so the client is the only thing under test.</summary>
    private sealed class CannedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private static WalletApiClient ClientAnswering(HttpStatusCode status, string body) =>
        new(new HttpClient(new CannedHandler(status, body)),
            // The constructor sets the base address from this key and now requires it to be
            // there (issue #205). The canned handler answers whatever is asked, so the host is
            // only ever a label.
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["WalletApiUrl"] = "http://wallet.test/api" })
                .Build(),
            NullLogger<WalletApiClient>.Instance);

    private static WalletRequest AnyRequest() => new()
    {
        UserId = Guid.NewGuid(),
        Asset = "CREDIT_MAUA",
        Amount = 100m
    };

    [Fact]
    public async Task AWithdrawalRefusedByTheWalletKeepsTheWalletsReason()
    {
        var client = ClientAnswering(HttpStatusCode.BadRequest,
            $"{{\"success\":false,\"message\":\"{TheWalletsReason}\",\"data\":null}}");

        var result = await client.WithdrawalAsync(AnyRequest());

        Assert.False(result.Success);
        Assert.Equal(TheWalletsReason, result.Message);
    }

    [Fact]
    public async Task ADepositRefusedByTheWalletKeepsTheWalletsReason()
    {
        const string reason = "کیف پول وجود ندارد";

        var client = ClientAnswering(HttpStatusCode.BadRequest,
            $"{{\"success\":false,\"message\":\"{reason}\",\"data\":null}}");

        var result = await client.DepositeAsync(AnyRequest());

        Assert.False(result.Success);
        Assert.Equal(reason, result.Message);
    }

    /// <summary>
    /// A failure with nothing to say falls back to the generic sentence. An unhandled fault's detail
    /// is not written for a customer, so there is nothing here worth passing on.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("<html>500 Internal Server Error</html>")]
    [InlineData("{\"success\":false,\"message\":null,\"data\":null}")]
    [InlineData("{\"success\":false,\"message\":\"   \",\"data\":null}")]
    public async Task AFailureWithNoUsableMessageFallsBackToTheGenericOne(string body)
    {
        var client = ClientAnswering(HttpStatusCode.InternalServerError, body);

        var result = await client.WithdrawalAsync(AnyRequest());

        Assert.False(result.Success);
        Assert.Equal("خطا در بروزرسانی", result.Message);
    }
}
