using Affiliate.Core;

namespace Affiliate.Application;

public static class AffiliateMessages
{
    public const string InviteNotEntered = "کد دعوت وارد نشده است.";
    public const string InviteInvalid = "کد دعوت نامعتبر است.";
    public const string InviteExpired = "کد دعوت منقضی شده است.";
    public const string InviteMaxUsed = "کد دعوت به حداکثر تعداد استفاده رسیده است.";
    public const string InviteValid = "کد دعوت معتبر است.";
}

public class AffiliateService
{
    private readonly IAffiliateRepository _affiliateRepository;

    public AffiliateService(IAffiliateRepository affiliateRepository)
    {
        _affiliateRepository = affiliateRepository;
    }

    public async Task<(bool isValid, string message, Invitation? invitation)> ValidateInvitationCodeAsync(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (false, AffiliateMessages.InviteNotEntered, null);

        var invitation = await _affiliateRepository.GetInvitationByCodeAsync(code);
        if (invitation == null)
            return (false, AffiliateMessages.InviteInvalid, null);

        if (invitation.ExpiresAt.HasValue && invitation.ExpiresAt.Value < DateTime.UtcNow)
            return (false, AffiliateMessages.InviteExpired, null);

        if (invitation.MaxUses > 0 && invitation.UsedCount >= invitation.MaxUses)
            return (false, AffiliateMessages.InviteMaxUsed, null);

        return (true, AffiliateMessages.InviteValid, invitation);
    }

    public async Task<Invitation> CreateInvitationAsync(Guid createdByUserId, InvitationType type = InvitationType.Regular, int maxUses = -1, DateTime? expiresAt = null)
    {
        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Code = GenerateInvitationCode(),
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt,
            MaxUses = maxUses,
            UsedCount = 0,
            IsActive = true,
            Type = type
        };

        return await _affiliateRepository.CreateInvitationAsync(invitation);
    }

    public async Task<Invitation> UseInvitationAsync(string code, Guid usedByUserId, string? userAgent = null, string? ipAddress = null)
    {
        var invitation = await _affiliateRepository.GetInvitationByCodeAsync(code);
        if (invitation == null)
        {
            throw new InvalidOperationException(AffiliateMessages.InviteInvalid);
        }

        // Update invitation usage count
        invitation.UsedCount++;
        await _affiliateRepository.UpdateInvitationAsync(invitation);

        // Record usage
        var usage = new InvitationUsage
        {
            Id = Guid.NewGuid(),
            InvitationId = invitation.Id,
            UsedByUserId = usedByUserId,
            UsedAt = DateTime.UtcNow,
            UserAgent = userAgent,
            IpAddress = ipAddress
        };

        await _affiliateRepository.CreateInvitationUsageAsync(usage);

        return invitation;
    }

    public async Task<IEnumerable<Invitation>> GetUserInvitationsAsync(Guid userId)
    {
        return await _affiliateRepository.GetInvitationsByUserIdAsync(userId);
    }

    private string GenerateInvitationCode()
    {
        // Generate a unique 8-character invitation code
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var random = new Random();
        var code = new string(Enumerable.Repeat(chars, 8)
            .Select(s => s[random.Next(s.Length)]).ToArray());

        return code;
    }
}