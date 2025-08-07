namespace Users.Core;

public class User
{
    public Guid Id { get; set; }
    public long TelegramId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public bool IsActive { get; set; } = true;
    public UserStatus Status { get; set; } = UserStatus.Pending;
    public UserRole Role { get; set; } = UserRole.RegularUser; // نقش کاربر
    public string? InvitationCode { get; set; } // کد دعوت
}

public enum UserStatus
{
    Pending,    // منتظر تایید
    Active,     // فعال
    Suspended,  // معلق
    Blocked     // مسدود
}

public enum UserRole
{
    RegularUser,    // کاربر معمولی
    Accountant,     // حسابدار
    Admin,          // مدیر
    SuperAdmin,      // مدیر ارشد
    User
} 