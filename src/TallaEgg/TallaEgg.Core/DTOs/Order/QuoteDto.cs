using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.DTOs.Order
{
    // نگاشت از موجودیت Quote عمداً اینجا نیست: این DTO بین سرویس‌ها مشترک است و
    // TallaEgg.Core نباید به مدل دامنهٔ سرویس سفارش‌ها وابسته شود. نگاشت در همان لایه‌ای
    // انجام می‌شود که هر دو را می‌شناسد.

    /// <summary>
    /// مظنهٔ منتشرشدهٔ ادمین، برای انتقال بین سرویس‌ها و ربات.
    ///
    /// قیمت‌ها بر حسب واحد پایه‌اند (تومان بر گرم). تبدیل به مثقال فقط در لایهٔ ربات و
    /// هنگام نمایش انجام می‌شود — همان‌جا که کاربر عدد را وارد و مشاهده می‌کند. اگر تبدیل
    /// در چند لایه پخش شود، همان ابهامی ساخته می‌شود که پیش‌تر باعث شد معلوم نباشد یک عدد
    /// «بر گرم» است یا «بر مثقال».
    /// </summary>
    public class QuoteDto
    {
        public Guid Id { get; set; }
        public string Symbol { get; set; } = string.Empty;

        /// <summary>قیمتی که ادمین می‌خرد — مشتری با این قیمت می‌فروشد.</summary>
        public decimal BuyPrice { get; set; }

        /// <summary>قیمتی که ادمین می‌فروشد — مشتری با این قیمت می‌خرد.</summary>
        public decimal SellPrice { get; set; }

        public DateTime PublishedAt { get; set; }
    }

    /// <summary>درخواست انتشار مظنه توسط ادمین. قیمت‌ها بر حسب واحد پایه (تومان بر گرم).</summary>
    public record PublishQuoteRequest(
        string Symbol,
        decimal BuyPrice,
        decimal SellPrice,
        Guid PublishedByUserId);

    /// <summary>
    /// درخواست پذیرش مظنه توسط مشتری.
    ///
    /// عمداً قیمت ندارد: قیمت از مظنهٔ منتشرشده خوانده می‌شود. اگر مشتری قیمت می‌فرستاد،
    /// باید بررسی می‌شد که با مظنه بخواند — یک قاعدهٔ اضافی که راهی برای اشتباه باز می‌کرد.
    /// </summary>
    public record AcceptQuoteRequest(
        Guid UserId,
        string Symbol,
        OrderSide Side,
        decimal Quantity);
}
