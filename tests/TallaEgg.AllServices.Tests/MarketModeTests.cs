using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// حالت بازار هر نماد (issue #48).
///
/// <para>
/// نکتهٔ اصلی این تست‌ها سازگاری با گذشته است: تنظیم
/// <c>Matching:RequireMarketMakerCounterparty</c> از قبل وجود داشت و دقیقاً همین قاعده را
/// بیان می‌کرد. اگر <c>MarketMode</c> آن را نادیده می‌گرفت، دو تعریف موازی برای یک قاعده
/// داشتیم — همان الگویی که در این کدبیس بارها به باگ ختم شده.
/// </para>
///
/// <para>
/// <b>«بازارگردان کیست» دیگر اینجا نیست.</b> این کلاس فقط می‌گوید یک نماد در کدام حالت کار
/// می‌کند. طرف مقابلِ معامله حالا خودِ مظنه است — منتشرکننده‌اش — که در
/// <see cref="QuoteFillCounterpartyTests"/> پوشش داده می‌شود.
/// </para>
/// </summary>
public class MarketModeTests
{
    private const string Symbol = "MAUA/IRT";

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
}
