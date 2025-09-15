using Microsoft.EntityFrameworkCore;
using Affiliate.Core;
using Affiliate.Infrastructure;
using Affiliate.Application;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server
builder.Services.AddDbContext<AffiliateDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("AffiliateDb") ??
        "Server=localhost;Database=TallaEggAffiliate;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("Affiliate.Api")));

builder.Services.AddScoped<IAffiliateRepository, AffiliateRepository>();
builder.Services.AddScoped<AffiliateService>();

// پیکربندی Serilog برای لاگ‌نویسی روی فایل و کنسول
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/affiliate-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// Affiliate management endpoints
app.MapPost("/api/affiliate/validate-invitation", async (ValidateInvitationRequest request, AffiliateService affiliateService) =>
{
    var result = await affiliateService.ValidateInvitationCodeAsync(request.InvitationCode);
    return Results.Ok(new { isValid = result.isValid, message = result.message });
});

app.MapPost("/api/affiliate/use-invitation", async (UseInvitationRequest request, AffiliateService affiliateService) =>
{
    try
    {
        var invitation = await affiliateService.UseInvitationAsync(
            request.InvitationCode, 
            request.UsedByUserId, 
            request.UserAgent, 
            request.IpAddress);
        return Results.Ok(new { success = true, invitationId = invitation.Id });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapPost("/api/affiliate/create-invitation", async (CreateInvitationRequest request, AffiliateService affiliateService) =>
{
    try
    {
        var invitation = await affiliateService.CreateInvitationAsync(
            request.CreatedByUserId, 
            request.Type, 
            request.MaxUses, 
            request.ExpiresAt);
        return Results.Ok(new { success = true, invitation = invitation });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapGet("/api/affiliate/user-invitations/{userId}", async (Guid userId, AffiliateService affiliateService) =>
{
    var invitations = await affiliateService.GetUserInvitationsAsync(userId);
    return Results.Ok(invitations);
});

app.Run();

// Request models
public record ValidateInvitationRequest(string InvitationCode);
public record UseInvitationRequest(string InvitationCode, Guid UsedByUserId, string? UserAgent = null, string? IpAddress = null);
public record CreateInvitationRequest(Guid CreatedByUserId, InvitationType Type = InvitationType.Regular, int MaxUses = -1, DateTime? ExpiresAt = null);