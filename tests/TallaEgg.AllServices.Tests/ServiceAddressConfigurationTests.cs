using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Service addresses come from the calling service's own configuration section, and a missing
/// one stops the host rather than defaulting (issue #205).
///
/// <para>
/// Five clients used to end their address read with <c>?? "http://localhost:…"</c>. None of the
/// fallbacks was ever taken, because configuration happened to supply every value — which is why
/// they survived. Two pointed at ports this system does not serve: 5001 at nothing at all, and
/// 5135 at <c>TallaEgg.Api</c>, a host that starts, binds and maps no routes. Taking either
/// branch meant a service talking to something that answers nothing, with nothing in the logs.
/// </para>
/// </summary>
public class ServiceAddressConfigurationTests
{
    private static IConfiguration Configuration(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => s.Value))
            .Build();

    private static UsersApiClient NewUsersClient(IConfiguration configuration) =>
        new(new HttpClient(), configuration, NullLogger<UsersApiClient>.Instance);

    /// <summary>
    /// The defect behind section 2 of #205, and the one the removed scan hid.
    ///
    /// <para>
    /// <c>Orders.Api</c> defined no <c>UsersApiUrl</c>, so the client fell past its direct lookup
    /// into a scan of every key in the merged configuration for anything ending in
    /// <c>UsersApiUrl</c> — and the whole shared file is loaded, so other services' sections were
    /// visible as full-path keys. Two of them matched, with incompatible shapes: one bare root,
    /// one carrying an <c>/api</c> suffix. Which one <c>Orders.Api</c> received depended on
    /// configuration enumeration order. It worked; it was not chosen.
    /// </para>
    ///
    /// <para>
    /// A key belonging to another service must now be no key at all.
    /// </para>
    /// </summary>
    [Fact]
    public void UsersApiClient_WhenOnlyAnotherServicesSectionDefinesTheKey_Throws()
    {
        var configuration = Configuration(
            ("Services:TallaEgg.Api:UsersApiUrl", "http://localhost:5136"),
            ("Services:TallaEgg.TelegramBot.Infrastructure:UsersApiUrl", "http://localhost:5136/api"));

        Assert.Throws<InvalidOperationException>(() => NewUsersClient(configuration));
    }

    /// <summary>The flattened key from the host's own section is what the client reads.</summary>
    [Fact]
    public void UsersApiClient_WhenItsOwnKeyIsPresent_IsConstructed()
    {
        var client = NewUsersClient(Configuration(("UsersApiUrl", "http://localhost:5136/api")));

        Assert.NotNull(client);
    }

    /// <summary>The fallback was <c>http://localhost:5001/api</c>, a port nothing here serves.</summary>
    [Fact]
    public void UsersApiClient_WhenTheKeyIsAbsent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => NewUsersClient(Configuration()));
    }

    /// <summary>
    /// The fallback was <c>http://localhost:5135/api</c> — <c>TallaEgg.Api</c>, which maps zero
    /// endpoints. Orders are on 5140.
    /// </summary>
    [Fact]
    public void OrderApiClient_WhenTheKeyIsAbsent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new OrderApiClient(
            new HttpClient(), Configuration(), NullLogger<OrderApiClient>.Instance));
    }

    [Fact]
    public void OrderApiClient_WhenItsOwnKeyIsPresent_IsConstructed()
    {
        var client = new OrderApiClient(
            new HttpClient(),
            Configuration(("OrderApiUrl", "http://localhost:5140/api")),
            NullLogger<OrderApiClient>.Instance);

        Assert.NotNull(client);
    }

    /// <summary>
    /// The constructor <c>Orders.Api</c> resolves. Its fallback was the live address in that
    /// service — the registration at <c>Program.cs:118</c> was shadowed, so the configure
    /// delegate's address never applied and this read was the only one that counted.
    /// </summary>
    [Fact]
    public void WalletApiClient_WhenTheKeyIsAbsent_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new WalletApiClient(
            new HttpClient(), Configuration(), NullLogger<WalletApiClient>.Instance));
    }

    /// <summary>The configured address is what the client dials, parsed once at construction.</summary>
    [Fact]
    public void WalletApiClient_WhenTheKeyIsPresent_UsesItAsTheBaseAddress()
    {
        var httpClient = new HttpClient();

        _ = new WalletApiClient(
            httpClient,
            Configuration(("WalletApiUrl", "http://localhost:60933/api")),
            NullLogger<WalletApiClient>.Instance);

        Assert.Equal(new Uri("http://localhost:60933/api"), httpClient.BaseAddress);
    }

    /// <summary>
    /// The other constructor, the one the bot and the simulator use. They pass a value bound
    /// into <c>TelegramBotOptions</c>, so an absent key arrives here as null.
    /// </summary>
    [Fact]
    public void WalletApiClient_WhenTheAddressPassedInIsNull_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => new WalletApiClient(apiUrl: null));
    }
}
