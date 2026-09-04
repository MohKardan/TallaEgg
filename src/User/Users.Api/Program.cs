using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Orders.Core;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TallaEgg.Core;
using TallaEgg.Core.Cors;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.User;
using TallaEgg.Core.Enums.User;
using TallaEgg.Core.ErrorHandling;
using TallaEgg.Core.Requests.User;
using Users.Api;
using Users.Application;
using Users.Application.Mappers;
using Users.Core;
using Users.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// No-op outside an actual Windows Service Control Manager session (e.g. local `dotnet run`),
// so this is always safe to include. Lets `sc.exe create` manage this process directly —
// no third-party supervisor needed (issue #70).
builder.Host.UseWindowsService();

// Serilog before anything that can throw. This configuration reads nothing from the shared file
// — the sinks are fixed here — so it can be installed ahead of the file being located, which is
// what lets a configuration failure reach the rolling log rather than a console no Windows
// service has (issue #205).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(StartupLogging.LogFilePath("users-api-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();
StartupLogging.ReportUnhandledExceptionsToLog();

// Answers "which build is this?" from the log even when the service never finishes starting (issue #218).
StartupLogging.LogBuildVersion();

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
    .Select(pair => new KeyValuePair<string, string?>(
        pair.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? pair.Key[prefix.Length..]
            : pair.Key,
        pair.Value!))
    .Where(pair => !string.IsNullOrWhiteSpace(pair.Key))
    .ToDictionary(pair => pair.Key, pair => pair.Value);

builder.Configuration.AddInMemoryCollection(flattened);

// Re-registered after the shared file and the section flattened from it so that last-wins
// puts a host on top of both. WebApplication.CreateBuilder registers these two, but ahead of
// the AddJsonFile above, which left the file outranking them: no port, URL or connection
// string could be varied per host without hand-editing config/appsettings.global.json, the
// one file that holds live credentials and is deliberately untracked (#33). The file stays
// the source of truth for every value a host does not explicitly override (issue #159).
builder.Configuration.AddEnvironmentVariables();
builder.Configuration.AddCommandLine(args);

// Trading symbols come from appsettings.global.json (Symbols section), not compiled-in defaults.
TallaEgg.Core.CurrenciesConstant.Configure(builder.Configuration);

// UseUrls writes through UseSetting, which bypasses the configuration providers entirely, so
// calling it unconditionally let the file's address beat ASPNETCORE_URLS and --urls however the
// providers were ordered — the one override #159 could not reach. The file now supplies the
// listen address only when the host has not already named one (issue #181).
var urls = serviceSection.GetSection("Urls").Get<string[]>();
if (string.IsNullOrWhiteSpace(builder.Configuration[WebHostDefaults.ServerUrlsKey]) && urls is { Length: > 0 })
{
    builder.WebHost.UseUrls(urls);
}

// SQL Server connection. Read here, not inside the options delegate below: that delegate does
// not run until DbContextOptions<T> is first resolved, so a missing connection string failed
// startup only because the migration block further down happens to resolve the context (#205).
var usersConnectionString = ConfigurationGuard.RequireConnectionString(builder.Configuration, "UsersDb");

builder.Services.AddDbContext<UsersDbContext>(options =>
    options.UseSqlServer(usersConnectionString,
        b => b.MigrationsAssembly("Users.Api")));

// Protection is only wired up in Production.
if (builder.Environment.IsProduction())
{
    // Read here, not inside the AddScheme delegate below: that delegate is an options
    // configuration action, so it does not run when the host is built but the first time
    // IOptionsMonitor<ApiKeyAuthenticationSchemeOptions>.Get is called — inside
    // AuthenticationHandler<T>.InitializeAsync, on a request. An unset TALLAEGG_API_KEY
    // therefore let the service start, bind its port and look healthy under sc.exe while
    // answering 500 to every request, one environment variable away from working (issue #214).
    var apiKey = APIKeyConstant.RequireTallaEggApiKey();

    builder.Services.AddAuthentication("ApiKey")
        .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options =>
        {
            options.ApiKey = apiKey;
        });

    // Global authorization policy, Production only.
    builder.Services.AddAuthorization(options =>
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
    });
}
else
{
    // Development adds authorization only, with no authentication in front of it.
    builder.Services.AddAuthorization();
}

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<UserMapper>();
builder.Services.AddTallaEggErrorHandling();

