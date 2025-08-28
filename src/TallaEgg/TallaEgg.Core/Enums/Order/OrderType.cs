using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    public enum OrderType
    {
        [Description("خرید")]
        Buy,
        
        [Description("فروش")]
        Sell
    }
}
