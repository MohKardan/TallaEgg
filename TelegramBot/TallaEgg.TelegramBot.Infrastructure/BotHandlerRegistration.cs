using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TallaEgg.Core.Services;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using TallaEgg.TelegramBot.Infrastructure.Messaging;
using TallaEgg.TelegramBot.Infrastructure.Options;
using TallaEgg.TelegramBot.Infrastructure.Services;
using Telegram.Bot;

namespace TallaEgg.TelegramBot.Infrastructure;

public static class BotHandlerRegistration
{
    /// <summary>
    /// Registers the bot handler and the abstractions it depends on.
    ///
    /// Split out of <c>Program.cs</c> so it can be exercised by a test (issue #65). When
    /// <c>BotHandler</c> moved from the concrete API clients to their interfaces the code
    /// still compiled, because a constructor parameter type is only matched against the
    /// container at run time — the bot would have failed on startup with "unable to resolve
    /// IOrderApiClient" and nothing before that point would have noticed.
    ///
    /// Assumes the concrete clients are already registered; the interface registrations
    /// below forward to those single instances rather than constructing second ones, which
    /// would each carry their own HttpClient.
    /// </summary>
    public static IServiceCollection AddBotHandler(this IServiceCollection services)
    {
        services.AddSingleton<IOrderApiClient>(p => p.GetRequiredService<OrderApiClient>());
        services.AddSingleton<IUsersApiClient>(p => p.GetRequiredService<UsersApiClient>());
        services.AddSingleton<IAffiliateApiClient>(p => p.GetRequiredService<AffiliateApiClient>());
        services.AddSingleton<IWalletApiClient>(p => p.GetRequiredService<WalletApiClient>());
        services.AddSingleton<ITelegramLogger>(p => p.GetRequiredService<TelegramLoggerService>());

        // Everything the handler says to a chat goes through the messenger; the raw client
        // stays registered for lifecycle and lookup calls.
        services.AddSingleton<IBotMessenger, TelegramBotMessenger>();

        // Singleton because a conversation must survive between the customer's messages;
        // a scoped store would forget the flow at every update.
        services.AddSingleton<IConversationStore, InMemoryConversationStore>();

        // Built by hand rather than by the activator. The activator can only supply constructor
        // arguments it can resolve from the container, so the settings below fell back to their
        // compiled-in defaults and the configured values were never read — silently, because a
        // default is indistinguishable from a deliberate choice.
        //
        // That was harmless while the defaults happened to equal the configuration, and stops
        // being harmless with OwnerTelegramIds: an unread owner list is an empty owner list, and
        // an empty owner list on a fresh database means nobody can ever be granted a role.
        services.AddSingleton<IBotHandler>(provider =>
        {
            var settings = provider.GetRequiredService<IOptions<TelegramBotOptions>>().Value.BotSettings;

            return new BotHandler(
                provider.GetRequiredService<ILogger<BotHandler>>(),
                provider.GetRequiredService<ITelegramBotClient>(),
                provider.GetRequiredService<IBotMessenger>(),
                provider.GetRequiredService<IConversationStore>(),
                provider.GetRequiredService<IOrderApiClient>(),
                provider.GetRequiredService<IUsersApiClient>(),
                provider.GetRequiredService<IAffiliateApiClient>(),
                provider.GetRequiredService<IWalletApiClient>(),
                provider.GetRequiredService<ITelegramLogger>(),
                provider.GetRequiredService<IVersionService>(),
                settings.RequireReferralCode,
                settings.DefaultReferralCode,
                settings.OwnerTelegramIds);
        });

        return services;
    }
}
