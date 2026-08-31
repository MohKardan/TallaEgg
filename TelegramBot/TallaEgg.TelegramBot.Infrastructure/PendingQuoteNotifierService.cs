using System.Collections.Concurrent;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.User;
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

    private readonly IOrderApiClient _orderApi;
    private readonly IUsersApiClient _usersApi;
    private readonly IBotMessenger _messenger;
    private readonly ILogger<PendingQuoteNotifierService> _logger;

    /// <summary>
    /// Proposals already announced, so a poll every thirty seconds does not re-send the same
    /// question while an admin is thinking about it.
    ///
    /// <para>
    /// In memory, and deliberately: losing it on a restart costs one duplicate message per open
    /// proposal, which is far cheaper than a table and a migration. Entries are dropped once the
    /// proposal stops being offered, so this cannot grow without bound.
    /// </para>
    /// </summary>
    private readonly ConcurrentDictionary<Guid, byte> _announced = new();

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

        // Forget anything no longer on offer — answered, expired, or superseded by a newer price.
        // Doing this every poll, rather than on a timer of its own, keeps the set exactly as large
        // as the number of open proposals.
        foreach (var id in _announced.Keys.Where(id => awaiting.All(p => p.Id != id)).ToList())
            _announced.TryRemove(id, out _);

        var unannounced = awaiting
            .Where(p => !string.Equals(p.Source, "Manual", StringComparison.OrdinalIgnoreCase))
            .Where(p => !_announced.ContainsKey(p.Id))
            .ToList();

        if (unannounced.Count == 0) return;

        var admins = await AdminChatIdsAsync();

        if (admins.Count == 0)
        {
            // Nobody to ask. Left unannounced on purpose, so that once an admin exists the question
            // is still asked rather than having been silently dropped — but said loudly now, because
            // a shop with no reachable admin and a held quote is stuck.
            _logger.LogError(
                "{Count} quote(s) are waiting for approval and no admin could be reached to ask.",
                unannounced.Count);
            return;
        }

        foreach (var pending in unannounced)
        {
            var (text, keyboard) = PendingQuoteMessage.Build(pending, DateTime.UtcNow);

            // Marked as announced before the first send, not after the last: a failure partway
            // through must not re-ask everyone who already received it on the next poll.
            _announced[pending.Id] = 0;

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
                "Asked {AdminCount} admin(s) about the held {Source} quote for {Symbol} (buy {BuyPrice}, sell {SellPrice}).",
                admins.Count, pending.Source, pending.Symbol, pending.BuyPrice, pending.SellPrice);
        }
    }

    /// <summary>
    /// Everyone who can answer: Admin and SuperAdmin alike, because anyone who can publish a quote
    /// can judge one. The first to press decides, and the rest are told the question is closed by
    /// the refusal their own button produces.
    /// </summary>
    private async Task<IReadOnlyList<long>> AdminChatIdsAsync()
    {
        var admins = await _usersApi.GetUsersByRoleAsync(UserRole.Admin);
        var superAdmins = await _usersApi.GetUsersByRoleAsync(UserRole.SuperAdmin);

        return admins.Concat(superAdmins)
            .Select(u => u.TelegramId)
            .Where(id => id != 0)
            .Distinct()
            .ToList();
    }
}
