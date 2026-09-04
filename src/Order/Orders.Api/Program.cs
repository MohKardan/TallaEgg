using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Orders.Application;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using Serilog;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TallaEgg.Core;
using TallaEgg.Core.Cors;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.ErrorHandling;
using TallaEgg.Core.Responses.Order;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using CancelActiveOrdersResponseDto = TallaEgg.Core.DTOs.Order.CancelActiveOrdersResponseDto;

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
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File(StartupLogging.LogFilePath("orders-api-.log"), rollingInterval: RollingInterval.Day, retainedFileCountLimit: 30)
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
// startup only because the migration block further down happens to resolve the context. Reorder
// or guard that block and the same mistake would surface at the first request instead (#205).
var ordersConnectionString = ConfigurationGuard.RequireConnectionString(builder.Configuration, "OrdersDb");

builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(ordersConnectionString,
        b => b.MigrationsAssembly("Orders.Infrastructure"))
    .LogTo(Console.WriteLine, LogLevel.None)); // Disable all EF Core logging

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

// Add services to the container.
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ITradeRepository, TradeRepository>();
builder.Services.AddScoped<OrderMatchingRepository>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<TradeService>();
// UsersApiClient is scoped, so its constructor — which reads UsersApiUrl — would not run until
// the first request. Reading the key here as well makes an absent one stop the service coming
// up, which is where a configuration mistake belongs (issue #205).
ConfigurationGuard.RequireUri(builder.Configuration, "UsersApiUrl");
builder.Services.AddScoped<UsersApiClient>();


// Add Wallet API Client. No configure delegate: WalletApiClient's constructor reads
// WalletApiUrl and assigns BaseAddress itself, so anything set here would be overwritten a
// moment later — which is what made the old fallback on this line unreachable twice over.
// The read below is what the delegate could not be: the typed client is transient, so its
// constructor runs on first resolution, and only reading here fails a missing key at startup.
ConfigurationGuard.RequireUri(builder.Configuration, "WalletApiUrl");
builder.Services.AddHttpClient<TallaEgg.Infrastructure.Clients.IWalletApiClient, TallaEgg.Infrastructure.Clients.WalletApiClient>();

// CORS — issue #31: a whitelist read from configuration, not AllowAnyOrigin.
builder.Services.AddTallaEggCors(builder.Configuration);

builder.Services.AddTallaEggErrorHandling();

// Matching engine — a single instance (issue #53). The reasoning lives in
// MatchingEngineRegistration, which the tests call too.
builder.Services.AddMatchingEngine();

// Residual collateral release (issue #52). Both OrderService, on cancellation, and
// OutboxProcessorService, after settlement, share this one instance so the "how much is still
// locked" formula is defined in exactly one place.
builder.Services.AddScoped<Orders.Application.Services.OrderCollateralReconciler>();

// Dealer model (issue #48): the admin publishes a price and the customer trades against it,
// with no orders resting in the book.
builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();
builder.Services.AddScoped<Orders.Core.IPendingQuoteRepository, Orders.Infrastructure.PendingQuoteRepository>();
builder.Services.AddScoped<Orders.Application.Services.MarketModeProvider>();
builder.Services.AddScoped<Orders.Application.Services.QuoteFillService>();
builder.Services.AddScoped<Orders.Application.Services.MarketModeStartupValidator>();
builder.Services.AddScoped<Orders.Application.Services.PositionService>();

// Names this instance and lets the background loops agree on which of them runs (issue #160).
// Registered before the hosted services below, all three of which depend on it.
builder.Services.AddInstanceCoordination();

// Outbox processor: reliably delivers trade settlements to the Wallet service.
builder.Services.AddHostedService<Orders.Application.Services.OutboxProcessorService>();

// Automated quote publishing (issue #90): fetches a live reference price (gold, coin, or
// Bitcoin — see Orders.Core.IReferencePriceProvider) and publishes a quote the same way an
// admin does by hand. Off for a symbol until an admin turns it on via the bot.
// A named HttpClient per provider rather than AddHttpClient<TInterface,TImplementation>: two
// implementations share IReferencePriceProvider. Each now takes IConfiguration directly (not
// just its own token/key) so it can also resolve a config-driven instrument mapping for a
// symbol added without a code change — see NerkhPriceProvider/BrsApiPriceProvider.InstrumentFor.
builder.Services.AddHttpClient("NerkhPriceProvider");
builder.Services.AddHttpClient("BrsApiPriceProvider");

builder.Services.AddScoped<Orders.Core.IAutoQuoteSettingsRepository, Orders.Infrastructure.AutoQuoteSettingsRepository>();

// Per-symbol enable/disable, changeable by an admin bot command — not a const needing a rebuild.
builder.Services.AddScoped<Orders.Core.ISymbolSettingsRepository, Orders.Infrastructure.SymbolSettingsRepository>();

builder.Services.AddScoped<Orders.Core.IReferencePriceProvider>(sp => new Orders.Infrastructure.Clients.NerkhPriceProvider(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("NerkhPriceProvider"),
    sp.GetRequiredService<ILogger<Orders.Infrastructure.Clients.NerkhPriceProvider>>(),
    sp.GetRequiredService<IConfiguration>()));

builder.Services.AddScoped<Orders.Core.IReferencePriceProvider>(sp => new Orders.Infrastructure.Clients.BrsApiPriceProvider(
    sp.GetRequiredService<IHttpClientFactory>().CreateClient("BrsApiPriceProvider"),
    sp.GetRequiredService<ILogger<Orders.Infrastructure.Clients.BrsApiPriceProvider>>(),
    sp.GetRequiredService<IConfiguration>()));

