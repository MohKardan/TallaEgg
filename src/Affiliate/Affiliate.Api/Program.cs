using Affiliate.Application;
using Affiliate.Core;
using Affiliate.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TallaEgg.Core;
using TallaEgg.Core.ErrorHandling;

var builder = WebApplication.CreateBuilder(args);

const string sharedConfigFileName = "appsettings.global.json";
var sharedConfigPath = ResolveSharedConfigPath(builder.Environment, sharedConfigFileName);
builder.Configuration.AddJsonFile(sharedConfigPath, optional: false, reloadOnChange: true);

var applicationName = builder.Environment.ApplicationName;
var serviceSection = builder.Configuration.GetSection($"Services:{applicationName}");
if (!serviceSection.Exists())
{
    throw new InvalidOperationException($"Missing configuration section 'Services:{applicationName}' in {sharedConfigFileName}.");
}

var prefix = $"Services:{applicationName}:";
var flattened = serviceSection.AsEnumerable(true)
    .Where(pair => pair.Value is not null)
    .Select(pair => new KeyValuePair<string, string>(
        pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? pair.Key[prefix.Length..]
            : pair.Key,
        pair.Value!))
    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
    .ToDictionary(pair => pair.Key, pair => pair.Value);

builder.Configuration.AddInMemoryCollection(flattened);

var urls = serviceSection.GetSection("Urls").Get<string[]>();
if (urls is { Length: > 0 })
{
    builder.WebHost.UseUrls(urls);
}

// تنظیم اتصال به دیتابیس SQL Server
builder.Services.AddDbContext<AffiliateDbContext>(options =>
    options.UseSqlServer(ConfigurationGuard.RequireConnectionString(builder.Configuration, "AffiliateDb"),
        b => b.MigrationsAssembly("Affiliate.Api")));

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

// Registered unconditionally, matching the other four services. It was missing entirely,
// which meant app.UseCors below could not resolve ICorsService and the process died at
// startup in Production — the one environment nobody runs locally (issue #72).
builder.Services.AddCors();

builder.Services.AddScoped<IAffiliateRepository, AffiliateRepository>();
builder.Services.AddScoped<AffiliateService>();
builder.Services.AddTallaEggErrorHandling();

// پیکربندی Serilog برای لاگ‌نویسی روی فایل و کنسول
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/affiliate-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

app.UseTallaEggErrorHandling();

// --- مایگریشن و سیید اولیه ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<AffiliateDbContext>();
    await context.Database.MigrateAsync(); // اجرای مایگریشن‌ها
}

// Authentication و Authorization فقط در production
if (app.Environment.IsProduction())
{
    app.UseAuthentication();
    app.MapGet("/api-docs/{**path}", (string path) => Results.Redirect($"/api-docs/{path}"))
       .AllowAnonymous();
}
app.UseAuthorization();

// The environment guard is gone along with the comment that justified it. The comment said
// "CORS services are only registered in Production (see AddCors above)" — there was no
// AddCors above, in Production or anywhere else, so the guard was not protecting Development
// from a missing registration; it was hiding a service that could never start in Production.
//
// Now registered unconditionally, so this runs in both, as it does in the other four services.
//
// AllowAnyOrigin is what #31 is about. Worth noting there: CORS constrains browsers, and this
// product has no browser client — the APIs are called only by the bot over loopback. The
// protection that matters is not opening these ports to the internet at all, which #69 covers.
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Affiliate management endpoints
app.MapPost("/api/affiliate/validate-invitation", async (ValidateInvitationRequest request, AffiliateService affiliateService) =>
{
    var result = await affiliateService.ValidateInvitationCodeAsync(request.InvitationCode);
    return Results.Ok(new { isValid = result.isValid, message = result.message });
});

app.MapPost("/api/affiliate/use-invitation", async (UseInvitationRequest request, AffiliateService affiliateService) =>
{
    // UseInvitationAsync throws InvalidOperationException with a user-facing message when the
    // code is invalid — that message is the point, not boilerplate, so it stays local. See #88.
    try
    {
        var invitation = await affiliateService.UseInvitationAsync(
            request.InvitationCode,
            request.UsedByUserId,
            request.UserAgent,
            request.IpAddress);
        return Results.Ok(new { success = true, invitationId = invitation.Id });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapPost("/api/affiliate/create-invitation", async (CreateInvitationRequest request, AffiliateService affiliateService) =>
{
    var invitation = await affiliateService.CreateInvitationAsync(
        request.CreatedByUserId,
        request.Type,
        request.MaxUses,
        request.ExpiresAt);
    return Results.Ok(new { success = true, invitation = invitation });
});

app.MapGet("/api/affiliate/user-invitations/{userId}", async (Guid userId, AffiliateService affiliateService) =>
{
    var invitations = await affiliateService.GetUserInvitationsAsync(userId);
    return Results.Ok(invitations);
});

static string ResolveSharedConfigPath(Microsoft.Extensions.Hosting.IHostEnvironment environment, string fileName)
{
    var current = new System.IO.DirectoryInfo(environment.ContentRootPath);
    try
    {
        while (current is not null)
        {
            var candidate = System.IO.Path.Combine(current.FullName, "config", fileName);
            if (System.IO.File.Exists(candidate))
            {
                return candidate;
            }
            current = current.Parent;
        }

        var errorMsg = $"Shared configuration '{fileName}' not found relative to '{environment.ContentRootPath}'.";
        Log.Error(errorMsg); // Serilog logs to file as configured
        throw new System.IO.FileNotFoundException(errorMsg, fileName);
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error resolving shared config path for file {FileName}", fileName);
        throw;
    }
}


app.Run();

// Request models
public record ValidateInvitationRequest(string InvitationCode);
public record UseInvitationRequest(string InvitationCode, Guid UsedByUserId, string? UserAgent = null, string? IpAddress = null);
public record CreateInvitationRequest(Guid CreatedByUserId, InvitationType Type = InvitationType.Regular, int MaxUses = -1, DateTime? ExpiresAt = null);