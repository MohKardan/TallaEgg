using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// What the bot tells a customer when cancelling an order fails.
///
/// <para>
/// <b>What was wrong:</b> <c>OrderApiClient.CancelOrderAsync</c> returned «خطا در لغو سفارش: »
/// followed by the <b>raw response body</b>, and the <c>cancel_order_</c> callback prefixed it with
/// «❌ خطا در لغو سفارش: » again. A customer who tapped cancel on an order that had just completed
/// read the prefix twice and then a line of JSON. <c>SubmitOrderAsync</c> had the same defect until
/// #347; this is the same fix on the cancel path.
/// </para>
/// </summary>
public class CancelOrderResponseTests
{
    private sealed class StubHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
    }

    private static OrderApiClient ClientAnswering(HttpStatusCode status, string body)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OrderApiUrl"] = "http://localhost:5140/api" })
            .Build();

        return new OrderApiClient(
            new HttpClient(new StubHandler(status, body)),
            configuration,
            NullLogger<OrderApiClient>.Instance);
    }

    /// <summary>
    /// The endpoint's refusal for an order that has already completed, as
    /// <c>POST /api/orders/{id}/cancel</c> sends it.
    /// </summary>
    [Fact]
    public async Task CancelOrderAsync_OrderAlreadyCompleted_ReturnsOnlyTheServersSentence()
    {
        const string sentence = "سفارشات کامل شده یا رد شده قابل کنسل شدن نیستند";
        var client = ClientAnswering(HttpStatusCode.BadRequest,
            $$"""{"success":false,"message":"{{sentence}}"}""");

        var (success, message) = await client.CancelOrderAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.Equal(sentence, message);
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "<html><body>Bad Gateway</body></html>")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    public async Task CancelOrderAsync_UnreadableRefusal_SaysTheServiceDidNotAnswer(HttpStatusCode status, string body)
    {
        var client = ClientAnswering(status, body);

        var (success, message) = await client.CancelOrderAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.Equal(RefusalMessages.OrderServiceUnavailable, message);
    }

    [Fact]
    public async Task CancelOrderAsync_Accepted_IsASuccess()
    {
        var client = ClientAnswering(HttpStatusCode.OK,
            """{"success":true,"message":"سفارش با موفقیت لغو شد","orderId":"00000000-0000-0000-0000-000000000001"}""");

        var (success, _) = await client.CancelOrderAsync(Guid.NewGuid());

        Assert.True(success);
    }

    /// <summary>
    /// The administrator's bulk cancel had the same fallback: an empty error body produced
    /// «خطا در لغو سفارشات: » with nothing after it, and an HTML one threw inside the JSON reader.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.BadGateway, "<html><body>Bad Gateway</body></html>")]
    public async Task CancelAllUserActiveOrdersAsync_UnreadableRefusal_SaysTheServiceDidNotAnswer(
        HttpStatusCode status, string body)
    {
        var client = ClientAnswering(status, body);

        var (success, message, cancelledCount) = await client.CancelAllUserActiveOrdersAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.Equal(RefusalMessages.OrderServiceUnavailable, message);
        Assert.Equal(0, cancelledCount);
    }
}