builder.Services.AddScoped<Orders.Application.Services.ReferencePriceProviderChain>();
builder.Services.AddHostedService<Orders.Application.Services.AutoQuotePublisherService>();

// Configure JSON serialization
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
    options.SerializerOptions.PropertyNamingPolicy = null;
});

// Add Swagger/OpenAPI support
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "TallaEgg Orders API", 
        Version = "v1",
        Description = "API for managing trading orders in the TallaEgg platform",
        Contact = new OpenApiContact
        {
            Name = "TallaEgg Development Team",
            Email = "dev@tallaegg.com"
        }
    });

    // Include XML comments for documentation
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
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
    var context = services.GetRequiredService<OrdersDbContext>();
    await context.Database.MigrateAsync(); // اجرای مایگریشن‌ها

    // Says who this instance is before anything it writes to the database carries that name
    // (issue #160). Running a second Orders.Api is supported now, but it is rarely intended here:
    // this line and the follower warnings from the background loops are what make it visible
    // instead of leaving the constraint in a code comment nobody deploying will read.
    Log.Information("Orders.Api instance identity is {InstanceId}. Background loops coordinate through the ServiceLeases table.",
        services.GetRequiredService<InstanceIdentity>().Value);

    // A symbol with an active quote but not in dealer mode means the admin's published price and
    // the configuration disagree. Logged only; it does not stop the service (issue #73).
    var marketModeValidator = services.GetRequiredService<Orders.Application.Services.MarketModeStartupValidator>();
    await marketModeValidator.ValidateAsync();
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
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TallaEgg Orders API V1");
        c.RoutePrefix = "api-docs";
    });
}

// Which build this service is running (issue #218) — the question a deployment leaves behind,
// answered without RDP and a file-properties dialog. Deliberately not AllowAnonymous: the
// Production fallback policy applies, so the caller needs the same X-API-Key as every other
// endpoint. The commit hash names an exact line of a public repository, and the operator asking
// the question already holds the key.
app.MapGet("/version", () => Results.Ok(ApiResponse<BuildVersionDto>.Ok(BuildVersion.Current)))
   .WithTags("Diagnostics");


// Order management endpoints

// ──────────────────────────── Dealer model (issue #48) ────────────────────────────

// Publishes the admin's quote for a symbol. Places no order in the book and locks no collateral.
// The symbol's previous quote is deactivated atomically.
app.MapPost("/api/quotes", async (
    PublishQuoteRequest request,
    IQuoteRepository quotes,
    Orders.Core.IPendingQuoteRepository pendingQuotes) =>
{
    try
    {
        // The plausibility band applies to a hand-typed price too, not only to the feed
        // (issue #158). It began as a guard on the price source, on the theory that the machine
        // was what could produce a wrong number; an admin typing one zero too many does the same
        // damage, and a manual quote is the more dangerous of the two — it is tradeable the
        // instant it lands and nobody is watching a log for it.
        var current = await quotes.GetActiveAsync(request.Symbol);
        var proposedMid = QuotePlausibility.MidOf(request.BuyPrice, request.SellPrice);
        var verdict = QuotePlausibility.Check(proposedMid, current);

        if (!verdict.IsWithinBand)
        {
            // Held, not refused: the admin may well mean it, and a real market can move more
            // than the band in a day. They are asked to confirm rather than told no.
            var held = await pendingQuotes.ProposeAsync(Orders.Core.PendingQuote.Propose(
                request.Symbol, request.BuyPrice, request.SellPrice,
                verdict.PreviousMid, verdict.DeviationPercent,
                Orders.Core.QuoteSource.Manual, request.PublishedByUserId));

            Log.Warning(
                "Manual quote for {Symbol} held for approval: buy {BuyPrice}, sell {SellPrice} is {Deviation}% " +
                "from the last published mid {PreviousMid}, outside ±{Band}%.",
                request.Symbol, request.BuyPrice, request.SellPrice,
                decimal.Round(verdict.DeviationPercent, 4), verdict.PreviousMid,
                QuotePlausibility.MaxDeviationPercent);

            return Results.Ok(ApiResponse<PublishQuoteResult>.Ok(
                new PublishQuoteResult { Pending = ToPendingQuoteDto(held) },
                "این مظنه با مظنهٔ فعلی اختلاف زیادی دارد و نیاز به تأیید دارد."));
        }

        var quote = Quote.Publish(request.Symbol, request.BuyPrice, request.SellPrice, request.PublishedByUserId);
        var published = await quotes.PublishAsync(quote);

        return Results.Ok(ApiResponse<PublishQuoteResult>.Ok(
            new PublishQuoteResult { Published = ToQuoteDto(published) }, "مظنه منتشر شد."));
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<PublishQuoteResult>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error publishing quote for {Symbol}", request.Symbol);
        return Results.BadRequest(ApiResponse<PublishQuoteResult>.Fail("خطا در انتشار مظنه."));
    }
})
.WithTags("Quotes");

// Quotes the band held back and nobody has answered yet. The bot polls this to know what to ask
// about — the Orders service has no way to reach Telegram itself.
app.MapGet("/api/quotes/pending", async (Orders.Core.IPendingQuoteRepository pendingQuotes) =>
{
    var awaiting = await pendingQuotes.GetAwaitingApprovalAsync();

    return Results.Ok(ApiResponse<IEnumerable<PendingQuoteDto>>.Ok(
        awaiting.Select(ToPendingQuoteDto), "مظنه‌های در انتظار تأیید"));
})
.WithTags("Quotes");

