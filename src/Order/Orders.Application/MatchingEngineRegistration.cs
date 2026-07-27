using Microsoft.Extensions.DependencyInjection;
using Orders.Application.Services;

namespace Orders.Application;

/// <summary>
/// ثبت موتور تطبیق در DI.
///
/// این کار عمداً در یک متد مشترک قرار گرفته تا هم <c>Orders.Api</c> و هم تست‌ها
/// دقیقاً از یک مسیر استفاده کنند. اگر ثبت مستقیماً در <c>Program.cs</c> نوشته شود،
/// تست فقط یک کپی از آن را می‌سنجد و اگر روزی <c>Program.cs</c> تغییر کند تست همچنان
/// سبز می‌ماند — یعنی دقیقاً همان چیزی را که باید محافظت کند از دست می‌دهد.
/// </summary>
public static class MatchingEngineRegistration
{
    /// <summary>
    /// موتور تطبیق را به‌صورت <b>یک نمونهٔ واحد</b> ثبت می‌کند و همان نمونه را هم
    /// به‌عنوان <see cref="IMatchingEngine"/> (برای تزریق) و هم به‌عنوان
    /// <c>IHostedService</c> (برای حلقهٔ پس‌زمینه) در دسترس می‌گذارد.
    ///
    /// پیش‌تر این سرویس دو بار جداگانه ثبت شده بود؛ نتیجه دو موتور مستقل با دو
    /// <see cref="SemaphoreSlim"/> جدا بود، و سمافوری که برای جلوگیری از پردازش
    /// همزمان نوشته شده بود عملاً هیچ چیزی را محافظت نمی‌کرد (issue #53).
    ///
    /// Singleton بودن امن است چون <see cref="MatchingEngineService"/> برای هر دسترسی
    /// به دیتابیس با <c>IServiceScopeFactory</c> اسکوپ خودش را می‌سازد. وابستگی یک
    /// سرویس Scoped مثل <c>OrderService</c> به یک Singleton مجاز است؛ عکسش نه.
    /// </summary>
    public static IServiceCollection AddMatchingEngine(this IServiceCollection services)
    {
        services.AddSingleton<MatchingEngineService>();
        services.AddSingleton<IMatchingEngine>(sp => sp.GetRequiredService<MatchingEngineService>());
        services.AddHostedService(sp => sp.GetRequiredService<MatchingEngineService>());

        return services;
    }
}
