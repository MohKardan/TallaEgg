using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using TallaEgg.Core.Enums.Order;

namespace Orders.Application.Services;

/// <summary>
/// می‌گوید هر نماد در کدام حالت بازار کار می‌کند و بازارگردانش کیست.
///
/// <para>
/// <b>چرا تنظیمات و نه جدول:</b> این مقدار به‌ندرت عوض می‌شود — وقتی نقدینگی واقعی برای
/// یک نماد به وجود بیاید. یک جدول، رابط ادمین و migration می‌خواست بدون اینکه چیزی اضافه
/// کند. مقدارها در سازنده کش نمی‌شوند تا تغییر فایل تنظیمات نیاز به ری‌استارت نداشته باشد.
/// </para>
///
/// <para>
/// <b>ساخته‌شده روی چیزی که از قبل بود:</b> تنظیم
/// <c>Matching:RequireMarketMakerCounterparty</c> از قبل دقیقاً همین قاعده را بیان می‌کرد
/// («مشتری فقط با بازارگردان معامله کند»). به‌جای ساختن یک مفهوم موازی، همان به‌عنوان
/// پیش‌فرض سراسری خوانده می‌شود و <c>Matching:MarketModes:{نماد}</c> می‌تواند برای هر نماد
/// بازنویسی‌اش کند. دو تعریف موازی برای یک قاعده، همان اشتباهی است که چند بار در این
/// کدبیس دیده‌ایم.
/// </para>
/// </summary>
public class MarketModeProvider
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<MarketModeProvider> _logger;

    public MarketModeProvider(IConfiguration configuration, ILogger<MarketModeProvider> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// حالت بازار برای یک نماد. ترتیب: تنظیم مخصوص همان نماد، بعد تنظیم قدیمی
    /// <c>RequireMarketMakerCounterparty</c>، و در نهایت OrderBook (رفتار تاریخی سیستم).
    /// </summary>
    public MarketMode GetMode(string symbol)
    {
        var perSymbol = _configuration[$"Matching:MarketModes:{symbol}"];
        if (Enum.TryParse<MarketMode>(perSymbol, ignoreCase: true, out var mode))
            return mode;

        return _configuration.GetValue("Matching:RequireMarketMakerCounterparty", defaultValue: false)
            ? MarketMode.Dealer
            : MarketMode.OrderBook;
    }

    /// <summary>شناسهٔ کاربر بازارگردان، یا null اگر تنظیم نشده باشد.</summary>
    public Guid? GetMarketMakerUserId()
    {
        var raw = _configuration["Matching:MarketMakerUserId"];
        return Guid.TryParse(raw, out var parsed) ? parsed : null;
    }

    /// <summary>
    /// آیا این نماد در حالت مظنه‌ای کار می‌کند و بازارگردانش مشخص است؟
    ///
    /// اگر حالت Dealer باشد ولی بازارگردان تنظیم نشده باشد، خطا لاگ می‌شود و false
    /// برمی‌گردد — تطبیق به‌کلی متوقف نمی‌شود، ولی یک اشتباه در کانفیگ بی‌صدا نمی‌ماند.
    /// </summary>
    public bool IsDealerMarket(string symbol, out Guid marketMakerUserId)
    {
        marketMakerUserId = Guid.Empty;

        if (GetMode(symbol) != MarketMode.Dealer)
            return false;

        var id = GetMarketMakerUserId();
        if (id is null)
        {
            _logger.LogError(
                "Symbol {Symbol} is in Dealer mode but Matching:MarketMakerUserId is not set. " +
                "The dealer rule will NOT be enforced.", symbol);
            return false;
        }

        marketMakerUserId = id.Value;
        return true;
    }
}
