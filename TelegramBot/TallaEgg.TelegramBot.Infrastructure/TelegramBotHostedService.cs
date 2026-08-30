using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
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

    // A gap this much longer than the retry delay between two transient polling failures means
    // updates were flowing again in between, so the next failure starts a fresh streak rather
    // than continuing the old one (issue #148).
    private static readonly TimeSpan PollingRetryDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan PollingRecoveryGap = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollingDownAlertThreshold = TimeSpan.FromMinutes(2);

    private DateTimeOffset? _pollingFailureStreakStartedAt;
    private DateTimeOffset? _lastPollingFailureAt;
    private int _consecutivePollingFailures;
    private bool _pollingDownAlertLogged;

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

        // The handler's background work starts here rather than in its constructor, so
        // that constructing it — in a test, or during container setup — has no side
        // effects (issue #65).
        _botHandler.Start(_receiverCts.Token);

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
        // Resolved once so both the log entry and the fallback reply below can use them —
        // whichever branch below throws, this is who was talking to the bot (issue #99).
        var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id;
        var telegramId = update.Message?.From?.Id ?? update.CallbackQuery?.From?.Id;
        var handler = update.Message is not null ? "HandleMessageAsync" : "HandleCallbackQueryAsync";

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
            else if (update.CallbackQuery?.Message?.Chat.Type == ChatType.Private)
            {
                _logger.LogInformation("Received callback query from {User}: {Data}", update.CallbackQuery.From?.Username ?? "Unknown", update.CallbackQuery.Data);
                await _botHandler.HandleCallbackQueryAsync(update.CallbackQuery);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unhandled exception in {Handler}. ChatId={ChatId} TelegramId={TelegramId} UpdateType={UpdateType}",
                handler, chatId, telegramId, update.Type);

            // Without this the customer sees nothing at all — the exception was already
            // logged above, but until now that was the only trace of it anywhere, including
            // to the one person actually waiting on a reply.
            if (chatId is { } id)
            {
                try
                {
                    await botClient.SendMessage(id, BotMsgs.MsgUnexpectedError, cancellationToken: cancellationToken);
                }
                catch (Exception sendEx)
                {
                    _logger.LogError(sendEx, "Could not send the fallback error message to ChatId={ChatId}.", id);
                }
            }
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        // ApiRequestException means Telegram itself rejected the request (rotated token, rate
        // limit, ...). That's a real fault, not a transport blip, so it stays loud immediately.
        if (exception is ApiRequestException apiException)
        {
            _logger.LogError(exception, "Polling error ({Source}): Telegram API rejected the request ({ErrorCode}).", source, apiException.ErrorCode);
            return Task.CompletedTask;
        }

        // Every other RequestException is TelegramBotClient.SendRequest wrapping a transport-level
        // failure -- a dropped connection, a timed-out request, a malformed response. These are
        // routine on a flaky connection and recover on their own; logging each one at Error with a
        // full stack trace was making the log 100% noise, burying the one entry that matters
        // (issue #148). Log at Warning, without the stack trace, unless the bot has genuinely
        // stopped receiving updates for a while -- that's worth escalating to Error once.
        if (exception is RequestException)
        {
            return HandleTransientPollingFailure(source, exception, DateTimeOffset.UtcNow, cancellationToken);
        }

        // Anything else is unexpected for this handler -- keep it loud rather than risk silencing
        // a real bug behind the transient-failure path above.
        _logger.LogError(exception, "Polling error ({Source})", source);
        return Task.CompletedTask;
    }

    // `now` is passed in rather than read here so the escalation timing can be driven
    // deterministically from a test without sleeping real wall-clock minutes.
    private Task HandleTransientPollingFailure(HandleErrorSource source, Exception exception, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (_lastPollingFailureAt is null || now - _lastPollingFailureAt.Value > PollingRecoveryGap)
        {
            _pollingFailureStreakStartedAt = now;
            _consecutivePollingFailures = 0;
            _pollingDownAlertLogged = false;
        }

        _lastPollingFailureAt = now;
        _consecutivePollingFailures++;
        var downFor = now - _pollingFailureStreakStartedAt!.Value;

        if (downFor >= PollingDownAlertThreshold && !_pollingDownAlertLogged)
        {
            _pollingDownAlertLogged = true;
            _logger.LogError(exception,
                "Polling has been failing for {Duration} ({Count} consecutive failures, {Source}); the bot may not be receiving updates.",
                downFor, _consecutivePollingFailures, source);
        }
        else
        {
            _logger.LogWarning("Polling error ({Source}): {ExceptionType} - {Message}. Retrying in {Delay}...",
                source, exception.GetType().Name, exception.Message, PollingRetryDelay);
        }

        return Task.Delay(PollingRetryDelay, cancellationToken);
    }
}

