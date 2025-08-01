namespace Orders.Core;

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
    
    // Navigation property
    public User? InvitedBy { get; set; }
    public ICollection<User> InvitedUsers { get; set; } = new List<User>();
} 