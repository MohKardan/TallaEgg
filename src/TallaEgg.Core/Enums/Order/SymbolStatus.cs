using System.ComponentModel;

namespace TallaEgg.Core.Enums.Order
{
    public enum SymbolStatus
    {
        [Description("فعال")]
        Active,
        
        [Description("غیرفعال")]
        Inactive,
        
        [Description("معلق")]
        Suspended
    }
}
