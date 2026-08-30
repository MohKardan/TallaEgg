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
        public string TrackingCode { get; set; } = string.Empty;

        /// <summary>
        /// True when the request carried a ReferenceId that had already been applied, so nothing
        /// moved and the figures above describe the original operation rather than this one
        /// (issue #157).
        ///
        /// <para>
        /// Callers that tell somebody money changed hands have to check it. The bot notifies the
        /// customer on every successful top-up, and a deduplicated repeat is successful — without
        /// this flag the customer would be told their credit rose again for money that never moved.
        /// </para>
        /// </summary>
        public bool WasAlreadyApplied { get; set; }
    }

  


}
