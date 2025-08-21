using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    public enum OrderRole
    {
        [Description("ایجاد کننده سفارش")]
        Maker,
        
        [Description("پذیرنده سفارش")]
        Taker
    }
}