// An admin approving a held quote: it is published now, under the identity of whoever proposed it.
app.MapPost("/api/quotes/pending/{id}/approve", async (
    Guid id,
    ResolvePendingQuoteRequest request,
    Orders.Core.IPendingQuoteRepository pendingQuotes) =>
{
    try
    {
        var pending = await pendingQuotes.GetAsync(id);
        if (pending is null)
            return Results.BadRequest(ApiResponse<QuoteDto>.Fail("این مظنه پیدا نشد."));

        var published = await pendingQuotes.ApproveAsync(pending, request.AdminUserId);

        return Results.Ok(ApiResponse<QuoteDto>.Ok(ToQuoteDto(published), "مظنه تأیید و منتشر شد."));
    }
    catch (BusinessRuleException ex)
    {
        // Already answered, or past its window. Both are ordinary outcomes of a button sitting
        // in a Telegram message, and the message says which.
        return Results.BadRequest(ApiResponse<QuoteDto>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error approving pending quote {PendingQuoteId}", id);
        return Results.BadRequest(ApiResponse<QuoteDto>.Fail("خطا در تأیید مظنه."));
    }
})
.WithTags("Quotes");

// An admin rejecting a held quote: nothing is published and the previous quote stands.
app.MapPost("/api/quotes/pending/{id}/reject", async (
    Guid id,
    ResolvePendingQuoteRequest request,
    Orders.Core.IPendingQuoteRepository pendingQuotes) =>
{
    try
    {
        var pending = await pendingQuotes.GetAsync(id);
        if (pending is null)
            return Results.BadRequest(ApiResponse<string>.Fail("این مظنه پیدا نشد."));

        await pendingQuotes.RejectAsync(pending, request.AdminUserId);

        return Results.Ok(ApiResponse<string>.Ok(pending.Symbol, "مظنه رد شد و مظنهٔ قبلی برقرار ماند."));
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<string>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error rejecting pending quote {PendingQuoteId}", id);
        return Results.BadRequest(ApiResponse<string>.Fail("خطا در رد مظنه."));
    }
})
.WithTags("Quotes");

// The active quote for a symbol.
app.MapGet("/api/quotes/{Base}/{Quote}", async (string Base, string Quote, IQuoteRepository quotes) =>
{
    var symbol = $"{Base}/{Quote}";
    var quote = await quotes.GetActiveAsync(symbol);

    return quote is null
        ? Results.NotFound(ApiResponse<QuoteDto>.Fail("مظنه‌ای منتشر نشده است."))
        : Results.Ok(ApiResponse<QuoteDto>.Ok(ToQuoteDto(quote)));
})
.WithTags("Quotes");

// ─────────────────────── Automatic quotes (issue #90) ───────────────────────

// A symbol's current automatic-quote settings.
app.MapGet("/api/autoquote-settings/{Base}/{Quote}", async (string Base, string Quote, Orders.Core.IAutoQuoteSettingsRepository settingsRepo) =>
{
    var symbol = $"{Base}/{Quote}";
    var settings = await settingsRepo.GetOrCreateAsync(symbol);

    return Results.Ok(ApiResponse<AutoQuoteSettingsDto>.Ok(ToAutoQuoteSettingsDto(settings)));
})
.WithTags("Auto-quote");

// Changes a symbol's automatic-quote spread.
app.MapPost("/api/autoquote-settings/{Base}/{Quote}/spread", async (
    string Base, string Quote, UpdateAutoQuoteSpreadRequest request, Orders.Core.IAutoQuoteSettingsRepository settingsRepo) =>
{
    var symbol = $"{Base}/{Quote}";

    try
    {
        var settings = await settingsRepo.GetOrCreateAsync(symbol);
        settings.UpdateSpread(request.SpreadPercent, request.UpdatedByUserId);
        await settingsRepo.SaveAsync(settings);

        return Results.Ok(ApiResponse<AutoQuoteSettingsDto>.Ok(ToAutoQuoteSettingsDto(settings), "اسپرد به‌روزرسانی شد."));
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<AutoQuoteSettingsDto>.Fail(ex.Message));
    }
})
.WithTags("Auto-quote");

// Turns a symbol's automatic quoting on or off.
app.MapPost("/api/autoquote-settings/{Base}/{Quote}/enabled", async (
    string Base, string Quote, SetAutoQuoteEnabledRequest request, Orders.Core.IAutoQuoteSettingsRepository settingsRepo) =>
{
    var symbol = $"{Base}/{Quote}";

    var settings = await settingsRepo.GetOrCreateAsync(symbol);
    settings.SetEnabled(request.IsEnabled, request.UpdatedByUserId);
    await settingsRepo.SaveAsync(settings);

    return Results.Ok(ApiResponse<AutoQuoteSettingsDto>.Ok(ToAutoQuoteSettingsDto(settings),
        request.IsEnabled ? "مظنهٔ اتومات روشن شد." : "مظنهٔ اتومات خاموش شد."));
})
.WithTags("Auto-quote");

static AutoQuoteSettingsDto ToAutoQuoteSettingsDto(Orders.Core.AutoQuoteSettings s) => new()
{
    Symbol = s.Symbol,
    SpreadPercent = s.SpreadPercent,
    IsEnabled = s.IsEnabled,
    UpdatedAt = s.UpdatedAt
};