// HttpClient for calling the Wallet API. The address is read and parsed here rather than
// inside the configure delegate, which would not run until the first client was created. A bad
// address has to stop the service coming up: registration creates the default wallets through
// this client and swallows what it throws (UserService.CreateDefaultWalletsAsync), so deferring
// the failure means users registered with no wallets and nothing said about it.
var walletApiBaseAddress = ConfigurationGuard.RequireUri(builder.Configuration, "WalletApiUrl");

// Read here rather than inside the configure delegate below, for the reason the address is:
// the delegate does not run until the first client is created, so a missing key would surface
// one registration at a time inside the broad catch in UserService.CreateDefaultWalletsAsync
// instead of stopping the service (issue #205). Production demands the real key — sending the
// placeholder to a Wallet.Api that enforces the real one reproduces issue #209 exactly, with
// the header present so the code reads as fixed. Outside Production the placeholder is right:
// no service registers API-key authentication there, and requiring the variable would stop
// every clone and CI job that has no reason to hold it.
var walletApiKey = builder.Environment.IsProduction()
    ? APIKeyConstant.RequireTallaEggApiKey()
    : APIKeyConstant.TallaEggApiKey;

builder.Services.AddHttpClient("WalletAPI", client =>
{
    client.BaseAddress = walletApiBaseAddress;
    client.Timeout = TimeSpan.FromSeconds(30);

    // Wallet.Api requires X-API-Key in Production, so without this every registration on a
    // deployed system got 401 and created no wallets. It stayed hidden because Development
    // registers no authentication at all, and because a missing wallet is created lazily on
    // first write — the first deposit or trade produced the rows registration had failed to
    // (issue #209). The typed clients set the same header in their constructors; a named
    // client has no constructor to set it in, so it goes here.
    client.DefaultRequestHeaders.Add("X-API-Key", walletApiKey);
})
// The only key-bearing client in the solution built through IHttpClientFactory, whose logging
// handlers write request headers at Trace and redact nothing by default. The other four are
// raw HttpClients and never reach those handlers, so this is the one place the shared key
// could be written to a log by lowering a level.
.RedactLoggedHeaders(["X-API-Key"]);

// CORS — issue #31: a whitelist read from configuration, not AllowAnyOrigin.
builder.Services.AddTallaEggCors(builder.Configuration);

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

app.UseTallaEggErrorHandling();

// --- Migrations and initial seed ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<UsersDbContext>();
    try
    {
        await context.Database.MigrateAsync(); // اجرای مایگریشن‌ها

        var adminId = TallaEgg.Core.BootstrapConstant.RootAdminUserId;
        var existingAdmin = await context.Users.FirstOrDefaultAsync(u => u.Id == adminId);

        if (existingAdmin == null)
        {
            User user = new User()
            {
                Id = adminId,
                FirstName = "مدیر",
                LastName = "کل",
                // Shared with the bot's fallback referral code. Registration rejects any code
                // that belongs to no user, so on an empty database this row's code is the only
                // one that can ever work — and the two sides had drifted apart.
                InvitationCode = TallaEgg.Core.BootstrapConstant.RootInvitationCode,
                IsActive = true,
                CreatedAt = DateTime.Parse("2025-08-04T08:43:43.1234567Z"),
                Role = UserRole.SuperAdmin
            };
            context.Users.Add(user);
            await context.SaveChangesAsync();
            Log.Information("مدیر کل با موفقیت ایجاد شد.");
        }
        else
        {
            Log.Information("مدیر کل قبلاً وجود دارد. برنامه بدون خطا اجرا می‌شود.");
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "خطا در مایگریشن یا سیید اولیه مدیر کل");
        throw;
    }
}





// Authentication and authorization, Production only.
if (app.Environment.IsProduction())
{
    app.UseAuthentication();
}
app.UseAuthorization();

// Apply the CORS policy.
app.UseTallaEggCors();

// API documentation, Development only. Swagger has no consumer in Production: the APIs are
// called by the Telegram bot through hand-written typed clients, and nothing generates a client
// from the OpenAPI document. Publishing the endpoint map and schemas there is attack surface
// bought for nothing.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TallaEgg Users API v1");
        c.RoutePrefix = "api-docs";
    });
}

