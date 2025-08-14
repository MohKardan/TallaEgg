using Microsoft.EntityFrameworkCore;
using TallaEgg.Api.Modules.Users.Core;
using TallaEgg.Api.Modules.Affiliate.Core;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.Api.Shared.Infrastructure;

namespace TallaEgg.Api.Modules.Users.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly TallaEggDbContext _context;

    public UserRepository(TallaEggDbContext context)
    {
        _context = context;
    }

    public async Task<User?> GetByTelegramIdAsync(long telegramId)
    {
        return await _context.Users
            .FirstOrDefaultAsync(u => u.TelegramId == telegramId);
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
        return await _context.Users
            .AnyAsync(u => u.TelegramId == telegramId);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> UpdateUserRoleAsync(Guid id, UserRole role)
    {
        var user = await _context.Users.FindAsync(id);
        if (user == null)
            return null;

        user.Role = role;
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
    {
        return await _context.Users
            .Where(u => u.Role == role)
            .OrderByDescending(u => u.CreatedAt)
            .ToListAsync();
    }

    public async Task<Invitation?> GetInvitationByCodeAsync(string invitationCode)
    {
        return await _context.Invitations
            .FirstOrDefaultAsync(i => i.Code == invitationCode && i.IsActive);
    }

    public async Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode)
    {
        var invitation = await _context.Invitations
            .FirstOrDefaultAsync(i => i.Code == invitationCode && i.IsActive);
        
        return invitation?.CreatedByUserId;
    }
}