// ─────────────────────── Symbol enable/disable ─────────────────────────

// The symbols currently tradable — the bot uses this for its symbol menu.
app.MapGet("/api/symbols/active", async (Orders.Core.ISymbolSettingsRepository settingsRepo) =>
{
    var symbols = await settingsRepo.GetActiveSymbolsAsync();
    return Results.Ok(ApiResponse<List<string>>.Ok(symbols.ToList()));
})
.WithTags("Symbols");

// Enables or disables a symbol.
app.MapPost("/api/symbols/{Base}/{Quote}/active", async (
    string Base, string Quote, SetSymbolActiveRequest request, Orders.Core.ISymbolSettingsRepository settingsRepo) =>
{
    var symbol = $"{Base}/{Quote}";

    var settings = await settingsRepo.GetOrCreateAsync(symbol);
    settings.SetActive(request.IsActive, request.UpdatedByUserId);
    await settingsRepo.SaveAsync(settings);

    return Results.Ok(ApiResponse<SymbolSettingsDto>.Ok(ToSymbolSettingsDto(settings),
        request.IsActive ? "نماد فعال شد." : "نماد غیرفعال شد."));
})
.WithTags("Symbols");

static SymbolSettingsDto ToSymbolSettingsDto(Orders.Core.SymbolSettings s) => new()
{
    Symbol = s.Symbol,
    IsActive = s.IsActive,
    UpdatedAt = s.UpdatedAt
};

// The mapping lives here rather than on the DTO: TallaEgg.Core does not reference Orders.Core and
// should not — the DTO is shared between services and must not depend on one service's domain model.
static QuoteDto ToQuoteDto(Quote q) => new()
{
    Id = q.Id,
    Symbol = q.Symbol,
    BuyPrice = q.BuyPrice,
    SellPrice = q.SellPrice,
    PublishedAt = q.PublishedAt,
    IsActive = q.IsActive,
    DeactivatedAt = q.DeactivatedAt
};

// Same reasoning as ToQuoteDto: the band and the lifetime are domain constants, and the bot needs
// both to write a message that states the rule as well as the breach.
static PendingQuoteDto ToPendingQuoteDto(Orders.Core.PendingQuote p) => new()
{
    Id = p.Id,
    Symbol = p.Symbol,
    BuyPrice = p.BuyPrice,
    SellPrice = p.SellPrice,
    ProposedMid = p.ProposedMid,
    PreviousMid = p.PreviousMid,
    DeviationPercent = p.DeviationPercent,
    BandPercent = QuotePlausibility.MaxDeviationPercent,
    Source = p.Source.ToString(),
    CreatedAt = p.CreatedAt,
    ExpiresAt = p.CreatedAt + Orders.Core.PendingQuote.Lifetime
};

// Published quotes for a symbol, newest first, including ones already replaced.
//
// This is what the bot's quote history reads. It replaced order history in the customer
// menu: in the dealer model an order lives only for the instant of a fill, so a customer's
// order list was always either empty or entirely completed rows — nothing to act on. The
// prices the shop published are the history that means something.
app.MapGet("/api/quotes/{Base}/{Quote}/history", async (
    string Base, string Quote, IQuoteRepository quotes, int page = 1, int size = 5) =>
{
    var symbol = $"{Base}/{Quote}";
    var (items, total) = await quotes.GetHistoryAsync(symbol, page, size);

    var result = new PagedResult<QuoteDto>
    {
        Items = items.Select(ToQuoteDto).ToList(),
        TotalCount = total,
        PageNumber = page,
        PageSize = size
    };

    return Results.Ok(ApiResponse<PagedResult<QuoteDto>>.Ok(result));
})
.WithTags("Quotes");

// A customer fills a quote: two orders for exactly the requested quantity are created, locked and
// matched immediately. The customer does not enter a price.
app.MapPost("/api/quotes/accept", async (AcceptQuoteRequest request, QuoteFillService fillService) =>
{
    var (success, message, trade) = await fillService.AcceptQuoteAsync(
        request.UserId, request.Symbol, request.Side, request.Quantity);

    return success
        ? Results.Ok(ApiResponse<Guid?>.Ok(trade?.Id, message))
        : Results.BadRequest(ApiResponse<Guid?>.Fail(message));
})
.WithTags("Quotes");

// ─────────────────────────────────────────────────────────────────────────────────

// Creates a single order of any type (limit or market), determining maker/taker automatically.
// request: Order creation request.
// orderService: Order service.
// Returns: The order, its role, and any trades executed.
// 200: Order created.
// 400: Invalid data, or a business rule was violated.
// 401: Unauthorized.
app.MapPost("/api/orders", async (TallaEgg.Core.DTOs.Order.OrderDto request, OrderService orderService) =>
{
    try
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Symbol))
            return Results.BadRequest(new { success = false, message = "نماد معاملاتی الزامی است" });
        
        if (request.Quantity <= 0)
            return Results.BadRequest(new { success = false, message = "مقدار سفارش باید بیشتر از صفر باشد" });
        
        if (request.Price <= 0)
            return Results.BadRequest(new { success = false, message = "قیمت برای سفارش محدود الزامی است" });

        var response = await orderService.CreateOrderAsync(request);
        
        return Results.Ok(ApiResponse<CreateOrderResponse>.Ok(response, response.Message));
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Json(ApiResponse<CreateOrderResponse>.Fail(ex.Message), statusCode: 401);
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(ApiResponse<CreateOrderResponse>.Fail(ex.Message));
    }
})
.WithName("CreateOrder")
.WithSummary("Create an order")
.WithDescription("Creates a limit or market order, determining the maker/taker role automatically.")
.WithTags("Orders")
.Produces<ApiResponse<CreateOrderResponse>>(200)
.ProducesValidationProblem(400);

