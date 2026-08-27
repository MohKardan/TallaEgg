namespace TallaEgg.Core.Enums.User
{
    public enum UserRole
    {
        RegularUser = 0,    // کاربر معمولی
        Accountant = 1,     // حسابدار
        Admin = 2,          // مدیر
        SuperAdmin = 3,      // مدیر ارشد
        /// <summary>
        /// TODO: unclear how this differs from a regular user.
        /// </summary>
        User
    }
}
