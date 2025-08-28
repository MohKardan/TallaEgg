using System.ComponentModel;

namespace TallaEgg.Core.Enums.Wallet
{
    public enum TransactionStatus
    {
        [Description("در انتظار")]
        Pending,

        [Description("تکمیل شده")]
        Completed,

        [Description("ناموفق")]
        Failed,

        [Description("لغو شده")]
        Canceled
    }
}
