using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Order;

namespace TallaEgg.TelegramBot.Infrastructure.Clients;

public interface IOrderApiClient
{
    
    Task<ApiResponse<PagedResult<OrderHistoryDto>>> GetUserOrdersAsync(Guid userId, int pageNumber = 1, int pageSize = 10);
    Task<ApiResponse<PagedResult<TradeHistoryDto>>> GetUserTradesAsync(Guid userId, int pageNumber = 1, int pageSize = 10);
    Task<ApiResponse<List<OrderHistoryDto>>> GetUserActiveOrdersAsync(Guid userId);
    Task<ApiResponse<List<OrderHistoryDto>>> GetAllActiveOrdersAsync();
    Task<(bool success, string message)> SubmitOrderAsync(OrderDto order);
    Task<(bool success, string message)> CancelOrderAsync(Guid orderId);

    // ── مدل مظنه‌ای (issue #48) ──────────────────────────────────────────────────
    // قیمت‌ها بر حسب واحد پایه (تومان بر گرم) رد و بدل می‌شوند؛ تبدیل مثقال فقط در لایهٔ
    // نمایش ربات انجام می‌شود.

    /// <summary>انتشار مظنهٔ ادمین. هیچ سفارشی در دفتر نمی‌گذارد و وثیقه‌ای قفل نمی‌کند.</summary>
    Task<(bool success, string message)> PublishQuoteAsync(
        string symbol, decimal buyPrice, decimal sellPrice, Guid publishedByUserId);

    /// <summary>مظنهٔ فعال یک نماد، یا null اگر منتشر نشده باشد.</summary>
    Task<QuoteDto?> GetActiveQuoteAsync(string symbol);
    /// <summary>Published quotes for a symbol, newest first, including replaced ones.</summary>
    Task<PagedResult<QuoteDto>> GetQuoteHistoryAsync(string symbol, int pageNumber = 1, int pageSize = 5);

    /// <summary>Best bid and ask, used by the market-order path.</summary>
    Task<ApiResponse<BestPricesDto>> GetBestPricesAsync(string symbol);

    /// <summary>پذیرش مظنه توسط مشتری. قیمت فرستاده نمی‌شود؛ سرور از مظنه می‌خواند.</summary>
    Task<(bool success, string message)> AcceptQuoteAsync(
        Guid userId, string symbol, OrderSide side, decimal quantity);
    
    /// <summary>
    /// کنسل کردن تمام سفارشات فعال یک کاربر
    /// </summary>
    /// <param name="userId">شناسه کاربر</param>
    /// <param name="reason">دلیل کنسل کردن (اختیاری)</param>
    /// <returns>نتیجه عملیات شامل موفقیت، پیام و تعداد سفارشات کنسل شده</returns>
    Task<(bool success, string message, int cancelledCount)> CancelAllUserActiveOrdersAsync(Guid userId, string? reason = null);
    
    Task<ApiResponse<bool>> NotifyMatchingEngineAsync(NotifyMatchingEngineRequest request);

    // ── مظنهٔ اتومات (issue #90) ─────────────────────────────────────────────────

    /// <summary>تنظیمات فعلی مظنهٔ اتومات یک نماد.</summary>
    Task<AutoQuoteSettingsDto?> GetAutoQuoteSettingsAsync(string symbol);

    /// <summary>تغییر اسپرد مظنهٔ اتومات یک نماد.</summary>
    Task<(bool success, string message)> UpdateAutoQuoteSpreadAsync(string symbol, decimal spreadPercent, Guid updatedByUserId);

    /// <summary>روشن/خاموش کردن مظنهٔ اتومات یک نماد.</summary>
    Task<(bool success, string message)> SetAutoQuoteEnabledAsync(string symbol, bool isEnabled, Guid updatedByUserId);

    // ── فعال/غیرفعال بودن نماد ───────────────────────────────────────────────────

    /// <summary>نمادهایی که الان قابل معامله‌اند.</summary>
    Task<List<string>> GetActiveSymbolsAsync();

    /// <summary>فعال/غیرفعال کردن یک نماد.</summary>
    Task<(bool success, string message)> SetSymbolActiveAsync(string symbol, bool isActive, Guid updatedByUserId);

    // ── سود و زیان (issue #93) ───────────────────────────────────────────────────

    /// <summary>موقعیت و سود/زیان کاربر در همهٔ نمادهایی که تا امروز معامله کرده.</summary>
    Task<ApiResponse<PositionsResponseDto>> GetPositionsAsync(Guid userId);
}