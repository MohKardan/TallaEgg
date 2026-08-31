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

        /// <summary>
        /// What the wallet holds now, as opposed to <see cref="BalanceAfter"/>, which is what the
        /// operation being reported left behind.
        ///
        /// <para>
        /// The two are the same for an operation that just ran, and differ whenever
        /// <see cref="WasAlreadyApplied"/> is true: a repeat reports the original transaction, so its
        /// BalanceAfter is the balance at that earlier moment, and anything that happened since is
        /// not in it. Any caller writing the words "current balance" has to use this one.
        /// </para>
        ///
        /// <para>
        /// <b>Nullable so that "the wallet did not send this" stays distinguishable from "the wallet
        /// holds nothing".</b> The services are installed and restarted individually, so a bot that
        /// comes back before the wallet does will deserialize a response without this field. As a
        /// plain decimal that is silently zero, and the admin would be told the customer holds
        /// nothing — a worse answer than the stale figure this field exists to replace. Callers fall
        /// back to <see cref="BalanceAfter"/> when it is null.
        /// </para>
        /// </summary>
        public decimal? CurrentBalance { get; set; }
    }

  


}
