namespace Affiliate.Core;

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
    public InvitationType Type { get; set; } = InvitationType.Regular;
}

public enum InvitationType
{
    Regular,    // معمولی
    Premium,    // ویژه
    VIP         // VIP
}

public class InvitationUsage
{
    public Guid Id { get; set; }
    public Guid InvitationId { get; set; }
    public Guid UsedByUserId { get; set; }
    public DateTime UsedAt { get; set; }
    public string? UserAgent { get; set; }
    public string? IpAddress { get; set; }
} 