using Affiliate.Core;
using Microsoft.EntityFrameworkCore;
using Users.Core;

namespace Users.Infrastructure;

public class UserRepository : IUserRepository
{
    private readonly UsersDbContext _context;

    public UserRepository(UsersDbContext context)
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
        return await _context.Users.AnyAsync(u => u.TelegramId == telegramId);
    }

    public async Task<IEnumerable<User>> GetAllAsync()
    {
        return await _context.Users.ToListAsync();
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        return await _context.Users.FindAsync(id);
    }

    public async Task<User?> UpdateUserRoleAsync(Guid id, UserRole role)
    {
        var user = await _context.Users.FindAsync(id);
        if (user != null)
        {
            user.Role = role;
            await _context.SaveChangesAsync();
        }
        return user;
    }

    public async Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role)
    {
        return await _context.Users
            .Where(u => u.Role == role)
            .ToListAsync();
    }

    Task<Invitation?> IUserRepository.GetInvitationByCodeAsync(string invitationCode)
    {
        throw new NotImplementedException();
    }
} 