using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    /// <summary>
    /// Order Execution Type
    /// </summary>
    public enum OrderType
    {
        [Description("سفارش بازار")]
        Market,     // خرید یا فروش فوری با بهترین قیمت موجود
        [Description("سفارش محدود")]
        Limit,      // سفارش معلق در دفتر سفارشات با قیمت مشخص
        [Description("سفارش شرطی")]
        StopLimit,  // سفارش شرطی: وقتی قیمت به حدی رسید، سفارش Limit فعال می‌شود
        [Description("سفارش ترکیبی")]
        Oco         // One Cancels the Other (دو سفارش همزمان، اجرای یکی دیگری را لغو می‌کند)
    }
}
