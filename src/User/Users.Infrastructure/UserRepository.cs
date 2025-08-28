using Affiliate.Core;
using Microsoft.EntityFrameworkCore;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
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

    public async Task<PagedResult<UserDto>> GetAllAsync(string? q, int page, int size)
    {
        var query = _context.Users
           .OrderByDescending(o => o.CreatedAt)
           .Select(o => new UserDto
           {
               Id = o.Id,
              FirstName = o.FirstName,
              LastName = o.LastName,
              PhoneNumber = o.PhoneNumber,
              Status = o.Status,
              TelegramId = o.TelegramId,
              Username = o.Username,
              LastActiveAt = o.LastActiveAt

           });
        if (!string.IsNullOrEmpty(q))
        {
            query = query.Where(u =>
            u.FirstName.Contains(q) ||
            u.LastName.Contains(q) ||
            u.PhoneNumber.Contains(q)
            );
        }
        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync();

        return new PagedResult<UserDto>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = page,
            PageSize = size
        };
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

    public async Task<Guid?> GetUserIdByInvitationCodeAsync(string invitationCode)
    {
        return await _context.Users.Where(u => u.InvitationCode == invitationCode).Select(u => u.Id)
            .FirstOrDefaultAsync();
    }

    public async Task<Guid?> GetUserIdByPhonenumberAsync(string phoneNumber)
    {
        return await _context.Users.Where(u => u.PhoneNumber == phoneNumber).Select(u => u.Id)
            .FirstOrDefaultAsync();
    }

    Task<Invitation?> IUserRepository.GetInvitationByCodeAsync(string invitationCode)
    {
        throw new NotImplementedException();
    }
} 