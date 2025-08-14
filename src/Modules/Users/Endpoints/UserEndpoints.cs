using Microsoft.AspNetCore.Mvc;
using TallaEgg.Api.Modules.Users.Application;
using TallaEgg.Core.Requests.User;

namespace TallaEgg.Api.Modules.Users.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/users")
            .WithTags("Users")
            .WithOpenApi();

        // Register user
        group.MapPost("/register", async (
            [FromBody] RegisterUserRequest request,
            [FromServices] UserService userService) =>
        {
            try
            {
                var user = await userService.RegisterUserAsync(request);
                return Results.Ok(new { success = true, user });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        })
        .WithName("RegisterUser")
        .WithSummary("Register a new user")
        .WithDescription("Registers a new user in the system");

        // Update phone
        group.MapPost("/update-phone", async (
            [FromBody] UpdatePhoneRequest request,
            [FromServices] UserService userService) =>
        {
            try
            {
                var user = await userService.UpdatePhoneAsync(request.UserId, request);
                if (user != null)
                {
                    return Results.Ok(new { success = true, user });
                }
                return Results.NotFound(new { success = false, message = "کاربر یافت نشد." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        })
        .WithName("UpdatePhone")
        .WithSummary("Update user phone")
        .WithDescription("Updates user's phone number");

        // Get user by Telegram ID
        group.MapGet("/telegram/{telegramId:long}", async (
            long telegramId,
            [FromServices] UserService userService) =>
        {
            var user = await userService.GetUserByTelegramIdAsync(telegramId);
            if (user == null)
                return Results.NotFound(new { message = "کاربر یافت نشد." });
            
            return Results.Ok(user);
        })
        .WithName("GetUserByTelegramId")
        .WithSummary("Get user by Telegram ID")
        .WithDescription("Gets user information by Telegram ID");

        // Update user status
        group.MapPost("/update-status", async (
            [FromBody] UpdateStatusRequest request,
            [FromServices] UserService userService) =>
        {
            try
            {
                var user = await userService.UpdateUserStatusAsync(request.UserId, request.Status);
                if (user != null)
                {
                    return Results.Ok(new { success = true, user });
                }
                return Results.NotFound(new { success = false, message = "کاربر یافت نشد." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        })
        .WithName("UpdateUserStatus")
        .WithSummary("Update user status")
        .WithDescription("Updates user's status");

        // Update user role
        group.MapPost("/update-role", async (
            [FromBody] UpdateRoleRequest request,
            [FromServices] UserService userService) =>
        {
            try
            {
                var user = await userService.UpdateUserRoleAsync(request.UserId, request.Role);
                if (user != null)
                {
                    return Results.Ok(new { success = true, user });
                }
                return Results.NotFound(new { success = false, message = "کاربر یافت نشد." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        })
        .WithName("UpdateUserRole")
        .WithSummary("Update user role")
        .WithDescription("Updates user's role");

        // Get all users
        group.MapGet("/", async (
            [FromServices] UserService userService) =>
        {
            var users = await userService.GetAllUsersAsync();
            return Results.Ok(users);
        })
        .WithName("GetAllUsers")
        .WithSummary("Get all users")
        .WithDescription("Gets all users in the system");

        // Get users by role
        group.MapGet("/by-role/{role}", async (
            string role,
            [FromServices] UserService userService) =>
        {
            try
            {
                if (!Enum.TryParse<TallaEgg.Core.Enums.User.UserRole>(role, true, out var userRole))
                    return Results.BadRequest(new { success = false, message = "نقش نامعتبر است." });

                var users = await userService.GetUsersByRoleAsync(userRole);
                return Results.Ok(users);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { success = false, message = ex.Message });
            }
        })
        .WithName("GetUsersByRole")
        .WithSummary("Get users by role")
        .WithDescription("Gets all users with a specific role");

        // Validate invitation
        group.MapPost("/validate-invitation", async (
            [FromBody] ValidateInvitationRequest request,
            [FromServices] UserService userService) =>
        {
            var isValid = await userService.ValidateInvitationCodeAsync(request.InvitationCode);
            return Results.Ok(new { isValid });
        })
        .WithName("ValidateInvitation")
        .WithSummary("Validate invitation code")
        .WithDescription("Validates an invitation code");

        // Get user ID by invitation code
        group.MapGet("/invitation/{invitationCode}", async (
            string invitationCode,
            [FromServices] UserService userService) =>
        {
            var userId = await userService.GetUserIdByInvitationCodeAsync(invitationCode);
            return Results.Ok(new { userId });
        })
        .WithName("GetUserIdByInvitationCode")
        .WithSummary("Get user ID by invitation code")
        .WithDescription("Gets user ID associated with an invitation code");

        // Check if user exists
        group.MapGet("/exists/{telegramId:long}", async (
            long telegramId,
            [FromServices] UserService userService) =>
        {
            var exists = await userService.UserExistsAsync(telegramId);
            return Results.Ok(new { exists });
        })
        .WithName("UserExists")
        .WithSummary("Check if user exists")
        .WithDescription("Checks if a user exists by Telegram ID");
    }
}

public record UpdateStatusRequest(Guid UserId, TallaEgg.Core.Enums.User.UserStatus Status);
public record UpdateRoleRequest(Guid UserId, TallaEgg.Core.Enums.User.UserRole Role);
public record ValidateInvitationRequest(string InvitationCode);
