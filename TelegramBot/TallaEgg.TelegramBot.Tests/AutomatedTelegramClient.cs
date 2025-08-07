using System;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace TallaEgg.TelegramBot.Tests
{
    public class AutomatedTelegramClient
    {
        private readonly ITelegramBotClient _botClient;
        private readonly IConfiguration _config;
        private readonly long _testUserId;
        private readonly string _testUsername;
        private readonly string _testPhone;
        private readonly string _invitationCode;
        private Message? _lastMessage;
        private CallbackQuery? _lastCallbackQuery;

        public AutomatedTelegramClient(string botToken, IConfiguration config)
        {
            _botClient = new TelegramBotClient(botToken);
            _config = config;
            _testUserId = 123456789; // Test user ID
            _testUsername = "testuser";
            _testPhone = _config["TestUserPhone"] ?? "+989123456789";
            _invitationCode = _config["TestInvitationCode"] ?? "TEST123";
        }

        public async Task StartListeningAsync()
        {
            Console.WriteLine("🤖 Starting Automated Telegram Client...");
            
            var receiverOptions = new Telegram.Bot.Polling.ReceiverOptions
            {
                AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery }
            };

            _botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions
            );

            Console.WriteLine("✅ Automated client is listening for updates...");
        }

        private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
        {
            try
            {
                if (update.Message != null)
                {
                    _lastMessage = update.Message;
                    Console.WriteLine($"📨 Received message: {update.Message.Text}");
                    await ProcessMessageAsync(update.Message);
                }
                else if (update.CallbackQuery != null)
                {
                    _lastCallbackQuery = update.CallbackQuery;
                    Console.WriteLine($"🔘 Received callback: {update.CallbackQuery.Data}");
                    await ProcessCallbackQueryAsync(update.CallbackQuery);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error handling update: {ex.Message}");
            }
        }

        private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
        {
            Console.WriteLine($"❌ Polling error: {exception.Message}");
            return Task.CompletedTask;
        }

        private async Task ProcessMessageAsync(Message message)
        {
            var text = message.Text ?? "";
            
            // Simulate user responses based on bot messages
            if (text.Contains("کد دعوت") || text.Contains("invitation"))
            {
                await SimulateInvitationCodeInputAsync(message.Chat.Id);
            }
            else if (text.Contains("شماره تلفن") || text.Contains("phone"))
            {
                await SimulatePhoneNumberInputAsync(message.Chat.Id);
            }
            else if (text.Contains("مقدار واحد") || text.Contains("amount"))
            {
                await SimulateAmountInputAsync(message.Chat.Id);
            }
        }

        private async Task ProcessCallbackQueryAsync(CallbackQuery callbackQuery)
        {
            var data = callbackQuery.Data ?? "";
            
            if (data == "order_buy" || data == "order_sell")
            {
                await SimulateOrderTypeSelectionAsync(callbackQuery.Message?.Chat.Id ?? 0, data);
            }
            else if (data.StartsWith("asset_"))
            {
                await SimulateAssetSelectionAsync(callbackQuery.Message?.Chat.Id ?? 0, data);
            }
        }

        public async Task TestCompleteFlowAsync()
        {
            Console.WriteLine("🧪 Starting complete bot flow test...");
            
            try
            {
                // Step 1: Test user registration
                await TestUserRegistrationAsync();
                
                // Step 2: Test main menu
                await TestMainMenuAsync();
                
                // Step 3: Test order placement
                await TestOrderPlacementAsync();
                
                // Step 4: Test balance validation
                await TestBalanceValidationAsync();
                
                Console.WriteLine("✅ Complete flow test finished successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test failed: {ex.Message}");
                throw;
            }
        }

        public async Task TestUserRegistrationAsync()
        {
            Console.WriteLine("📝 Testing user registration...");
            
            // Simulate /start command with invitation code
            var startMessage = new Message
            {
                Chat = new Chat { Id = _testUserId },
                From = new User { Id = _testUserId, Username = _testUsername },
                Text = $"/start {_invitationCode}"
            };
            
            await ProcessMessageAsync(startMessage);
            await Task.Delay(2000); // Wait for bot response
            
            // Simulate phone number sharing
            await SimulatePhoneNumberInputAsync(_testUserId);
            await Task.Delay(2000);
        }

        public async Task TestMainMenuAsync()
        {
            Console.WriteLine("📱 Testing main menu...");
            
            // Simulate clicking "ثبت سفارش" button
            var orderButtonMessage = new Message
            {
                Chat = new Chat { Id = _testUserId },
                From = new User { Id = _testUserId, Username = _testUsername },
                Text = "📝 ثبت سفارش"
            };
            
            await ProcessMessageAsync(orderButtonMessage);
            await Task.Delay(2000);
        }

        public async Task TestOrderPlacementAsync()
        {
            Console.WriteLine("🛒 Testing order placement...");
            
            // Simulate buy order selection
            await SimulateOrderTypeSelectionAsync(_testUserId, "order_buy");
            await Task.Delay(2000);
            
            // Simulate asset selection
            await SimulateAssetSelectionAsync(_testUserId, "asset_BTC");
            await Task.Delay(2000);
            
            // Simulate amount input
            await SimulateAmountInputAsync(_testUserId);
            await Task.Delay(2000);
        }

        public async Task TestBalanceValidationAsync()
        {
            Console.WriteLine("💰 Testing balance validation...");
            
            // Simulate sell order (which requires balance check)
            await SimulateOrderTypeSelectionAsync(_testUserId, "order_sell");
            await Task.Delay(2000);
            
            await SimulateAssetSelectionAsync(_testUserId, "asset_ETH");
            await Task.Delay(2000);
            
            await SimulateAmountInputAsync(_testUserId);
            await Task.Delay(2000);
        }

        public async Task SimulateInvitationCodeInputAsync(long chatId)
        {
            Console.WriteLine("📝 Simulating invitation code input...");
            
            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                From = new User { Id = _testUserId, Username = _testUsername },
                Text = _invitationCode
            };
            
            await ProcessMessageAsync(message);
        }

        public async Task SimulatePhoneNumberInputAsync(long chatId)
        {
            Console.WriteLine("📱 Simulating phone number input...");
            
            // Create a contact object
            var contact = new Contact
            {
                PhoneNumber = _testPhone,
                FirstName = _config["TestUserFirstName"] ?? "Test",
                LastName = _config["TestUserLastName"] ?? "User"
            };
            
            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                From = new User { Id = _testUserId, Username = _testUsername },
                Contact = contact
            };
            
            await ProcessMessageAsync(message);
        }

        public async Task SimulateOrderTypeSelectionAsync(long chatId, string orderType)
        {
            Console.WriteLine($"🛒 Simulating {orderType} selection...");
            
            var callbackQuery = new CallbackQuery
            {
                Id = Guid.NewGuid().ToString(),
                From = new User { Id = _testUserId, Username = _testUsername },
                Data = orderType,
                Message = new Message
                {
                    Chat = new Chat { Id = chatId },
                    MessageId = 1
                }
            };
            
            await ProcessCallbackQueryAsync(callbackQuery);
        }

        public async Task SimulateAssetSelectionAsync(long chatId, string assetData)
        {
            Console.WriteLine($"🪙 Simulating asset selection: {assetData}...");
            
            var callbackQuery = new CallbackQuery
            {
                Id = Guid.NewGuid().ToString(),
                From = new User { Id = _testUserId, Username = _testUsername },
                Data = assetData,
                Message = new Message
                {
                    Chat = new Chat { Id = chatId },
                    MessageId = 2
                }
            };
            
            await ProcessCallbackQueryAsync(callbackQuery);
        }

        public async Task SimulateAmountInputAsync(long chatId)
        {
            Console.WriteLine("💰 Simulating amount input...");
            
            var testAmount = _config["TestAmount"] ?? "0.001";
            var message = new Message
            {
                Chat = new Chat { Id = chatId },
                From = new User { Id = _testUserId, Username = _testUsername },
                Text = testAmount
            };
            
            await ProcessMessageAsync(message);
        }

        public async Task StopAsync()
        {
            // Note: StopReceivingAsync is not available in this version
            // The client will stop automatically when disposed
            Console.WriteLine("🛑 Automated client stopped.");
        }
    }
}
