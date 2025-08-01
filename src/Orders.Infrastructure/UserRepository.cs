using Microsoft.EntityFrameworkCore;
using Orders.Core;

namespace Orders.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly OrdersDbContext _context;

    public UserRepository(OrdersDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByTelegramIdAsync(long telegramId)
    {
        return await _context.Users
            .Include(u => u.InvitedBy)
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);
    }

    public async Task<User?> GetByInvitationCodeAsync(string invitationCode)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.InvitationCode == invitationCode);
    }

    public async Task<User> CreateAsync(User user)
    {
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<User> UpdateAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<bool> ExistsByTelegramIdAsync(long telegramId)
    {
        return await _context.Users.AnyAsync(u => u.TelegramId == telegramId);
    }

    public async Task<Invitation?> GetInvitationByCodeAsync(string code)
    {
        return await _context.Invitations
            .Include(i => i.CreatedBy)
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
} 