using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using TallaEgg.Core.Enums.Order;

namespace Wallet.Tests;

/// <summary>
/// حالت بازار هر نماد (issue #48).
///
/// نکتهٔ اصلی این تست‌ها سازگاری با گذشته است: تنظیم
/// <c>Matching:RequireMarketMakerCounterparty</c> از قبل وجود داشت و دقیقاً همین قاعده را
/// بیان می‌کرد. اگر <c>MarketMode</c> آن را نادیده می‌گرفت، دو تعریف موازی برای یک قاعده
/// داشتیم — همان الگویی که در این کدبیس بارها به باگ ختم شده.
/// </summary>
public class MarketModeTests
{
    private const string Symbol = "MAUA/IRT";
    private static readonly Guid MarketMaker = Guid.Parse("ace3a6e8-b507-45ca-9fcf-46b3810b0c1e");

    private static MarketModeProvider Provider(params (string Key, string Value)[] settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings.ToDictionary(s => s.Key, s => (string?)s.Value))
            .Build();

        return new MarketModeProvider(configuration, NullLogger<MarketModeProvider>.Instance);
    }

    /// <summary>بدون هیچ تنظیمی، رفتار تاریخی سیستم حفظ می‌شود: دفتر سفارش.</summary>
    [Fact]
    public void WithNoConfiguration_TheModeIsOrderBook()
    {
        Assert.Equal(MarketMode.OrderBook, Provider().GetMode(Symbol));
    }

    /// <summary>
    /// تنظیم قدیمی باید همچنان کار کند. اگر کسی آن را روشن کرده بود، نباید با این تغییر
    /// بی‌صدا خاموش شود.
    /// </summary>
    [Fact]
    public void TheLegacyMarketMakerSettingStillMeansDealerMode()
    {
        var provider = Provider(("Matching:RequireMarketMakerCounterparty", "true"));

        Assert.Equal(MarketMode.Dealer, provider.GetMode(Symbol));
    }

    /// <summary>تنظیم مخصوص یک نماد بر تنظیم سراسری اولویت دارد.</summary>
    [Fact]
    public void APerSymbolSettingOverridesTheGlobalOne()
    {
        var provider = Provider(
            ("Matching:RequireMarketMakerCounterparty", "true"),
            ($"Matching:MarketModes:{Symbol}", "OrderBook"));

        Assert.Equal(MarketMode.OrderBook, provider.GetMode(Symbol));
    }

    /// <summary>
    /// دو نماد می‌توانند همزمان در دو حالت باشند — همان چیزی که «همزیستی» در issue #48
    /// یعنی: یک نماد مظنه‌ای بماند و نماد دیگری با نقدینگی واقعی به دفتر سفارش برود.
    /// </summary>
    [Fact]
    public void TwoSymbolsCanRunInDifferentModes()
    {
        var provider = Provider(
            ("Matching:MarketModes:MAUA/IRT", "Dealer"),
            ("Matching:MarketModes:BTC/IRT", "OrderBook"));

        Assert.Equal(MarketMode.Dealer, provider.GetMode("MAUA/IRT"));
        Assert.Equal(MarketMode.OrderBook, provider.GetMode("BTC/IRT"));
    }

    /// <summary>مقدار نامعتبر نباید باعث استثنا شود؛ به رفتار پیش‌فرض برمی‌گردیم.</summary>
    [Fact]
    public void AnUnrecognisedModeFallsBackInsteadOfThrowing()
    {
        var provider = Provider(($"Matching:MarketModes:{Symbol}", "چیز نامعتبر"));

        Assert.Equal(MarketMode.OrderBook, provider.GetMode(Symbol));
    }

    // ── تشخیص بازار مظنه‌ای ─────────────────────────────────────────────────────

    [Fact]
    public void DealerModeWithAMarketMakerIsDetected()
    {
        var provider = Provider(
            ($"Matching:MarketModes:{Symbol}", "Dealer"),
            ("Matching:MarketMakerUserId", MarketMaker.ToString()));

        Assert.True(provider.IsDealerMarket(Symbol, out var id));
        Assert.Equal(MarketMaker, id);
    }

    /// <summary>
    /// حالت Dealer بدون تعیین بازارگردان یک اشتباه پیکربندی است: معلوم نیست طرف مقابلِ
    /// مشتری کیست. false برمی‌گردد (و خطا لاگ می‌شود) تا تطبیق به‌کلی متوقف نشود، ولی
    /// قاعده هم بی‌صدا اعمال نگردد.
    /// </summary>
    [Fact]
    public void DealerModeWithoutAMarketMakerIsNotTreatedAsDealer()
    {
        var provider = Provider(($"Matching:MarketModes:{Symbol}", "Dealer"));

        Assert.False(provider.IsDealerMarket(Symbol, out var id));
        Assert.Equal(Guid.Empty, id);
    }

    [Fact]
    public void OrderBookModeIsNotADealerMarket()
    {
        var provider = Provider(
            ($"Matching:MarketModes:{Symbol}", "OrderBook"),
            ("Matching:MarketMakerUserId", MarketMaker.ToString()));

        Assert.False(provider.IsDealerMarket(Symbol, out _));
    }
}
