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
        Active,     // فعال
        Suspended,  // معلق
        Blocked     // مسدود
    }
}
