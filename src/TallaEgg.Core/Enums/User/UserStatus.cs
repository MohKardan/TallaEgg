using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TallaEgg.Core.Enums.User
{
    public enum UserStatus
    {
        Pending,    // منتظر تایید
        Approved,     // فعال
        Rejected,    // رد شده 
        Suspended,  // معلق
        Blocked     // مسدود
    }
}
