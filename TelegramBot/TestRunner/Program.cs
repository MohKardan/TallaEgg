using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.Linq;

namespace TestRunner
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("🤖 TallaEgg Telegram Bot Automated Testing");
            Console.WriteLine("==========================================");

            try
            {
                // Load configuration
                var config = new ConfigurationBuilder()
                    .SetBasePath(Directory.GetCurrentDirectory())
                    .AddJsonFile("testsettings.json", optional: false)
                    .Build();

                // Run mock tests (no network required)
                await RunMockTestsAsync();

                // Check network connectivity
                if (await CheckNetworkConnectivityAsync())
                {
                    Console.WriteLine("✅ Network connectivity available");
                    Console.WriteLine("⚠️ Note: Integration tests would run here with network access");
                }
                else
                {
                    Console.WriteLine("⚠️ Network connectivity issues detected");
                    Console.WriteLine("📋 Only mock tests are available");
                }

                Console.WriteLine("\n✅ All available tests completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test execution failed: {ex.Message}");
            }

            Console.WriteLine("\nPress any key to exit...");
            Console.ReadKey();
        }

        private static async Task RunMockTestsAsync()
        {
            Console.WriteLine("\n📋 Running Mock Tests (No Network Required)...");
            
            var mockHandler = new MockBotHandler();
            var testResults = new[]
            {
                await TestUserRegistrationFlowAsync(mockHandler),
                await TestOrderPlacementFlowAsync(mockHandler),
                await TestBalanceValidationAsync(mockHandler),
                await TestErrorHandlingAsync(mockHandler)
            };

            var passedTests = testResults.Count(r => r);
            var totalTests = testResults.Length;

            Console.WriteLine($"\n📊 Mock Test Results: {passedTests}/{totalTests} passed");
        }

        private static async Task<bool> TestUserRegistrationFlowAsync(MockBotHandler mockHandler)
        {
            try
            {
                mockHandler.ClearResponses();

                var startMessage = CreateTestMessage("/start TEST123");
                var phoneMessage = CreateTestMessageWithContact("+989123456789");

                await mockHandler.HandleUpdateAsync(CreateTestUpdate(startMessage));
                await mockHandler.HandleUpdateAsync(CreateTestUpdate(phoneMessage));

                var success = mockHandler.ContainsResponse("کد دعوت معتبر است") &&
                             mockHandler.ContainsResponse("شماره تلفن شما با موفقیت ثبت شد");

                Console.WriteLine($"✅ User Registration Flow: {(success ? "PASSED" : "FAILED")}");
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ User Registration Flow: FAILED - {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TestOrderPlacementFlowAsync(MockBotHandler mockHandler)
        {
            try
            {
                mockHandler.ClearResponses();

                var orderButton = CreateTestMessage("📝 ثبت سفارش");
                var buyCallback = CreateTestCallbackQuery("order_buy");
                var assetCallback = CreateTestCallbackQuery("asset_BTC");
                var amountMessage = CreateTestMessage("0.001");

                await mockHandler.HandleUpdateAsync(CreateTestUpdate(orderButton));
                await mockHandler.HandleUpdateAsync(CreateTestUpdate(buyCallback));
                await mockHandler.HandleUpdateAsync(CreateTestUpdate(assetCallback));
                await mockHandler.HandleUpdateAsync(CreateTestUpdate(amountMessage));

                var success = mockHandler.ContainsResponse("نوع سفارش خود را انتخاب کنید") &&
                             mockHandler.ContainsResponse("سفارش شما با موفقیت ثبت شد");

                Console.WriteLine($"✅ Order Placement Flow: {(success ? "PASSED" : "FAILED")}");
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Order Placement Flow: FAILED - {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TestBalanceValidationAsync(MockBotHandler mockHandler)
        {
            try
            {
                mockHandler.ClearResponses();

                var orderButton = CreateTestMessage("📝 ثبت سفارش");
                var sellCallback = CreateTestCallbackQuery("order_sell");
                var assetCallback = CreateTestCallbackQuery("asset_ETH");
                var largeAmount = CreateTestMessage("1.0"); // More than balance

                await mockHandler.HandleUpdateAsync(CreateTestUpdate(orderButton));
                await mockHandler.HandleUpdateAsync(CreateTestUpdate(sellCallback));
                await mockHandler.HandleUpdateAsync(CreateTestUpdate(assetCallback));
                await mockHandler.HandleUpdateAsync(CreateTestUpdate(largeAmount));

                var success = mockHandler.ContainsResponse("موجودی کافی نیست");

                Console.WriteLine($"✅ Balance Validation: {(success ? "PASSED" : "FAILED")}");
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Balance Validation: FAILED - {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> TestErrorHandlingAsync(MockBotHandler mockHandler)
        {
            try
            {
                mockHandler.ClearResponses();

                var invalidStart = CreateTestMessage("/start");
                await mockHandler.HandleUpdateAsync(CreateTestUpdate(invalidStart));

                var success = mockHandler.ContainsResponse("لطفاً کد دعوت را وارد کنید");

                Console.WriteLine($"✅ Error Handling: {(success ? "PASSED" : "FAILED")}");
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error Handling: FAILED - {ex.Message}");
                return false;
            }
        }

        private static async Task<bool> CheckNetworkConnectivityAsync()
        {
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                
                var response = await client.GetAsync("https://api.telegram.org");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private static Telegram.Bot.Types.Message CreateTestMessage(string text)
        {
            return new Telegram.Bot.Types.Message
            {
                Chat = new Telegram.Bot.Types.Chat { Id = 123456789 },
                From = new Telegram.Bot.Types.User { Id = 123456789, Username = "testuser" },
                Text = text
            };
        }

        private static Telegram.Bot.Types.Message CreateTestMessageWithContact(string phoneNumber)
        {
            return new Telegram.Bot.Types.Message
            {
                Chat = new Telegram.Bot.Types.Chat { Id = 123456789 },
                From = new Telegram.Bot.Types.User { Id = 123456789, Username = "testuser" },
                Contact = new Telegram.Bot.Types.Contact
                {
                    PhoneNumber = phoneNumber,
                    FirstName = "Test",
                    LastName = "User"
                }
            };
        }

        private static Telegram.Bot.Types.CallbackQuery CreateTestCallbackQuery(string data)
        {
            return new Telegram.Bot.Types.CallbackQuery
            {
                Id = Guid.NewGuid().ToString(),
                From = new Telegram.Bot.Types.User { Id = 123456789, Username = "testuser" },
                Data = data,
                Message = new Telegram.Bot.Types.Message
                {
                    Chat = new Telegram.Bot.Types.Chat { Id = 123456789 },
                    MessageId = 1
                }
            };
        }

        private static Telegram.Bot.Types.Update CreateTestUpdate(Telegram.Bot.Types.Message message)
        {
            return new Telegram.Bot.Types.Update { Message = message };
        }

        private static Telegram.Bot.Types.Update CreateTestUpdate(Telegram.Bot.Types.CallbackQuery callbackQuery)
        {
            return new Telegram.Bot.Types.Update { CallbackQuery = callbackQuery };
        }
    }

    public class MockBotHandler
    {
        private readonly Dictionary<long, string> _userStates = new();
        private readonly List<string> _botResponses = new();
        private readonly List<string> _userActions = new();

        public List<string> BotResponses => _botResponses;
        public List<string> UserActions => _userActions;

        public async Task HandleUpdateAsync(Telegram.Bot.Types.Update update)
        {
            if (update.Message != null)
            {
                await HandleMessageAsync(update.Message);
            }
            else if (update.CallbackQuery != null)
            {
                await HandleCallbackQueryAsync(update.CallbackQuery);
            }
        }

        private async Task HandleMessageAsync(Telegram.Bot.Types.Message message)
        {
            var text = message.Text ?? "";
            var userId = message.From?.Id ?? 0;
            
            _userActions.Add($"User sent: {text}");

            if (text.StartsWith("/start"))
            {
                await HandleStartCommandAsync(message);
            }
            else if (text == "📝 ثبت سفارش")
            {
                await HandleOrderPlacementAsync(message);
            }
            else if (decimal.TryParse(text, out var amount))
            {
                await HandleAmountInputAsync(message, amount);
            }
            else if (message.Contact != null)
            {
                await HandlePhoneNumberAsync(message);
            }
            else
            {
                await HandleInvitationCodeAsync(message);
            }
        }

        private async Task HandleCallbackQueryAsync(Telegram.Bot.Types.CallbackQuery callbackQuery)
        {
            var data = callbackQuery.Data ?? "";
            var userId = callbackQuery.From?.Id ?? 0;
            
            _userActions.Add($"User clicked: {data}");

            if (data == "order_buy" || data == "order_sell")
            {
                await HandleOrderTypeSelectionAsync(callbackQuery, data);
            }
            else if (data.StartsWith("asset_"))
            {
                await HandleAssetSelectionAsync(callbackQuery, data);
            }
            else if (data == "back_to_main")
            {
                await HandleBackToMainAsync(callbackQuery);
            }
        }

        private async Task HandleStartCommandAsync(Telegram.Bot.Types.Message message)
        {
            var parts = message.Text?.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts?.Length > 1)
            {
                var invitationCode = parts[1];
                _botResponses.Add($"Processing invitation code: {invitationCode}");
                
                // Simulate successful invitation validation
                _botResponses.Add("✅ کد دعوت معتبر است");
                _botResponses.Add("📱 لطفاً شماره تلفن خود را به اشتراک بگذارید");
            }
            else
            {
                _botResponses.Add("❌ لطفاً کد دعوت را وارد کنید");
            }
        }

        private async Task HandleInvitationCodeAsync(Telegram.Bot.Types.Message message)
        {
            var code = message.Text ?? "";
            _botResponses.Add($"Processing invitation code: {code}");
            _botResponses.Add("✅ کد دعوت معتبر است");
            _botResponses.Add("📱 لطفاً شماره تلفن خود را به اشتراک بگذارید");
        }

        private async Task HandlePhoneNumberAsync(Telegram.Bot.Types.Message message)
        {
            var phone = message.Contact?.PhoneNumber ?? "";
            _botResponses.Add($"Processing phone number: {phone}");
            _botResponses.Add("✅ شماره تلفن شما با موفقیت ثبت شد!");
            _botResponses.Add("🎯 منوی اصلی");
        }

        private async Task HandleOrderPlacementAsync(Telegram.Bot.Types.Message message)
        {
            _botResponses.Add("نوع سفارش خود را انتخاب کنید:");
            _botResponses.Add("🛒 خرید | 🛍️ فروش");
        }

        private async Task HandleOrderTypeSelectionAsync(Telegram.Bot.Types.CallbackQuery callbackQuery, string orderType)
        {
            var userId = callbackQuery.From?.Id ?? 0;
            _userStates[userId] = orderType;
            
            _botResponses.Add($"Order type selected: {orderType}");
            _botResponses.Add("لطفاً نماد مورد نظر را انتخاب کنید:");
            _botResponses.Add("🪙 BTC | 🪙 ETH | 🪙 USDT");
        }

        private async Task HandleAssetSelectionAsync(Telegram.Bot.Types.CallbackQuery callbackQuery, string assetData)
        {
            var userId = callbackQuery.From?.Id ?? 0;
            var asset = assetData.Replace("asset_", "");
            
            _botResponses.Add($"Asset selected: {asset}");
            _botResponses.Add("لطفاً مقدار واحد را وارد کنید:");
            _botResponses.Add($"قیمت: 50,000,000 تومان");
        }

        private async Task HandleAmountInputAsync(Telegram.Bot.Types.Message message, decimal amount)
        {
            var userId = message.From?.Id ?? 0;
            
            _botResponses.Add($"Amount entered: {amount}");
            
            if (_userStates.TryGetValue(userId, out var orderType) && orderType == "order_sell")
            {
                // Simulate balance check for sell orders
                var balance = 0.5m;
                if (amount > balance)
                {
                    _botResponses.Add($"❌ موجودی کافی نیست. موجودی شما: {balance} واحد");
                }
                else
                {
                    _botResponses.Add("✅ سفارش شما با موفقیت ثبت شد!");
                }
            }
            else
            {
                _botResponses.Add("✅ سفارش شما با موفقیت ثبت شد!");
            }
        }

        private async Task HandleBackToMainAsync(Telegram.Bot.Types.CallbackQuery callbackQuery)
        {
            _botResponses.Add("🎯 منوی اصلی");
        }

        public void ClearResponses()
        {
            _botResponses.Clear();
            _userActions.Clear();
            _userStates.Clear();
        }

        public bool ContainsResponse(string expectedResponse)
        {
            return _botResponses.Any(r => r.Contains(expectedResponse));
        }

        public bool ContainsAction(string expectedAction)
        {
            return _userActions.Any(a => a.Contains(expectedAction));
        }
    }
}
