using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Configuration;

namespace TallaEgg.TelegramBot;

class Program
{
    static async Task Main(string[] args)
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

        if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(orderApiUrl) || 
            string.IsNullOrEmpty(usersApiUrl) || string.IsNullOrEmpty(affiliateApiUrl))
        {
            Console.WriteLine("توکن یا آدرس‌های API تنظیم نشده است.");
            return;
        }

        var botClient = new TelegramBotClient(botToken);
        var orderApi = new OrderApiClient(orderApiUrl);
        var usersApi = new UsersApiClient(usersApiUrl);
        var affiliateApi = new AffiliateApiClient(affiliateApiUrl);
        var priceApi = new PriceApiClient(pricesApiUrl);
        var botHandler = new BotHandler(botClient, orderApi, usersApi, affiliateApi, priceApi);

        // حذف webhook قبلی
        await botClient.DeleteWebhookAsync();

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Bot Started: @{me.Username}");

        var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
        };

        botClient.StartReceiving(
            updateHandler: async (client, update, token) =>
            {
                await HandleUpdateAsync(client, update, botHandler);
            },
            pollingErrorHandler: (client, ex, token) =>
            {
                Console.WriteLine($"Error: {ex.Message}");
                return Task.CompletedTask;
            },
            receiverOptions: receiverOptions
        );

        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, BotHandler botHandler)
    {
        try
        {
            if (update.Message != null)
            {
                await botHandler.HandleUpdateAsync(update);
            }
            else if (update.CallbackQuery != null)
            {
                await botHandler.HandleCallbackQueryAsync(update.CallbackQuery);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling update: {ex.Message}");
        }
    }
}