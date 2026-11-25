using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TallaEgg.AllServices.Tests.Fakes;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Options;
using Telegram.Bot;
using Telegram.Bot.Types;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Pins the opt-in gate for blocking external startup diagnostics in
/// <see cref="TelegramBotHostedService"/> (issue #366). By default, <c>StartAsync</c> skips
/// <c>NetworkTest</c>/<c>SimpleHttpTest</c>/<c>ProxyTest</c>/<c>TestBotToken</c> (which made
/// unproxied external HTTP requests and fell back to <c>OfflineTestMode</c>) and initializes
/// directly through the configured <see cref="ITelegramBotClient"/>.
/// </summary>
public class TelegramBotHostedServiceStartupDiagnosticsTests
{
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("false", false)]
    [InlineData("FALSE", false)]
    [InlineData("0", false)]
    [InlineData("no", false)]
    [InlineData("yes", false)]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("True", true)]
    [InlineData(" true ", true)]
    [InlineData("1", true)]
    [InlineData(" 1 ", true)]
    public void ShouldRunStartupDiagnostics_RequiresExplicitTrueOrOne(string? envValue, bool expected)
    {
        Assert.Equal(expected, TelegramBotHostedService.ShouldRunStartupDiagnostics(envValue));
    }

    [Fact]
    public async Task StartAsync_WhenDiagnosticsEnvUnset_SkipsExternalProbesAndUsesConfiguredBotClient()
    {
        var previous = Environment.GetEnvironmentVariable(TelegramBotHostedService.RunStartupDiagnosticsEnvVar);
        Environment.SetEnvironmentVariable(TelegramBotHostedService.RunStartupDiagnosticsEnvVar, null);

        try
        {
            var handler = new StubTelegramHttpMessageHandler();
            using var httpClient = new HttpClient(handler);
            var botClient = new TelegramBotClient("1:AA", httpClient);
            var botHandler = new StubBotHandler();
            var logger = new CapturingLogger<TelegramBotHostedService>();
            var lifetime = new FakeHostApplicationLifetime();

            var options = Options.Create(new TelegramBotOptions
            {
                TelegramBotToken = "1:AA",
                OrderApiUrl = "http://127.0.0.1:5140",
                UsersApiUrl = "http://127.0.0.1:5136",
                AffiliateApiUrl = "http://127.0.0.1:60812",
                WalletApiUrl = "http://127.0.0.1:60933",
            });

            var service = new TelegramBotHostedService(botClient, botHandler, logger, options, lifetime);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await service.StartAsync(cts.Token);
            await service.StopAsync(CancellationToken.None);

            Assert.False(lifetime.StopApplicationCalled);
            Assert.True(botHandler.Started);
            Assert.Contains(handler.RequestedPaths, p => p.EndsWith("/deleteWebhook", StringComparison.Ordinal));
            Assert.Contains(handler.RequestedPaths, p => p.EndsWith("/getMe", StringComparison.Ordinal));
            Assert.Contains(logger.Entries, e => e.Level == LogLevel.Information && e.Message.Contains("@test_bot", StringComparison.Ordinal));
            Assert.DoesNotContain(logger.Entries, e => e.Message.Contains("offline test mode", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            Environment.SetEnvironmentVariable(TelegramBotHostedService.RunStartupDiagnosticsEnvVar, previous);
        }
    }

    private sealed class StubTelegramHttpMessageHandler : HttpMessageHandler
    {
        public List<string> RequestedPaths { get; } = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            lock (RequestedPaths)
            {
                RequestedPaths.Add(path);
            }

            string payload = path switch
            {
                var p when p.EndsWith("/deleteWebhook", StringComparison.Ordinal) =>
                    """{"ok":true,"result":true}""",
                var p when p.EndsWith("/getMe", StringComparison.Ordinal) =>
                    """{"ok":true,"result":{"id":1,"is_bot":true,"first_name":"Test","username":"test_bot"}}""",
                var p when p.EndsWith("/getUpdates", StringComparison.Ordinal) =>
                    """{"ok":true,"result":[]}""",
                _ => """{"ok":true,"result":true}"""
            };

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class StubBotHandler : IBotHandler
    {
        public bool Started { get; private set; }

        public Task HandleMessageAsync(Message message) => Task.CompletedTask;
        public Task HandleCallbackQueryAsync(CallbackQuery callbackQuery) => Task.CompletedTask;
        public void Start(CancellationToken cancellationToken = default) => Started = true;
    }
}