// Which build this service is running (issue #218) — the question a deployment leaves behind,
// answered without RDP and a file-properties dialog. Deliberately not AllowAnonymous: the
// Production fallback policy applies, so the caller needs the same X-API-Key as every other
// endpoint. The commit hash names an exact line of a public repository, and the operator asking
// the question already holds the key.
app.MapGet("/version", () => Results.Ok(ApiResponse<BuildVersionDto>.Ok(BuildVersion.Current)))
   .WithSummary("Report the running build")
   .WithDescription(
        "Returns the version and commit hash stamped into the running assembly. In Production the " +
        "global fallback policy applies, so this needs the same X-API-Key as every other endpoint.")
   .WithTags("Diagnostics");



app.MapPost("/api/user/register", async (RegisterUserRequest request, UserService userService) =>
{
    // RegisterUserAsync throws a plain Exception with a user-facing message when the invitation
    // code is invalid — that message is the point, not boilerplate, so it stays local. See #88.
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
    catch (BusinessRuleException ex)
    {
        return ApiResponse<UserDto>.Fail(ex.Message);
    }
})
.WithSummary("Register a Telegram user")
.WithDescription(
    "Requires an invitation code that some existing user already holds; an unknown code is refused " +
    "with «کد دعوت معتبر نیست.». The new user starts Pending with the RegularUser role and is given " +
    "an invitation code of their own. Default wallets are requested from Wallet.Api as part of " +
    "this call, and a failure there is logged without failing the registration — so a 'registered' " +
    "user may briefly have no wallet rows. Note that both outcomes answer 200: read the `success` " +
    "field rather than the status code.")
.WithTags("Users");

app.MapPost("/api/user/update-phone", async (UpdatePhoneRequest request, UserService userService) =>
{
    // UpdateUserPhoneAsync throws InvalidOperationException with a user-facing "not found" message — a
    // message, not boilerplate, so it stays local. See #88.
    try
    {
        var response = await userService.UpdateUserPhoneAsync(request.TelegramId, request.PhoneNumber);
        return Results.Ok(ApiResponse<UserDto>.Ok(response, "Phone number updated successfully"));
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
    }
})
.WithSummary("Set a user's phone number")
.WithDescription(
    "Identifies the user by Telegram id and overwrites the stored phone number, with no format " +
    "check and no uniqueness check. An unknown Telegram id answers 400 with «کاربر یافت نشد.».")
.WithTags("Users");

app.MapGet("/api/user/{telegramId}", async (long telegramId, UserService userService) =>
{
    var user = await userService.GetUserByTelegramIdAsync(telegramId);
    if (user == null)
        return Results.BadRequest(ApiResponse<UserDto>.Fail("User not found"));

    return Results.Ok(ApiResponse<UserDto>.Ok(user, "User loaded successfully"));
})
.WithSummary("Get a user by Telegram id")
.WithDescription(
    "The lookup the bot uses on nearly every message. A user who does not exist answers 400, not " +
    "404 — use /api/user/exists/{telegramId} if the distinction matters to the caller.")
.WithTags("Users");

app.MapGet("/api/user/userId/{userId}", async (Guid userId, UserService userService) =>
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
})
.WithSummary("Get a user by internal id")
.WithDescription(
    "The same record as the Telegram-id lookup, keyed on the platform's own user id — the id the " +
    "Wallet and Orders services store. Unlike that lookup, a missing user here answers 404.")
.WithTags("Users");

app.MapGet("/api/userByPhone/{phone}", async (string phone, UserService userService) =>
{
    var user = await userService.GetUserByPhoneNumberAsync(phone);
    if (user == null)
        return Results.BadRequest(ApiResponse<UserDto>.Fail("User not found"));

    return Results.Ok(ApiResponse<UserDto>.Ok(user, "User loaded successfully"));
})
.WithSummary("Get a user by phone number")
.WithDescription(
    "Matches the stored phone number exactly — no normalisation of country code or leading zero, " +
    "so the caller has to send it in the form registration stored. A miss answers 400, not 404.")
.WithTags("Users");


app.MapGet("/api/users/list", async (
        string? q,
        int? pageNumber,
        int? pageSize, UserService userService) =>
{
    // Validation.
    var page = pageNumber ?? 1;
    var size = Math.Clamp(pageSize ?? 10, 1, 100);

    var users = await userService.GetUsersAsync(q, page, size);
    return Results.Ok(ApiResponse<PagedResult<UserDto>>.Ok(users, "کاربران دریافت شد"));
})
.WithSummary("Search and page through users")
.WithDescription(
    "Newest first. `q` is a substring match over first name, last name and phone number; omit it " +
    "to list everyone. `pageNumber` defaults to 1 and `pageSize` to 10, clamped to between 1 and " +
    "100 — a larger page size is silently reduced rather than refused.")
