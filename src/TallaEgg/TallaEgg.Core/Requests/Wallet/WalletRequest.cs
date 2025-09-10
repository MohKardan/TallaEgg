using TallaEgg.Core.Enums.Wallet;

namespace TallaEgg.Core.Requests.Wallet
{
    public class WalletRequest
    {
        public Guid UserId { get; set; }
        public string Asset { get; set; }
        public decimal Amount { get; set; }
        public string? ReferenceId { get; set; }
        public WalletType WalletType { get; set; }
    }
   


}
