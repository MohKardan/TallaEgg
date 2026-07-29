namespace Orders.Core;

public interface IQuoteRepository
{
    /// <summary>مظنهٔ فعال یک نماد، یا null اگر ادمین هنوز قیمتی منتشر نکرده باشد.</summary>
    Task<Quote?> GetActiveAsync(string symbol);

    /// <summary>
    /// مظنهٔ جدید را منتشر و مظنهٔ قبلیِ همان نماد را غیرفعال می‌کند — هر دو در یک تراکنش.
    ///
    /// اتمی بودن مهم است: اگر غیرفعال‌سازی و درج جدا انجام شوند، لحظه‌ای وجود دارد که یا
    /// دو مظنهٔ فعال هست یا هیچ‌کدام. در حالت اول معلوم نیست مشتری روی کدام قیمت معامله
    /// می‌کند؛ در حالت دوم معاملهٔ کاملاً معتبری رد می‌شود.
    /// </summary>
    Task<Quote> PublishAsync(Quote quote);
}
