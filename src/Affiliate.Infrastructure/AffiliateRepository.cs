using Microsoft.EntityFrameworkCore;
using Affiliate.Core;

namespace Affiliate.Infrastructure;

public class AffiliateRepository : IAffiliateRepository
{
    private readonly AffiliateDbContext _context;

    public AffiliateRepository(AffiliateDbContext context)
    {
        _context = context;
    }

    public async Task<Invitation?> GetInvitationByCodeAsync(string code)
    {
        return await _context.Invitations
            .FirstOrDefaultAsync(i => i.Code == code && i.IsActive);
    }

    public async Task<Invitation> CreateInvitationAsync(Invitation invitation)
    {
        _context.Invitations.Add(invitation);
        await _context.SaveChangesAsync();
        return invitation;
    }

    public async Task<Invitation> UpdateInvitationAsync(Invitation invitation)
    {
        _context.Invitations.Update(invitation);
        await _context.SaveChangesAsync();
        return invitation;
    }

    public async Task<IEnumerable<Invitation>> GetInvitationsByUserIdAsync(Guid userId)
    {
        return await _context.Invitations
            .Where(i => i.CreatedByUserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
    }

    public async Task<InvitationUsage> CreateInvitationUsageAsync(InvitationUsage usage)
    {
        _context.InvitationUsages.Add(usage);
        await _context.SaveChangesAsync();
        return usage;
    }

    public async Task<IEnumerable<InvitationUsage>> GetInvitationUsagesAsync(Guid invitationId)
    {
        return await _context.InvitationUsages
            .Where(iu => iu.InvitationId == invitationId)
            .OrderByDescending(iu => iu.UsedAt)
            .ToListAsync();
    }
} 