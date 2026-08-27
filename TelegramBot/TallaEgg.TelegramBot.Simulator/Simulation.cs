using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;

namespace TallaEgg.TelegramBot.Simulator;

/// <summary>
/// Drives the real bot end to end: registration and scattered navigation (help, history,
/// balance) go through <see cref="IBotHandler"/> exactly as a real Telegram update would,
/// using <see cref="FakeBotMessenger"/> instead of a live chat. Bulk data generation — wallet
/// funding, quote volume, trade volume — calls the same typed API clients BotHandler itself
/// uses, directly, since replaying a multi-turn conversation a thousand times over buys no
/// extra coverage once the conversation path itself has been exercised.
/// </summary>
public sealed class Simulation(
    IBotHandler botHandler,
    IUsersApiClient usersApi,
    IOrderApiClient orderApi,
    IWalletApiClient walletApi,
    DataReset dataReset,
    ILogger<Simulation> logger)
{
    private const string Symbol = "MAUA/IRT";
    private static readonly string[] FirstNames = ["Ali", "Sara", "Reza", "Mina", "Hamid", "Neda", "Omid", "Yalda", "Kian", "Roya"];
    private static readonly string[] LastNames = ["Ahmadi", "Karimi", "Hosseini", "Moradi", "Jafari", "Rahimi", "Ghasemi", "Sadeghi"];

    private readonly List<string> _errors = [];

    public async Task RunAsync(SimulationOptions options)
    {
        var stopwatch = Stopwatch.StartNew();
        var random = new Random(options.Seed);

        logger.LogInformation(
            "=== TallaEgg bot simulator: {Users} users, {Quotes}+ quotes, {Trades}+ trades (seed {Seed}) ===",
            options.UserCount, options.QuoteCount, options.TradeCount, options.Seed);

        logger.LogInformation("-- Phase 0: reset previously simulated data --");
        await dataReset.RunAsync();

        logger.LogInformation("-- Phase 1: register {Count} virtual users via /start + phone share --", options.UserCount);
        var users = await RegisterUsersAsync(options.UserCount, random);

        logger.LogInformation("-- Phase 2: promote user #0 to admin, so it can approve the rest --");
        var admin = users[0];
        await PromoteToAdminAsync(admin);

        // Orders.Api runs its own AutoQuotePublisherService (issue #90) as a hosted service,
        // independently of anything this simulator does. Left on, it periodically replaces
        // whichever quote this run just published with one from a different market maker —
        // discovered by trades starting to fail ~600 in with "wallet not found" for a user
        // this run never touched. Every quote-accept trade needs a single, known market
        // maker throughout the run, so auto-quote is turned off for the run's symbol first.
        await orderApi.SetAutoQuoteEnabledAsync(Symbol, isEnabled: false, admin.UserId!.Value);

        logger.LogInformation("-- Phase 3: admin approves/rejects the remaining {Count} registrations --", users.Count - 1);
        await ApproveOrRejectUsersAsync(admin, users.Skip(1).ToList(), random);

        var approved = users.Where(u => u.Approved && u.UserId.HasValue).ToList();
        logger.LogInformation("{Approved}/{Total} users approved", approved.Count, users.Count);

        // Admin is the market maker behind every published quote, so admin can never be a
        // counterparty to its own fills — AcceptQuoteAsync correctly rejects that.
        var customers = approved.Where(u => u.TelegramId != admin.TelegramId).ToList();

        logger.LogInformation("-- Phase 4: fund every approved wallet so trades can clear --");
        await FundWalletsAsync(customers);

        // Admin is the counterparty to every quote fill in the market, not just its own
        // trades — its reserve depletes across the whole run, not per-user, so it needs an
        // order of magnitude more than a single customer regardless of run size. A first
        // pass at 100 users / 1000 trades ran out of admin MAUA around trade #656 and every
        // fill failed after that with "در حال حاضر امکان انجام این معامله نیست." — the
        // customer-sized funding below was the bug, not the product.
        await FundWalletsAsync([admin], multiplier: 50m);

        logger.LogInformation("-- Phase 5: admin charge/discharge sample --");
        await ChargeAndDischargeSampleAsync(admin, customers, random);

        logger.LogInformation("-- Phase 6: admin publishes {Count}+ quotes --", options.QuoteCount);
        await PublishQuotesAsync(admin, options.QuoteCount, random);

        logger.LogInformation("-- Phase 7: {Count}+ trades via quote acceptance --", options.TradeCount);
        var tradesDone = await GenerateTradesAsync(customers, options.TradeCount, random);

        logger.LogInformation("-- Phase 8: scattered user navigation (help, history, balance, active orders) --");
        await ScatterUserBehaviorAsync(approved, random);

        stopwatch.Stop();
        logger.LogInformation(
            "=== Done in {Elapsed}. Registered {Users} ({Approved} approved), trades attempted {Trades}, errors {Errors} ===",
            stopwatch.Elapsed, users.Count, approved.Count, tradesDone, _errors.Count);

        if (_errors.Count > 0)
        {
            logger.LogWarning("Errors encountered ({Count}), first 20:", _errors.Count);
            foreach (var e in _errors.Take(20))
            {
                logger.LogWarning("  {Error}", e);
            }
        }

        logger.LogInformation(
            "Every logged error above corresponds to a TraceId entry in logs/*.log under the relevant API service (issue #88).");
    }

    // ── Phase 1: registration, through the real conversation ──────────────────────────────

    private async Task<List<VirtualUser>> RegisterUsersAsync(int count, Random random)
    {
        var users = new List<VirtualUser>(count);

        for (var i = 0; i < count; i++)
        {
            var telegramId = SimulationOptions.TelegramIdBase + i;
            var user = new VirtualUser
            {
                TelegramId = telegramId,
                FirstName = FirstNames[random.Next(FirstNames.Length)],
                LastName = LastNames[random.Next(LastNames.Length)],
                Username = $"sim_user_{i}",
                Phone = $"+98912{i:D7}",
            };

            try
            {
                await botHandler.HandleMessageAsync(NewMessage(user, "/start?admin"));
                await botHandler.HandleMessageAsync(NewMessageWithContact(user));
                users.Add(user);
            }
            catch (Exception ex)
            {
                RecordError($"register user {i} (telegramId {telegramId})", ex);
            }
        }

        // Look up the ids the API assigned, and each account's current status, in one pass.
        foreach (var user in users)
        {
            try
            {
                var dto = await usersApi.GetUserAsync(user.TelegramId);
                user.UserId = dto?.Id;
            }
            catch (Exception ex)
            {
                RecordError($"look up registered user {user.TelegramId}", ex);
            }
        }

        logger.LogInformation("Registered {Count}/{Total} users (some may have failed — see errors).",
            users.Count(u => u.UserId.HasValue), count);
        return users;
    }

    private async Task PromoteToAdminAsync(VirtualUser admin)
    {
        if (admin.UserId is not { } userId)
        {
            RecordError("promote admin", new InvalidOperationException("User #0 has no UserId — registration must have failed."));
            return;
        }

        try
        {
            await usersApi.UpdateUserStatusAsync(admin.TelegramId, UserStatus.Approved);
            await usersApi.UpdateRoleAsync(userId, UserRole.Admin);
            admin.Approved = true;
        }
        catch (Exception ex)
        {
            RecordError("promote admin", ex);
        }
    }

    // ── Phase 3: approve/reject, through the real approve_/reject_ callback ───────────────

    private async Task ApproveOrRejectUsersAsync(VirtualUser admin, List<VirtualUser> pending, Random random)
    {
        foreach (var user in pending)
        {
            if (!user.UserId.HasValue)
            {
                continue; // registration already failed and was recorded
            }

            // 90% approved so there is enough of a population left to trade with; the rest
            // exercises the reject path, which nothing else in this run reaches.
            var approve = random.NextDouble() < 0.9;
            var data = (approve ? "approve_" : "reject_") + user.TelegramId;

            try
            {
                await botHandler.HandleCallbackQueryAsync(NewCallback(admin, data));
                user.Approved = approve;
            }
            catch (Exception ex)
            {
                RecordError($"approve/reject user {user.TelegramId}", ex);
            }
        }
    }

    // ── Phase 4: wallet funding (direct API — not a "behavior" worth replaying per user) ──

    private async Task FundWalletsAsync(List<VirtualUser> users, decimal multiplier = 1m)
    {
        foreach (var user in users)
        {
            try
            {
                await walletApi.DepositeAsync(new WalletRequest
                {
                    UserId = user.UserId!.Value,
                    Asset = TallaEgg.Core.CurrenciesConstant.Toman,
                    Amount = 500_000_000m * multiplier,
                });
                await walletApi.DepositeAsync(new WalletRequest
                {
                    UserId = user.UserId!.Value,
                    Asset = TallaEgg.Core.CurrenciesConstant.Maua,
                    Amount = 500m * multiplier,
                });
            }
            catch (Exception ex)
            {
                RecordError($"fund wallet for {user.TelegramId}", ex);
            }
        }
    }

    // ── Phase 5: admin charge/discharge, through the real "ش"/"د" text commands ───────────

    private async Task ChargeAndDischargeSampleAsync(VirtualUser admin, List<VirtualUser> users, Random random)
    {
        var sample = users.OrderBy(_ => random.Next()).Take(Math.Max(1, users.Count / 5)).ToList();

        foreach (var user in sample)
        {
            var normalizedPhone = user.Phone.Replace("+98", "0");
            try
            {
                // "ش <phone> <amount>" — charges CREDIT_MAUA (Maua is the default currency).
                await botHandler.HandleMessageAsync(NewMessage(admin, $"ش {normalizedPhone} {random.Next(1, 20)}"));

                // "د <phone> <amount>" — discharges spot IRT (Toman is the default currency).
                await botHandler.HandleMessageAsync(NewMessage(admin, $"د {normalizedPhone} {random.Next(1000, 50000)}"));
            }
            catch (Exception ex)
            {
                RecordError($"charge/discharge {user.TelegramId}", ex);
            }
        }
    }

    // ── Phase 6: quote publishing, through the real "buyPrice-sellPrice" text command ─────

    private async Task PublishQuotesAsync(VirtualUser admin, int count, Random random)
    {
        // A basis around a plausible gold price, walking randomly so quotes actually vary
        // instead of the matching engine seeing the same price a hundred times in a row.
        var basePrice = 18_500_000m;

        for (var i = 0; i < count; i++)
        {
            basePrice += random.Next(-50_000, 50_001);
            var spread = basePrice * 0.002m;
            var buy = Math.Round(basePrice - spread / 2, 0);
            var sell = Math.Round(basePrice + spread / 2, 0);

            try
            {
                await botHandler.HandleMessageAsync(NewMessage(admin, $"{(long)buy}-{(long)sell}"));
            }
            catch (Exception ex)
            {
                RecordError($"publish quote #{i}", ex);
            }
        }
    }

    // ── Phase 7: trade volume via quote acceptance (direct API — see class remarks) ───────

    private async Task<int> GenerateTradesAsync(List<VirtualUser> users, int targetCount, Random random)
    {
        if (users.Count == 0)
        {
            RecordError("generate trades", new InvalidOperationException("No approved users to trade with."));
            return 0;
        }

        var done = 0;
        for (var i = 0; i < targetCount; i++)
        {
            var user = users[random.Next(users.Count)];
            var side = random.Next(2) == 0 ? OrderSide.Buy : OrderSide.Sell;
            var quantity = Math.Round((decimal)(random.NextDouble() * 2.9 + 0.1), 2);

            try
            {
                var (success, message) = await orderApi.AcceptQuoteAsync(user.UserId!.Value, Symbol, side, quantity);
                if (!success)
                {
                    RecordError($"trade #{i} for {user.TelegramId}", new InvalidOperationException(message));
                }
                done++;
            }
            catch (Exception ex)
            {
                RecordError($"trade #{i} for {user.TelegramId}", ex);
            }
        }

        return done;
    }

    // ── Phase 8: the behaviors named explicitly — help, history, balance, active orders ───

    private async Task ScatterUserBehaviorAsync(List<VirtualUser> users, Random random)
    {
        string[] buttons =
        [
            "❓ راهنما",
            "📊 تاریخچه معاملات",
            "💵 موجودی",
        ];

        foreach (var user in users)
        {
            // Each user clicks two or three menu buttons in a random order, the way an
            // actual customer idly checking their account would — not every user checks
            // everything, and not always in the same sequence.
            var clicks = buttons.OrderBy(_ => random.Next()).Take(random.Next(1, buttons.Length + 1));
            foreach (var button in clicks)
            {
                try
                {
                    await botHandler.HandleMessageAsync(NewMessage(user, button));
                }
                catch (Exception ex)
                {
                    RecordError($"{button} for {user.TelegramId}", ex);
                }
            }
        }
    }

    // ── helpers ─────────────────────────────────────────────────────────────────────────

    private void RecordError(string action, Exception ex)
    {
        var line = $"{action}";
        _errors.Add(line);
        logger.LogWarning(ex, "Simulation step failed: {Action}", action);
    }

    private static Message NewMessage(VirtualUser user, string text) => new()
    {
        Chat = new Chat { Id = user.TelegramId, Type = ChatType.Private },
        From = new User { Id = user.TelegramId, FirstName = user.FirstName, LastName = user.LastName, Username = user.Username },
        Text = text,
        Date = DateTime.UtcNow,
    };

    private static Message NewMessageWithContact(VirtualUser user) => new()
    {
        Chat = new Chat { Id = user.TelegramId, Type = ChatType.Private },
        From = new User { Id = user.TelegramId, FirstName = user.FirstName, LastName = user.LastName, Username = user.Username },
        Contact = new Contact { PhoneNumber = user.Phone, FirstName = user.FirstName, LastName = user.LastName },
        Date = DateTime.UtcNow,
    };

    private static CallbackQuery NewCallback(VirtualUser actor, string data) => new()
    {
        Id = Guid.NewGuid().ToString(),
        From = new User { Id = actor.TelegramId, FirstName = actor.FirstName, LastName = actor.LastName, Username = actor.Username },
        Data = data,
        Message = new Message
        {
            Chat = new Chat { Id = actor.TelegramId, Type = ChatType.Private },
            Id = 1,
            Date = DateTime.UtcNow,
        },
        ChatInstance = actor.TelegramId.ToString(),
    };
}
