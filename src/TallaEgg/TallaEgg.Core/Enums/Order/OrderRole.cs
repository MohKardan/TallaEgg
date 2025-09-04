using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    /// <summary>
    /// نقش سفارش در بازار
    /// Order Liquidity Role
    /// </summary>
    public enum OrderRole
    {
        /// <summary>
        /// سفارش نقدینگی فراهم می‌کند (در Order Book منتظر)
        /// Liquidity Provider
        /// </summary>
        [Description("تامین‌کننده نقدینگی")]
        Maker = 0,

        /// <summary>
        /// سفارش نقدینگی مصرف می‌کند (فوری اجرا شد)
        /// Liquidity Consumer
        /// </summary>
        [Description("مصرف‌کننده نقدینگی")]
        Taker = 1,

        /// <summary>
        /// سفارش هم نقدینگی مصرف و هم فراهم کرد (ترکیبی)
        /// </summary>
        [Description("ترکیبی")]
        Mixed = 2
    }
}
