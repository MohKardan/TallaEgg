using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    public enum OrderStatus
    {
        [Description("در انتظار")]
        Pending,
        
        [Description("تایید شده")]
        Confirmed,
        
        [Description("لغو شده")]
        Cancelled,
        
        [Description("تکمیل شده")]
        Completed,
        
        [Description("ناموفق")]
        Failed,
        
        [Description("نیمه تکمیل")]
        Partially
    }
}