.WithTags("Users");

app.MapPut("/api/user/status", async (UpdateUserStatusRequest request, UserService userService) =>
{
    // UpdateUserStatusAsync throws InvalidOperationException with a user-facing "not found" message — a
    // message, not boilerplate, so it stays local. See #88.
    try
    {
        var user = await userService.UpdateUserStatusAsync(request.TelegramId, request.NewStatus);
        return Results.Ok(ApiResponse<UserDto>.Ok(user, "وضعیت کاربر با موفقیت به‌روزرسانی شد."));
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<UserDto>.Fail(ex.Message));
    }
})
.WithSummary("Change a user's status")
.WithDescription(
    "Moves the user identified by Telegram id between Pending, Approved, Rejected, Suspended and " +
    "Blocked. This is the approval gate an administrator drives from the bot. No transition is " +
    "forbidden — any status can be set from any other. An unknown Telegram id answers 400 with " +
    "«کاربر یافت نشد.».")
.WithTags("Users");

app.MapGet("/api/user/getUserIdByInvitationCode/{invitationCode}", async (string invitationCode, UserService userService) =>
{
    var userId = await userService.GetUserIdByInvitationCode(invitationCode);
    return Results.Ok(userId);
})
.WithSummary("Resolve an invitation code to the user who owns it")
.WithDescription(
    "Every user carries an invitation code of their own, and this returns the id of the user whose " +
    "code this is — the referrer recorded against a new registration. A code nobody holds answers " +
    "200 with a null body rather than 404, and the id is returned bare rather than in the " +
    "ApiResponse envelope.")
.WithTags("Invitations");

app.MapGet("/api/user/getUserIdByPhoneNumber/{phonenumber}", async (string phonenumber, UserService userService) =>
{
    var userId = await userService.GetUserIdByPhoneNumber(phonenumber);
    return Results.Ok(userId);
})
.WithSummary("Resolve a phone number to a user id")
.WithDescription(
    "Matches the stored phone number exactly, as /api/userByPhone does. A number nobody has " +
    "answers 200 with a null body rather than 404, and the id is returned bare rather than in the " +
    "ApiResponse envelope.")
.WithTags("Users");

app.MapPost("/api/user/update-role", async (UpdateUserRoleRequest request, UserService userService) =>
{
    var user = await userService.UpdateUserRoleAsync(request.UserId, request.NewRole);
    if (user == null)
        return Results.NotFound(new { success = false, message = "کاربر یافت نشد." });

    return Results.Ok(new { success = true, message = "نقش کاربر با موفقیت به‌روزرسانی شد." });
})
.WithSummary("Change a user's role")
.WithDescription(
    "Sets the role on the user with the given internal id — note the id, not the Telegram id every " +
    "other user command takes. No transition is forbidden and the caller's own role is not " +
    "considered here; in Production the X-API-Key is the only thing standing in front of this. " +
    "Answers 404 for an unknown user, and returns a bare {success, message} object rather than the " +
    "ApiResponse envelope.")
.WithTags("Users");

app.MapGet("/api/users/by-role/{role}", async (string role, UserService userService) =>
{
    if (!Enum.TryParse<UserRole>(role, true, out var userRole))
        return Results.BadRequest(new { success = false, message = "نقش نامعتبر است." });

    var users = await userService.GetUsersByRoleAsync(userRole);
    return Results.Ok(users);
})
.WithSummary("List every user holding a given role")
.WithDescription(
    "The role name is parsed case-insensitively from the path; a name that matches no role answers " +
    "400 with «نقش نامعتبر است.». Unlike the single-user lookups, this returns the stored user " +
    "records themselves rather than the trimmed UserDto, so the response carries fields those " +
    "endpoints leave out.")
.WithTags("Users");

app.MapGet("/api/user/exists/{telegramId}", async (long telegramId, UserService userService) =>
{
    var exists = await userService.UserExistsAsync(telegramId);
    return Results.Ok(new { exists = exists });
})
.WithSummary("Check whether a Telegram id is registered")
.WithDescription(
    "Answers 200 with {exists: true|false} either way — an unregistered id is a normal answer " +
    "here, not an error, which is what distinguishes this from the by-Telegram-id lookup.")
.WithTags("Users");

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




/// <summary>
/// Request model for updating user roles
/// </summary>
public record UpdateUserRoleRequest(Guid UserId, UserRole NewRole);