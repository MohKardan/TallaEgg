using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    /// <summary>
    /// The order's role in the market.
    /// Order Liquidity Role
    /// </summary>
    public enum OrderRole
    {
        /// <summary>
        /// The order provides liquidity: it rests in the order book.
        /// Liquidity Provider
        /// </summary>
        [Description("تامین‌کننده نقدینگی")]
        Maker = 0,

        /// <summary>
        /// The order consumes liquidity: it executed immediately.
        /// Liquidity Consumer
        /// </summary>
        [Description("مصرف‌کننده نقدینگی")]
        Taker = 1,

        /// <summary>
        /// The order both consumed and provided liquidity.
        /// </summary>
        [Description("ترکیبی")]
        Mixed = 2
    }
}