// Returns an order by id.
// orderId: Order id.
// orderService: Order service.
// Returns: The order, if found.
// 200: Order found.
// 404: Order not found.
app.MapGet("/api/orders/{orderId}", async (Guid orderId, OrderService orderService) =>
{
    var order = await orderService.GetOrderByIdAsync(orderId);
    if (order == null)
        return Results.NotFound(new { success = false, message = $"سفارش با شناسه {orderId} یافت نشد" });

    return Results.Ok(new { success = true, data = order });
})
.WithName("GetOrderById")
.WithSummary("Get an order by id")
.WithTags("Orders")
.Produces(200)
.Produces(404);

// Cancels an order.
// orderId: Order id.
// reason: Optional cancellation reason.
// orderService: Order service.
// Returns: The result of the cancellation.
// 200: Order cancelled.
// 400: Cancellation failed or was not valid.
// 404: Order not found.
app.MapPost("/api/orders/{orderId}/cancel", async (Guid orderId, string? reason, OrderService orderService) =>
{
    try
    {
        var success = await orderService.CancelOrderAsync(orderId, reason);
        if (!success)
            return Results.NotFound(new { success = false, message = $"سفارش با شناسه {orderId} یافت نشد یا قابل لغو نیست" });

        return Results.Ok(new { success = true, message = "سفارش با موفقیت لغو شد", orderId });
    }
    catch (BusinessRuleException ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
})
.WithName("CancelOrder")
.WithSummary("Cancel an order")
.WithTags("Orders")
.Produces(200)
.Produces(400)
.Produces(404);

// Cancels all of a user's active orders.
// userId: User id.
// reason: Optional cancellation reason.
// orderService: Order service.
// Returns: How many orders were cancelled.
// 200: Orders cancelled.
// 400: Cancelling the orders failed.
// Finds the user's active orders, cancels them with the supplied reason, and returns how many were
// cancelled, wrapped in the standard ApiResponse shape.
app.MapPost("/api/orders/user/{userId}/cancel-active", async (Guid userId, string? reason, OrderService orderService) =>
{
    var cancelledCount = await orderService.CancelAllActiveOrdersByUserIdAsync(userId, reason ?? "لغو همه سفارشات فعال");
    
    var response = new CancelActiveOrdersResponseDto { CancelledCount = cancelledCount };
    
    return Results.Ok(ApiResponse<CancelActiveOrdersResponseDto>.Ok(response, $"{cancelledCount} سفارش فعال لغو شد"));
})
.WithName("CancelUserActiveOrders")
.WithSummary("Cancel every active order of a user")
.WithTags("Orders")
.Produces(200)
.Produces(400);

// Confirms an order, moving it from Pending to Confirmed.
// orderId: Order id.
// orderService: Order service.
// Returns: The result of the confirmation.
// 200: Order confirmed.
// 400: Order cannot be confirmed.
// 404: Order not found.
app.MapPost("/api/orders/{orderId}/confirm", async (Guid orderId, OrderService orderService) =>
{
    try
    {
        var success = await orderService.ConfirmOrderIfPendingAsync(orderId);
        
        if (!success)
        {
            return Results.BadRequest(new { 
                success = false, 
                message = $"سفارش با شناسه {orderId} یافت نشد یا در وضعیت Pending نیست" 
            });
        }

        return Results.Ok(new { 
            success = true, 
            message = "سفارش با موفقیت تایید شد", 
            orderId,
            newStatus = "Confirmed"
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Unhandled error confirming order {OrderId}.", orderId);
        return Results.Json(new { 
            success = false, 
            message = "خطای داخلی سرور در تایید سفارش" 
        }, statusCode: 500);
    }
})
.WithName("ConfirmOrder")
.WithSummary("Confirm an order")
.WithDescription("Moves an order from Pending to Confirmed without losing concurrency safety.")
.WithTags("Orders")
.Produces(200)
.Produces(400)
.Produces(404);

// Returns a user's orders, paginated.
// userId: User id.
// pageNumber: Page number, default 1.
// pageSize: Items per page, default 10, maximum 100.
// orderService: Order service.
// Returns: A page of the user's orders.
// 200: Orders returned.
// 400: Invalid request parameters.
app.MapGet("/api/orders/user/{userId}", async (
    Guid userId,
    int? pageNumber,
    int? pageSize,
    OrderService orderService) =>
{
    // Validation
    var page = pageNumber ?? 1;
    var size = Math.Clamp(pageSize ?? 10, 1, 100);

    if (page < 1)
        return Results.BadRequest(new { success = false, message = "شماره صفحه باید بیشتر از صفر باشد" });

    var orders = await orderService.GetOrdersByUserIdAsync(userId, page, size);
    return Results.Ok(ApiResponse<PagedResult<OrderHistoryDto>>.Ok(orders, "سفارشات دریافت شد"));
})
.WithName("GetUserOrders")
.WithSummary("Get a user's orders")
.WithTags("Orders")
.Produces<ApiResponse<PagedResult<OrderHistoryDto>>>(200)
.Produces(400);

// Returns a user's trades, paginated.
// userId: User id.
// pageNumber: Page number, default 1.
// pageSize: Items per page, default 10, maximum 100.
// tradeService: Trade service.
// Returns: A page of the user's trades.
// 200: Trades returned.
// 400: Invalid request parameters.
app.MapGet("/api/trades/user/{userId}", async (
    Guid userId,
    int? pageNumber,
    int? pageSize,
    TradeService tradeService) =>
{
    // Validation
    var page = pageNumber ?? 1;
    var size = Math.Clamp(pageSize ?? 10, 1, 100);

    if (page < 1)
        return Results.BadRequest(new { success = false, message = "شماره صفحه باید بیشتر از صفر باشد" });

    var trades = await tradeService.GetTradesByUserIdAsync(userId, page, size);
    return Results.Ok(ApiResponse<PagedResult<TradeHistoryDto>>.Ok(trades, "معاملات دریافت شد"));
})
.WithName("GetUserTrades")
.WithSummary("Get a user's trades")
.WithTags("Trades")
.Produces<ApiResponse<PagedResult<TradeHistoryDto>>>(200)
.Produces(400);

// A user's position and profit/loss across every symbol they have traded (issue #93). The same
// endpoint serves the admin/SuperAdmin — they are the counterparty to every quote fill, so the
// shop's profit and loss is this same calculation run against the admin's own user id.
app.MapGet("/api/positions/user/{userId}", async (
    Guid userId,
    Orders.Application.Services.PositionService positionService) =>
{
    var positions = await positionService.GetPositionsAsync(userId);
    return Results.Ok(ApiResponse<PositionsResponseDto>.Ok(positions, "سود و زیان دریافت شد"));
})
.WithName("GetUserPositions")
.WithSummary("Get a user's position and profit/loss across every symbol")
.WithTags("Positions")
.Produces<ApiResponse<PositionsResponseDto>>(200)
.Produces(500);

// Returns a user's active orders.
// userId: User id.
// orderService: Order service.
// Returns: The user's active orders.
// 200: Active orders returned.
// 400: Invalid request.
app.MapGet("/api/orders/active/user/{userId}", async (
    Guid userId,
    OrderService orderService) =>
{
    var orders = await orderService.GetActiveOrdersByUserIdAsync(userId);
    var orderDtos = orders.Select(o => new OrderHistoryDto
    {
        Id = o.Id,
        Asset = o.Asset,
        Amount = o.Amount,
        RemainingAmount = o.RemainingAmount,
        Price = o.Price,
        Type = o.Side,
        Status = o.Status,
        TradingType = o.TradingType,
        Role = o.Role,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt,
        Notes = o.Notes,
        ParentOrderId = o.ParentOrderId
    }).ToList();

    return Results.Ok(ApiResponse<List<OrderHistoryDto>>.Ok(orderDtos, "سفارشات فعال دریافت شد"));
})
.WithName("GetUserActiveOrders")
.WithSummary("Get a user's active orders")
.WithTags("Orders")
.Produces<ApiResponse<List<OrderHistoryDto>>>(200)
.Produces(400);

// Returns every active order in the system, for admins.
// orderService: Order service.
// Returns: All active orders.
// 200: All active orders returned.
// 500: Internal server error.
app.MapGet("/api/orders/active/all", async (OrderService orderService) =>
{
    var orders = await orderService.GetAllActiveOrdersAsync();
    var orderDtos = orders.Select(o => new OrderHistoryDto
    {
        Id = o.Id,
        Asset = o.Asset,
        Amount = o.Amount,
        RemainingAmount = o.RemainingAmount,
        Price = o.Price,
        Type = o.Side,
        Status = o.Status,
        TradingType = o.TradingType,
        Role = o.Role,
        CreatedAt = o.CreatedAt,
        UpdatedAt = o.UpdatedAt,
        Notes = o.Notes,
        ParentOrderId = o.ParentOrderId
    }).ToList();

    return Results.Ok(ApiResponse<List<OrderHistoryDto>>.Ok(orderDtos, "تمام سفارشات فعال دریافت شد"));
})
.WithName("GetAllActiveOrders")
.WithSummary("Get every active order")
.WithTags("Orders")
.Produces<ApiResponse<List<OrderHistoryDto>>>(200)
.Produces(500);

// Returns the best bid and ask prices.
// symbol: Trading symbol, for example MAUA/IRT.
// tradingType: Trading type, default standard.
// orderService: Order service.
// Returns: The best bid and ask prices.
// 200: Best prices returned.
// 400: Invalid request.
// 404: Trading symbol not found.
// 500: Internal server error.
app.MapGet("/api/orders/{Base}/{Quote}/best-prices", async (
    string Base,
    string Quote,
    TradingType? tradingType,
    OrderService orderService) =>
{
    Log.Information(">--------------------- start best-prices ---------------------<");

    try
    {
        string symbol = $"{Base}/{Quote}";
        // Input validation
        if (string.IsNullOrWhiteSpace(symbol))
        {
            Log.Warning("Symbol is null or empty");

            return Results.BadRequest(ApiResponse<BestPricesDto>.Fail("نماد معاملاتی الزامی است."));
        }

        // Normalize symbol format (remove special characters, convert to uppercase)
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        Log.Information("Normalized symbol: {Symbol}", normalizedSymbol);

        // Validate symbol format (basic validation for trading pairs like BTC/USDT)
        if (!IsValidSymbolFormat(normalizedSymbol))
        {
            Log.Warning("Invalid symbol format: {Symbol}", normalizedSymbol);

            return Results.BadRequest(ApiResponse<BestPricesDto>.Fail("فرمت نماد معاملاتی نامعتبر است. (مثال صحیح: BTC/USDT)"));
        }

        Log.Information("befor Trading type: {TradingType}", tradingType);

        var type = tradingType ?? TradingType.Spot;

        Log.Information("after Trading type: {TradingType}", type);

        // Get best bid/ask prices
        var result = await orderService.GetBestBidAskAsync(normalizedSymbol, type);

        if (result == null)
        {
            Log.Warning("Symbol not found or market inactive: {Symbol}", normalizedSymbol);

            return Results.NotFound(ApiResponse<BestPricesDto>.Fail("نماد معاملاتی یافت نشد یا بازار برای این نماد فعال نیست."));
        }

        // Create response DTO
        var bestPricesDto = new BestPricesDto
        {
            Symbol = normalizedSymbol,
            BestBidPrice = result.BestBidPrice,
            BestAskPrice = result.BestAskPrice,
            BidVolume = result.BidVolume,
            AskVolume = result.AskVolume,
            Spread = result.BestAskPrice.HasValue && result.BestBidPrice.HasValue
                ? result.BestAskPrice.Value - result.BestBidPrice.Value
                : null,
            Timestamp = DateTime.UtcNow
        };

        Log.Information("\n" + JsonConvert.SerializeObject(bestPricesDto, Formatting.Indented));
        //Log.Information<BestPricesDto>("Retrieved best prices successfully", bestPricesDto);    

        return Results.Ok(ApiResponse<BestPricesDto>.Ok(bestPricesDto, "بهترین قیمت‌ها با موفقیت دریافت شد."));
    }
    catch (ArgumentException argEx)
    {
        Log.Error(argEx, "Invalid argument while getting best prices");

        return Results.BadRequest(ApiResponse<BestPricesDto>.Fail($"پارامتر نامعتبر: {argEx.Message}"));
    }
    catch (InvalidOperationException invOpEx)
    {
        Log.Error(invOpEx, "Service unavailable while getting best prices");

        return Results.Json(ApiResponse<BestPricesDto>.Fail("سرویس قیمت‌گذاری در حال حاضر در دسترس نیست."), statusCode: 503);
    }
    catch (TimeoutException)
    {
        Log.Error("Timeout occurred while getting best prices");

        return Results.Json(ApiResponse<BestPricesDto>.Fail("زمان انتظار درخواست به پایان رسید."), statusCode: 408);
    }
    catch (Exception ex)
    {
        
        Log.Error(ex, "Error getting best prices");

        return Results.Json(ApiResponse<BestPricesDto>.Fail("خطای داخلی سرور. لطفاً مجدداً تلاش کنید."), statusCode: 500);
    }
    finally
    {
        Log.Information(">--------------------- finally best-prices ---------------------<");
    }

})
.WithName("GetBestPrices")
.WithSummary("Get the best bid and ask prices")
.WithDescription("Returns the best bid and ask prices for the given trading symbol.")
.WithTags("Market Data")
.Produces<ApiResponse<BestPricesDto>>(200, "application/json")
.Produces<ApiResponse<BestPricesDto>>(400, "application/json")
.Produces<ApiResponse<BestPricesDto>>(404, "application/json")
.Produces<ApiResponse<BestPricesDto>>(408, "application/json")
.Produces<ApiResponse<BestPricesDto>>(500, "application/json")
.Produces<ApiResponse<BestPricesDto>>(503, "application/json");

// Helper method for symbol validation
static bool IsValidSymbolFormat(string symbol)
{
    if (string.IsNullOrWhiteSpace(symbol))
        return false;

    // Basic validation for trading pairs (e.g., BTC/IRT, SEKE_BAHAR/IRT). Underscores are
    // allowed within a segment — SEKE_BAHAR is a real platform symbol, not a typo — so this
    // must not be tightened back to letters-only without checking CurrenciesConstant first.
    return System.Text.RegularExpressions.Regex.IsMatch(symbol, @"^[A-Z][A-Z0-9_]{1,14}(/[A-Z][A-Z0-9_]{1,14})?$");
}

// Remove all other endpoints - keeping only the essential unified ones
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

// ─────────────────────────────────────────────────────────────────────────────
// Settlement reconciliation (issue #39)
//
// When an outbox settlement exhausts its retries it is marked Failed and abandoned,
// but the trade is already recorded and the participants' collateral stays locked.
// Nothing surfaced those trades and there was no way to settle them once the cause
// was fixed. These endpoints make stuck settlements visible and re-drivable.
//
// Re-driving is safe because settlement is idempotent on the trade id: a redundant
// delivery is a no-op rather than a double settlement.
// ─────────────────────────────────────────────────────────────────────────────

// Lists trades whose settlement never completed, newest first. Abandoned messages are
// excluded by default — an operator already reviewed and closed those — but stay
// queryable via includeAbandoned=true for reconciliation and audit.
app.MapGet("/api/outbox/unsettled", async (OrdersDbContext db, bool includeAbandoned = false) =>
{
    var query = db.OutboxMessages.AsNoTracking().Where(m => m.Status != OutboxMessageStatus.Completed);
    if (!includeAbandoned)
        query = query.Where(m => m.Status != OutboxMessageStatus.Abandoned);

    var stuck = await query
        .OrderByDescending(m => m.CreatedAt)
        .Select(m => new
        {
            m.Id,
            TradeId = m.AggregateId,
            m.Type,
            Status = m.Status.ToString(),
            m.RetryCount,
            m.CreatedAt,
            m.NextAttemptAt,
            m.LastError,
            m.AbandonReason,
            m.AbandonedAt,
            // Which instance is holding this message and until when (issue #160). A message that
            // looks stuck while an unexpired lease sits on it is being worked on right now; one
            // whose lease has passed was dropped by an instance that died mid-attempt.
            m.LeasedBy,
            m.LeaseExpiresAt
        })
        .ToListAsync();

    return Results.Ok(ApiResponse<object>.Ok(new
    {
        Count = stuck.Count,
        FailedCount = stuck.Count(s => s.Status == nameof(OutboxMessageStatus.Failed)),
        AbandonedCount = stuck.Count(s => s.Status == nameof(OutboxMessageStatus.Abandoned)),
        Items = stuck
    }, "فهرست تسویه‌های ناتمام"));
})
.WithTags("Outbox");

// Puts a permanently-failed settlement back in the queue after the cause has been fixed.
app.MapPost("/api/outbox/{messageId}/redrive", async (Guid messageId, OrdersDbContext db) =>
{
    try
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == messageId);
        if (message is null)
            return Results.NotFound(ApiResponse<string>.Fail("پیام یافت نشد."));

        message.ResetForRetry();
        await db.SaveChangesAsync();

        Log.Information("Outbox message {MessageId} (trade {TradeId}) was re-driven by an operator.",
            message.Id, message.AggregateId);

        return Results.Ok(ApiResponse<string>.Ok(message.AggregateId.ToString(),
            "پیام برای پردازش مجدد در صف قرار گرفت."));
    }
    catch (InvalidOperationException ex)
    {
        // Raised when the message is not in a re-drivable state (Completed or Pending).
        return Results.BadRequest(ApiResponse<string>.Fail(ex.Message));
    }
})
.WithTags("Outbox");

// Re-drives every failed settlement at once, for use after a fix that affects them all.
app.MapPost("/api/outbox/redrive-all-failed", async (OrdersDbContext db) =>
{
    var failed = await db.OutboxMessages
        .Where(m => m.Status == OutboxMessageStatus.Failed)
        .ToListAsync();

    foreach (var message in failed)
        message.ResetForRetry();

    await db.SaveChangesAsync();

    Log.Information("{Count} failed outbox message(s) were re-driven by an operator.", failed.Count);

    return Results.Ok(ApiResponse<int>.Ok(failed.Count,
        $"{failed.Count} تسویهٔ ناموفق دوباره در صف قرار گرفت."));
})
.WithTags("Outbox");

// Closes a permanently-failed settlement an operator has reviewed and decided will never
// settle (e.g. its collateral was consumed by later activity). The record is kept, not
// deleted, so the trade stays reconcilable — it just stops showing up as actionable and can
// never be re-driven again. No compensating wallet action is taken: for this shop, settling
// such a trade is a manual, offline decision, and the reason recorded here is that note.
app.MapPost("/api/outbox/{messageId}/abandon", async (Guid messageId, AbandonOutboxMessageRequest request, OrdersDbContext db) =>
{
    try
    {
        var message = await db.OutboxMessages.FirstOrDefaultAsync(m => m.Id == messageId);
        if (message is null)
            return Results.NotFound(ApiResponse<string>.Fail("پیام یافت نشد."));

        message.MarkAbandoned(request.Reason);
        await db.SaveChangesAsync();

        Log.Information(
            "Outbox message {MessageId} (trade {TradeId}) was abandoned by an operator: {Reason}",
            message.Id, message.AggregateId, request.Reason);

        return Results.Ok(ApiResponse<string>.Ok(message.AggregateId.ToString(),
            "معامله رها شد و دیگر برای تسویه پردازش نمی‌شود."));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ApiResponse<string>.Fail(ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        // Raised when the message is not in an abandonable state (only Failed qualifies).
        return Results.BadRequest(ApiResponse<string>.Fail(ex.Message));
    }
})
.WithTags("Outbox");

app.Run();

// Request models

/// <summary>
/// Request model for creating a new maker order
/// </summary>
public record CreateOrderRequest(
    string Asset, 
    decimal Amount, 
    decimal Price, 
    Guid UserId, 
    OrderSide Type,
    TradingType TradingType,
    string? Notes = null);

/// <summary>
/// Request model for creating a new limit order
/// </summary>
public record CreateLimitOrderRequest(
    string Symbol,
    decimal Quantity,
    decimal Price,
    Guid UserId);

/// <summary>
/// Request model for creating a new taker order
/// </summary>
public record CreateTakerOrderRequest(
    Guid ParentOrderId,
    decimal Amount,
    Guid UserId,
    string? Notes = null);

/// <summary>
/// Request model for updating order status
/// </summary>
public record UpdateOrderStatusRequest(
    OrderStatus Status, 
    string? Notes = null);

/// <summary>
/// Request model for cancelling an order
/// </summary>
public record CancelOrderRequest(
    string? Reason = null);

/// <summary>
/// Request model for abandoning a permanently-failed outbox message (issue #39). The reason
/// is mandatory — abandoning is an audited operator decision, not a silent drop.
/// </summary>
public record AbandonOutboxMessageRequest(string Reason);

/// <summary>
/// Request model for notifying the matching engine about a new order
/// </summary>
public record NotifyMatchingEngineRequest(
    Guid OrderId,
    string Asset,
OrderSide Type);







