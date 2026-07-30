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
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Responses.Order;
using TallaEgg.Infrastructure.Clients;
using TallaEgg.TelegramBot.Infrastructure.Clients;
using CancelActiveOrdersResponseDto = TallaEgg.Core.DTOs.Order.CancelActiveOrdersResponseDto;

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
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb") ??
        "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("Orders.Infrastructure"))
    .LogTo(Console.WriteLine, LogLevel.None)); // Disable all EF Core logging

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

// Add services to the container.
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ITradeRepository, TradeRepository>();
builder.Services.AddScoped<OrderMatchingRepository>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<TradeService>();
builder.Services.AddScoped<UsersApiClient>();


// Add Wallet API Client
builder.Services.AddHttpClient<TallaEgg.Infrastructure.Clients.IWalletApiClient, TallaEgg.Infrastructure.Clients.WalletApiClient>(client =>
{
    var walletApiUrl = builder.Configuration.GetValue<string>("WalletApiUrl") ?? "http://localhost:60933";
    client.BaseAddress = new Uri(walletApiUrl);
});

// اضافه کردن CORS
builder.Services.AddCors();


builder.Services.AddScoped<TallaEgg.Infrastructure.Clients.IWalletApiClient, TallaEgg.Infrastructure.Clients.WalletApiClient>();

// Add Matching Engine — یک نمونهٔ واحد (issue #53). جزئیات و دلیلش در
// MatchingEngineRegistration نوشته شده، که تست‌ها هم از همان استفاده می‌کنند.
builder.Services.AddMatchingEngine();

// آزادسازی باقی‌ماندهٔ وثیقه (issue #52). هم OrderService (هنگام لغو) و هم
// OutboxProcessorService (پس از تسویه) از همین یک نمونه استفاده می‌کنند تا فرمول
// «چقدر قفل مانده» فقط یک جا تعریف شده باشد.
builder.Services.AddScoped<Orders.Application.Services.OrderCollateralReconciler>();

// مدل مظنه‌ای (issue #48): ادمین قیمت منتشر می‌کند و مشتری روی همان قیمت معامله می‌کند،
// بدون اینکه سفارشی از قبل در دفتر بخوابد.
builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();
builder.Services.AddScoped<Orders.Application.Services.MarketModeProvider>();
builder.Services.AddScoped<Orders.Application.Services.QuoteFillService>();

// Outbox processor: reliably delivers trade settlements to the Wallet service.
builder.Services.AddHostedService<Orders.Application.Services.OutboxProcessorService>();

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

// پیکربندی Serilog برای لاگ‌نویسی روی فایل و کنسول و فیلتر EF Core
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", Serilog.Events.LogEventLevel.Warning)
    .WriteTo.Console()
    .WriteTo.File("logs/orders-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// --- مایگریشن و سیید اولیه ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<OrdersDbContext>();
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

// تنظیم CORS
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());



app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TallaEgg Orders API V1");
        c.RoutePrefix = "api-docs";
    });


// Order management endpoints

// ──────────────────────────── مدل مظنه‌ای (issue #48) ────────────────────────────

/// <summary>
/// انتشار مظنهٔ ادمین برای یک نماد. هیچ سفارشی در دفتر نمی‌گذارد و هیچ وثیقه‌ای قفل نمی‌کند.
/// مظنهٔ قبلی همان نماد به‌صورت اتمی غیرفعال می‌شود.
/// </summary>
app.MapPost("/api/quotes", async (PublishQuoteRequest request, IQuoteRepository quotes) =>
{
    try
    {
        var quote = Quote.Publish(request.Symbol, request.BuyPrice, request.SellPrice, request.PublishedByUserId);
        var published = await quotes.PublishAsync(quote);

        return Results.Ok(ApiResponse<QuoteDto>.Ok(ToQuoteDto(published), "مظنه منتشر شد."));
    }
    catch (ArgumentException ex)
    {
        // پیام‌های Quote.Publish برای کاربر نوشته شده‌اند (مثلاً اسپرد منفی)، پس عیناً
        // برگردانده می‌شوند و با یک متن عمومی جایگزین نمی‌گردند.
        return Results.BadRequest(ApiResponse<QuoteDto>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error publishing quote for {Symbol}", request.Symbol);
        return Results.BadRequest(ApiResponse<QuoteDto>.Fail("خطا در انتشار مظنه."));
    }
});

/// <summary>مظنهٔ فعال یک نماد.</summary>
app.MapGet("/api/quotes/{Base}/{Quote}", async (string Base, string Quote, IQuoteRepository quotes) =>
{
    var symbol = $"{Base}/{Quote}";
    var quote = await quotes.GetActiveAsync(symbol);

    return quote is null
        ? Results.NotFound(ApiResponse<QuoteDto>.Fail("مظنه‌ای منتشر نشده است."))
        : Results.Ok(ApiResponse<QuoteDto>.Ok(ToQuoteDto(quote)));
});

// نگاشت اینجاست و نه روی خود DTO: TallaEgg.Core به Orders.Core ارجاع ندارد و نباید
// داشته باشد — DTO مشترک بین سرویس‌هاست و نباید به مدل دامنهٔ یکی از آن‌ها وابسته شود.
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

/// <summary>
/// Published quotes for a symbol, newest first, including ones already replaced.
///
/// This is what the bot's quote history reads. It replaced order history in the customer
/// menu: in the dealer model an order lives only for the instant of a fill, so a customer's
/// order list was always either empty or entirely completed rows — nothing to act on. The
/// prices the shop published are the history that means something.
/// </summary>
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
});

