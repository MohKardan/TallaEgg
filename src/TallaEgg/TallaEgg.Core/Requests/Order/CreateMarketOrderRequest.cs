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

}
