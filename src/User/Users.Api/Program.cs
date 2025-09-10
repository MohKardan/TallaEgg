using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Orders.Core;
using TallaEgg.Core;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Requests.User;
using Users.Api;
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

// فقط در production محافظت فعال شود
if (builder.Environment.IsProduction())
{
    builder.Services.AddAuthentication("ApiKey")
        .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options =>
        {
            options.ApiKey = APIKeyConstant.TallaEggApiKey;
        });

    // Authorization Policy سراسری فقط برای production
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}
else
{
    // برای development فقط authorization اضافه کنید (بدون authentication)
    builder.Services.AddAuthorization();
}

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<UserMapper>();

// اضافه کردن CORS
builder.Services.AddCors();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TallaEgg Users API", Version = "v1" });
    
    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});



var app = builder.Build();

// Authentication و Authorization فقط در production
if (app.Environment.IsProduction())
{
    app.UseAuthentication();
    app.MapGet("/api-docs/{**path}", (string path) => Results.Redirect($"/api-docs/{path}"))
       .AllowAnonymous();
}
app.UseAuthorization();

// Add Swagger middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TallaEgg Users API v1");
    c.RoutePrefix = "api-docs";
});

// تنظیم CORS
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Add Swagger middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TallaEgg Users API v1");
    c.RoutePrefix = "api-docs";
});



/// <summary>
/// Registers a new user in the system
/// </summary>
/// <param name="request">User registration request containing Telegram ID, invitation code, and user details</param>
/// <param name="userService">User service for business logic</param>
/// <returns>Registered user details with success status</returns>
/// <response code="200">User registered successfully</response>
/// <response code="400">Invalid request data or validation error</response>
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

/// <summary>
/// Updates the phone number for an existing user
/// </summary>
/// <param name="request">Phone update request containing Telegram ID and new phone number</param>
/// <param name="userService">User service for business logic</param>
/// <returns>Updated user details with success status</returns>
/// <response code="200">Phone number updated successfully</response>
/// <response code="400">Invalid request data or validation error</response>
/// <response code="404">User not found</response>
app.MapPost("/api/user/update-phone", async (UpdatePhoneRequest request, UserService userService) =>
{
    try
    {
        var response = await userService.UpdateUserPhoneAsync(request.TelegramId, request.PhoneNumber);
        return Results.Ok(ApiResponse<UserDto>.Ok(response, "Phone number updated successfully"));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
    }
});

/// <summary>
/// Retrieves user information by Telegram ID
/// </summary>
/// <param name="telegramId">Telegram ID of the user</param>
/// <param name="userService">User service for business logic</param>
/// <returns>User details if found</returns>
/// <response code="200">User found and returned successfully</response>
/// <response code="404">User not found</response>
app.MapGet("/api/user/{telegramId}", async (long telegramId, UserService userService) =>
{
    var user = await userService.GetUserByTelegramIdAsync(telegramId);
    if (user == null)
        return Results.BadRequest(ApiResponse<UserDto>.Fail("User not found"));

    return Results.Ok(ApiResponse<UserDto>.Ok(user, "User loaded successfully"));
});

/// <summary>
/// دریافت اطلاعات کاربر بر اساس شناسه
/// </summary>
/// <param name="userId">شناسه یکتای کاربر</param>
/// <param name="userService">سرویس کاربر برای منطق تجاری</param>
/// <returns>جزئیات کاربر در صورت یافتن</returns>
/// <response code="200">کاربر پیدا شد و با موفقیت برگردانده شد</response>
/// <response code="404">کاربر پیدا نشد</response>
app.MapGet("/api/user/id/{userId}", async (Guid userId, UserService userService) =>
{
    try
    {
        var user = await userService.GetUserByIdAsync(userId);
        
        if (user == null)
        {
            return Results.Json(
                ApiResponse<UserDto>.NotFound("کاربر مورد نظر یافت نشد."),
                statusCode: 404
            );
        }

        return Results.Json(
            ApiResponse<UserDto>.Ok(user, "اطلاعات کاربر با موفقیت دریافت شد.")
        );
    }
    catch (Exception ex)
    {
        return Results.Json(
            ApiResponse<UserDto>.Error($"خطا در دریافت اطلاعات کاربر: {ex.Message}"),
            statusCode: 500
        );
    }
});

