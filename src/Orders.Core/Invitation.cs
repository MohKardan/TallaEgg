namespace Orders.Core;

public class Invitation
{
    public Guid Id { get; set; }
    public string Code { get; set; } = "";
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int MaxUses { get; set; } = -1; // -1 means unlimited
    public int UsedCount { get; set; } = 0;
    public bool IsActive { get; set; } = true;
    
    // Navigation property
    public User CreatedBy { get; set; } = null!;
} 