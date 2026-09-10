using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// That the bot asks cancel-active the way the endpoint listens (issue #270) and reads its answer
/// into the right number (issue #267).
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
///
/// The request-side tests were added by #270 and are the gap the review of #269 named: the stub
/// below has always been handed the <see cref="HttpRequestMessage"/> and never looked at it, so
/// nothing noticed that the reason was being posted in a body the endpoint does not read.
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

    // ── The request side (issue #270) ───────────────────────────────────────────
    //
    // The endpoint is `(Guid userId, string? reason, OrderService orderService)`. A simple type
    // that is not in the route template binds from the query string, and the published schema says
    // so — `reason` is declared `in: query` with no requestBody at all. So the query is the only
    // place the server will look.

    /// <summary>Captures the request the client actually builds, and answers a plausible success.</summary>
    private static (OrderApiClient Client, Func<HttpRequestMessage?> Sent) CapturingClient()
    {
        HttpRequestMessage? sent = null;
        var client = ClientAnswering(request =>
        {
            sent = request;
            return Json(
                HttpStatusCode.OK,
                """{"success":true,"message":"۱ سفارش فعال لغو شد","data":{"cancelledCount":1}}""");
        });

        return (client, () => sent);
    }

    /// <summary>
    /// The reason reaches the server, which means it is in the query string.
    /// </summary>
    /// <remarks>
    /// Asserted against <c>Uri.EscapeDataString</c> of the expected value rather than against a
    /// hand-written escape sequence. The reason is Persian, so it is percent-encoded on the wire,
    /// and writing the expected bytes out by hand would be asserting that this test and the client
    /// agree about UTF-8 rather than that the value survives.
    /// </remarks>
    [Fact]
    public async Task TheReason_IsSentInTheQueryString_WhereTheEndpointReadsIt()
    {
        const string reason = "کنسل شده توسط ادمین برای ثبت سفارش جدید";
        var (client, sent) = CapturingClient();

        await client.CancelAllUserActiveOrdersAsync(Guid.NewGuid(), reason);

        var request = sent();
        Assert.NotNull(request);
        Assert.Equal(HttpMethod.Post, request!.Method);
        Assert.Contains($"reason={Uri.EscapeDataString(reason)}", request.RequestUri!.Query, StringComparison.Ordinal);

        // And nowhere else. Asserting the query alone would stay green if a body were restored
        // beside it, which is exactly the edit this issue is about: the server ignores the body,
        // so nothing would fail and the next reader would find two apparent sources of truth.
        Assert.Null(request.Content);
    }

    /// <summary>
    /// And it survives the round trip, so the encoding is right rather than merely present.
    /// </summary>
    [Fact]
    public async Task ThePersianReason_DecodesBackToItself()
    {
        const string reason = "کنسل شده توسط ادمین برای ثبت سفارش جدید";
        var (client, sent) = CapturingClient();

        await client.CancelAllUserActiveOrdersAsync(Guid.NewGuid(), reason);

        var query = sent()!.RequestUri!.Query.TrimStart('?');
        var value = query.Split('&')
            .Select(pair => pair.Split('=', 2))
            .Where(parts => parts.Length == 2 && parts[0] == "reason")
            .Select(parts => Uri.UnescapeDataString(parts[1]))
            .Single();

        Assert.Equal(reason, value);
    }

    /// <summary>
    /// No reason means no parameter — not an empty one.
    /// </summary>
    /// <remarks>
    /// This is the edge the fix could easily get wrong. The endpoint writes
    /// <c>reason ?? "لغو همه سفارشات فعال"</c>, and an empty string is not null: sending
    /// <c>?reason=</c> would bind <c>""</c>, skip the fallback, and store an empty note — a
    /// different defect from the one #270 is about, introduced while fixing it.
    /// </remarks>
    [Fact]
    public async Task NoReason_SendsNoReasonParameter_SoTheServersDefaultStillApplies()
    {
        var (client, sent) = CapturingClient();

        await client.CancelAllUserActiveOrdersAsync(Guid.NewGuid());

        Assert.DoesNotContain("reason", sent()!.RequestUri!.Query, StringComparison.Ordinal);
    }
}
