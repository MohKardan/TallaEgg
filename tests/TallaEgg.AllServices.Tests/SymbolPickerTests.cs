using Microsoft.Extensions.Logging;
using TallaEgg.Core;
using TallaEgg.TelegramBot.Infrastructure;
using TallaEgg.TelegramBot.Infrastructure.Conversations;
using TallaEgg.AllServices.Tests.Fakes;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// The customer's symbol picker (issue #296).
///
/// <para>
/// The picker used to build from <c>tradingPairs.Take(10)</c> with no second page, so from the
/// eleventh active symbol on, a symbol was simply absent — while the admin who activated it had
/// been told «نماد X فعال شد و برای مشتریان قابل‌معامله است». Three symbols are active today, so
/// nothing shows; adding a symbol needs only a config block, so an eleventh costs no deploy.
/// </para>
///
/// <para>
/// These tests call <c>CreateSymbolButtons</c> directly rather than driving the bot, because the
/// pairs a driven bot can see come from <c>CurrenciesConstant</c>'s shared static catalog, and
/// xUnit runs test classes in parallel — registering eight extra symbols there would make
/// unrelated tests flaky. <c>CurrenciesConstantSymbolsTests</c> documents the same constraint.
/// </para>
/// </summary>
public class SymbolPickerTests
{
    private readonly CapturingLogger<BotHandler> _logger = new();
    private readonly BotHandler _handler;

    public SymbolPickerTests()
    {
        _handler = new BotHandler(
            _logger,
            botClient: null!,
            messenger: new FakeBotMessenger(),
            conversations: new InMemoryConversationStore(),
            orderApi: new FakeOrderApiClient(),
            usersApi: new FakeUsersApiClient(),
            affiliateApi: new FakeAffiliateApiClient(),
            walletApi: new StubWalletApiClient(),
            versionService: new FakeVersionService());
    }

    /// <summary>
    /// Realistic shapes: a short Latin pair symbol and a Persian display name, the way every
    /// entry in <c>DefaultTradingPairs</c> is built.
    /// </summary>
    private static List<TradingPairInfo> Pairs(int count) =>
        Enumerable.Range(1, count)
            .Select(i => new TradingPairInfo
            {
                Symbol = $"SYM{i:D2}/IRT",
                BaseAsset = $"SYM{i:D2}",
                QuoteAsset = CurrenciesConstant.Toman,
                PersianName = $"نماد {i}/تومان"
            })
            .ToList();

    /// <summary>
    /// The acceptance of #296: with eleven active symbols, every one is reachable.
    /// </summary>
    [Fact]
    public void CreateSymbolButtons_WithElevenPairs_OffersEveryOne()
    {
        var buttons = _handler.CreateSymbolButtons(Pairs(11));

        Assert.Equal(11, buttons.Count);

        // By name, not by count alone: dropping the eleventh and duplicating the first would
        // keep the count right and still be the defect.
        var labels = buttons.SelectMany(row => row).Select(b => b.Text).ToList();
        Assert.Equal(Pairs(11).Select(p => p.PersianName), labels);
    }

    /// <summary>
    /// Far past the old limit, to show nothing else caps it on the way.
    /// </summary>
    [Fact]
    public void CreateSymbolButtons_WithMorePairsThanTheOldLimit_DropsNone()
    {
        var buttons = _handler.CreateSymbolButtons(Pairs(30));

        Assert.Equal(30, buttons.Count);
    }

    /// <summary>
    /// A safety rail remains, because a keyboard Telegram refuses outright leaves the customer
    /// with no picker at all — worse than a missing symbol.
    /// </summary>
    [Fact]
    public void CreateSymbolButtons_PastTheSafetyCeiling_StopsAtTheCeiling()
    {
        var buttons = _handler.CreateSymbolButtons(Pairs(60));

        Assert.Equal(BotHandler.MaxSymbolButtons, buttons.Count);
    }

    /// <summary>
    /// And crossing it must be loud. It used to be <c>LogInformation</c>, which is the level
    /// nobody greps for, and it was the only sign anywhere that symbols had gone missing.
    /// </summary>
    [Fact]
    public void CreateSymbolButtons_PastTheSafetyCeiling_WarnsRatherThanInforms()
    {
        _handler.CreateSymbolButtons(Pairs(60));

        Assert.Contains(_logger.Entries, e => e.Level == LogLevel.Warning && e.Message.Contains("60"));
    }

    /// <summary>
    /// Nothing is logged when every symbol fits, or the warning would mean nothing.
    /// </summary>
    [Fact]
    public void CreateSymbolButtons_WhenEverySymbolFits_WarnsAboutNothing()
    {
        _handler.CreateSymbolButtons(Pairs(11));

        Assert.DoesNotContain(_logger.Entries, e => e.Level == LogLevel.Warning);
    }

    /// <summary>
    /// Telegram caps callback data at 64 bytes, so a pair whose symbol is long enough to breach
    /// it cannot get a working button and is skipped. That behaviour predates this change and is
    /// pinned here because the next test depends on it.
    /// </summary>
    [Fact]
    public void CreateSymbolButtons_SkipsAPairWhoseCallbackDataExceedsTelegramsLimit()
    {
        var pairs = Pairs(3);
        pairs[1].Symbol = new string('X', 70) + "/IRT";

        var buttons = _handler.CreateSymbolButtons(pairs);

        Assert.Equal(2, buttons.Count);
        Assert.DoesNotContain(buttons.SelectMany(r => r), b => b.Text == pairs[1].PersianName);
    }

    /// <summary>
    /// The count in the log must be the number of buttons actually built, not the ceiling.
    ///
    /// <para>
    /// It used to log <c>maxButtonsPerPage</c> — the constant, always 10 — as the number shown,
    /// so the one record of a symbol going missing misreported how many had survived whenever a
    /// pair was skipped for an over-long callback. A log that is the only witness has to be right.
    /// </para>
    /// </summary>
    [Fact]
    public void CreateSymbolButtons_WhenAPairIsSkipped_TheWarningCountsTheButtonsItActuallyBuilt()
    {
        var pairs = Pairs(60);
        pairs[0].Symbol = new string('X', 70) + "/IRT";   // skipped: callback data too long

        var buttons = _handler.CreateSymbolButtons(pairs);

        // The skipped pair logs a warning of its own, so the summary is picked out by name
        // rather than by being the only warning.
        var summary = Assert.Single(_logger.Entries,
            e => e.Level == LogLevel.Warning && e.Message.Contains("Symbol picker"));
        Assert.Contains(buttons.Count.ToString(), summary.Message);
    }
}
