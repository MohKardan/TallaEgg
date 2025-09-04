using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    public enum OrderSide
    {
        [Description("خرید")]
        Buy = 0,
        
        [Description("فروش")]
        Sell = 1
    }
}
