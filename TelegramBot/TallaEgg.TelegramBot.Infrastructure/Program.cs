using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
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

public class Program
{
    private const string SharedConfigFileName = "appsettings.global.json";

    public static async Task Main(string[] args)
    {
        // Matches the 5 API services' own pattern (issue #88) — console for a live session,
        // file for everything else. Before this the bot had no file sink at all: an
        // exception left no trace once the console it printed to was gone (issue #99).
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .WriteTo.File("logs/telegrambot-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .CreateLogger();

        using var host = CreateHostBuilder(args).Build();
        await host.RunAsync();
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            // No-op outside an actual Windows Service Control Manager session (e.g. local
            // `dotnet run`), so this is always safe to include. Lets `sc.exe create` manage
            // this process directly — no third-party supervisor needed (issue #70).
            .UseWindowsService()
            .UseSerilog()
            .ConfigureAppConfiguration((context, configBuilder) =>
            {
                var sharedConfigPath = ResolveSharedConfigPath(SharedConfigFileName);
                configBuilder.AddJsonFile(sharedConfigPath, optional: false, reloadOnChange: true);

                var tempConfiguration = configBuilder.Build();
                var applicationName = context.HostingEnvironment.ApplicationName
                    ?? typeof(Program).Assembly.GetName().Name
                    ?? "TallaEgg.TelegramBot.Infrastructure";

                var serviceSection = tempConfiguration.GetSection($"Services:{applicationName}");
                if (!serviceSection.Exists())
                {
                    throw new InvalidOperationException($"Missing configuration section 'Services:{applicationName}' in {SharedConfigFileName}.");
                }

                var prefix = $"Services:{applicationName}:";
                var flattened = serviceSection.AsEnumerable(true)
                    .Where(pair => pair.Value is not null)
                    .Select(pair => new KeyValuePair<string, string>(
                        pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                            ? pair.Key[prefix.Length..]
                            : pair.Key,
                        pair.Value!))
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key));

                configBuilder.AddInMemoryCollection(flattened);
                configBuilder.AddEnvironmentVariables();
            })
            .ConfigureServices((context, services) =>
            {
                // Trading symbols come from appsettings.global.json (Symbols section), not
                // compiled-in defaults.
                TallaEgg.Core.CurrenciesConstant.Configure(context.Configuration);

                services.AddOptions<TelegramBotOptions>()
                    .Bind(context.Configuration)
                    .ValidateDataAnnotations();

                services.AddHttpClient();

                services.AddSingleton<ITelegramBotClient>(provider =>
                {
                    var options = provider.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
                    if (string.IsNullOrWhiteSpace(options.TelegramBotToken))
                    {
                        throw new InvalidOperationException("TelegramBotToken is not configured.");
                    }

                    return ProxyBotClient.CreateWithProxy(options.TelegramBotToken);
                });

                services.AddSingleton<OrderApiClient>(provider => new OrderApiClient(
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<ILogger<OrderApiClient>>()));

                services.AddSingleton<UsersApiClient>(provider => new UsersApiClient(
                    provider.GetRequiredService<HttpClient>(),
                    provider.GetRequiredService<IConfiguration>(),
                    provider.GetRequiredService<ILogger<UsersApiClient>>()));

                services.AddSingleton<AffiliateApiClient>(provider =>
                {
                    var options = provider.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
                    if (string.IsNullOrWhiteSpace(options.AffiliateApiUrl))
                    {
                        throw new InvalidOperationException("AffiliateApiUrl is not configured.");
                    }

                    return new AffiliateApiClient(
                        options.AffiliateApiUrl,
                        new HttpClient(),
                        provider.GetRequiredService<ILogger<AffiliateApiClient>>());
                });

                services.AddSingleton<WalletApiClient>(provider =>
                {
                    var options = provider.GetRequiredService<IOptions<TelegramBotOptions>>().Value;
                    return new WalletApiClient(options.WalletApiUrl,
                        provider.GetRequiredService<ILogger<WalletApiClient>>());
                });

                services.AddSingleton<TelegramLoggerService>(provider =>
                {
                    var httpClientFactory = provider.GetRequiredService<IHttpClientFactory>();

                    // A second bot, used only to deliver error reports — not the one customers talk
                    // to, whose token is TelegramBotOptions.TelegramBotToken. There is no settings
                    // key for this one, which is why it is a literal. Giving it a key of its own is
                    // the fix; the value itself is dead and rotated, see CLAUDE.md.
                    return new TelegramLoggerService(httpClientFactory, "7331560325:AAHgmgugtatg0XmoIMgTd7_Nj6G09jvo9g4");
                });

                services.AddSingleton<IVersionService, VersionService>();

                // One place, so a test can exercise the same wiring (issue #65).
                services.AddBotHandler();

                services.AddHostedService<TelegramBotHostedService>();

            });

    private static string ResolveSharedConfigPath(string fileName)
    {
        var current = new DirectoryInfo(Directory.GetCurrentDirectory());
        try
        {
            while (current is not null)
            {
                var candidate = Path.Combine(current.FullName, "config", fileName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                current = current.Parent;
            }

            var errorMsg = $"Shared configuration '{fileName}' not found relative to '{Directory.GetCurrentDirectory()}'.";
            Log.Error(errorMsg);
            throw new FileNotFoundException(errorMsg, fileName);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error resolving shared config path for file {FileName}", fileName);
            throw;
        }
    }
}
