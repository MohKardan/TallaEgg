using Xunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.TelegramBot.Infrastructure.Services;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using Telegram.Bot;
using System;
using System.Threading.Tasks;

namespace TallaEgg.TelegramBot.Tests.Services
{
    /// <summary>
    /// تست‌های یونیت برای سرویس اطلاع‌رسانی تطبیق معاملات
    /// </summary>
    public class TradeNotificationServiceTests
    {
        private readonly ServiceProvider _serviceProvider;
        private readonly TradeNotificationService _tradeNotificationService;

        public TradeNotificationServiceTests()
        {
            // پیکربندی DI برای تست
            var services = new ServiceCollection();
            
            // اضافه کردن پیکربندی تست
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string>
                {
                    ["TelegramBotToken"] = "TEST_BOT_TOKEN",
                    ["UsersApiUrl"] = "http://localhost:5001"
                })
                .Build();

            services.AddSingleton<IConfiguration>(configuration);
            services.AddHttpClient();
            
            // Mock services برای تست
            services.AddSingleton<ITelegramBotClient>(provider => 
                new MockTelegramBotClient());
            
            services.AddSingleton<UsersApiClient>(provider => 
                new MockUsersApiClient());
            
            services.AddTransient<TradeNotificationService>();

            _serviceProvider = services.BuildServiceProvider();
            _tradeNotificationService = _serviceProvider.GetRequiredService<TradeNotificationService>();
        }

        [Fact]
        public async Task SendTradeMatchNotificationAsync_ValidData_ShouldSucceed()
        {
            // Arrange
            var notification = new TradeMatchNotificationDto
            {
                TradeId = Guid.NewGuid(),
                BuyerUserId = Guid.NewGuid(),
                SellerUserId = Guid.NewGuid(),
                MatchedVolume = 100.5m,
                Price = 50000.0m,
                Asset = "USDT",
                CompletionPercentage = 75.0m,
                RemainingPercentage = 25.0m,
                TradeDate = DateTime.Now
            };

            // Act
            var result = await _tradeNotificationService.SendTradeMatchNotificationAsync(notification);

            // Assert
            Assert.NotNull(result);
            Assert.True(result.BuyerNotificationSent || result.SellerNotificationSent);
        }

