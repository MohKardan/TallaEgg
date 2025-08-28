using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    public enum OrderTypeEnum
    {
        [Description("سفارش محدود")]
        Limit,
        
        [Description("سفارش بازار")]
        Market
    }
}