/// <summary>
/// پذیرش مظنه توسط مشتری: دو سفارش دقیقاً به اندازهٔ مقدار درخواستی ساخته، قفل و بلافاصله
/// تطبیق داده می‌شوند. مشتری قیمت وارد نمی‌کند.
/// </summary>
app.MapPost("/api/quotes/accept", async (AcceptQuoteRequest request, QuoteFillService fillService) =>
{
    var (success, message, trade) = await fillService.AcceptQuoteAsync(
        request.UserId, request.Symbol, request.Side, request.Quantity);

    return success
        ? Results.Ok(ApiResponse<Guid?>.Ok(trade?.Id, message))
        : Results.BadRequest(ApiResponse<Guid?>.Fail(message));
});

// ─────────────────────────────────────────────────────────────────────────────────

/// <summary>
/// ایجاد سفارش واحد - پشتیبانی از تمام انواع سفارشات (Limit/Market) با تشخیص خودکار Maker/Taker
/// </summary>
/// <param name="request">درخواست ایجاد سفارش</param>
/// <param name="orderService">سرویس مدیریت سفارشات</param>
/// <returns>پاسخ جامع شامل سفارش، نقش، و معاملات اجرا شده</returns>
/// <response code="200">سفارش با موفقیت ایجاد شد</response>
/// <response code="400">داده‌های نامعتبر یا نقض قوانین تجاری</response>
/// <response code="401">دسترسی غیرمجاز</response>
app.MapPost("/api/orders", async (TallaEgg.Core.DTOs.Order.OrderDto request, OrderService orderService) =>
{
    try
    {
        // Validate request
        if (string.IsNullOrWhiteSpace(request.Symbol))
            return Results.BadRequest(new { success = false, message = "نماد معاملاتی الزامی است" });
        
        if (request.Quantity <= 0)
            return Results.BadRequest(new { success = false, message = "مقدار سفارش باید بیشتر از صفر باشد" });
        
        if ((request.Price == null || request.Price <= 0))
            return Results.BadRequest(new { success = false, message = "قیمت برای سفارش محدود الزامی است" });

        var response = await orderService.CreateOrderAsync(request);
        
        return Results.Ok(ApiResponse<CreateOrderResponse>.Ok(response, response.Message));
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Json(ApiResponse<CreateOrderResponse>.Fail(ex.Message), statusCode: 401);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(ApiResponse<CreateOrderResponse>.Fail(ex.Message));
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(ApiResponse<CreateOrderResponse>.Fail(ex.Message));
    }
    catch (Exception ex)
    {
        return Results.Json(ApiResponse<CreateOrderResponse>.Fail("خطای داخلی سرور"), statusCode: 500);
    }
})
.WithName("CreateOrder")
.WithSummary("ایجاد سفارش واحد")
.WithDescription("ایجاد سفارش Limit یا Market با تشخیص خودکار نقش Maker/Taker")
.WithTags("Orders")
.Produces<ApiResponse<CreateOrderResponse>>(200)
.ProducesValidationProblem(400);

/// <summary>
/// دریافت اطلاعات سفارش با ID
/// </summary>
/// <param name="orderId">شناسه سفارش</param>
/// <param name="orderService">سرویس مدیریت سفارشات</param>
/// <returns>اطلاعات سفارش در صورت یافتن</returns>
/// <response code="200">سفارش یافت و بازگردانده شد</response>
/// <response code="404">سفارش یافت نشد</response>
app.MapGet("/api/orders/{orderId}", async (Guid orderId, OrderService orderService) =>
{
    try
    {
        var order = await orderService.GetOrderByIdAsync(orderId);
        if (order == null)
            return Results.NotFound(new { success = false, message = $"سفارش با شناسه {orderId} یافت نشد" });

        return Results.Ok(new { success = true, data = order });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, message = "خطای داخلی سرور" }, statusCode: 500);
    }
})
.WithName("GetOrderById")
.WithSummary("دریافت سفارش با شناسه")
.WithTags("Orders")
.Produces(200)
.Produces(404);

