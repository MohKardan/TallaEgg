using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.DTOs.Wallet
{
    public class WalletBallanceDTO
    {
        public string Asset { get; set; } = "";
        public decimal BalanceBefore { get; set; }
        public decimal LockedBalance { get; set; } = 0; // For pending orders
        public DateTime UpdatedAt { get; set; }
        public decimal BalanceAfter { get; set; }
        public string TrackingCode { get; set; }
    }

  


}
