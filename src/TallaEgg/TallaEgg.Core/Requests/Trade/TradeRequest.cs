using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.Requests.Trade
{
    public class TradeRequest
    {
        
        public Guid FromUserId { get; set; }
        public Guid ToUserId { get; set; }
        public string Asset { get; set; }
        public decimal Amount { get; set; }
        public string ReferenceId { get; set; }

    }
}
