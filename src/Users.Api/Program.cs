using Microsoft.EntityFrameworkCore;
using Users.Core;
using Users.Infrastructure;
using Users.Application;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server
builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UsersDb") ??
        "Server=localhost;Database=TallaEggUsers;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("Users.Api")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<UserService>();

var app = builder.Build();

// User management endpoints
app.MapPost("/api/user/register", async (RegisterUserRequest request, UserService userService) =>
{
    try
    {
        var user = await userService.RegisterUserAsync(
            request.TelegramId, 
            request.Username, 
            request.FirstName, 
            request.LastName);
        return Results.Ok(new { success = true, userId = user.Id });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapPost("/api/user/update-phone", async (UpdatePhoneRequest request, UserService userService) =>
{
    try
    {
        var user = await userService.UpdateUserPhoneAsync(request.TelegramId, request.PhoneNumber);
        return Results.Ok(new { success = true, message = "شماره تلفن با موفقیت ثبت شد." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapGet("/api/user/{telegramId}", async (long telegramId, UserService userService) =>
{
    var user = await userService.GetUserByTelegramIdAsync(telegramId);
    if (user == null)
        return Results.NotFound();
    return Results.Ok(user);
});

app.MapPost("/api/user/update-status", async (UpdateStatusRequest request, UserService userService) =>
{
    try
    {
        var user = await userService.UpdateUserStatusAsync(request.TelegramId, request.Status);
        return Results.Ok(new { success = true, message = "وضعیت کاربر با موفقیت به‌روزرسانی شد." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.Run();

// Request models
public record RegisterUserRequest(long TelegramId, string? Username, string? FirstName, string? LastName);
public record UpdatePhoneRequest(long TelegramId, string PhoneNumber);
public record UpdateStatusRequest(long TelegramId, UserStatus Status); 