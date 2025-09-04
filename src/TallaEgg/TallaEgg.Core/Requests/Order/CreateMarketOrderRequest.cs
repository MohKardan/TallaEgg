using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TallaEgg.Core.Enums.Order;

namespace TallaEgg.Core.Requests.Order
{

    public class CreateMarketOrderRequest
    {
        public string Asset { get; set; } = "";
        public decimal Amount { get; set; }
        public Guid UserId { get; set; }
        public OrderSide Type { get; set; }
        public TradingType TradingType { get; set; }
        public string? Notes { get; set; }
    }

//    /// <summary>
//    /// Request model for creating a new market order
//    /// </summary>
//    public record CreateMarketOrderRequest(
//    /// <summary>
//    /// Trading asset symbol (e.g., BTC, ETH, USDT)
//    /// </summary>
//    string Asset,
//    /// <summary>
//    /// Order quantity/amount
//    /// </summary>
//    decimal Amount,
//    /// <summary>
//    /// Unique identifier of the user placing the order
//    /// </summary>
//    Guid UserId,
//    /// <summary>
//    /// Side of order (Buy or Sell)
//    /// </summary>
//    OrderSide Side,
//    /// <summary>
//    /// Trading type (Spot or Futures)
//    /// </summary>
//    TradingType TradingType,
//    /// <summary>
//    /// Optional notes for the order
//    /// </summary>
//    string? Notes = null);
}
