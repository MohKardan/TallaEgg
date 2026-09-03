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
            .WriteTo.File(TallaEgg.Core.StartupLogging.LogFilePath("telegrambot-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
            .CreateLogger();

        // Configuration guards throw before the host exists, and the bot runs as a Windows
        // service with no console to print to — same as the five APIs (issue #205).
        TallaEgg.Core.StartupLogging.ReportUnhandledExceptionsToLog();

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
                var sharedConfigPath = ResolveSharedConfigPath(context.HostingEnvironment, SharedConfigFileName);
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
                    .Select(pair => new KeyValuePair<string, string?>(
                        pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                            ? pair.Key[prefix.Length..]
                            : pair.Key,
                        pair.Value))
                    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key));

                configBuilder.AddInMemoryCollection(flattened);
                configBuilder.AddEnvironmentVariables();

                // Host.CreateDefaultBuilder(args) registers a command-line provider among its
                // defaults, which run before this callback — so the shared file outranked
                // --Key=value, the same defect #159 fixed in the five APIs. Re-registered here
                // beside the environment, which was already in the right place (issue #181).
                configBuilder.AddCommandLine(args);
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

                    return ProxyBotClient.CreateWithProxy(
                        options.TelegramBotToken,
                        provider.GetRequiredService<ILogger<ProxyBotClient>>());
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

                // Asks the admins about quotes the plausibility band is holding (issue #158). It
                // polls Orders rather than Orders pushing here: Orders has no Telegram dependency
                // and this process exposes no HTTP endpoint, so the call has to run in this
                // direction — the same one every other bot-to-service call already takes.
                services.AddHostedService<PendingQuoteNotifierService>();

            });

    /// <summary>
    /// Walks up from the content root looking for <c>config/<paramref name="fileName"/></c>.
    /// </summary>
    /// <remarks>
    /// The anchor is the content root, not the working directory, which is what the five API
    /// services have always used. The bot was the only host of the six reading
    /// <c>Directory.GetCurrentDirectory()</c>, and that asymmetry was drift rather than a
    /// decision: <c>UseWindowsService()</c> sets the content root to
    /// <c>AppContext.BaseDirectory</c> but leaves the working directory alone, and
    /// <c>sc.exe create</c> has no option to set one — so the SCM started the bot in
    /// <c>C:\Windows\System32</c>, where the walk up never reaches the deployment's
    /// <c>config\</c> folder. The bot could not start as a service at all (issue #212).
    ///
    /// <para>
    /// Under <c>dotnet run</c> the content root is the project folder, the same directory the
    /// working directory pointed at, so the development path is unchanged.
    /// </para>
    ///
    /// <para>
    /// The Serilog sink next to this uses <see cref="AppContext.BaseDirectory"/> rather than the
    /// content root, and the two differ in exactly one case: a *published* exe launched by hand
    /// from some other directory, where the content root is still that shell's directory and this
    /// walk can miss a config the sink would have found. Anchoring here on
    /// <c>AppContext.BaseDirectory</c> too would cover that case, and is deliberately not done —
    /// it would make the bot the one host of six resolving configuration differently, which is the
    /// asymmetry issue #212 was about. If this is ever worth changing, change all six together.
    /// The sink cannot use the content root regardless: it is configured before the host exists.
    /// </para>
    /// </remarks>
    private static string ResolveSharedConfigPath(IHostEnvironment environment, string fileName)
    {
        var current = new DirectoryInfo(environment.ContentRootPath);
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

            var errorMsg = $"Shared configuration '{fileName}' not found relative to '{environment.ContentRootPath}'.";
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
