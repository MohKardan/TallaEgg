namespace Orders.Application;

public class AuthorizationService : IAuthorizationService
{
    public async Task<bool> CanCreateOrderAsync(Guid userId)
    {
        // منطق بررسی نقش کاربر (مثلاً فقط ادمین‌ها مجاز باشند)
        // اینجا فقط برای نمونه true برمی‌گردد
        return await Task.FromResult(true);
    }

    public async Task<bool> CanManageUsersAsync(Guid userId)
    {
        // منطق بررسی نقش کاربر برای مدیریت کاربران (مثلاً فقط ادمین‌ها)
        // اینجا فقط برای نمونه true برمی‌گردد
        return await Task.FromResult(true);
    }
}
