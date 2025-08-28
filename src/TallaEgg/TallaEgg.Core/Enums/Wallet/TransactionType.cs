using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.Enums.Wallet
{
    public enum TransactionType
    {
        [Description("واریز")]
        Deposit,

        [Description("برداشت")]
        Withdraw,

        [Description("معامله")]
        Trade,

        [Description("فریز کردن موجودی")]
        Freeze,

        [Description("آزادسازی موجودی")]
        Unfreeze,

        [Description("کارمزد")]
        Fee,

        [Description("انتقال")]
        Transfer,

        [Description("تعدیل")]
        Adjustment
    }
}
