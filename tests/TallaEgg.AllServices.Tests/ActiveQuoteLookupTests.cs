using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// What <c>GetActiveQuoteAsync</c> makes of each answer the Orders service can give (issue #258).
///
/// <para>
/// The bot's whole dealer path turns on one bit: was the service reached? A quote that is
/// published but unreadable must not look like a quote that does not exist, because every symbol
/// is in dealer mode and an order placed on the order-book fallback rests forever with the
/// customer's collateral locked while they are told it succeeded.
/// </para>
///
/// <para>
/// These tests cross HTTP, and that is the point. The conversation-level tests in
/// <see cref="BotConversationFlowTests"/> drive a fake client, so they cannot see how a status
/// code is mapped — and the first version of this fix mapped 404 to "unreachable", which would
/// have made the order-book fallback unreachable in production for every unquoted symbol while
/// every fake-driven test stayed green.
/// </para>
/// </summary>
public class ActiveQuoteLookupTests
{
    private const string Symbol = "MAUA/IRT";

    /// <summary>Answers every request with whatever the test supplied.</summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }

    private static OrderApiClient ClientAnswering(Func<HttpRequestMessage, HttpResponseMessage> answer)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OrderApiUrl"] = "http://localhost:5140/api" })
            .Build();

        return new OrderApiClient(
            new HttpClient(new StubHandler(answer)),
            configuration,
            NullLogger<OrderApiClient>.Instance);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    /// <summary>
    /// The service answered and there is no quote. This is the one case the order-book fallback
    /// is for, and <c>GET /api/quotes/{Base}/{Quote}</c> reports it as <c>404</c> — so a mapping
    /// that keys off <c>IsSuccessStatusCode</c> alone gets it exactly backwards.
    /// </summary>
    [Fact]
    public async Task NotFound_IsTheServiceSayingThereIsNoQuote_NotAFailureToReachIt()
    {
        var client = ClientAnswering(_ => Json(
            HttpStatusCode.NotFound,
            """{"success":false,"message":"مظنه‌ای منتشر نشده است.","data":null}"""));

        var (reached, quote) = await client.GetActiveQuoteAsync(Symbol);

        Assert.True(reached, "404 means the service answered; only the order-book path is correct here");
        Assert.Null(quote);
    }

    /// <summary>
    /// The readiness gate answers 503 until migration finishes (#230). The quote may be published
    /// and active — this is the case that must never reach the order book.
    /// </summary>
    [Fact]
    public async Task ServiceUnavailable_IsNotReached()
    {
        var client = ClientAnswering(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));

        var (reached, quote) = await client.GetActiveQuoteAsync(Symbol);

        Assert.False(reached);
        Assert.Null(quote);
    }

    /// <summary>A timeout, a reset, a DNS failure — no answer at all.</summary>
    [Fact]
    public async Task AThrownRequest_IsNotReached()
    {
        var client = ClientAnswering(_ => throw new HttpRequestException("connection reset"));

        var (reached, quote) = await client.GetActiveQuoteAsync(Symbol);

        Assert.False(reached);
        Assert.Null(quote);
    }

    /// <summary>The ordinary case, so the mapping is pinned in both directions.</summary>
    [Fact]
    public async Task APublishedQuote_IsReachedAndReturned()
    {
        var client = ClientAnswering(_ => Json(
            HttpStatusCode.OK,
            """{"success":true,"message":null,"data":{"symbol":"MAUA/IRT","buyPrice":18237000,"sellPrice":18468073.32}}"""));

        var (reached, quote) = await client.GetActiveQuoteAsync(Symbol);

        Assert.True(reached);
        Assert.NotNull(quote);
        Assert.Equal(18_468_073.32m, quote!.SellPrice);
    }
}
