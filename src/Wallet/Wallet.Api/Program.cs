using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TallaEgg.Core;
using TallaEgg.Core.Cors;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.ErrorHandling;
using TallaEgg.Core.Requests.Trade;
using TallaEgg.Core.Requests.Wallet;
using Wallet.Application;
using Wallet.Application.Mappers;
using Wallet.Core;
using Wallet.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// No-op outside an actual Windows Service Control Manager session (e.g. local `dotnet run`),
// so this is always safe to include. Lets `sc.exe create` manage this process directly —
// no third-party supervisor needed (issue #70).
builder.Host.UseWindowsService();

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

// Trading symbols come from appsettings.global.json (Symbols section), not compiled-in defaults.
TallaEgg.Core.CurrenciesConstant.Configure(builder.Configuration);

var urls = serviceSection.GetSection("Urls").Get<string[]>();
if (urls is { Length: > 0 })
{
    builder.WebHost.UseUrls(urls);
}

// Serilog: log to rolling files and the console.
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/wallet-api-.log", rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
    .CreateLogger();

builder.Host.UseSerilog();

// SQL Server connection.
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseSqlServer(ConfigurationGuard.RequireConnectionString(builder.Configuration, "WalletDb"),
        b => b.MigrationsAssembly("Wallet.Api")));


// Protection is only wired up in Production.
if (builder.Environment.IsProduction())
{
    builder.Services.AddAuthentication("ApiKey")
        .AddScheme<ApiKeyAuthenticationSchemeOptions, ApiKeyAuthenticationHandler>("ApiKey", options =>
        {
            options.ApiKey = APIKeyConstant.RequireTallaEggApiKey();
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

builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<WalletMapper>();
builder.Services.AddTallaEggErrorHandling();

// CORS — issue #31: a whitelist read from configuration, not AllowAnyOrigin.
builder.Services.AddTallaEggCors(builder.Configuration);

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TallaEgg Wallet API", Version = "v1" });

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
    var context = services.GetRequiredService<WalletDbContext>();
    await context.Database.MigrateAsync();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TallaEgg Wallet API v1");
        c.RoutePrefix = "api-docs";
    });
}

// Wallet management endpoints
app.MapGet("/api/wallet/balance/{userId}/{asset}", async (Guid userId, string asset, IWalletService walletService) =>
{
    try
    {
        var balance = await walletService.GetBalanceAsync(userId, asset);
        return Results.Ok(ApiResponse<WalletDTO>.Ok(balance, ""));
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<WalletDTO>.Fail(ex.Message));
    }
})
.WithTags("Balances");

app.MapGet("/api/wallet/balances/{userId}", async (Guid userId, IWalletService walletService) =>
{
    var wallets = await walletService.GetUserWalletsAsync(userId);
    return Results.Ok(ApiResponse<IEnumerable<WalletDTO>>.Ok(wallets, "لیست کیف پول های کاربر"));
})
.WithTags("Balances");

app.MapPost("/api/wallet/deposit", async (WalletRequest request, IWalletService walletService) =>
{
    try
    {
       var result = await walletService.DepositAsync(request.UserId, request.Asset, request.Amount, request.ReferenceId);
       return Results.Ok(ApiResponse<WalletBallanceDTO>.Ok(result, "عملیات با موفقیت انجام شد"));

    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<WalletBallanceDTO>.Fail(ex.Message));
    }
  
})
.WithTags("Transactions");

app.MapPost("/api/wallet/withdrawal", async (WalletRequest request, IWalletService walletService) =>
{
    try
    {
       var result = await walletService.WithdrawalAsync(request.UserId, request.Asset, request.Amount, request.ReferenceId);
       return Results.Ok(ApiResponse<WalletBallanceDTO>.Ok(result, "عملیات با موفقیت انجام شد"));

    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<WalletBallanceDTO>.Fail(ex.Message));
    }
  
})
.WithTags("Transactions");

app.MapPost("/api/wallet/lockBalance", async (WalletRequest request, IWalletService walletService) =>
{
    try
    {
       var result = await walletService.LockBalanceAsync(request.UserId, request.Asset, request.Amount);
       return Results.Ok(ApiResponse<WalletDTO>.Ok(result, "عملیات با موفقیت انجام شد"));

    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<WalletDTO>.Fail(ex.Message));
    }
  
})
.WithTags("Locks");

app.MapPost("/api/wallet/unlockBalance", async (WalletRequest request, IWalletService walletService) =>
{
    try
    {
        var result = await walletService.UnlockBalanceAsync(request.UserId, request.Asset, request.Amount);
        return Results.Ok(ApiResponse<WalletDTO>.Ok(result, "عملیات با موفقیت انجام شد"));
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<WalletDTO>.Fail(ex.Message));
    }
})
.WithTags("Locks");

app.MapPost("/api/wallet/increaseBalance", async (WalletRequest request, IWalletService walletService) =>
{
    try
    {
        var result = await walletService.IncreaseBalanceAsync(request.UserId, request.Asset, request.Amount);
        return Results.Ok(ApiResponse<(WalletEntity,Transaction)>.Ok(result, "عملیات با موفقیت انجام شد"));
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<WalletDTO>.Fail(ex.Message));
    }
})
.WithTags("Transactions");

