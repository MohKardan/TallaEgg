using Microsoft.AspNetCore.Mvc;
using TallaEgg.Api.Modules.Affiliate.Application;

namespace TallaEgg.Api.Modules.Affiliate.Endpoints;

public static class AffiliateEndpoints
{
    public static void MapAffiliateEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/affiliate")
            .WithTags("Affiliate")
            .WithOpenApi();

        // Validate invitation
        group.MapPost("/validate-invitation", async (
            [FromBody] ValidateInvitationRequest request,
            [FromServices] AffiliateService affiliateService) =>
        {
            var result = await affiliateService.ValidateInvitationAsync(request.InvitationCode);
            
            return Results.Ok(new { 
                isValid = result.isValid, 
                message = result.message 
            });
        })
        .WithName("ValidateInvitation")
        .WithSummary("Validate invitation code")
        .WithDescription("Validates an invitation code");

        // Use invitation
        group.MapPost("/use-invitation", async (
            [FromBody] UseInvitationRequest request,
            [FromServices] AffiliateService affiliateService) =>
        {
            var result = await affiliateService.UseInvitationAsync(
                request.InvitationCode, 
                request.UsedByUserId);
            
            if (result.success)
            {
                return Results.Ok(new { 
                    success = true, 
                    message = result.message 
                });
            }
            
            return Results.BadRequest(new { 
                success = false, 
                message = result.message 
            });
        })
        .WithName("UseInvitation")
        .WithSummary("Use invitation code")
        .WithDescription("Uses an invitation code for a user");

        // Create invitation
        group.MapPost("/create-invitation", async (
            [FromBody] CreateInvitationRequest request,
            [FromServices] AffiliateService affiliateService) =>
        {
            var result = await affiliateService.CreateInvitationAsync(
                request.CreatedByUserId, 
                request.Type);
            
            if (result.success)
            {
                return Results.Ok(new { 
                    success = true, 
                    message = result.message, 
                    invitation = result.invitation 
                });
            }
            
            return Results.BadRequest(new { 
                success = false, 
                message = result.message 
            });
        })
        .WithName("CreateInvitation")
        .WithSummary("Create invitation")
        .WithDescription("Creates a new invitation code");

        // Get user invitations
        group.MapGet("/user-invitations/{userId:guid}", async (
            Guid userId,
            [FromServices] AffiliateService affiliateService) =>
        {
            var invitations = await affiliateService.GetUserInvitationsAsync(userId);
            return Results.Ok(invitations);
        })
        .WithName("GetUserInvitations")
        .WithSummary("Get user invitations")
        .WithDescription("Gets all invitations created by a user");

        // Get invitation usage count
        group.MapGet("/invitation-usage/{invitationId:guid}", async (
            Guid invitationId,
            [FromServices] AffiliateService affiliateService) =>
        {
            var usageCount = await affiliateService.GetInvitationUsageCountAsync(invitationId);
            return Results.Ok(new { invitationId, usageCount });
        })
        .WithName("GetInvitationUsageCount")
        .WithSummary("Get invitation usage count")
        .WithDescription("Gets the number of times an invitation has been used");
    }
}

public record ValidateInvitationRequest(string InvitationCode);
public record UseInvitationRequest(string InvitationCode, Guid UsedByUserId);
public record CreateInvitationRequest(Guid CreatedByUserId, TallaEgg.Api.Modules.Affiliate.Core.InvitationType Type = TallaEgg.Api.Modules.Affiliate.Core.InvitationType.Regular);
