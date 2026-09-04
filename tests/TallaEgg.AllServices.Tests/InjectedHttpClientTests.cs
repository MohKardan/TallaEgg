using System.Net;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The two clients that take an <see cref="HttpClient"/> from the container send their requests
/// through that client, and not through one of their own (issue #214).
///
/// <para>
/// Both constructors assigned the injected client and then, twenty lines later, replaced it with
/// <c>new HttpClient(handler)</c> over a freshly built <c>HttpClientHandler</c>. Nothing disposed
/// either object, so each instance leaked a connection pool and bypassed
/// <c>IHttpClientFactory</c>'s handler rotation. <c>Orders.Api</c> registers
/// <see cref="TallaEgg.TelegramBot.Infrastructure.Clients.UsersApiClient"/> <c>Scoped</c>, which
/// made that one pool per inbound request.
/// </para>
///
/// <para>
/// Reading the diff is not proof that the injected client is the one that carries traffic, so
/// these tests hand each client an <see cref="HttpClient"/> over a handler that records what it
/// is asked to send. If the client builds its own, the recorder sees nothing — and the request
/// goes to a host that does not resolve, which the clients swallow into a failed
/// <c>ApiResponse</c>, exactly the shape of a green test proving nothing.
/// </para>
/// </summary>
public class InjectedHttpClientTests
{
    /// <summary>Records every request and answers each one the same way.</summary>
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    "{\"success\":true,\"message\":\"ok\",\"data\":null}", Encoding.UTF8, "application/json")
            });
        }
    }

    private static IConfiguration Configuration(string key, string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [key] = value })
            .Build();

    [Fact]
    public async Task UsersApiClient_SendsThroughTheInjectedHttpClient()
    {
        var recorder = new RecordingHandler();
        var client = new UsersApiClient(
            new HttpClient(recorder),
            Configuration("UsersApiUrl", "http://users.test/api"),
            NullLogger<UsersApiClient>.Instance);

        await client.GetUsersAsync(pageNumber: 1, pageSize: 10);

        var sent = Assert.Single(recorder.Requests);
        Assert.StartsWith("http://users.test/api/users/list", sent.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task OrderApiClient_SendsThroughTheInjectedHttpClient()
    {
        var recorder = new RecordingHandler();
        var client = new OrderApiClient(
            new HttpClient(recorder),
            Configuration("OrderApiUrl", "http://orders.test/api"),
            NullLogger<OrderApiClient>.Instance);

        await client.GetUserOrdersAsync(Guid.NewGuid(), pageNumber: 1, pageSize: 10);

        var sent = Assert.Single(recorder.Requests);
        Assert.StartsWith("http://orders.test/api/orders/user/", sent.RequestUri!.ToString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// The header the discarded client used to carry. It is set on the injected instance now, so
    /// it has to still reach the wire — <c>Wallet.Api</c>, <c>Users.Api</c> and <c>Orders.Api</c>
    /// reject a Production request without it.
    /// </summary>
    [Theory]
    [InlineData("users")]
    [InlineData("orders")]
    public async Task BothClients_StillSendTheApiKeyHeader(string which)
    {
        var recorder = new RecordingHandler();

        if (which == "users")
        {
            var client = new UsersApiClient(
                new HttpClient(recorder),
                Configuration("UsersApiUrl", "http://users.test/api"),
                NullLogger<UsersApiClient>.Instance);

            await client.GetUsersAsync(pageNumber: 1, pageSize: 10);
        }
        else
        {
            var client = new OrderApiClient(
                new HttpClient(recorder),
                Configuration("OrderApiUrl", "http://orders.test/api"),
                NullLogger<OrderApiClient>.Instance);

            await client.GetUserOrdersAsync(Guid.NewGuid(), pageNumber: 1, pageSize: 10);
        }

        var sent = Assert.Single(recorder.Requests);
        Assert.True(sent.Headers.Contains("X-API-Key"));
    }

    /// <summary>
    /// A null client used to be harmless, because the constructor threw it away and built its
    /// own. It is load-bearing now, so it is rejected where the mistake is made rather than at
    /// the first call.
    /// </summary>
    [Fact]
    public void UsersApiClient_WithoutAnHttpClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new UsersApiClient(
            null!, Configuration("UsersApiUrl", "http://users.test/api"), NullLogger<UsersApiClient>.Instance));
    }

    [Fact]
    public void OrderApiClient_WithoutAnHttpClient_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new OrderApiClient(
            null!, Configuration("OrderApiUrl", "http://orders.test/api"), NullLogger<OrderApiClient>.Instance));
    }
}
