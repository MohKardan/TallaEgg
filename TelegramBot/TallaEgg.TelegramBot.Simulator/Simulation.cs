using System.Diagnostics;
using Microsoft.Extensions.Logging;
using TallaEgg.Core;
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
    /// <summary>
    /// How much of the symbol's own spread the published buy and sell legs sit either side of the
    /// walking mid. A fraction rather than an amount, so it means the same thing on a gram of gold
    /// and on a Bitcoin.
    /// </summary>
    private const decimal QuoteSpreadFraction = 0.002m;

    /// <summary>
    /// How far one published quote may move from the one before it, as a fraction of the price.
    ///
    /// Deliberately far inside the ±5% plausibility band: a quote outside it is held for an admin
    /// to confirm rather than published (issue #158), and a held quote is not a published one — so
    /// a run that walked too far would leave a symbol with no quote and every trade on it failing.
    /// </summary>
    private const double QuoteWalkFraction = 0.0025;

    /// <summary>
    /// How many maximum-size trades each user is funded for, per symbol. Every base asset, its
    /// credit ledger and the shared quote currency are all sized from this one number, so no
    /// symbol runs its wallets dry earlier than another.
    /// </summary>
    private const int FundedTradesPerUser = 40;

    /// <summary>
    /// A plausible price per base unit, in the quote currency, for each symbol the platform ships
    /// with — per gram for gold, not the per-mesghal figure an admin types.
    ///
    /// <para>
    /// Only consulted for a symbol that has no published quote to anchor on. Whatever this run
    /// publishes becomes the price the next quote is measured against, and one more than ±5% away
    /// is held for approval instead of published (issue #158) — so an invented figure here would
    /// lock the live price feed out of its own market. These are the levels the feed itself was
    /// publishing on 2026-09-01: melted gold ≈ 22.1M toman per gram, a full Bahar Azadi coin at
    /// ≈ 1.24x the mesghal price of melted gold, Bitcoin ≈ 16.6 billion toman.
    /// </para>
    /// </summary>
    private static readonly Dictionary<string, decimal> ReferenceUnitPrices =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [CurrenciesConstant.Maua] = 22_100_000m,
            [CurrenciesConstant.SekeBahar] = 118_900_000m,
            [CurrenciesConstant.Btc] = 16_620_000_000m,
        };

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

        // Every symbol the platform trades, not one of them. A run that touches a single symbol
        // proves that symbol works and reads as if it proved the platform works — which is how
        // 1000 clean MAUA trades sat on top of #146 for as long as Bitcoin had been tradable.
        var plans = await BuildSymbolPlansAsync();
        logger.LogInformation("Trading {Count} symbol(s) this run:", plans.Count);
        foreach (var plan in plans)
        {
            logger.LogInformation(
                "  {Symbol}: quantities {Min} to {Max} at {Decimals} decimal place(s), around {Price} {Quote} per {Unit}",
                plan.Symbol, plan.MinTradeQuantity, plan.MaxTradeQuantity, plan.Pair.BaseDecimalPlaces,
                plan.ReferenceUnitPrice, plan.Pair.QuoteAsset, plan.Pair.BaseUnit);
        }

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
        // maker throughout the run, so auto-quote is turned off for every symbol it trades.
        foreach (var plan in plans)
        {
            await orderApi.SetAutoQuoteEnabledAsync(plan.Symbol, isEnabled: false, admin.UserId!.Value);
        }

        logger.LogInformation("-- Phase 3: admin approves/rejects the remaining {Count} registrations --", users.Count - 1);
        await ApproveOrRejectUsersAsync(admin, users.Skip(1).ToList(), random);

        var approved = users.Where(u => u.Approved && u.UserId.HasValue).ToList();
        logger.LogInformation("{Approved}/{Total} users approved", approved.Count, users.Count);

        // Admin is the market maker behind every published quote, so admin can never be a
        // counterparty to its own fills — AcceptQuoteAsync correctly rejects that.
        var customers = approved.Where(u => u.TelegramId != admin.TelegramId).ToList();

        logger.LogInformation("-- Phase 4: fund every approved wallet so trades can clear --");
        await FundWalletsAsync(customers, plans);

        // Admin is the counterparty to every quote fill in the market, not just its own
        // trades — its reserve depletes across the whole run, not per-user, so it needs an
        // order of magnitude more than a single customer regardless of run size. A first
        // pass at 100 users / 1000 trades ran out of admin MAUA around trade #656 and every
        // fill failed after that with "در حال حاضر امکان انجام این معامله نیست." — the
        // customer-sized funding below was the bug, not the product.
        //
        // Trading several symbols does not make that reserve run out sooner: the funding above is
        // per symbol, so the same budget is not divided between them — a symbol added to the run
        // brings its own base asset and its own credit ledger with it.
        await FundWalletsAsync([admin], plans, multiplier: 50m);

        logger.LogInformation("-- Phase 5: admin charge/discharge sample --");
        await ChargeAndDischargeSampleAsync(admin, customers, random);

        logger.LogInformation("-- Phase 6: admin publishes {Count}+ quotes across {Symbols} symbol(s) --",
            options.QuoteCount, plans.Count);
        await PublishQuotesAsync(admin, plans, options.QuoteCount, random);

        // A quote too far from the one already published is held for an admin to confirm rather
        // than published (issue #158), and a symbol with no active quote refuses every fill. Left
        // unchecked that is a symbol quietly contributing nothing to a run that still reports
        // itself green — the exact shape of gap this simulator exists to close.
        var tradable = await FilterToQuotedSymbolsAsync(plans);

        logger.LogInformation("-- Phase 7: {Count}+ trades via quote acceptance --", options.TradeCount);
        var (tradesDone, tradesBySymbol) = await GenerateTradesAsync(customers, tradable, options.TradeCount, random);

        logger.LogInformation("-- Phase 8: scattered user navigation (help, history, balance, active orders) --");
        await ScatterUserBehaviorAsync(approved, random);

        stopwatch.Stop();
        logger.LogInformation(
            "=== Done in {Elapsed}. Registered {Users} ({Approved} approved), trades attempted {Trades}, errors {Errors} ===",
            stopwatch.Elapsed, users.Count, approved.Count, tradesDone, _errors.Count);

        // Reported per symbol because the total says nothing about coverage: a symbol with zero
        // settled trades is a symbol this run did not exercise, however green the total looks.
        logger.LogInformation("Settled trades by symbol:");
        foreach (var plan in plans)
        {
            logger.LogInformation("  {Symbol}: {Count} settled", plan.Symbol,
                tradesBySymbol.TryGetValue(plan.Symbol, out var settled) ? settled : 0);
        }

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

    // ── The run's symbols, and the sizes and prices each one gets ─────────────────────────

    /// <summary>
    /// Builds one <see cref="SymbolPlan"/> per trading pair the platform knows about, read from
    /// <see cref="CurrenciesConstant.AllTradingPairs"/> rather than named here — a pair added by
    /// configuration alone is then traded by the next run with no change to this file.
    /// </summary>
    private async Task<List<SymbolPlan>> BuildSymbolPlansAsync()
    {
        var plans = new List<SymbolPlan>();

        // Ordered by symbol so a run is reproducible from its seed alone: the pair catalogue is a
        // dictionary, and the order configuration happens to merge into it would otherwise decide
        // which symbol each round-robin trade lands on.
        foreach (var pair in CurrenciesConstant.AllTradingPairs.OrderBy(p => p.Symbol, StringComparer.Ordinal))
        {
            plans.Add(SymbolPlan.For(pair, await ResolveReferenceUnitPriceAsync(pair)));
        }

        return plans;
    }

    /// <summary>
    /// The price this run will publish quotes around, per base unit in the quote currency.
    ///
    /// <para>
    /// Anchored on whatever is already published, because a quote more than ±5% from the current
    /// one is held for approval instead of published (issue #158): a run that ignored the live
    /// price would publish nothing for the symbol, and every trade on it would then fail for want
    /// of a quote. <see cref="DataReset"/> deliberately leaves quotes behind, so this is normally
    /// the price the last run or the price feed left.
    /// </para>
    /// </summary>
    private async Task<decimal> ResolveReferenceUnitPriceAsync(TradingPairInfo pair)
    {
        try
        {
            var quote = await orderApi.GetActiveQuoteAsync(pair.Symbol);
            if (quote is not null && quote.BuyPrice > 0 && quote.SellPrice > 0)
                return (quote.BuyPrice + quote.SellPrice) / 2m;
        }
        catch (Exception ex)
        {
            RecordError($"read the active quote for {pair.Symbol}", ex);
        }

        if (ReferenceUnitPrices.TryGetValue(pair.BaseAsset, out var reference))
            return reference;

        // A pair nobody has listed above and that has never been quoted: the price at which its
        // own smallest tradable quantity is worth its own smallest tradable value. Not a market
        // price, but the right order of magnitude — which is all the run needs to size trades and
        // fund wallets, and the only figure available without inventing one.
        return pair.MinQuantity > 0 ? pair.MinNotional / pair.MinQuantity : 1m;
    }

    /// <summary>
    /// Drops any symbol that ended Phase 6 without an active quote, and records an error for it so
    /// the run cannot report itself clean while silently covering fewer symbols than it claims.
    /// </summary>
    private async Task<List<SymbolPlan>> FilterToQuotedSymbolsAsync(List<SymbolPlan> plans)
    {
        var quoted = new List<SymbolPlan>();

        foreach (var plan in plans)
        {
            try
            {
                if (await orderApi.GetActiveQuoteAsync(plan.Symbol) is not null)
                {
                    quoted.Add(plan);
                    continue;
                }

                RecordError($"publish a quote for {plan.Symbol}", new InvalidOperationException(
                    "No active quote after the quote phase — it was probably held for approval as " +
                    "implausible; no trade on this symbol can be filled."));
            }
            catch (Exception ex)
            {
                RecordError($"check the active quote for {plan.Symbol}", ex);
            }
        }

        return quoted;
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

    /// <summary>
    /// Funds every wallet a run needs: each quote currency, then each traded symbol's base asset
    /// and that asset's credit ledger.
    ///
    /// <para>
    /// It used to deposit Toman and MAUA and nothing else, which is why registration's three
    /// default wallets were all anyone ever had. A symbol is only exercised if the customers can
    /// pay for it in both directions — a buy is settled from the quote currency, a sell from the
    /// base asset — so the funding follows whatever is being traded rather than naming an asset.
    /// </para>
    /// </summary>
    private async Task FundWalletsAsync(List<VirtualUser> users, IReadOnlyList<SymbolPlan> plans, decimal multiplier = 1m)
    {
        // A quote currency has to cover a buy on every symbol priced in it, so it is funded for
        // that whole group; each base asset only has to cover its own symbol's sells. Grouped
        // rather than assuming toman — every pair is quoted in IRT today, but the funding follows
        // the pair here exactly as it does on the base side.
        var quoteFunding = plans
            .GroupBy(p => p.Pair.QuoteAsset, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => CurrenciesConstant.RoundToCurrencyPrecision(
                    group.Sum(p => p.MaxTradeQuantity * p.ReferenceUnitPrice) * FundedTradesPerUser * multiplier,
                    group.Key),
                StringComparer.OrdinalIgnoreCase);

        foreach (var user in users)
        {
            try
            {
                foreach (var (asset, amount) in quoteFunding)
                {
                    await walletApi.DepositeAsync(new WalletRequest
                    {
                        UserId = user.UserId!.Value,
                        Asset = asset,
                        Amount = amount,
                    });
                }

                foreach (var plan in plans)
                {
                    var baseFunding = CurrenciesConstant.RoundToCurrencyPrecision(
                        plan.MaxTradeQuantity * FundedTradesPerUser * multiplier, plan.BaseAsset);

                    await walletApi.DepositeAsync(new WalletRequest
                    {
                        UserId = user.UserId!.Value,
                        Asset = plan.BaseAsset,
                        Amount = baseFunding,
                    });

                    // Credit as well as balance: credit is what the trade path actually checks
                    // (ValidateCreditAndBalanceAsync), and it is cross-asset — a customer's
                    // CREDIT_MAUA legitimately backs an IRT position — so a symbol funded with
                    // balance alone is a symbol whose credit ledger this run never touches.
                    await walletApi.DepositeAsync(new WalletRequest
                    {
                        UserId = user.UserId!.Value,
                        Asset = plan.CreditAsset,
                        Amount = baseFunding,
                    });
                }
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

    private async Task PublishQuotesAsync(VirtualUser admin, IReadOnlyList<SymbolPlan> plans, int count, Random random)
    {
        if (plans.Count == 0)
        {
            RecordError("publish quotes", new InvalidOperationException("No trading pairs to quote."));
            return;
        }

        // Each symbol's mid walks on its own, from the price already published for it. A shared
        // absolute step is what a single-symbol run could get away with: ±50,000 toman is a
        // plausible move on a mesghal of gold and is lost in the rounding of a Bitcoin price.
        var mids = plans.ToDictionary(p => p.Symbol, p => p.ReferenceUnitPrice);

        // At least one quote per symbol whatever the caller asked for: a symbol without one
        // refuses every fill, so a small run would otherwise trade fewer symbols than it planned.
        var total = Math.Max(count, plans.Count);

        for (var i = 0; i < total; i++)
        {
            var plan = plans[i % plans.Count];

            var mid = mids[plan.Symbol] * (1m + (decimal)((random.NextDouble() - 0.5) * 2 * QuoteWalkFraction));
            mids[plan.Symbol] = mid;

            // Prices are typed the way an admin types them — per mesghal for gold, per traded
            // unit for everything else — and the bot converts. The command only accepts whole
            // numbers, which every symbol this platform quotes is comfortably above.
            var typed = ToTypedPrice(plan.Symbol, mid);
            var spread = typed * QuoteSpreadFraction;
            var buy = (long)Math.Round(typed - spread / 2, 0);
            var sell = (long)Math.Round(typed + spread / 2, 0);

            try
            {
                if (plan.QuoteKeyword is { } keyword)
                {
                    var command = keyword.Length == 0 ? $"{buy}-{sell}" : $"{buy}-{sell} {keyword}";
                    await botHandler.HandleMessageAsync(NewMessage(admin, command));
                }
                else
                {
                    // No keyword can name this pair, so no admin could quote it from the bot
                    // either. Publishing it directly keeps the run's coverage complete and leaves
                    // the gap visible in the log rather than as a symbol with no trades.
                    logger.LogWarning(
                        "{Symbol} has no alias, so it cannot be quoted through the admin command; publishing it directly.",
                        plan.Symbol);

                    await orderApi.PublishQuoteAsync(plan.Symbol, buy, sell, admin.UserId!.Value);
                }
            }
            catch (Exception ex)
            {
                RecordError($"publish quote #{i} for {plan.Symbol}", ex);
            }
        }
    }

    /// <summary>
    /// The price an admin types for a symbol, given the price the system stores for it. Gold is
    /// the one symbol with two units — typed per mesghal and stored per gram, converted by
    /// <c>QuoteMessage.Prepare</c> — so anchoring on a published quote has to undo that.
    /// </summary>
    private static decimal ToTypedPrice(string symbol, decimal unitPrice) =>
        symbol == CurrenciesConstant.MAUA_IRT ? unitPrice * CurrenciesConstant.GramsPerMesghal : unitPrice;

    // ── Phase 7: trade volume via quote acceptance (direct API — see class remarks) ───────

    private async Task<(int Attempted, Dictionary<string, int> SettledBySymbol)> GenerateTradesAsync(
        List<VirtualUser> users, IReadOnlyList<SymbolPlan> plans, int targetCount, Random random)
    {
        var settledBySymbol = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (users.Count == 0)
        {
            RecordError("generate trades", new InvalidOperationException("No approved users to trade with."));
            return (0, settledBySymbol);
        }

        if (plans.Count == 0)
        {
            RecordError("generate trades", new InvalidOperationException("No quoted symbols to trade."));
            return (0, settledBySymbol);
        }

        var done = 0;
        for (var i = 0; i < targetCount; i++)
        {
            // The symbol cycles rather than being drawn at random, so coverage is a property of
            // the run instead of a probability: any run of at least as many trades as there are
            // symbols touches every one of them, down to the driver's ten-trade smoke default.
            var plan = plans[i % plans.Count];

            var user = users[random.Next(users.Count)];
            var side = random.Next(2) == 0 ? OrderSide.Buy : OrderSide.Sell;
            var quantity = plan.RandomQuantity(random);

            try
            {
                var (success, message) = await orderApi.AcceptQuoteAsync(user.UserId!.Value, plan.Symbol, side, quantity);
                if (success)
                {
                    settledBySymbol[plan.Symbol] = settledBySymbol.GetValueOrDefault(plan.Symbol) + 1;
                }
                else
                {
                    RecordError($"trade #{i} ({plan.Symbol}) for {user.TelegramId}", new InvalidOperationException(message));
                }
                done++;
            }
            catch (Exception ex)
            {
                RecordError($"trade #{i} ({plan.Symbol}) for {user.TelegramId}", ex);
            }
        }

        return (done, settledBySymbol);
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