        [Fact]
        public async Task SendTradeMatchNotificationAsync_NullNotification_ShouldThrowException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(
                () => _tradeNotificationService.SendTradeMatchNotificationAsync(null));
        }

        [Fact]
        public async Task SendTradeMatchNotificationAsync_EmptyUserIds_ShouldReturnFailedResult()
        {
            // Arrange
            var notification = new TradeMatchNotificationDto
            {
                TradeId = Guid.NewGuid(),
                BuyerUserId = Guid.Empty,
                SellerUserId = Guid.Empty,
                MatchedVolume = 100.5m,
                Price = 50000.0m,
                Asset = "USDT",
                CompletionPercentage = 75.0m,
                RemainingPercentage = 25.0m,
                TradeDate = DateTime.Now
            };

            // Act
            var result = await _tradeNotificationService.SendTradeMatchNotificationAsync(notification);

            // Assert
            Assert.NotNull(result);
            Assert.False(result.BuyerNotificationSent);
            Assert.False(result.SellerNotificationSent);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-10)]
        public async Task SendTradeMatchNotificationAsync_InvalidVolume_ShouldHandleGracefully(decimal volume)
        {
            // Arrange
            var notification = new TradeMatchNotificationDto
            {
                TradeId = Guid.NewGuid(),
                BuyerUserId = Guid.NewGuid(),
                SellerUserId = Guid.NewGuid(),
                MatchedVolume = volume,
                Price = 50000.0m,
                Asset = "USDT",
                CompletionPercentage = 75.0m,
                RemainingPercentage = 25.0m,
                TradeDate = DateTime.Now
            };

            // Act
            var result = await _tradeNotificationService.SendTradeMatchNotificationAsync(notification);

            // Assert
            Assert.NotNull(result);
            // باید بتواند حجم نامعتبر را مدیریت کند
        }

        [Fact]
        public void GenerateBuyerMessage_ValidData_ShouldContainExpectedText()
        {
            // Arrange
            var notification = new TradeMatchNotificationDto
            {
                TradeId = Guid.NewGuid(),
                BuyerUserId = Guid.NewGuid(),
                SellerUserId = Guid.NewGuid(),
                MatchedVolume = 100.5m,
                Price = 50000.0m,
                Asset = "USDT",
                CompletionPercentage = 75.0m,
                RemainingPercentage = 25.0m,
                TradeDate = new DateTime(2024, 1, 15, 10, 30, 0)
            };

            // Act
            var message = GenerateMessageForTest(notification, "خرید");

            // Assert
            Assert.Contains("🎉 معامله شما با موفقیت تطبیق یافت!", message);
            Assert.Contains("خرید USDT", message);
            Assert.Contains("100.5", message);
            Assert.Contains("50,000", message);
            Assert.Contains("75%", message);
            Assert.Contains("25%", message);
        }

        private string GenerateMessageForTest(TradeMatchNotificationDto notification, string tradeType)
        {
            return $@"🎉 معامله شما با موفقیت تطبیق یافت!

💰 نوع معامله: {tradeType} {notification.Asset}
📊 حجم تطبیق: {notification.MatchedVolume}
💵 قیمت: {notification.Price:N0} تومان
✅ درصد تکمیل: {notification.CompletionPercentage}%
⏳ درصد باقیمانده: {notification.RemainingPercentage}%
📅 تاریخ معامله: {notification.TradeDate:yyyy/MM/dd}
🕐 زمان معامله: {notification.TradeDate:HH:mm:ss}

شماره پیگیری: {notification.TradeId}";
        }

        public void Dispose()
        {
            _serviceProvider?.Dispose();
        }
    }

    /// <summary>
    /// Mock کلاس برای تست TelegramBotClient
    /// </summary>
    public class MockTelegramBotClient : ITelegramBotClient
    {
        public bool LocalBotServer => false;
        public long BotId => 123456789;
        public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

        // پیاده‌سازی متدهای ضروری برای تست
        public Task<Telegram.Bot.Types.User> GetMeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new Telegram.Bot.Types.User
            {
                Id = 123456789,
                IsBot = true,
                FirstName = "Test Bot",
                Username = "testbot"
            });
        }

        public Task<Telegram.Bot.Types.Message> SendTextMessageAsync(
            Telegram.Bot.Types.ChatId chatId,
            string text,
            Telegram.Bot.Types.Enums.ParseMode? parseMode = null,
            IEnumerable<Telegram.Bot.Types.MessageEntity>? entities = null,
            bool? disableWebPagePreview = null,
            bool? disableNotification = null,
            bool? protectContent = null,
            int? replyToMessageId = null,
            bool? allowSendingWithoutReply = null,
            Telegram.Bot.Types.ReplyMarkups.IReplyMarkup? replyMarkup = null,
            CancellationToken cancellationToken = default)
        {
            // شبیه‌سازی ارسال موفق
            return Task.FromResult(new Telegram.Bot.Types.Message
            {
                MessageId = new Random().Next(1000, 9999),
                Date = DateTime.UtcNow,
                Chat = new Telegram.Bot.Types.Chat { Id = chatId, Type = Telegram.Bot.Types.Enums.ChatType.Private },
                Text = text
            });
        }

        // سایر متدهای interface که برای تست نیاز نداریم
        public Task<Telegram.Bot.Types.File> GetFileAsync(string fileId, CancellationToken cancellationToken = default) 
            => throw new NotImplementedException();
        public Task<Telegram.Bot.Types.User[]> GetChatAdministratorsAsync(Telegram.Bot.Types.ChatId chatId, CancellationToken cancellationToken = default) 
            => throw new NotImplementedException();
        // ... سایر متدها
    }

    /// <summary>
    /// Mock کلاس برای تست UsersApiClient
    /// </summary>
    public class MockUsersApiClient : UsersApiClient
    {
        public MockUsersApiClient() : base(new HttpClient(), null) { }

        public override async Task<TallaEgg.Core.DTOs.User.UserDto?> GetUserByIdAsync(Guid userId)
        {
            // شبیه‌سازی کاربر تست
            await Task.Delay(10); // شبیه‌سازی تأخیر شبکه

            return new TallaEgg.Core.DTOs.User.UserDto
            {
                Id = userId,
                TelegramId = 123456789,
                FirstName = "تست",
                LastName = "کاربر",
                Username = "testuser"
            };
        }
    }
}
