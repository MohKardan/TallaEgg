using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    public enum OrderStatus
    {
        [Description("در انتظار")]
        Pending = 0,
        
        [Description("تایید شده")]
        Confirmed = 1,
        
        [Description("نیمه تکمیل")]
        Partially = 2,
        
        [Description("تکمیل شده")]
        Completed = 3,
        
        [Description("لغو شده")]
        Cancelled = 4,
        
        [Description("ناموفق")]
        Failed = 5
    }
}