/// <summary>
/// Retrieves user information by phone number
/// </summary>
/// <param name="phone">phone of the user</param>
/// <param name="userService">User service for business logic</param>
/// <returns>User details if found</returns>
/// <response code="200">User found and returned successfully</response>
/// <response code="404">User not found</response>
app.MapGet("/api/userByPhone/{phone}", async (string phone, UserService userService) =>
{
    var user = await userService.GetUserByPhoneNumberAsync(phone);
    if (user == null)
        return Results.BadRequest(ApiResponse<UserDto>.Fail("User not found"));

    return Results.Ok(ApiResponse<UserDto>.Ok(user, "User loaded successfully"));
});


app.MapGet("/api/users/list", async (
        string? q,
        int? pageNumber,
        int? pageSize, UserService userService) =>
{
    // اعتبارسنجی
    var page = pageNumber ?? 1;
    var size = Math.Clamp(pageSize ?? 10, 1, 100);

    try
    {
        var users = await userService.GetUsersAsync(q, page, size);
       return Results.Ok(ApiResponse<PagedResult<UserDto>>.Ok(users, "کاربران دریافت شد"));

    }
    catch (Exception ex)
    {
       return Results.BadRequest(ApiResponse<PagedResult<OrderHistoryDto>>.Fail("خطا در دریافت اطلاعات"));
    }

});

/// <summary>
/// Updates the status of an existing user
/// </summary>
/// <param name="request">Status update request containing Telegram ID and new status</param>
/// <param name="userService">User service for business logic</param>
/// <returns>Success status with confirmation message</returns>
/// <response code="200">User status updated successfully</response>
/// <response code="400">Invalid request data or validation error</response>
/// <response code="404">User not found</response>
app.MapPut("/api/user/status", async (UpdateUserStatusRequest request, UserService userService) =>
{
    try
    {
        var user = await userService.UpdateUserStatusAsync(request.TelegramId, request.NewStatus);
        return Results.Ok(ApiResponse<UserDto>.Ok(user, "وضعیت کاربر با موفقیت به‌روزرسانی شد."));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
    }
});

/// <summary>
/// Gets user ID by invitation code
/// </summary>
/// <param name="invitationCode">Invitation code to lookup</param>
/// <param name="userService">User service for business logic</param>
/// <returns>User ID associated with the invitation code</returns>
/// <response code="200">User ID found and returned</response>
/// <response code="400">Invalid invitation code or error occurred</response>
/// <response code="404">Invitation code not found</response>
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

app.MapGet("/api/user/getUserIdByPhoneNumber/{phonenumber}", async (string phonenumber, UserService userService) =>
{
    try
    {
        var userId = await userService.GetUserIdByPhoneNumber(phonenumber);
        return Results.Ok(userId);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Validates an invitation code
/// </summary>
/// <param name="request">Invitation validation request containing the code to validate</param>
/// <param name="userService">User service for business logic</param>
/// <returns>Validation result with success status and message</returns>
/// <response code="200">Invitation code validated successfully</response>
/// <response code="400">Invalid invitation code or error occurred</response>
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

/// <summary>
/// Registers a new user with invitation code
/// </summary>
/// <param name="request">User registration request with invitation code</param>
/// <param name="userService">User service for business logic</param>
/// <returns>Registered user details with success status</returns>
/// <response code="200">User registered successfully with invitation</response>
/// <response code="400">Invalid request data or validation error</response>
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

/// <summary>
/// Updates the role of an existing user
/// </summary>
/// <param name="request">Role update request containing user ID and new role</param>
/// <param name="userService">User service for business logic</param>
/// <returns>Success status with confirmation message</returns>
/// <response code="200">User role updated successfully</response>
/// <response code="400">Invalid request data or validation error</response>
/// <response code="404">User not found</response>
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

/// <summary>
/// Gets all users by role
/// </summary>
/// <param name="role">Role to filter users by</param>
/// <param name="userService">User service for business logic</param>
/// <returns>List of users with the specified role</returns>
/// <response code="200">Users found and returned successfully</response>
/// <response code="400">Invalid role or error occurred</response>
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

/// <summary>
/// Checks if a user exists by Telegram ID
/// </summary>
/// <param name="telegramId">Telegram ID to check</param>
/// <param name="userService">User service for business logic</param>
/// <returns>Boolean indicating if user exists</returns>
/// <response code="200">User existence check completed</response>
/// <response code="400">Error occurred during check</response>
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




/// <summary>
/// Request model for validating invitation codes
/// </summary>
public record ValidateInvitationRequest(string InvitationCode);

/// <summary>
/// Request model for registering users with invitation codes
/// </summary>
public record RegisterUserWithInvitationRequest(User User);

/// <summary>
/// Request model for updating user roles
/// </summary>
public record UpdateUserRoleRequest(Guid UserId, UserRole NewRole);