// Trade settlement — called by the Orders outbox processor after a match.
// Atomically consumes both sides' locked collateral, credits each side, and records
// a Transaction per leg. Idempotent on the trade id, so retries are safe.
app.MapPost("/api/wallet/changeBalance", async (TradeDto trade, IWalletService walletService, ILogger<Program> logger) =>
{
    try
    {
        var result = await walletService.SettleTradeAsync(
            trade.Id, trade.BuyerUserId, trade.SellerUserId,
            trade.Symbol, trade.Quantity, trade.QuoteQuantity,
            trade.FeeBuyer, trade.FeeSeller);

        if (!result.Success)
        {
            logger.LogWarning("Trade settlement rejected for {TradeId}: {Message}", trade.Id, result.Message);
            return Results.BadRequest(ApiResponse<string>.Fail(result.Message));
        }

        return Results.Ok(ApiResponse<string>.Ok(result.Message, "Trade settled"));
    }
    catch (BusinessRuleException ex)
    {
        logger.LogError(ex, "Error settling trade {TradeId}", trade.Id);
        return Results.BadRequest(ApiResponse<string>.Fail(ex.Message));
    }
})
.WithTags("Transactions");

app.MapPost("/api/wallet/transaction/trade", async (TradeRequest request, IWalletService walletService, ILogger<Program> logger, IConfiguration configuration) =>
{
    // Quarantine stub endpoint audit:C-8
    // Check if stub endpoint quarantine is enabled
    var quarantineEnabled = configuration.GetValue<bool>("FeatureFlags:QuarantineStubEndpoints", defaultValue: true);
    
    if (quarantineEnabled)
    {
        logger.LogWarning(
            "Stub endpoint quarantined — audit:C-8 | Endpoint: POST /api/wallet/transaction/trade | " +
            "UserId: {FromUserId}, ToUserId: {ToUserId}, Asset: {Asset}, Amount: {Amount}, ReferenceId: {ReferenceId}",
            request.FromUserId, request.ToUserId, request.Asset, request.Amount, request.ReferenceId);
        
        return Results.Json(new {
            error = "Not Implemented",
            message = "Stub endpoint quarantined. Implementation pending.",
            auditRef = "C-8"
        }, statusCode: 501);
    }
    
    // Production implementation (currently unreachable due to quarantine)
    try
    {
        var result = await walletService.MakeTradeAsync(request.FromUserId, request.ToUserId, request.Asset, request.Amount, request.ReferenceId);
        return Results.Ok(ApiResponse<WalletBallanceDTO>.Ok(result, "Operation completed successfully"));
    }
    catch (BusinessRuleException ex)
    {
        logger.LogError(ex, "Error in MakeTradeAsync for users {FromUserId} -> {ToUserId}", request.FromUserId, request.ToUserId);
        return Results.BadRequest(ApiResponse<WalletBallanceDTO>.Fail(ex.Message));
    }
})
.WithTags("Transactions");

// The withdraw, charge, transfer, internal/credit and internal/debit endpoints were commented
// out here rather than deleted. Their service methods still exist and are now unreachable —
// WalletService.WithdrawalAsync, ChargeWalletAsync, TransferAsync, DebitAsync. See audit finding
// N-3: TransferAsync in particular is not atomic and writes no audit trail, so none of them
// should be re-exposed as they stand.

app.MapGet("/api/wallet/transactions/{userId}", async (Guid userId, string? asset, IWalletService walletService) =>
{
    var transactions = await walletService.GetUserTransactionsAsync(userId, asset);
    return Results.Ok(transactions);
})
.WithTags("Transactions");

// Creates the wallets a new user starts with: Toman, gold, and the gold credit ledger.
// userId: User id.
// walletService: Wallet service.
// Returns: The wallets that were created.
// 200: Default wallets created.
// 400: Wallet creation failed.
app.MapGet("/api/wallet/create-default/{userId}", async (Guid userId, IWalletService walletService) =>
{
    try
    {
        var wallets = await walletService.CreateDefaultWalletsAsync(userId);
        return Results.Ok(ApiResponse<IEnumerable<WalletDTO>>.Ok(wallets, "کیف پول‌های پیش‌فرض با موفقیت ایجاد شدند"));
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<IEnumerable<WalletDTO>>.Fail(ex.Message));
    }
})
.WithTags("Wallets");

static string ResolveSharedConfigPath(Microsoft.Extensions.Hosting.IHostEnvironment environment, string fileName)
{
    var current = new System.IO.DirectoryInfo(environment.ContentRootPath);
    while (current is not null)
    {
        var candidate = System.IO.Path.Combine(current.FullName, "config", fileName);
        if (System.IO.File.Exists(candidate))
        {
            return candidate;
        }
        current = current.Parent;
    }

    throw new System.IO.FileNotFoundException($"Shared configuration '{fileName}' not found relative to '{environment.ContentRootPath}'.", fileName);
}


app.Run();

// Request models
public record WithdrawRequest(Guid UserId, string Asset, decimal Amount, string? ReferenceId = null);
public record ChargeRequest(Guid UserId, string Asset, decimal Amount, string? PaymentMethod = null);
public record TransferRequest(Guid FromUserId, Guid ToUserId, string Asset, decimal Amount);
public record CreditRequest(Guid UserId, string Asset, decimal Amount);
public record DebitRequest(Guid UserId, string Asset, decimal Amount);

// Market order balance request models
public record ValidateBalanceRequest(Guid UserId, string Asset, decimal Amount, OrderSide orderSide); // 0 = Buy, 1 = Sell
public record UpdateBalanceRequest(Guid UserId, string Asset, decimal Amount, OrderSide orderSide, Guid OrderId); // 0 = Buy, 1 = Sell