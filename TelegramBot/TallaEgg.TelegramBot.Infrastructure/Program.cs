using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TallaEgg.TelegramBot.Core.Interfaces;
using System.Net.Http;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Services;
using TallaEgg.Infrastructure.Clients;

namespace TallaEgg.TelegramBot.Infrastructure;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            
            // خواندن تنظیمات
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", true)
                .Build();

            var botToken = config["TelegramBotToken"];
            var orderApiUrl = config["OrderApiUrl"];
            var usersApiUrl = config["UsersApiUrl"];
            var affiliateApiUrl = config["AffiliateApiUrl"];
            var pricesApiUrl = config["PricesApiUrl"];
            var walletApiUrl = config["WalletApiUrl"];
            
            // Bot settings
            var requireReferralCode = bool.Parse(config["BotSettings:RequireReferralCode"] ?? "false");
            var defaultReferralCode = config["BotSettings:DefaultReferralCode"] ?? "admin";

            Console.WriteLine($"Bot Token: {botToken?.Substring(0, Math.Min(10, botToken?.Length ?? 0))}...");
            Console.WriteLine($"Order API URL: {orderApiUrl}");
            Console.WriteLine($"Users API URL: {usersApiUrl}");
            Console.WriteLine($"Affiliate API URL: {affiliateApiUrl}");
            Console.WriteLine($"Prices API URL: {pricesApiUrl}");
            Console.WriteLine($"Wallet API URL: {walletApiUrl}");
            Console.WriteLine($"Require Referral Code: {requireReferralCode}");
            Console.WriteLine($"Default Referral Code: {defaultReferralCode}");

            if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(orderApiUrl) || 
                string.IsNullOrEmpty(usersApiUrl) || string.IsNullOrEmpty(affiliateApiUrl) ||
                string.IsNullOrEmpty(walletApiUrl))
            {
                Console.WriteLine("❌ توکن یا آدرس‌های API تنظیم نشده است.");
                return;
            }

            Console.WriteLine("✅ Configuration loaded successfully");

            // Test network connectivity first
            await NetworkTest.TestConnectivityAsync();

            // Test HTTP vs HTTPS
            await SimpleHttpTest.TestHttpRequestsAsync();

            // Test with proxy settings
            await ProxyTest.TestWithProxyAsync();

            // Test bot token first
            Console.WriteLine("🔍 Testing bot token...");
            var tokenTestResult = await TestBotToken.TestTokenAsync(botToken);

            // If network connectivity fails, run offline test
            if (!tokenTestResult)
            {
                Console.WriteLine("\n⚠️ Network connectivity issues detected.");
                Console.WriteLine("Running offline test mode...");
                await OfflineTestMode.RunOfflineTestAsync();
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }

            
            // Setup Dependency Injection
            var services = new ServiceCollection();

            services.AddHttpClient();

            // Register services
            services.AddSingleton<ITelegramBotClient>(provider => ProxyBotClient.CreateWithProxy(botToken));
            services.AddSingleton<OrderApiClient>(provider => new OrderApiClient(provider.GetRequiredService<HttpClient>(), config));
            services.AddSingleton<UsersApiClient>(provider => new UsersApiClient(provider.GetRequiredService<HttpClient>(), config));
            services.AddSingleton<AffiliateApiClient>(provider => new AffiliateApiClient(affiliateApiUrl, new HttpClient()));
            services.AddSingleton<WalletApiClient>(provider => new WalletApiClient(walletApiUrl));
            services.AddSingleton<TradeNotificationService>();
            services.AddSingleton<IBotHandler>(provider => new BotHandler(
                provider.GetRequiredService<ITelegramBotClient>(),
                provider.GetRequiredService<OrderApiClient>(),
                provider.GetRequiredService<UsersApiClient>(),
                provider.GetRequiredService<AffiliateApiClient>(),
                provider.GetRequiredService<WalletApiClient>(),
                requireReferralCode,
                defaultReferralCode
            ));

            var serviceProvider = services.BuildServiceProvider();
            var botClient = serviceProvider.GetRequiredService<ITelegramBotClient>();
            var botHandler = serviceProvider.GetRequiredService<IBotHandler>();

            Console.WriteLine("✅ API clients initialized");

            // حذف webhook قبلی
            try
            {
                await botClient.DeleteWebhook(dropPendingUpdates: true);
                Console.WriteLine("✅ Webhook deleted successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Warning: Could not delete webhook: {ex.Message}");
            }

            // Test bot connection
            try
            {
                var me = await botClient.GetMe();
                Console.WriteLine($"✅ Bot connection successful: @{me.Username}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Bot connection failed: {ex.Message}");
                Console.WriteLine("Please check your bot token and internet connection.");
                return;
            }

            var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery },
                Limit = 100
            };

            Console.WriteLine("🔄 Starting message polling...");

            var cts = new CancellationTokenSource();

            botClient.StartReceiving(
                updateHandler: async (bot, update, ct) =>
                {
                    await HandleUpdateAsync(bot, update, botHandler);
                },

                errorHandler: async (bot, exception, source, ct) =>
                {
                    Console.WriteLine($"❌ Polling Error: {exception.Message}");
                    Console.WriteLine($"Error Type: {exception.GetType().Name}");
                    if (exception.InnerException != null)
                    {
                        Console.WriteLine($"Inner Error: {exception.InnerException.Message}");
                    }
                    
                    // اگر خطای timeout باشد، کمی صبر کنیم و دوباره تلاش کنیم
                    if (exception.Message.Contains("timeout") || exception.Message.Contains("timed out"))
                    {
                        Console.WriteLine("⏳ Timeout detected. Waiting 10 seconds before retrying...");
                        await Task.Delay(10000, ct);
                    }
                },

                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            TelegramNotificationApi.RunNotificationApi(args);

            Console.WriteLine("✅ Bot is now running and listening for messages...");
            Console.WriteLine("Press any key to stop...");
            Console.ReadKey();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Fatal Error: {ex.Message}");
            Console.WriteLine($"Error Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
            }
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, IBotHandler botHandler)
    {
        try
        {
            
            if (update.Message != null)
            {
                Console.WriteLine($"📨 Received message from {update.Message.From?.Username ?? "Unknown"}: {update.Message.Text?.Substring(0, Math.Min(50, update.Message.Text?.Length ?? 0))}...");
                await botHandler.HandleMessageAsync(update.Message);
            }
            else if (update.CallbackQuery != null)
            {
                Console.WriteLine($"🔘 Received callback query from {update.CallbackQuery.From?.Username ?? "Unknown"}: {update.CallbackQuery.Data}");
                await botHandler.HandleCallbackQueryAsync(update.CallbackQuery);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error handling update: {ex.Message}");
            Console.WriteLine($"Error Type: {ex.GetType().Name}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
            }
        }
    }
}