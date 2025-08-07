using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace TallaEgg.TelegramBot.Tests
{
    public class TestRunner
    {
        private readonly IConfiguration _config;
        private readonly AutomatedTelegramClient _client;
        private readonly MockBotHandler _mockHandler;

        public TestRunner()
        {
            _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("testsettings.json", optional: false)
                .Build();

            var botToken = _config["BotToken"];
            _client = new AutomatedTelegramClient(botToken, _config);
            _mockHandler = new MockBotHandler();
        }

        public async Task RunAllTestsAsync()
        {
            Console.WriteLine("🧪 Starting Automated Bot Tests...");
            Console.WriteLine("=====================================");

            try
            {
                // Run unit tests (no network required)
                await RunUnitTestsAsync();

                // Run integration tests (requires network)
                if (await CheckNetworkConnectivityAsync())
                {
                    await RunIntegrationTestsAsync();
                }
                else
                {
                    Console.WriteLine("⚠️ Network connectivity issues detected. Skipping integration tests.");
                }

                Console.WriteLine("✅ All tests completed!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Test execution failed: {ex.Message}");
                throw;
            }
        }

        private async Task RunUnitTestsAsync()
        {
            Console.WriteLine("\n📋 Running Unit Tests (No Network Required)...");
            
            var testResults = new[]
            {
                await TestUserRegistrationFlowAsync(),
                await TestOrderPlacementFlowAsync(),
                await TestBalanceValidationAsync(),
                await TestErrorHandlingAsync()
            };

            var passedTests = testResults.Count(r => r);
            var totalTests = testResults.Length;

            Console.WriteLine($"\n📊 Unit Test Results: {passedTests}/{totalTests} passed");
        }

        private async Task RunIntegrationTestsAsync()
        {
            Console.WriteLine("\n🌐 Running Integration Tests (Network Required)...");
            
            try
            {
                await _client.StartListeningAsync();
                await _client.TestCompleteFlowAsync();
                await _client.StopAsync();
                
                Console.WriteLine("✅ Integration tests completed successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Integration tests failed: {ex.Message}");
            }
        }

        private async Task<bool> TestUserRegistrationFlowAsync()
        {
            try
            {
                _mockHandler.ClearResponses();

                var startMessage = CreateTestMessage("/start TEST123");
                var phoneMessage = CreateTestMessageWithContact("+989123456789");

                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(startMessage));
                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(phoneMessage));

                var success = _mockHandler.ContainsResponse("کد دعوت معتبر است") &&
                             _mockHandler.ContainsResponse("شماره تلفن شما با موفقیت ثبت شد");

                Console.WriteLine($"✅ User Registration Flow: {(success ? "PASSED" : "FAILED")}");
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ User Registration Flow: FAILED - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestOrderPlacementFlowAsync()
        {
            try
            {
                _mockHandler.ClearResponses();

                var orderButton = CreateTestMessage("📝 ثبت سفارش");
                var buyCallback = CreateTestCallbackQuery("order_buy");
                var assetCallback = CreateTestCallbackQuery("asset_BTC");
                var amountMessage = CreateTestMessage("0.001");

                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(orderButton));
                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(buyCallback));
                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(assetCallback));
                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(amountMessage));

                var success = _mockHandler.ContainsResponse("نوع سفارش خود را انتخاب کنید") &&
                             _mockHandler.ContainsResponse("سفارش شما با موفقیت ثبت شد");

                Console.WriteLine($"✅ Order Placement Flow: {(success ? "PASSED" : "FAILED")}");
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Order Placement Flow: FAILED - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestBalanceValidationAsync()
        {
            try
            {
                _mockHandler.ClearResponses();

                var orderButton = CreateTestMessage("📝 ثبت سفارش");
                var sellCallback = CreateTestCallbackQuery("order_sell");
                var assetCallback = CreateTestCallbackQuery("asset_ETH");
                var largeAmount = CreateTestMessage("1.0"); // More than balance

                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(orderButton));
                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(sellCallback));
                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(assetCallback));
                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(largeAmount));

                var success = _mockHandler.ContainsResponse("موجودی کافی نیست");

                Console.WriteLine($"✅ Balance Validation: {(success ? "PASSED" : "FAILED")}");
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Balance Validation: FAILED - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> TestErrorHandlingAsync()
        {
            try
            {
                _mockHandler.ClearResponses();

                var invalidStart = CreateTestMessage("/start");
                await _mockHandler.HandleUpdateAsync(CreateTestUpdate(invalidStart));

                var success = _mockHandler.ContainsResponse("لطفاً کد دعوت را وارد کنید");

                Console.WriteLine($"✅ Error Handling: {(success ? "PASSED" : "FAILED")}");
                return success;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error Handling: FAILED - {ex.Message}");
                return false;
            }
        }

        private async Task<bool> CheckNetworkConnectivityAsync()
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
}
