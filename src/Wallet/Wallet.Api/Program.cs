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

// Serilog before anything that can throw. This configuration reads nothing from the shared file
// — the sinks are fixed here — so it can be installed ahead of the file being located, which is
// what lets a configuration failure reach the rolling log rather than a console no Windows
// service has (issue #205).
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File(StartupLogging.LogFilePath("wallet-api-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
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
var walletConnectionString = ConfigurationGuard.RequireConnectionString(builder.Configuration, "WalletDb");

builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseSqlServer(walletConnectionString,
        b => b.MigrationsAssembly("Wallet.Api")));


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
.WithSummary("Get one asset's balance for a user")
.WithDescription(
    "Reads a single wallet row, matching the asset code case-insensitively. Unlike the deposit, " +
    "lock and settlement paths, this one does not create a missing wallet: an asset the user has " +
    "never held answers 400 with «کیف پول پیدا نشد». Pass a credit ledger code such as CREDIT_MAUA " +
    "to read a credit ceiling rather than a balance.")
.WithTags("Balances");

app.MapGet("/api/wallet/balances/{userId}", async (Guid userId, IWalletService walletService) =>
{
    var wallets = await walletService.GetUserWalletsAsync(userId);
    return Results.Ok(ApiResponse<IEnumerable<WalletDTO>>.Ok(wallets, "لیست کیف پول های کاربر"));
})
.WithSummary("List every wallet a user holds")
.WithDescription(
    "One row per asset, ordered by asset code, with the CREDIT_<ASSET> credit ledgers among them. " +
    "A normally registered user has at least the three rows create-default seeds (IRT, MAUA, " +
    "CREDIT_MAUA); every other asset's wallet appears only once something writes to it. An empty " +
    "list is therefore not a normal answer for a registered user — it means the create-default " +
    "call during registration did not land, which Users.Api logs but does not treat as a failed " +
    "registration.")
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
.WithSummary("Credit a user's wallet")
.WithDescription(
    "Adds to the balance and records a Deposit transaction. Idempotent on ReferenceId (issue #157): " +
    "a reference already applied to that wallet moves nothing, returns the transaction the first " +
    "call produced and reports WasAlreadyApplied, so an admin top-up re-sent after a lost reply " +
    "cannot credit twice. A request without a ReferenceId is never deduplicated. The wallet is " +
    "created on first deposit when the asset is one the platform knows; an unrecognised asset code " +
    "answers 400 with «کیف پول وجود ندارد».")
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
.WithSummary("Debit a user's wallet")
.WithDescription(
    "Subtracts from the balance and records a Withdraw transaction, under the same ReferenceId " +
    "idempotency contract as deposit — the repeat is checked before the balance is touched, so a " +
    "re-sent deduction reports the original success rather than «مقدار کسر از حساب بیشتر از حد مجاز است». " +
    "This path will not take a balance below zero. That is unrelated to the credit ceiling, which " +
    "governs trading rather than admin deductions.")
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
.WithSummary("Reserve funds against an open order")
.WithDescription(
    "Moves the amount out of Balance and into LockedBalance. It deliberately enforces no " +
    "sufficiency rule, and must not: customers trade on credit and are expected to go negative " +
    "down to a ceiling held in a separate CREDIT_<ASSET> wallet row, which a single-asset write " +
    "cannot see, so that check belongs in the caller that can read both rows. What it does check " +
    "is the asset — an unrecognised code answers 400 with «کیف پول پیدا نشد», while a known one " +
    "with no wallet yet has that wallet created here — and the amount, which has to be positive, " +
    "since a negative one would mint money by running the arithmetic backwards. Takes no reference " +
    "and is therefore not idempotent; a repeat locks again. Retries internally when it loses an " +
    "optimistic-concurrency race.")
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
.WithSummary("Release funds reserved against an order")
.WithDescription(
    "Returns the amount from LockedBalance to Balance when an order is cancelled or ends. Unlike " +
    "lockBalance, this one is guarded against the stored LockedBalance: releasing more than is " +
    "locked would raise Balance while driving LockedBalance negative — money from nothing — so it " +
    "is refused with 400 naming both figures (issue #52). A negative amount is refused earlier and " +
    "less gracefully, as an unhandled ArgumentOutOfRangeException that surfaces as 500 rather than " +
    "400. Takes no reference and is not idempotent: a repeat releases the amount again if that " +
    "much is still locked.")
.WithTags("Locks");

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
.WithSummary("Settle a matched trade")
.WithDescription(
    "Called by the Orders outbox once a match is recorded. In one database transaction it consumes " +
    "both sides' locked collateral, credits each side, and writes four Transaction rows and one " +
    "TradeSettlement row. Exactly-once is guaranteed by the TradeSettlements primary key rather " +
    "than by the order the code runs in, so outbox redelivery is safe: an already-settled trade " +
    "answers success and moves nothing. Consuming locked collateral does not return it to Balance, " +
    "which is what lets a credit-backed position stay legitimately negative. It refuses a " +
    "self-trade, refuses a non-zero fee — fees are 0.00 by design and a collected fee is credited " +
    "to no account (issue #35) — and refuses to settle unless both sides' collateral is actually " +
    "locked.")
.WithTags("Transactions");

app.MapGet("/api/wallet/transactions/{userId}", async (Guid userId, string? asset, IWalletService walletService) =>
{
    var transactions = await walletService.GetUserTransactionsAsync(userId, asset);
    return Results.Ok(transactions);
})
.WithSummary("List a user's wallet transactions — currently always empty")
.WithDescription(
    "Reads the legacy WalletTransactions table, which no code writes to any more: deposits, " +
    "withdrawals, locks, unlocks and settlement all record their history in the Transactions " +
    "table instead. This endpoint therefore answers an empty list for every user, however much " +
    "they have traded — verified by depositing and reading it back. Do not build on it until that " +
    "is repaired; it is described here as it behaves rather than as its name suggests. Takes an " +
    "optional `asset` query parameter and answers with the bare list rather than the ApiResponse " +
    "envelope the other endpoints use.")
.WithTags("Transactions");

// POST, not GET: this creates rows, and GET is the verb every intermediary assumes it may
// retry or prefetch freely. Issue #206.
// Nothing on this path throws BusinessRuleException, so the catch that used to sit here could
// never run and its "400" documented a status the endpoint cannot return. A failure now reaches
// GlobalExceptionHandler, which logs it with its own type and a trace id. Issue #210.
app.MapPost("/api/wallet/create-default/{userId}", async (Guid userId, IWalletService walletService) =>
{
    var wallets = await walletService.CreateDefaultWalletsAsync(userId);
    return Results.Ok(ApiResponse<IEnumerable<WalletDTO>>.Ok(wallets, "کیف پول‌های پیش‌فرض با موفقیت ایجاد شدند"));
})
.WithSummary("Create a new user's default wallets")
.WithDescription(
    "Creates the three wallets a registration starts from: Toman (IRT), melted gold (MAUA) and the " +
    "gold credit ledger (CREDIT_MAUA). Every other asset's wallet — the other assets' CREDIT_ " +
    "ledgers included — is created lazily on first write instead, so this is not a full account " +
    "set-up. Safe to repeat: a wallet that already exists is returned as it stands, neither " +
    "duplicated nor reset. Called by Users.Api during registration, where a failure here is logged " +
    "but does not fail the registration.")
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