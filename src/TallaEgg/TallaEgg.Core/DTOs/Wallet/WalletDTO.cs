using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.DTOs.Wallet
{
    public class WalletDTO
    {
        public string Asset { get; set; } = "";
        public decimal Balance { get; set; } = 0;
        public decimal LockedBalance { get; set; } = 0; // For pending orders
        public DateTime UpdatedAt { get; set; }

    }
  
}
