using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Messages;
using TallaEgg.TelegramBot.Infrastructure.Messaging;

namespace TallaEgg.TelegramBot.Infrastructure;

/// <summary>
/// Asks the admins about quotes the plausibility band is holding (issue #158).
///
/// <para>
/// This exists because the two halves of the question live in different services. The band is in
/// Orders, where the price arrives; the admins are in Telegram, which only the bot can reach.
/// Orders has no Telegram dependency and the bot exposes no HTTP endpoint, so the bot polls rather
/// than Orders pushing — the same direction every other bot-to-service call already runs in, and
/// it survives a bot restart without Orders needing to retry anything.
/// </para>
///
/// <para>
/// A manual quote is not announced here: the admin who typed it is already in a conversation and
/// gets the question in their own chat, immediately. This is for the automatic ones, which arrive
/// while nobody is looking.
/// </para>
/// </summary>
public class PendingQuoteNotifierService : BackgroundService
{
    /// <summary>
    /// How often to look for a quote waiting on an answer.
    ///
    /// Faster than the two-minute auto-quote tick that produces them, so a proposal is put in front
    /// of somebody well inside the five minutes it stays publishable — most of that window should be
    /// the admin's thinking time, not the bot's polling lag.
    /// </summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long to stay quiet about a symbol after asking, however many new proposals arrive for it.
    ///
    /// <para>
    /// A feed stuck on a wrong price produces a fresh proposal every tick, each superseding the last
    /// and each with a new id — so keying "have I asked about this?" on the proposal alone would put
    /// a message in front of every admin every two minutes, all night. Keying it on the symbol
    /// instead asks once and then holds off, and the admin who answers is answering about the newest
    /// price either way, because the button carries whichever proposal is live when they press it.
    /// </para>
    ///
    /// <para>
    /// Fifteen minutes rather than the proposal's own five: the point is to stop repeating the same
    /// question, and re-asking the moment one expires would restore the flood a proposal at a time.
    /// </para>
    /// </summary>
    private static readonly TimeSpan QuietPeriodPerSymbol = TimeSpan.FromMinutes(15);

    private readonly IOrderApiClient _orderApi;
    private readonly IUsersApiClient _usersApi;
    private readonly IBotMessenger _messenger;
    private readonly ILogger<PendingQuoteNotifierService> _logger;

    /// <summary>
    /// When each symbol was last asked about, so a stuck feed does not turn into a stream of
    /// identical questions.
    ///
    /// <para>
    /// In memory, and deliberately: losing it on a restart costs one extra message per symbol with
    /// something outstanding, which is far cheaper than a table and a migration. Symbols with
    /// nothing waiting are dropped on every poll, so it cannot grow without bound.
    /// </para>
    /// </summary>
    private readonly ConcurrentDictionary<string, DateTime> _lastAskedPerSymbol = new(StringComparer.OrdinalIgnoreCase);

    public PendingQuoteNotifierService(
        IOrderApiClient orderApi,
        IUsersApiClient usersApi,
        IBotMessenger messenger,
        ILogger<PendingQuoteNotifierService> logger)
    {
        _orderApi = orderApi;
        _usersApi = usersApi;
        _messenger = messenger;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "PendingQuoteNotifierService started (poll every {Seconds}s).", PollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await AnnounceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Never let one bad poll stop the loop: the next tick is thirty seconds away and the
                // proposal stays in Orders either way.
                _logger.LogError(ex, "Unexpected error while announcing quotes awaiting approval.");
            }

            try
            {
                await Task.Delay(PollInterval, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("PendingQuoteNotifierService stopped.");
    }

    private async Task AnnounceAsync(CancellationToken ct)
    {
        var awaiting = await _orderApi.GetPendingQuotesAsync();

        // Null is "Orders could not be answered", which says nothing about what is waiting. Treating
        // it as an empty list would clear the record below and re-ask every admin about every open
        // proposal on the next poll, turning one network blip into a broadcast.
        if (awaiting is null) return;

        var now = DateTime.UtcNow;

        // Forget symbols with nothing outstanding, so a symbol that goes quiet and later needs
        // asking about again is asked immediately rather than serving out an old quiet period.
        foreach (var symbol in _lastAskedPerSymbol.Keys.ToList())
        {
            if (!awaiting.Any(p => string.Equals(p.Symbol, symbol, StringComparison.OrdinalIgnoreCase)))
                _lastAskedPerSymbol.TryRemove(symbol, out _);
        }

        var toAsk = awaiting
            .Where(p => !string.Equals(p.Source, "Manual", StringComparison.OrdinalIgnoreCase))
            .Where(p => !AskedRecently(p.Symbol, now))
            .GroupBy(p => p.Symbol, StringComparer.OrdinalIgnoreCase)
            .Select(bySymbol => bySymbol.OrderByDescending(p => p.CreatedAt).First())
            .ToList();

        if (toAsk.Count == 0) return;

        var admins = await _usersApi.GetOperatorTelegramIdsAsync();

        if (admins.Count == 0)
        {
            // Nobody to ask. Nothing is recorded as asked, so once an admin exists the question is
            // still put — but said loudly now, because a shop with a held quote and no reachable
            // admin is stuck on its previous price with no way to move.
            _logger.LogError(
                "{Count} quote(s) are waiting for approval and no admin could be reached to ask.",
                toAsk.Count);
            return;
        }

        foreach (var pending in toAsk)
        {
            var (text, keyboard) = PendingQuoteMessage.Build(pending, now);

            // Recorded before the first send, not after the last: a failure partway through must not
            // re-ask everyone who already received it on the next poll.
            _lastAskedPerSymbol[pending.Symbol] = now;

            foreach (var chatId in admins)
            {
                try
                {
                    await _messenger.SendAsync(chatId, text, keyboard, cancellationToken: ct);
                }
                catch (Exception ex)
                {
                    // One admin having blocked the bot must not stop the others being asked.
                    _logger.LogWarning(ex,
                        "Could not ask admin {ChatId} about the held quote for {Symbol}.", chatId, pending.Symbol);
                }
            }

            _logger.LogInformation(
                "Asked {AdminCount} admin(s) about the held {Source} quote for {Symbol} (buy {BuyPrice}, sell {SellPrice}). " +
                "No further question about this symbol for {QuietMinutes} minutes.",
                admins.Count, pending.Source, pending.Symbol, pending.BuyPrice, pending.SellPrice,
                QuietPeriodPerSymbol.TotalMinutes);
        }
    }

    private bool AskedRecently(string symbol, DateTime now) =>
        _lastAskedPerSymbol.TryGetValue(symbol, out var lastAsked) && now - lastAsked < QuietPeriodPerSymbol;
}
