using TallaEgg.TelegramBot.Core.Interfaces;
using TallaEgg.TelegramBot.Core.Models;
using TallaEgg.TelegramBot.Infrastructure.Clients;

namespace TallaEgg.TelegramBot.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly IUsersApiClient _usersApiClient;

    public UserRepository(IUsersApiClient usersApiClient)
    {
        _usersApiClient = usersApiClient;
    }

    public async Task<User?> GetByTelegramIdAsync(long telegramId)
    {
        return await _usersApiClient.GetUserByTelegramIdAsync(telegramId);
    }

    public async Task<User?> GetByInvitationCodeAsync(string invitationCode)
    {
        // This would need to be implemented in the API
        throw new NotImplementedException();
    }

    public async Task<User> CreateAsync(User user)
    {
        var createdUser = await _usersApiClient.RegisterUserAsync(
            user.TelegramId, 
            user.Username, 
            user.FirstName, 
            user.LastName, 
            user.InvitationCode ?? "");
        
        if (createdUser == null)
            throw new InvalidOperationException("Failed to create user");
            
        return createdUser;
    }

    public async Task<User> UpdateAsync(User user)
    {
        if (user.PhoneNumber != null)
        {
            var updatedUser = await _usersApiClient.UpdateUserPhoneAsync(user.TelegramId, user.PhoneNumber);
            if (updatedUser == null)
                throw new InvalidOperationException("Failed to update user");
            return updatedUser;
        }
        
        return user;
    }

    public async Task<bool> ExistsByTelegramIdAsync(long telegramId)
    {
        var user = await GetByTelegramIdAsync(telegramId);
        return user != null;
    }

    public async Task<Invitation?> GetInvitationByCodeAsync(string code)
    {
        var (isValid, message) = await _usersApiClient.ValidateInvitationCodeAsync(code);
        if (!isValid)
            return null;
            
        // This is a simplified implementation - in a real scenario, you'd need a proper invitation API
        return new Invitation
        {
            Id = Guid.NewGuid(),
            Code = code,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public async Task<Invitation> CreateInvitationAsync(Invitation invitation)
    {
        // This would need to be implemented in the API
        throw new NotImplementedException();
    }

    public async Task<Invitation> UpdateInvitationAsync(Invitation invitation)
    {
        // This would need to be implemented in the API
        throw new NotImplementedException();
    }
} 