/// <summary>
/// لغو سفارش
/// </summary>
/// <param name="orderId">شناسه سفارش</param>
/// <param name="reason">دلیل لغو (اختیاری)</param>
/// <param name="orderService">سرویس مدیریت سفارشات</param>
/// <returns>نتیجه عملیات لغو</returns>
/// <response code="200">سفارش با موفقیت لغو شد</response>
/// <response code="400">عملیات لغو ناموفق یا نامعتبر</response>
/// <response code="404">سفارش یافت نشد</response>
app.MapPost("/api/orders/{orderId}/cancel", async (Guid orderId, string? reason, OrderService orderService) =>
{
    try
    {
        var success = await orderService.CancelOrderAsync(orderId, reason);
        if (!success)
            return Results.NotFound(new { success = false, message = $"سفارش با شناسه {orderId} یافت نشد یا قابل لغو نیست" });

        return Results.Ok(new { success = true, message = "سفارش با موفقیت لغو شد", orderId });
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, message = "خطای داخلی سرور" }, statusCode: 500);
    }
})
.WithName("CancelOrder")
.WithSummary("لغو سفارش")
.WithTags("Orders")
.Produces(200)
.Produces(400)
.Produces(404);

/// <summary>
/// لغو همه سفارشات فعال کاربر
/// </summary>
/// <param name="userId">شناسه کاربر</param>
/// <param name="reason">دلیل لغو (اختیاری)</param>
/// <param name="orderService">سرویس مدیریت سفارشات</param>
/// <returns>تعداد سفارشات لغو شده</returns>
/// <response code="200">سفارشات با موفقیت لغو شدند</response>
/// <response code="400">خطا در لغو سفارشات</response>
/// <remarks>
/// این endpoint:
/// 1. تمام سفارشات فعال کاربر مشخص شده را پیدا می‌کند
/// 2. آنها را با دلیل ارائه شده کنسل می‌کند
/// 3. تعداد سفارشات کنسل شده را در پاسخ برمی‌گرداند
/// 4. از الگوی ApiResponse برای پاسخ استاندارد استفاده می‌کند
/// </remarks>
app.MapPost("/api/orders/user/{userId}/cancel-active", async (Guid userId, string? reason, OrderService orderService) =>
{
    try
    {
        var cancelledCount = await orderService.CancelAllActiveOrdersByUserIdAsync(userId, reason ?? "لغو همه سفارشات فعال");
        
        var response = new CancelActiveOrdersResponseDto { CancelledCount = cancelledCount };
        
        return Results.Ok(ApiResponse<CancelActiveOrdersResponseDto>.Ok(response, $"{cancelledCount} سفارش فعال لغو شد"));
    }
    catch (Exception ex)
    {
        return Results.Json(ApiResponse<CancelActiveOrdersResponseDto>.Fail("خطای داخلی سرور"), statusCode: 500);
    }
})
.WithName("CancelUserActiveOrders")
.WithSummary("لغو همه سفارشات فعال کاربر")
.WithTags("Orders")
.Produces(200)
.Produces(400);

/// <summary>
/// تایید سفارش - تغییر وضعیت از Pending به Confirmed
/// </summary>
/// <param name="orderId">شناسه سفارش</param>
/// <param name="orderService">سرویس مدیریت سفارشات</param>
/// <returns>نتیجه عملیات تایید</returns>
/// <response code="200">سفارش با موفقیت تایید شد</response>
/// <response code="400">سفارش قابل تایید نیست</response>
/// <response code="404">سفارش یافت نشد</response>
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
        return Results.Json(new { 
            success = false, 
            message = "خطای داخلی سرور در تایید سفارش" 
        }, statusCode: 500);
    }
})
.WithName("ConfirmOrder")
.WithSummary("تایید سفارش")
.WithDescription("تغییر وضعیت سفارش از Pending به Confirmed با حفظ ایمنی همزمانی")
.WithTags("Orders")
.Produces(200)
.Produces(400)
.Produces(404);

