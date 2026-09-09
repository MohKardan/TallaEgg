using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That the bot reads the cancel-active response into the right number (issue #267).
/// </summary>
/// <remarks>
/// This is the one code path whose type identity changed when the duplicate
/// <c>CancelActiveOrdersResponseDto</c> was collapsed onto the one in <c>TallaEgg.Core</c>. Nothing
/// covered it: <c>FakeOrderApiClient.CancelAllUserActiveOrdersAsync</c> throws
/// <c>NotSupportedException</c>, and the simulator never cancels anything, so a green suite and a
/// green smoke run both said nothing about it.
///
/// It crosses HTTP for the reason <see cref="ActiveQuoteLookupTests"/> gives: a fake client cannot
/// see how a body is parsed, and parsing is the whole of what changed here. The body below is not
/// invented — it is what the running Orders service answered on 2026-09-09:
/// <c>{"success":true,"message":"0 سفارش فعال لغو شد","data":{"cancelledCount":0}}</c>.
///
/// The camelCase <c>cancelledCount</c> against the PascalCase property is the part worth keeping an
/// eye on. It works because this call site deserializes with <c>Newtonsoft.Json</c>, which matches
/// names case-insensitively with nothing configured — one of the twenty-two reads #233 left on that
/// library. Migrate this one to <c>System.Text.Json</c> without <c>ApiJson.ResponseOptions</c> and
/// the count silently becomes zero; this test is what would say so.
/// </remarks>
public class CancelActiveOrdersResponseTests
{
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

    /// <summary>The shape the service actually answers, with a count that is not the default.</summary>
    [Fact]
    public async Task ACancelledCount_IsReadOffTheWire_AndNotLeftAtZero()
    {
        var client = ClientAnswering(_ => Json(
            HttpStatusCode.OK,
            """{"success":true,"message":"۳ سفارش فعال لغو شد","data":{"cancelledCount":3}}"""));

        var (success, message, cancelledCount) = await client.CancelAllUserActiveOrdersAsync(Guid.NewGuid());

        Assert.True(success);
        Assert.Equal("۳ سفارش فعال لغو شد", message);
        Assert.Equal(3, cancelledCount);
    }

    /// <summary>
    /// Cancelling when there is nothing to cancel is a success with a count of zero, which is the
    /// exact body the running service returned. Worth pinning separately: zero is also what a
    /// failed parse produces, so only the pair of tests tells the two apart.
    /// </summary>
    [Fact]
    public async Task NothingToCancel_IsStillASuccess()
    {
        var client = ClientAnswering(_ => Json(
            HttpStatusCode.OK,
            """{"success":true,"message":"0 سفارش فعال لغو شد","data":{"cancelledCount":0}}"""));

        var (success, _, cancelledCount) = await client.CancelAllUserActiveOrdersAsync(Guid.NewGuid());

        Assert.True(success);
        Assert.Equal(0, cancelledCount);
    }

    /// <summary>A refusal carries the server's reason rather than a generic one.</summary>
    [Fact]
    public async Task ARefusal_KeepsTheServersMessage()
    {
        var client = ClientAnswering(_ => Json(
            HttpStatusCode.BadRequest,
            """{"success":false,"message":"کاربر یافت نشد","data":null}"""));

        var (success, message, cancelledCount) = await client.CancelAllUserActiveOrdersAsync(Guid.NewGuid());

        Assert.False(success);
        Assert.Equal("کاربر یافت نشد", message);
        Assert.Equal(0, cancelledCount);
    }
}
