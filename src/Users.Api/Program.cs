using Microsoft.EntityFrameworkCore;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Requests.User;
using Users.Application;
using Users.Application.Mappers;
using Users.Core;
using Users.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server
builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("UsersDb") ??
        "Server=localhost;Database=TallaEggUsers;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("Users.Api")));

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<UserMapper>();

// اضافه کردن CORS
builder.Services.AddCors();

var app = builder.Build();

// تنظیم CORS
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// User management endpoints
app.MapPost("/api/user/register", async (RegisterUserRequest request, UserService userService) =>
{
    try
    {
        var user = await userService.RegisterUserAsync(
            request.TelegramId,
            request.InvitationCode,
            request.Username, 
            request.FirstName, 
            request.LastName);

        return ApiResponse<UserDto>.Ok(user, "User loaded successfully");
    }
    catch (Exception ex)
    {
        return ApiResponse<UserDto>.Fail(ex.Message);
    }
});

app.MapPost("/api/user/update-phone", async (UpdatePhoneRequest request, UserService userService) =>
{
    try
    {
        var response = await userService.UpdateUserPhoneAsync(request.TelegramId, request.PhoneNumber);
        return ApiResponse<UserDto>.Ok(response, "Phone number updated successfully");
    }
    catch (Exception ex)
    {
        return ApiResponse<UserDto>.Fail(ex.Message);
    }
});

app.MapGet("/api/user/{telegramId}", async (long telegramId, UserService userService) =>
{
    var user = await userService.GetUserByTelegramIdAsync(telegramId);
    if (user == null)
        return ApiResponse<UserDto>.Fail("User not found");

    return ApiResponse<UserDto>.Ok(user, "User loaded successfully");
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

// Invitation code endpoints
app.MapGet("/api/user/getUserIdByInvitationCode/{invitationCode}", async (string invitationCode, UserService userService) =>
{
    try
    {
        var userId = await userService.GetUserIdByInvitationCode(invitationCode);
        return Results.Ok(userId);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapPost("/api/user/validate-invitation", async (ValidateInvitationRequest request, UserService userService) =>
{
    try
    {
        var result = await userService.ValidateInvitationCodeAsync(request.InvitationCode);
        return Results.Ok(new { isValid = result.isValid, message = result.message });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

// User registration with invitation code
app.MapPost("/api/user/register-with-invitation", async (RegisterUserWithInvitationRequest request, UserService userService) =>
{
    try
    {
        var user = await userService.RegisterUserAsync(request.User);
        return Results.Ok(new { success = true, userId = user.Id });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

// User role management endpoints
app.MapPost("/api/user/update-role", async (UpdateUserRoleRequest request, UserService userService) =>
{
    try
    {
        var user = await userService.UpdateUserRoleAsync(request.UserId, request.NewRole);
        if (user == null)
            return Results.NotFound(new { success = false, message = "کاربر یافت نشد." });
        
        return Results.Ok(new { success = true, message = "نقش کاربر با موفقیت به‌روزرسانی شد." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapGet("/api/users/by-role/{role}", async (string role, UserService userService) =>
{
    try
    {
        if (!Enum.TryParse<UserRole>(role, true, out var userRole))
            return Results.BadRequest(new { success = false, message = "نقش نامعتبر است." });

        var users = await userService.GetUsersByRoleAsync(userRole);
        return Results.Ok(users);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

// User existence check
app.MapGet("/api/user/exists/{telegramId}", async (long telegramId, UserService userService) =>
{
    try
    {
        var exists = await userService.UserExistsAsync(telegramId);
        return Results.Ok(new { exists = exists });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.Run();

// Request models
public record UpdateStatusRequest(long TelegramId, UserStatus Status);
public record ValidateInvitationRequest(string InvitationCode);
public record RegisterUserWithInvitationRequest(User User);
public record UpdateUserRoleRequest(Guid UserId, UserRole NewRole); 