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
        var apiUrl = config["OrderApiUrl"];

        if (string.IsNullOrEmpty(botToken) || string.IsNullOrEmpty(apiUrl))
        {
            Console.WriteLine("توکن یا آدرس API تنظیم نشده است.");
            return;
        }

        var botClient = new TelegramBotClient(botToken);
        var orderApi = new OrderApiClient(apiUrl);

        var me = await botClient.GetMeAsync();
        Console.WriteLine($"Bot Started: @{me.Username}");

        botClient.StartReceiving(
            updateHandler: async (client, update, token) =>
            {
                await HandleUpdateAsync(client, update, orderApi);
            },
            pollingErrorHandler: (client, ex, token) =>
            {
                Console.WriteLine($"Error: {ex.Message}");
                return Task.CompletedTask;
            },
            receiverOptions: new Telegram.Bot.Polling.ReceiverOptions
            {
                AllowedUpdates = Array.Empty<UpdateType>()
            }
        );

        Console.WriteLine("Press any key to stop...");
        Console.ReadKey();
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, OrderApiClient orderApi)
    {
        if (update.Message is not { } message || message.Text is not { } msgText)
            return;

        if (msgText.StartsWith("/buy", StringComparison.OrdinalIgnoreCase))
        {
            var parts = msgText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "فرمت صحیح: /buy [Asset] [Amount] [Price]");
                return;
            }

            var asset = parts[1];
            if (!decimal.TryParse(parts[2], out var amount))
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "مقدار (Amount) باید عدد باشد.");
                return;
            }
            if (!decimal.TryParse(parts[3], out var price))
            {
                await botClient.SendTextMessageAsync(message.Chat.Id, "قیمت (Price) باید عدد باشد.");
                return;
            }

            var order = new OrderDto
            {
                Asset = asset,
                Amount = amount,
                Price = price,
                UserId = GuidFromTelegramId(message.From.Id),
                Type = "BUY"
            };

            var (success, resultMsg) = await orderApi.SubmitOrderAsync(order);
            await botClient.SendTextMessageAsync(message.Chat.Id, resultMsg);
        }
        else
        {
            await botClient.SendTextMessageAsync(message.Chat.Id,
                "برای ثبت سفارش خرید، از دستور زیر استفاده کنید:\n/buy [Asset] [Amount] [Price]\nمثال: /buy Gold 2 5300000");
        }
    }

    static Guid GuidFromTelegramId(long telegramId)
    {
        var bytes = new byte[16];
        BitConverter.GetBytes(telegramId).CopyTo(bytes, 0);
        return new Guid(bytes);
    }
}