/// <summary>
/// دریافت سفارشات کاربر با صفحه‌بندی
/// </summary>
/// <param name="userId">شناسه کاربر</param>
/// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
/// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
/// <param name="orderService">سرویس مدیریت سفارشات</param>
/// <returns>لیست صفحه‌بندی شده سفارشات کاربر</returns>
/// <response code="200">سفارشات کاربر با موفقیت دریافت شد</response>
/// <response code="400">پارامترهای درخواست نامعتبر</response>
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

    try
    {
        var orders = await orderService.GetOrdersByUserIdAsync(userId, page, size);
        return Results.Ok(ApiResponse<PagedResult<OrderHistoryDto>>.Ok(orders, "سفارشات دریافت شد"));
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, message = "خطای داخلی سرور" }, statusCode: 500);
    }
})
.WithName("GetUserOrders")
.WithSummary("دریافت سفارشات کاربر")
.WithTags("Orders")
.Produces<ApiResponse<PagedResult<OrderHistoryDto>>>(200)
.Produces(400);

/// <summary>
/// دریافت معاملات کاربر با صفحه‌بندی
/// </summary>
/// <param name="userId">شناسه کاربر</param>
/// <param name="pageNumber">شماره صفحه (پیش‌فرض: 1)</param>
/// <param name="pageSize">تعداد آیتم در هر صفحه (پیش‌فرض: 10، حداکثر: 100)</param>
/// <param name="tradeService">سرویس مدیریت معاملات</param>
/// <returns>لیست صفحه‌بندی شده معاملات کاربر</returns>
/// <response code="200">معاملات کاربر با موفقیت دریافت شد</response>
/// <response code="400">پارامترهای درخواست نامعتبر</response>
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

    try
    {
        var trades = await tradeService.GetTradesByUserIdAsync(userId, page, size);
        return Results.Ok(ApiResponse<PagedResult<TradeHistoryDto>>.Ok(trades, "معاملات دریافت شد"));
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, message = "خطای داخلی سرور" }, statusCode: 500);
    }
})
.WithName("GetUserTrades")
.WithSummary("دریافت معاملات کاربر")
.WithTags("Trades")
.Produces<ApiResponse<PagedResult<TradeHistoryDto>>>(200)
.Produces(400);

/// <summary>
/// دریافت سفارشات فعال کاربر
/// </summary>
/// <param name="userId">شناسه کاربر</param>
/// <param name="orderService">سرویس مدیریت سفارشات</param>
/// <returns>لیست سفارشات فعال کاربر</returns>
/// <response code="200">سفارشات فعال کاربر با موفقیت دریافت شد</response>
/// <response code="400">درخواست نامعتبر</response>
app.MapGet("/api/orders/active/user/{userId}", async (
    Guid userId,
    OrderService orderService) =>
{
    try
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
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, message = "خطای داخلی سرور" }, statusCode: 500);
    }
})
.WithName("GetUserActiveOrders")
.WithSummary("دریافت سفارشات فعال کاربر")
.WithTags("Orders")
.Produces<ApiResponse<List<OrderHistoryDto>>>(200)
.Produces(400);

/// <summary>
/// دریافت تمام سفارشات فعال سیستم (برای ادمین)
/// </summary>
/// <param name="orderService">سرویس مدیریت سفارشات</param>
/// <returns>لیست تمام سفارشات فعال</returns>
/// <response code="200">تمام سفارشات فعال با موفقیت دریافت شد</response>
/// <response code="500">خطای داخلی سرور</response>
app.MapGet("/api/orders/active/all", async (OrderService orderService) =>
{
    try
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
    }
    catch (Exception ex)
    {
        return Results.Json(new { success = false, message = "خطای داخلی سرور" }, statusCode: 500);
    }
})
.WithName("GetAllActiveOrders")
.WithSummary("دریافت تمام سفارشات فعال")
.WithTags("Orders")
.Produces<ApiResponse<List<OrderHistoryDto>>>(200)
.Produces(500);

/// <summary>
/// دریافت بهترین قیمت‌های خرید و فروش
/// </summary>
/// <param name="symbol">نماد معاملاتی (مثل BTC/USDT، ETH/USDT)</param>
/// <param name="tradingType">نوع معامله (پیش‌فرض: استاندارد)</param>
/// <param name="orderService">سرویس مدیریت سفارشات</param>
/// <returns>بهترین قیمت‌های Bid و Ask</returns>
/// <response code="200">بهترین قیمت‌ها با موفقیت دریافت شد</response>
/// <response code="400">درخواست نامعتبر</response>
/// <response code="404">نماد معاملاتی یافت نشد</response>
/// <response code="500">خطای داخلی سرور</response>
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

    Log.Information(">--------------------- end best-prices ---------------------<");

})
.WithName("GetBestPrices")
.WithSummary("دریافت بهترین قیمت‌های خرید و فروش")
.WithDescription("این endpoint بهترین قیمت‌های Bid (خرید) و Ask (فروش) را برای نماد معاملاتی مشخص شده بازمی‌گرداند.")
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

    // Basic validation for trading pairs (e.g., BTC/USDT, ETH/BTC)
    // Adjust regex pattern based on your symbol format requirements
    return System.Text.RegularExpressions.Regex.IsMatch(symbol, @"^[A-Z]{2,10}(/[A-Z]{2,10})?$");
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

