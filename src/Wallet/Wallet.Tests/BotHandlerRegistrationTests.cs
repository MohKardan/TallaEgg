using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using TallaEgg.Core.Services;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using TallaEgg.TelegramBot.Infrastructure.Messaging;
using TallaEgg.TelegramBot.Infrastructure.Services;
using Telegram.Bot;
using Wallet.Tests.Fakes;

namespace Wallet.Tests;

/// <summary>
/// The bot's dependency wiring must actually resolve (issue #65).
///
/// A constructor parameter type is matched against the container at run time, not build
/// time. When <c>BotHandler</c> moved from the concrete API clients to their interfaces the
/// solution still compiled cleanly and every other test still passed — but the bot would
/// have died on startup with "unable to resolve IOrderApiClient", and the only way to find
/// out was to launch it.
///
/// This calls the very same <c>AddBotHandler</c> that <c>Program.cs</c> calls, so it covers
/// the production wiring rather than a copy that could drift.
/// </summary>
public class BotHandlerRegistrationTests
{
    /// <summary>
    /// Registers the concrete clients the way <c>Program.cs</c> does, but built from
    /// harmless values — no HTTP call is made by construction, only by use.
    /// </summary>
    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection().Build());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddLogging();
        services.AddHttpClient();

        services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient("1:AA"));
        services.AddSingleton(p => new OrderApiClient(
            new HttpClient { BaseAddress = new Uri("http://localhost") },
            p.GetRequiredService<IConfiguration>(),
            p.GetRequiredService<ILogger<OrderApiClient>>()));
        services.AddSingleton(p => new UsersApiClient(
            new HttpClient { BaseAddress = new Uri("http://localhost") },
            p.GetRequiredService<IConfiguration>(),
            p.GetRequiredService<ILogger<UsersApiClient>>()));
        services.AddSingleton(p => new AffiliateApiClient(
            "http://localhost", new HttpClient(), p.GetRequiredService<ILogger<AffiliateApiClient>>()));
        services.AddSingleton(_ => new WalletApiClient("http://localhost"));
        services.AddSingleton(p => new TelegramLoggerService(
            p.GetRequiredService<IHttpClientFactory>(), "1:AA"));
        services.AddSingleton<IVersionService, FakeVersionService>();

        services.AddBotHandler();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// The assertion that would have caught the interface switch: every dependency the
    /// handler declares can be satisfied.
    /// </summary>
    [Fact]
    public void TheBotHandler_CanBeResolved()
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService<IBotHandler>());
    }

    [Theory]
    [InlineData(typeof(IOrderApiClient))]
    [InlineData(typeof(IUsersApiClient))]
    [InlineData(typeof(IAffiliateApiClient))]
    [InlineData(typeof(IWalletApiClient))]
    [InlineData(typeof(ITelegramLogger))]
    [InlineData(typeof(IBotMessenger))]
    [InlineData(typeof(IConversationStore))]
    public void EveryAbstractionTheHandlerNeeds_IsRegistered(Type serviceType)
    {
        using var provider = BuildProvider();

        Assert.NotNull(provider.GetRequiredService(serviceType));
    }

    /// <summary>
    /// The interface registrations must forward to the instances already registered, not
    /// construct second ones. A duplicate client would carry its own HttpClient, doubling
    /// the connection pool and making any future per-client state silently diverge.
    /// </summary>
    [Fact]
    public void TheInterfacesResolveToTheSameInstancesAsTheConcreteClients()
    {
        using var provider = BuildProvider();

        Assert.Same(provider.GetRequiredService<OrderApiClient>(), provider.GetRequiredService<IOrderApiClient>());
        Assert.Same(provider.GetRequiredService<UsersApiClient>(), provider.GetRequiredService<IUsersApiClient>());
        Assert.Same(provider.GetRequiredService<WalletApiClient>(), provider.GetRequiredService<IWalletApiClient>());
        Assert.Same(provider.GetRequiredService<TelegramLoggerService>(), provider.GetRequiredService<ITelegramLogger>());
    }

    /// <summary>
    /// The conversation store must be a singleton. Scoped or transient would hand each
    /// update its own store, so the customer's asset and side would be forgotten between
    /// one message and the next and every order would start over.
    /// </summary>
    [Fact]
    public void TheConversationStore_IsSharedAcrossScopes()
    {
        using var provider = BuildProvider();

        using var scopeA = provider.CreateScope();
        using var scopeB = provider.CreateScope();

        Assert.Same(
            scopeA.ServiceProvider.GetRequiredService<IConversationStore>(),
            scopeB.ServiceProvider.GetRequiredService<IConversationStore>());
    }

    /// <summary>
    /// Building the handler must have no side effects. It used to start a background thread
    /// and message every user from its constructor, which ran before the rest of the
    /// container had finished being wired — and made it impossible to construct in a test.
    /// </summary>
    [Fact]
    public void ResolvingTheHandler_SendsNothingAndStartsNothing()
    {
        var messenger = new FakeBotMessenger();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection().Build());
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
        services.AddSingleton<ITelegramBotClient>(_ => new TelegramBotClient("1:AA"));
        services.AddSingleton<IBotMessenger>(messenger);
        services.AddSingleton<IConversationStore, InMemoryConversationStore>();
        services.AddSingleton<IOrderApiClient, FakeOrderApiClient>();
        services.AddSingleton<IUsersApiClient, FakeUsersApiClient>();
        services.AddSingleton<IAffiliateApiClient, FakeAffiliateApiClient>();
        services.AddSingleton<IWalletApiClient, StubWalletApiClient>();
        services.AddSingleton<ITelegramLogger, SilentTelegramLogger>();
        services.AddSingleton<IVersionService, FakeVersionService>();
        services.AddSingleton<IBotHandler, BotHandler>();

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IBotHandler>();

        Assert.Empty(messenger.Sent);
    }
}
