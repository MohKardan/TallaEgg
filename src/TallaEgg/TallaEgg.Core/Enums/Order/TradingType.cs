using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    public enum TradingType
    {
        [Description("معاملات نقدی")]
        Spot,
        
        [Description("معاملات آتی")]
        Futures
    }
}
