namespace TallaEgg.Core.Requests.Wallet
{
    public class BaseWalletRequest
    {
        public Guid UserId { get; set; }
        public string Asset { get; set; }
        public decimal Amount { get; set; }
    }
}
