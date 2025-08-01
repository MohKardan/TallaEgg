namespace Affiliate.Core;

public interface IAffiliateRepository
{
    Task<Invitation?> GetInvitationByCodeAsync(string code);
    Task<Invitation> CreateInvitationAsync(Invitation invitation);
    Task<Invitation> UpdateInvitationAsync(Invitation invitation);
    Task<IEnumerable<Invitation>> GetInvitationsByUserIdAsync(Guid userId);
    Task<InvitationUsage> CreateInvitationUsageAsync(InvitationUsage usage);
    Task<IEnumerable<InvitationUsage>> GetInvitationUsagesAsync(Guid invitationId);
} 