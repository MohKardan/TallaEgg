using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// What the bot tells a customer after <c>POST /api/orders</c> (issue #345).
///
/// <para>
/// <b>What was wrong:</b> on a refusal, <c>OrderApiClient.SubmitOrderAsync</c> returned
/// «خطا در ثبت سفارش: » followed by the <b>raw response body</b>, and the bot put that inside
/// <c>MsgOrderFailed</c>. So every refusal on this path, insufficient funds or a trading limit as much
/// as an order that failed to confirm, reached the customer as a line of JSON. The quote path never
/// had this: <c>AcceptQuoteAsync</c> reads the message out of the <c>ApiResponse</c> envelope.
/// </para>
/// </summary>
public class SubmitOrderResponseTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(answer(request));
    }

    private static OrderApiClient ClientAnswering(HttpStatusCode status, string body)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OrderApiUrl"] = "http://localhost:5140/api" })
            .Build();

        return new OrderApiClient(
            new HttpClient(new StubHandler(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            })),
            configuration,
            NullLogger<OrderApiClient>.Instance);
    }

    private static readonly OrderDto AnOrder = new()
    {
        Asset = CurrenciesConstant.MAUA_IRT,
        Amount = 1m,
        Price = 17_000_000m,
        UserId = Guid.NewGuid(),
        Side = OrderSide.Buy,
        Type = OrderType.Limit,
        TradingType = TradingType.Spot
    };

    [Fact]
    public async Task SubmitOrderAsync_OrderNotConfirmed_ReturnsTheServersSentence()
    {
        var client = ClientAnswering(HttpStatusCode.BadRequest,
            $$"""{"success":false,"message":"{{RefusalMessages.OrderNotPlaced}}","data":null}""");

        var (success, message) = await client.SubmitOrderAsync(AnOrder);

        Assert.False(success);
        Assert.Equal(RefusalMessages.OrderNotPlaced, message);
    }

    [Fact]
    public async Task SubmitOrderAsync_InsufficientFunds_ReturnsTheServersSentence()
    {
        var client = ClientAnswering(HttpStatusCode.BadRequest,
            $$"""{"success":false,"message":"{{RefusalMessages.InsufficientFunds}}","data":null}""");

        var (success, message) = await client.SubmitOrderAsync(AnOrder);

        Assert.False(success);
        Assert.Equal(RefusalMessages.InsufficientFunds, message);
    }

    /// <summary>
    /// A body that is not the envelope (a proxy's HTML page, an empty 500) still must not reach the
    /// customer verbatim.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "<html><body>Bad Gateway</body></html>")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    public async Task SubmitOrderAsync_UnreadableRefusal_DoesNotEchoTheBody(HttpStatusCode status, string body)
    {
        var client = ClientAnswering(status, body);

        var (success, message) = await client.SubmitOrderAsync(AnOrder);

        Assert.False(success);
        Assert.Equal(RefusalMessages.OrderServiceUnavailable, message);
        if (body.Length > 0)
            Assert.DoesNotContain(body, message);
    }

    [Fact]
    public async Task SubmitOrderAsync_Accepted_IsASuccess()
    {
        var client = ClientAnswering(HttpStatusCode.OK,
            """{"success":true,"message":"سفارش شما با موفقیت در Order Book قرار گرفت و منتظر تطبیق است","data":null}""");

        var (success, _) = await client.SubmitOrderAsync(AnOrder);

        Assert.True(success);
    }
}
