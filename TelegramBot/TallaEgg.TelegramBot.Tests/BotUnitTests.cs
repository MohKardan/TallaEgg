using System;
using System.Threading.Tasks;
using Xunit;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TallaEgg.TelegramBot.Tests
{
    public class BotUnitTests
    {
        private readonly MockBotHandler _mockHandler;

        public BotUnitTests()
        {
            _mockHandler = new MockBotHandler();
        }

        [Fact]
        public async Task TestUserRegistrationFlow()
        {
            // Arrange
            var startMessage = CreateMessage("/start TEST123");
            var invitationMessage = CreateMessage("TEST123");
            var phoneMessage = CreateMessageWithContact("+989123456789");

            // Act
            await _mockHandler.HandleUpdateAsync(CreateUpdate(startMessage));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(invitationMessage));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(phoneMessage));

            // Assert
            Assert.True(_mockHandler.ContainsResponse("کد دعوت معتبر است"));
            Assert.True(_mockHandler.ContainsResponse("شماره تلفن شما با موفقیت ثبت شد"));
            Assert.True(_mockHandler.ContainsResponse("منوی اصلی"));
        }

        [Fact]
        public async Task TestOrderPlacementFlow()
        {
            // Arrange
            var orderButtonMessage = CreateMessage("📝 ثبت سفارش");
            var buyOrderCallback = CreateCallbackQuery("order_buy");
            var assetCallback = CreateCallbackQuery("asset_BTC");
            var amountMessage = CreateMessage("0.001");

            // Act
            await _mockHandler.HandleUpdateAsync(CreateUpdate(orderButtonMessage));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(buyOrderCallback));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(assetCallback));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(amountMessage));

            // Assert
            Assert.True(_mockHandler.ContainsResponse("نوع سفارش خود را انتخاب کنید"));
            Assert.True(_mockHandler.ContainsResponse("Order type selected: order_buy"));
            Assert.True(_mockHandler.ContainsResponse("Asset selected: BTC"));
            Assert.True(_mockHandler.ContainsResponse("سفارش شما با موفقیت ثبت شد"));
        }

        [Fact]
        public async Task TestSellOrderWithInsufficientBalance()
        {
            // Arrange
            var orderButtonMessage = CreateMessage("📝 ثبت سفارش");
            var sellOrderCallback = CreateCallbackQuery("order_sell");
            var assetCallback = CreateCallbackQuery("asset_ETH");
            var amountMessage = CreateMessage("1.0"); // More than available balance (0.5)

            // Act
            await _mockHandler.HandleUpdateAsync(CreateUpdate(orderButtonMessage));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(sellOrderCallback));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(assetCallback));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(amountMessage));

            // Assert
            Assert.True(_mockHandler.ContainsResponse("Order type selected: order_sell"));
            Assert.True(_mockHandler.ContainsResponse("موجودی کافی نیست"));
        }

        [Fact]
        public async Task TestSellOrderWithSufficientBalance()
        {
            // Arrange
            var orderButtonMessage = CreateMessage("📝 ثبت سفارش");
            var sellOrderCallback = CreateCallbackQuery("order_sell");
            var assetCallback = CreateCallbackQuery("asset_ETH");
            var amountMessage = CreateMessage("0.1"); // Less than available balance (0.5)

            // Act
            await _mockHandler.HandleUpdateAsync(CreateUpdate(orderButtonMessage));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(sellOrderCallback));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(assetCallback));
            await _mockHandler.HandleUpdateAsync(CreateUpdate(amountMessage));

            // Assert
            Assert.True(_mockHandler.ContainsResponse("Order type selected: order_sell"));
            Assert.True(_mockHandler.ContainsResponse("سفارش شما با موفقیت ثبت شد"));
        }

        [Theory]
        [InlineData("order_buy")]
        [InlineData("order_sell")]
        public async Task TestOrderTypeSelection(string orderType)
        {
            // Arrange
            var callback = CreateCallbackQuery(orderType);

            // Act
            await _mockHandler.HandleUpdateAsync(CreateUpdate(callback));

            // Assert
            Assert.True(_mockHandler.ContainsResponse($"Order type selected: {orderType}"));
        }

        [Theory]
        [InlineData("asset_BTC", "BTC")]
        [InlineData("asset_ETH", "ETH")]
        [InlineData("asset_USDT", "USDT")]
        public async Task TestAssetSelection(string assetData, string expectedAsset)
        {
            // Arrange
            var callback = CreateCallbackQuery(assetData);

            // Act
            await _mockHandler.HandleUpdateAsync(CreateUpdate(callback));

            // Assert
            Assert.True(_mockHandler.ContainsResponse($"Asset selected: {expectedAsset}"));
        }

        [Theory]
        [InlineData("0.001")]
        [InlineData("0.01")]
        [InlineData("1.0")]
        public async Task TestAmountInput(string amount)
        {
            // Arrange
            var message = CreateMessage(amount);

            // Act
            await _mockHandler.HandleUpdateAsync(CreateUpdate(message));

            // Assert
            Assert.True(_mockHandler.ContainsResponse($"Amount entered: {amount}"));
        }

        [Fact]
        public async Task TestBackToMainMenu()
        {
            // Arrange
            var callback = CreateCallbackQuery("back_to_main");

            // Act
            await _mockHandler.HandleUpdateAsync(CreateUpdate(callback));

            // Assert
            Assert.True(_mockHandler.ContainsResponse("منوی اصلی"));
        }

        [Fact]
        public async Task TestInvalidStartCommand()
        {
            // Arrange
            var startMessage = CreateMessage("/start");

            // Act
            await _mockHandler.HandleUpdateAsync(CreateUpdate(startMessage));

            // Assert
            Assert.True(_mockHandler.ContainsResponse("لطفاً کد دعوت را وارد کنید"));
        }

        private static Message CreateMessage(string text)
        {
            return new Message
            {
                Chat = new Chat { Id = 123456789 },
                From = new User { Id = 123456789, Username = "testuser" },
                Text = text
            };
        }

        private static Message CreateMessageWithContact(string phoneNumber)
        {
            return new Message
            {
                Chat = new Chat { Id = 123456789 },
                From = new User { Id = 123456789, Username = "testuser" },
                Contact = new Contact
                {
                    PhoneNumber = phoneNumber,
                    FirstName = "Test",
                    LastName = "User"
                }
            };
        }

        private static CallbackQuery CreateCallbackQuery(string data)
        {
            return new CallbackQuery
            {
                Id = Guid.NewGuid().ToString(),
                From = new User { Id = 123456789, Username = "testuser" },
                Data = data,
                Message = new Message
                {
                    Chat = new Chat { Id = 123456789 },
                    MessageId = 1
                }
            };
        }

        private static Update CreateUpdate(Message message)
        {
            return new Update { Message = message };
        }

        private static Update CreateUpdate(CallbackQuery callbackQuery)
        {
            return new Update { CallbackQuery = callbackQuery };
        }
    }
}
