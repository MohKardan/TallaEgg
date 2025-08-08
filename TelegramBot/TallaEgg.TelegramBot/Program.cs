using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Configuration;
using System.Net.Http;

namespace TallaEgg.TelegramBot;

class Program
{
    static async Task Main(string[] args)
    {
        try
        {
            Console.WriteLine("Starting Telegram Bot...");
            
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
            var requireReferralCode = bool.Parse(config["BotSettings:RequireReferralCode"] ?? "true");
            var defaultReferralCode = config["BotSettings:DefaultReferralCode"] ?? "ADMIN2024";

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

            var botClient = ProxyBotClient.CreateWithProxy(botToken);
            var orderApi = new OrderApiClient(orderApiUrl);
            var usersApi = new UsersApiClient(usersApiUrl);
            var httpClient = new HttpClient();
            var affiliateApi = new AffiliateApiClient(affiliateApiUrl, httpClient);
            var priceApi = new PriceApiClient(pricesApiUrl);
            var walletApi = new WalletApiClient(walletApiUrl);
            var botHandler = new BotHandler(botClient, orderApi, usersApi, affiliateApi, priceApi, walletApi, 
                                           requireReferralCode, defaultReferralCode);

            Console.WriteLine("✅ API clients initialized");

            // حذف webhook قبلی
            try
            {
                await botClient.DeleteWebhookAsync();
                Console.WriteLine("✅ Webhook deleted successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Warning: Could not delete webhook: {ex.Message}");
            }

            // Test bot connection
            try
            {
                var me = await botClient.GetMeAsync();
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
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            Console.WriteLine("🔄 Starting message polling...");

            botClient.StartReceiving(
                updateHandler: async (client, update, token) =>
                {
                    await HandleUpdateAsync(client, update, botHandler);
                },
                pollingErrorHandler: (client, ex, token) =>
                {
                    Console.WriteLine($"❌ Polling Error: {ex.Message}");
                    Console.WriteLine($"Error Type: {ex.GetType().Name}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"Inner Error: {ex.InnerException.Message}");
                    }
                    return Task.CompletedTask;
                },
                receiverOptions: receiverOptions
            );

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

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, BotHandler botHandler)
    {
        try
        {
            if (update.Message != null)
            {
                Console.WriteLine($"📨 Received message from {update.Message.From?.Username ?? "Unknown"}: {update.Message.Text?.Substring(0, Math.Min(50, update.Message.Text?.Length ?? 0))}...");
                await botHandler.HandleUpdateAsync(update);
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