using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using TallaEgg.TelegramBot;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Infrastructure.Options;

namespace TallaEgg.TelegramBot.Infrastructure;

public class TelegramBotHostedService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly IBotHandler _botHandler;
    private readonly ILogger<TelegramBotHostedService> _logger;
    private readonly TelegramBotOptions _options;
    private readonly IHostApplicationLifetime _applicationLifetime;

    private CancellationTokenSource? _receiverCts;

    public TelegramBotHostedService(
        ITelegramBotClient botClient,
        IBotHandler botHandler,
        ILogger<TelegramBotHostedService> logger,
        IOptions<TelegramBotOptions> options,
        IHostApplicationLifetime applicationLifetime)
    {
        _botClient = botClient;
        _botHandler = botHandler;
        _logger = logger;
        _options = options.Value;
        _applicationLifetime = applicationLifetime;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        LogConfiguration();

        if (!ValidateConfiguration())
        {
            _applicationLifetime.StopApplication();
            return;
        }

        if (!await RunDiagnosticsAsync())
        {
            _applicationLifetime.StopApplication();
            return;
        }

        if (!await InitializeBotAsync(cancellationToken))
        {
            _applicationLifetime.StopApplication();
            return;
        }

        await base.StartAsync(cancellationToken);
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _receiverCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);

        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = new[] { UpdateType.Message, UpdateType.CallbackQuery },
            Limit = 100
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: _receiverCts.Token);

        _logger.LogInformation("Bot is now running and listening for messages...");
        return Task.CompletedTask;
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        _receiverCts?.Cancel();
        _logger.LogInformation("Telegram bot is stopping.");
        return base.StopAsync(cancellationToken);
    }

    private void LogConfiguration()
    {
        _logger.LogInformation("Order API URL: {Url}", _options.OrderApiUrl);
        _logger.LogInformation("Users API URL: {Url}", _options.UsersApiUrl);
        _logger.LogInformation("Affiliate API URL: {Url}", _options.AffiliateApiUrl);
        _logger.LogInformation("Wallet API URL: {Url}", _options.WalletApiUrl);
        _logger.LogInformation("Require Referral Code: {Require}", _options.BotSettings.RequireReferralCode);
        _logger.LogInformation("Default Referral Code: {Code}", _options.BotSettings.DefaultReferralCode);
    }

    private bool ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.TelegramBotToken))
        {
            _logger.LogError("TelegramBotToken is not configured.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_options.OrderApiUrl) ||
            string.IsNullOrWhiteSpace(_options.UsersApiUrl) ||
            string.IsNullOrWhiteSpace(_options.AffiliateApiUrl) ||
            string.IsNullOrWhiteSpace(_options.WalletApiUrl))
        {
            _logger.LogError("One or more API URLs are not configured.");
            return false;
        }

        return true;
    }

    private async Task<bool> RunDiagnosticsAsync()
    {
        await NetworkTest.TestConnectivityAsync();
        await SimpleHttpTest.TestHttpRequestsAsync();
        await ProxyTest.TestWithProxyAsync();

        _logger.LogInformation("Testing bot token...");
        var tokenTest = await TestBotToken.TestTokenAsync(_options.TelegramBotToken!);
        if (tokenTest)
        {
            return true;
        }

        _logger.LogWarning("Network connectivity issues detected. Running offline test mode...");
        await OfflineTestMode.RunOfflineTestAsync();
        _logger.LogWarning("Offline test mode finished. Stopping application.");
        return false;
    }

    private async Task<bool> InitializeBotAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _botClient.DeleteWebhook(dropPendingUpdates: true, cancellationToken: cancellationToken);
            _logger.LogInformation("Webhook deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete webhook");
        }

        try
        {
            var me = await _botClient.GetMe(cancellationToken);
            _logger.LogInformation("Bot connection successful: @{Username}", me.Username);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Bot connection failed: {Message}", ex.Message);
            return false;
        }

        return true;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            
            if (update.Message is not null && update.Message.Chat.Type == ChatType.Private)
            {
                var preview = update.Message.Text is null
                    ? string.Empty
                    : update.Message.Text[..Math.Min(update.Message.Text.Length, 50)];
                _logger.LogInformation("Received message from {User}: {Preview}", update.Message.From?.Username ?? "Unknown", preview);
                await _botHandler.HandleMessageAsync(update.Message);
            }
            else if (update.CallbackQuery is not null && update.CallbackQuery.Message.Chat.Type == ChatType.Private)
            {
                _logger.LogInformation("Received callback query from {User}: {Data}", update.CallbackQuery.From?.Username ?? "Unknown", update.CallbackQuery.Data);
                await _botHandler.HandleCallbackQueryAsync(update.CallbackQuery);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Polling error ({Source})", source);

        if (exception.Message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
            exception.Message.Contains("timed out", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Timeout detected. Waiting 10 seconds before retrying...");
            return Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        }

        return Task.CompletedTask;
    }
}

