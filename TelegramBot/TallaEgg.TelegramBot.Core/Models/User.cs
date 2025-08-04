namespace TallaEgg.TelegramBot.Core.Models;

public class User
{
    public Guid Id { get; set; }
    public long TelegramId { get; set; }
    public string? PhoneNumber { get; set; }
    public string? Username { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public Guid? InvitedByUserId { get; set; }
    public string? InvitationCode { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastActiveAt { get; set; }
    public bool IsActive { get; set; } = true;
    public UserStatus Status { get; set; } = UserStatus.Pending;
    public UserRole Role { get; set; } = UserRole.User;
    public string? InvitationCodeGenerated { get; set; }
}

public enum UserStatus
{
    Pending,
    Active,
    Blocked
}

public enum UserRole
{
    User,
    Admin,
    Root
} 