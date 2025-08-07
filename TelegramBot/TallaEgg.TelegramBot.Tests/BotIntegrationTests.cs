using System;
using System.Threading.Tasks;
using Xunit;
using Microsoft.Extensions.Configuration;
using TallaEgg.TelegramBot.Tests;

namespace TallaEgg.TelegramBot.Tests
{
    public class BotIntegrationTests : IDisposable
    {
        private readonly AutomatedTelegramClient _client;
        private readonly IConfiguration _config;

        public BotIntegrationTests()
        {
            // Load test configuration
            _config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("testsettings.json", optional: false)
                .Build();

            var botToken = _config["BotToken"];
            if (string.IsNullOrEmpty(botToken))
            {
                throw new InvalidOperationException("Bot token not found in test settings");
            }

            _client = new AutomatedTelegramClient(botToken, _config);
        }

        [Fact]
        public async Task TestCompleteBotFlow()
        {
            // Arrange
            await _client.StartListeningAsync();

            // Act & Assert
            await _client.TestCompleteFlowAsync();
        }

        [Fact]
        public async Task TestUserRegistration()
        {
            // Arrange
            await _client.StartListeningAsync();

            // Act & Assert
            await _client.TestUserRegistrationAsync();
        }

        [Fact]
        public async Task TestOrderPlacement()
        {
            // Arrange
            await _client.StartListeningAsync();

            // Act & Assert
            await _client.TestOrderPlacementAsync();
        }

        [Fact]
        public async Task TestBalanceValidation()
        {
            // Arrange
            await _client.StartListeningAsync();

            // Act & Assert
            await _client.TestBalanceValidationAsync();
        }

        [Fact]
        public async Task TestMainMenuNavigation()
        {
            // Arrange
            await _client.StartListeningAsync();

            // Act & Assert
            await _client.TestMainMenuAsync();
        }

        [Theory]
        [InlineData("order_buy")]
        [InlineData("order_sell")]
        public async Task TestOrderTypeSelection(string orderType)
        {
            // Arrange
            await _client.StartListeningAsync();

            // Act & Assert
            await _client.SimulateOrderTypeSelectionAsync(123456789, orderType);
        }

        [Theory]
        [InlineData("asset_BTC")]
        [InlineData("asset_ETH")]
        [InlineData("asset_USDT")]
        public async Task TestAssetSelection(string asset)
        {
            // Arrange
            await _client.StartListeningAsync();

            // Act & Assert
            await _client.SimulateAssetSelectionAsync(123456789, asset);
        }

        [Theory]
        [InlineData("0.001")]
        [InlineData("0.01")]
        [InlineData("1.0")]
        public async Task TestAmountInput(string amount)
        {
            // Arrange
            await _client.StartListeningAsync();

            // Act & Assert
            await _client.SimulateAmountInputAsync(123456789);
        }

        public void Dispose()
        {
            _client?.StopAsync().Wait();
        }
    }
}
