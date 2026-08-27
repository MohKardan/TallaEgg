using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The wallet service's real rejection reason must reach the caller.
///
/// It used to be discarded: every non-success response was mapped to the fixed string
/// "خطا در آزاد کردن موجودی" (which was itself copied from the unlock method and did not
/// even describe settlement). During live testing the wallet logged
/// "Fee exceeds the trade amount" while the outbox processor logged the generic string, so
/// finding the cause meant cross-referencing two services' logs by hand — and
/// OutboxMessage.LastError, which #39's reconciliation reads, stored the useless string.
/// See issue #38.
/// </summary>
public class SettlementFailureReasonTests
{
    private const string WalletBaseUrl = "http://localhost:60933/api";

    /// <summary>Returns a canned response so no wallet service is needed.</summary>
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body)
        {
            _status = status;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
    }

    private static WalletApiClient ClientReturning(HttpStatusCode status, string body)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["WalletApiUrl"] = WalletBaseUrl })
            .Build();

        return new WalletApiClient(
            new HttpClient(new StubHandler(status, body)),
            config,
            NullLogger<WalletApiClient>.Instance);
    }

    private static string ApiResponseBody(bool success, string message) =>
        JsonSerializer.Serialize(new ApiResponse<string>(success, message, null));

    private static TradeDto AnyTrade() => new()
    {
        Id = Guid.NewGuid(),
        BuyerUserId = Guid.NewGuid(),
        SellerUserId = Guid.NewGuid(),
        Symbol = "MAUA/IRT",
        Quantity = 10m,
        QuoteQuantity = 184_680_733m,
        FeeBuyer = 0m,
        FeeSeller = 0m
    };

    /// <summary>
    /// The exact scenario from the issue: the wallet rejects with a specific reason and the
    /// caller must see that reason, not a generic substitute.
    /// </summary>
    [Fact]
    public async Task Rejection_PropagatesTheWalletsOwnReason()
    {
        const string walletReason = "Fee exceeds the trade amount.";
        var client = ClientReturning(HttpStatusCode.BadRequest, ApiResponseBody(false, walletReason));

        var (success, message) = await client.TradeTransactionAndBalanceChangeAsync(AnyTrade());

        Assert.False(success);
        Assert.Equal(walletReason, message);
    }

    /// <summary>A different reason must come through as itself — not one hardcoded string swapped for another.</summary>
    [Fact]
    public async Task Rejection_PropagatesADifferentReasonVerbatim()
    {
        const string walletReason = "Seller locked MAUA (4.00) is less than required (12.00).";
        var client = ClientReturning(HttpStatusCode.BadRequest, ApiResponseBody(false, walletReason));

        var (success, message) = await client.TradeTransactionAndBalanceChangeAsync(AnyTrade());

        Assert.False(success);
        Assert.Equal(walletReason, message);
    }

    /// <summary>
    /// Reading the reason must never itself break settlement: an unparsable body still yields a
    /// failure with something actionable (the status code) rather than an exception.
    /// </summary>
    [Fact]
    public async Task Rejection_WithUnparsableBody_StillFailsWithAnInformativeMessage()
    {
        var client = ClientReturning(HttpStatusCode.InternalServerError, "<html>502 Bad Gateway</html>");

        var (success, message) = await client.TradeTransactionAndBalanceChangeAsync(AnyTrade());

        Assert.False(success);
        Assert.Contains("500", message);
    }

    /// <summary>An empty body is the other shape a proxy or a crashed service can return.</summary>
    [Fact]
    public async Task Rejection_WithEmptyBody_StillFailsWithAnInformativeMessage()
    {
        var client = ClientReturning(HttpStatusCode.ServiceUnavailable, string.Empty);

        var (success, message) = await client.TradeTransactionAndBalanceChangeAsync(AnyTrade());

        Assert.False(success);
        Assert.Contains("503", message);
    }

    [Fact]
    public async Task Success_IsReportedAsSuccess()
    {
        var client = ClientReturning(HttpStatusCode.OK, ApiResponseBody(true, "Trade settled"));

        var (success, _) = await client.TradeTransactionAndBalanceChangeAsync(AnyTrade());

        Assert.True(success);
    }
}
