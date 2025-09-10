using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Orders.Application;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using System.Reflection;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Responses.Order;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb") ??
        "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("Orders.Infrastructure"))
    .LogTo(Console.WriteLine, LogLevel.None)); // Disable all EF Core logging

// Add services to the container.
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<ITradeRepository, TradeRepository>();
builder.Services.AddScoped<OrderMatchingRepository>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<TradeService>();

// Add Wallet API Client
builder.Services.AddHttpClient<Orders.Infrastructure.Clients.IWalletApiClient, Orders.Infrastructure.Clients.WalletApiClient>(client =>
{
    var walletApiUrl = builder.Configuration.GetValue<string>("WalletApiUrl") ?? "http://localhost:60933";
    client.BaseAddress = new Uri(walletApiUrl);
});
builder.Services.AddScoped<Orders.Infrastructure.Clients.IWalletApiClient, Orders.Infrastructure.Clients.WalletApiClient>();

// Add Matching Engine
builder.Services.AddScoped<IMatchingEngine, Orders.Application.Services.MatchingEngineService>();
builder.Services.AddHostedService<Orders.Application.Services.MatchingEngineService>();

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

// Configure Swagger UI
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "TallaEgg Orders API V1");
        c.RoutePrefix = "api-docs";
    });
}

// Order management endpoints

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
    try
    {
        string symbol = $"{Base}/{Quote}";
        // Input validation
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return Results.BadRequest(ApiResponse<BestPricesDto>.Fail("نماد معاملاتی الزامی است."));
        }

        // Normalize symbol format (remove special characters, convert to uppercase)
        var normalizedSymbol = symbol.Trim().ToUpperInvariant();

        // Validate symbol format (basic validation for trading pairs like BTC/USDT)
        if (!IsValidSymbolFormat(normalizedSymbol))
        {
            return Results.BadRequest(ApiResponse<BestPricesDto>.Fail("فرمت نماد معاملاتی نامعتبر است. (مثال صحیح: BTC/USDT)"));
        }

        var type = tradingType ?? TradingType.Spot;

        // Get best bid/ask prices
        var result = await orderService.GetBestBidAskAsync(normalizedSymbol, type);

        if (result == null)
        {
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

        return Results.Ok(ApiResponse<BestPricesDto>.Ok(bestPricesDto, "بهترین قیمت‌ها با موفقیت دریافت شد."));
    }
    catch (ArgumentException argEx)
    {
        return Results.BadRequest(ApiResponse<BestPricesDto>.Fail($"پارامتر نامعتبر: {argEx.Message}"));
    }
    catch (InvalidOperationException invOpEx)
    {
        return Results.Json(ApiResponse<BestPricesDto>.Fail("سرویس قیمت‌گذاری در حال حاضر در دسترس نیست."), statusCode: 503);
    }
    catch (TimeoutException)
    {
        return Results.Json(ApiResponse<BestPricesDto>.Fail("زمان انتظار درخواست به پایان رسید."), statusCode: 408);
    }
    catch (Exception ex)
    {
        // Log the exception (در محیط واقعی باید لاگ شود)
        // logger.LogError(ex, "Error getting best prices for symbol: {Symbol}", symbol);

        return Results.Json(ApiResponse<BestPricesDto>.Fail("خطای داخلی سرور. لطفاً مجدداً تلاش کنید."), statusCode: 500);
    }
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



