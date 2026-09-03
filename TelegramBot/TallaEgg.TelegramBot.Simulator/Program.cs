using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TallaEgg.Core.Services;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using TallaEgg.TelegramBot.Infrastructure.Messaging;
using TallaEgg.TelegramBot.Infrastructure.Options;
using TallaEgg.TelegramBot.Infrastructure.Services;
using TallaEgg.TelegramBot.Simulator;
using Telegram.Bot;

const string SharedConfigFileName = "appsettings.global.json";

// Reuses the real bot's own config section (its API URLs, bot token, owner ids) rather than
// inventing a separate one — the simulator stands in for real bot traffic, so it should run
// under the same identity, not a parallel one nobody maintains.
const string BotApplicationName = "TallaEgg.TelegramBot.Infrastructure";

var configBuilder = new ConfigurationBuilder()
    .AddJsonFile(ResolveSharedConfigPath(SharedConfigFileName), optional: false, reloadOnChange: false);

var tempConfiguration = configBuilder.Build();
var serviceSection = tempConfiguration.GetSection($"Services:{BotApplicationName}");
if (!serviceSection.Exists())
{
    throw new InvalidOperationException($"Missing configuration section 'Services:{BotApplicationName}' in {SharedConfigFileName}.");
}

// Some of the bot's own clients (OrderApiClient, UsersApiClient) read flat keys like
// "OrderApiUrl" straight off IConfiguration rather than through TelegramBotOptions — the real
// bot's Program.cs flattens Services:{ApplicationName}:* into root-level keys for exactly
// this reason. Without this step those clients find no key at all and refuse to be built;
// before issue #205 they were worse than that, falling back to compiled-in defaults so the
// simulator ran against addresses nobody had configured.
var prefix = $"Services:{BotApplicationName}:";
var flattened = serviceSection.AsEnumerable(true)
    .Where(pair => pair.Value is not null)
    .Select(pair => new KeyValuePair<string, string?>(
        pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? pair.Key[prefix.Length..] : pair.Key,
        pair.Value))
    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key));

configBuilder.AddInMemoryCollection(flattened);
configBuilder.AddEnvironmentVariables();
var configuration = configBuilder.Build();
serviceSection = configuration.GetSection($"Services:{BotApplicationName}");

// The same call every service and the real bot make at startup. It matters here now that the run
// reads its symbols from CurrenciesConstant.AllTradingPairs (issue #147): without it the simulator
// would trade the compiled defaults while the APIs it drives were serving whatever the shared
// file's "Symbols" section adds on top of them.
TallaEgg.Core.CurrenciesConstant.Configure(configuration);

var services = new ServiceCollection();
services.AddLogging(b => b.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
}).SetMinimumLevel(LogLevel.Information));

services.AddSingleton<IConfiguration>(configuration);
services.AddOptions<TelegramBotOptions>().Bind(serviceSection).ValidateDataAnnotations();

services.AddHttpClient();

services.AddSingleton<OrderApiClient>(p => new OrderApiClient(
    p.GetRequiredService<HttpClient>(), p.GetRequiredService<IConfiguration>(),
    p.GetRequiredService<ILogger<OrderApiClient>>()));

services.AddSingleton<UsersApiClient>(p => new UsersApiClient(
    p.GetRequiredService<HttpClient>(), p.GetRequiredService<IConfiguration>(),
    p.GetRequiredService<ILogger<UsersApiClient>>()));

services.AddSingleton<AffiliateApiClient>(p =>
{
    var options = p.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
    return new AffiliateApiClient(options.AffiliateApiUrl!, new HttpClient(),
        p.GetRequiredService<ILogger<AffiliateApiClient>>());
});

services.AddSingleton<WalletApiClient>(p =>
{
    var options = p.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
    return new WalletApiClient(options.WalletApiUrl!,
        p.GetRequiredService<ILogger<WalletApiClient>>());
});

// Same wiring the real bot uses (issue #65) — only IBotMessenger, ITelegramLogger and
// IVersionService are swapped below for fakes that never touch a real Telegram chat.
services.AddBotHandler();
services.AddSingleton<IBotMessenger, FakeBotMessenger>();
services.AddSingleton<ITelegramLogger, NullTelegramLogger>();
services.AddSingleton<IVersionService, NullVersionService>();

// BotHandler still takes the raw ITelegramBotClient for lifecycle/lookup calls the messenger
// doesn't cover (see IBotMessenger's doc comment). None of the simulated flows below exercise
// those calls, but the constructor requires an instance, so a real (idle) client is used —
// it never calls StartReceiving, so it never actually talks to Telegram.
services.AddSingleton<ITelegramBotClient>(p =>
{
    var options = p.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
    return new TelegramBotClient(options.TelegramBotToken!);
});

// AddBotHandler() registers IBotHandler by hand-building a BotHandler; re-registering
// IBotMessenger above after that call wins because the container resolves the last
// registration, so BotHandler receives FakeBotMessenger without changing that file.

var connectionStrings = configuration.GetSection("ConnectionStrings");
services.AddSingleton(p => new DataReset(
    connectionStrings["UsersDb"]!, connectionStrings["WalletDb"]!, connectionStrings["OrdersDb"]!,
    p.GetRequiredService<ILogger<DataReset>>()));

services.AddSingleton<Simulation>();

await using var provider = services.BuildServiceProvider();
var options = SimulationOptions.FromArgs(args);
var simulation = provider.GetRequiredService<Simulation>();
await simulation.RunAsync(options);
return;

static string ResolveSharedConfigPath(string fileName)
{
    var current = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (current is not null)
    {
        var candidate = Path.Combine(current.FullName, "config", fileName);
        if (File.Exists(candidate))
        {
            return candidate;
        }
        current = current.Parent;
    }

    throw new FileNotFoundException(
        $"Shared configuration '{fileName}' not found relative to '{Directory.GetCurrentDirectory()}'.", fileName);
}