/// Lists trades whose settlement never completed, newest first.
app.MapGet("/api/outbox/unsettled", async (OrdersDbContext db) =>
{
    try
    {
        var stuck = await db.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Status != OutboxMessageStatus.Completed)
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
                m.LastError
            })
            .ToListAsync();

        return Results.Ok(ApiResponse<object>.Ok(new
        {
            Count = stuck.Count,
            FailedCount = stuck.Count(s => s.Status == nameof(OutboxMessageStatus.Failed)),
            Items = stuck
        }, "فهرست تسویه‌های ناتمام"));
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error listing unsettled outbox messages");
        return Results.BadRequest(ApiResponse<string>.Fail(ex.Message));
    }
});

/// Puts a permanently-failed settlement back in the queue after the cause has been fixed.
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
    catch (Exception ex)
    {
        Log.Error(ex, "Error re-driving outbox message {MessageId}", messageId);
        return Results.BadRequest(ApiResponse<string>.Fail(ex.Message));
    }
});

/// Re-drives every failed settlement at once, for use after a fix that affects them all.
app.MapPost("/api/outbox/redrive-all-failed", async (OrdersDbContext db) =>
{
    try
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
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Error re-driving all failed outbox messages");
        return Results.BadRequest(ApiResponse<string>.Fail(ex.Message));
    }
});

app.Run();

// Request models

/// <summary>
/// Request model for creating a new maker order
/// </summary>
public record CreateOrderRequest(
    /// <summary>
    /// Trading asset symbol (e.g., BTC, ETH, USDT)
    /// </summary>
    string Asset, 
    /// <summary>
    /// Order quantity/amount
    /// </summary>
    decimal Amount, 
    /// <summary>
    /// Order price per unit
    /// </summary>
    decimal Price, 
    /// <summary>
    /// Unique identifier of the user placing the order
    /// </summary>
    Guid UserId, 
    /// <summary>
    /// Type of order (Buy or Sell)
    /// </summary>
    OrderSide Type,
    /// <summary>
    /// Trading type (Spot or Futures)
    /// </summary>
    TradingType TradingType,
    /// <summary>
    /// Optional notes for the order
    /// </summary>
    string? Notes = null);

/// <summary>
/// Request model for creating a new limit order
/// </summary>
public record CreateLimitOrderRequest(
    /// <summary>
    /// Trading symbol (e.g., BTC, ETH)
    /// </summary>
    string Symbol,
    /// <summary>
    /// Order quantity
    /// </summary>
    decimal Quantity,
    /// <summary>
    /// Limit price for the order
    /// </summary>
    decimal Price,
    /// <summary>
    /// Unique identifier of the user placing the order
    /// </summary>
    Guid UserId);

/// <summary>
/// Request model for creating a new taker order
/// </summary>
public record CreateTakerOrderRequest(
    /// <summary>
    /// Unique identifier of the parent maker order
    /// </summary>
    Guid ParentOrderId,
    /// <summary>
    /// Order amount
    /// </summary>
    decimal Amount,
    /// <summary>
    /// Unique identifier of the user placing the order
    /// </summary>
    Guid UserId,
    /// <summary>
    /// Optional notes for the order
    /// </summary>
    string? Notes = null);

/// <summary>
/// Request model for updating order status
/// </summary>
public record UpdateOrderStatusRequest(
    /// <summary>
    /// New status for the order
    /// </summary>
    OrderStatus Status, 
    /// <summary>
    /// Optional notes for the status change
    /// </summary>
    string? Notes = null);

/// <summary>
/// Request model for cancelling an order
/// </summary>
public record CancelOrderRequest(
    /// <summary>
    /// Optional reason for cancellation
    /// </summary>
    string? Reason = null);

/// <summary>
/// Request model for notifying the matching engine about a new order
/// </summary>
public record NotifyMatchingEngineRequest(
    /// <summary>
    /// Unique identifier of the order to process
    /// </summary>
    Guid OrderId,
    /// <summary>
    /// Trading asset symbol
    /// </summary>
    string Asset,
/// <summary>
/// Type of order (Buy or Sell)
/// </summary>
OrderSide Type);







