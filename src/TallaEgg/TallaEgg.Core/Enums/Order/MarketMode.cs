using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    /// <summary>
    /// How the market operates for a symbol.
    ///
    /// Both modes end in the same <c>Trade</c> and go through the same settlement path, so they can
    /// coexist: one symbol in Dealer mode and another in OrderBook mode. History and reporting are
    /// identical for both, because the underlying data is the same.
    /// </summary>
    public enum MarketMode
    {
        /// <summary>
        /// Only the admin quotes, and customers trade against that quote; customers never trade with
        /// each other. This is the current business model: the gold shop names a price and its
        /// customers deal with the shop.
        /// </summary>
        [Description("مظنه‌ای")]
        Dealer = 0,

        /// <summary>
        /// A peer-to-peer order book: customers place orders and match against each other. This mode
        /// becomes useful once there is real liquidity.
        /// </summary>
        [Description("دفتر سفارش")]
        OrderBook = 1
    }
}
