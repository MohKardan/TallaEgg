namespace TallaEgg.Api.Modules.Orders.Application;

public interface IAuthorizationService
{
    Task<bool> CanCreateOrderAsync(Guid userId);
    Task<bool> CanManageUsersAsync(Guid userId);
}

