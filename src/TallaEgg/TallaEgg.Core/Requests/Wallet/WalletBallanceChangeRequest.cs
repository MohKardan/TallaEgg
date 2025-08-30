using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.Requests.Wallet
{
    public class WalletBallanceChangeRequest: BaseWalletRequest
    {
        public string? ReferenceId { get; set; }
    }
}
