namespace TallaEgg.Core.Requests.Wallet
{
    public class WalletRequest
    {
        public Guid UserId { get; set; }
        public string Asset { get; set; }
        public decimal Amount { get; set; }
        public string? ReferenceId { get; set; }

    }
   


}
