using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Orders.Application;
using Orders.Core;
using Orders.Infrastructure;
using System.Reflection;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.Enums.Order;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb") ??
        "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("Orders.Api")));

// Add services to the container.
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();

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
/// Creates a new maker order
/// </summary>
/// <param name="request">Order creation request containing asset, amount, price, user ID, type, trading type, and optional notes</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Created order details with success status</returns>
/// <response code="200">Order created successfully</response>
/// <response code="400">Invalid request data or business rule violation</response>
/// <response code="403">Unauthorized access</response>
app.MapPost("/api/orders", async (CreateOrderRequest request, OrderService orderService) =>
{
    try
    {
        var command = new CreateOrderCommand(
            request.Asset,
            request.Amount,
            request.Price,
            request.UserId,
            request.Type,
            request.TradingType,
            request.Notes
        );

        var order = await orderService.CreateMakerOrderAsync(command);
        return Results.Ok(new { success = true, message = "سفارش با موفقیت ثبت شد", order = order });
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Forbid();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Creates a new limit order
/// </summary>
/// <param name="request">Limit order request containing symbol, quantity, price, and user ID</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Created limit order details with success status</returns>
/// <response code="200">Limit order created successfully</response>
/// <response code="400">Invalid request data or validation error</response>
app.MapPost("/api/orders/limit", async (CreateLimitOrderRequest request, OrderService orderService) =>
{
    try
    {
        var order = await orderService.CreateLimitOrderAsync(request.Symbol, request.Quantity, request.Price, request.UserId);
        return Results.Ok(new { success = true, message = "Limit order created successfully", order = order });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Creates a new taker order
/// </summary>
/// <param name="request">Taker order request containing parent order ID, amount, user ID, and optional notes</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Created taker order details with success status</returns>
/// <response code="200">Taker order created successfully</response>
/// <response code="400">Invalid request data or business rule violation</response>
app.MapPost("/api/orders/taker", async (CreateTakerOrderRequest request, OrderService orderService) =>
{
    try
    {
        var command = new CreateTakerOrderCommand(
            request.ParentOrderId,
            request.Amount,
            request.UserId,
            request.Notes
        );

        var order = await orderService.CreateTakerOrderAsync(command);
        return Results.Ok(new { success = true, message = "سفارش taker با موفقیت ثبت شد", order = order });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Retrieves an order by its ID
/// </summary>
/// <param name="orderId">Unique identifier of the order</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Order details if found</returns>
/// <response code="200">Order found and returned successfully</response>
/// <response code="404">Order not found</response>
/// <response code="400">Invalid request or error occurred</response>
app.MapGet("/api/orders/{orderId}", async (Guid orderId, OrderService orderService) =>
{
    try
    {
        var order = await orderService.GetOrderByIdAsync(orderId);
        if (order == null)
            return Results.NotFound(new { success = false, message = "سفارش یافت نشد" });

        return Results.Ok(new { success = true, order = order });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Retrieves paginated orders for a specific user
/// </summary>
/// <param name="userId">Unique identifier of the user</param>
/// <param name="pageNumber">Page number for pagination (default: 1)</param>
/// <param name="pageSize">Number of items per page (default: 10, max: 100)</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Paginated list of user orders</returns>
/// <response code="200">User orders retrieved successfully</response>
/// <response code="400">Invalid request parameters</response>
app.MapGet("/api/orders/userorders/{userId}",
    async (
        Guid userId,
        int? pageNumber,
        int? pageSize,
        OrderService orderService) =>
    {
    
        // اعتبارسنجی
        var page = pageNumber ?? 1;
        var size = Math.Clamp(pageSize ?? 10, 1, 100);

        var orders = await orderService.GetOrdersByUserIdAsync(userId, page, size);

        return orders?.Items?.Any() == true
            ? Results.Ok(ApiResponse<PagedResult<OrderHistoryDto>>.Ok(orders, "سفارشات دریافت شد"))
            : Results.Ok(ApiResponse<PagedResult<OrderHistoryDto>>.Fail("کاربر یافت نشد یا سفارشی وجود ندارد"));
    });

/// <summary>
/// Retrieves all orders for a specific asset
/// </summary>
/// <param name="asset">Trading asset symbol (e.g., BTC, ETH)</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>List of orders for the specified asset</returns>
/// <response code="200">Asset orders retrieved successfully</response>
/// <response code="400">Invalid request or error occurred</response>
app.MapGet("/api/orders/asset/{asset}", async (string asset, OrderService orderService) =>
{
    try
    {
        var orders = await orderService.GetOrdersByAssetAsync(asset);
        return Results.Ok(new { success = true, orders = orders });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Retrieves all active orders
/// </summary>
/// <param name="orderService">Order service for business logic</param>
/// <returns>List of all active orders</returns>
/// <response code="200">Active orders retrieved successfully</response>
/// <response code="400">Error occurred while retrieving orders</response>
app.MapGet("/api/orders/active", async (OrderService orderService) =>
{
    try
    {
        var orders = await orderService.GetActiveOrdersAsync();
        return Results.Ok(new { success = true, orders = orders });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Retrieves available maker orders for a specific asset and trading type
/// </summary>
/// <param name="asset">Trading asset symbol</param>
/// <param name="tradingType">Type of trading (Spot or Futures)</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>List of available maker orders</returns>
/// <response code="200">Maker orders retrieved successfully</response>
/// <response code="400">Invalid request or error occurred</response>
app.MapGet("/api/orders/maker/{asset}", async (string asset, TradingType tradingType, OrderService orderService) =>
{
    try
    {
        var orders = await orderService.GetAvailableMakerOrdersAsync(asset, tradingType);
        return Results.Ok(new { success = true, orders = orders });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Updates the status of an order
/// </summary>
/// <param name="orderId">Unique identifier of the order</param>
/// <param name="request">Status update request containing new status and optional notes</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Success status of the update operation</returns>
/// <response code="200">Order status updated successfully</response>
/// <response code="400">Invalid request or update failed</response>
app.MapPut("/api/orders/{orderId}/status", async (Guid orderId, UpdateOrderStatusRequest request, OrderService orderService) =>
{
    try
    {
        var success = await orderService.UpdateOrderStatusAsync(orderId, request.Status, request.Notes);
        if (success)
            return Results.Ok(new { success = true, message = "وضعیت سفارش با موفقیت به‌روزرسانی شد" });
        else
            return Results.BadRequest(new { success = false, message = "خطا در به‌روزرسانی وضعیت سفارش" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Cancels an order with optional reason
/// </summary>
/// <param name="orderId">Unique identifier of the order</param>
/// <param name="request">Cancel request containing optional reason</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Success status of the cancellation operation</returns>
/// <response code="200">Order cancelled successfully</response>
/// <response code="400">Invalid request or cancellation failed</response>
app.MapPut("/api/orders/{orderId}/cancel", async (Guid orderId, CancelOrderRequest request, OrderService orderService) =>
{
    try
    {
        var success = await orderService.CancelOrderAsync(orderId, request.Reason);
        if (success)
            return Results.Ok(new { success = true, message = "سفارش با موفقیت لغو شد" });
        else
            return Results.BadRequest(new { success = false, message = "خطا در لغو سفارش" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Cancels an order (simple cancellation)
/// </summary>
/// <param name="orderId">Unique identifier of the order</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Updated order details after cancellation</returns>
/// <response code="200">Order cancelled successfully</response>
/// <response code="404">Order not found</response>
/// <response code="400">Invalid request or cancellation failed</response>
app.MapPost("/api/orders/{orderId}/cancel", async (Guid orderId, OrderService orderService) =>
{
    try
    {
        // Load order by Id from DB
        var order = await orderService.GetOrderByIdAsync(orderId);
        if (order == null)
            return Results.NotFound(new { success = false, message = "Order not found" });

        // Cancel the order
        var success = await orderService.CancelOrderAsync(orderId);
        if (success)
        {
            // Get updated order
            var updatedOrder = await orderService.GetOrderByIdAsync(orderId);
            return Results.Ok(new { success = true, message = "Order cancelled successfully", order = updatedOrder });
        }
        else
            return Results.BadRequest(new { success = false, message = "Error cancelling order" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Confirms an order
/// </summary>
/// <param name="orderId">Unique identifier of the order</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Success status of the confirmation operation</returns>
/// <response code="200">Order confirmed successfully</response>
/// <response code="400">Invalid request or confirmation failed</response>
app.MapPut("/api/orders/{orderId}/confirm", async (Guid orderId, OrderService orderService) =>
{
    try
    {
        var success = await orderService.ConfirmOrderAsync(orderId);
        if (success)
            return Results.Ok(new { success = true, message = "سفارش با موفقیت تایید شد" });
        else
            return Results.BadRequest(new { success = false, message = "خطا در تایید سفارش" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Completes an order
/// </summary>
/// <param name="orderId">Unique identifier of the order</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Success status of the completion operation</returns>
/// <response code="200">Order completed successfully</response>
/// <response code="400">Invalid request or completion failed</response>
app.MapPut("/api/orders/{orderId}/complete", async (Guid orderId, OrderService orderService) =>
{
    try
    {
        var success = await orderService.CompleteOrderAsync(orderId);
        if (success)
            return Results.Ok(new { success = true, message = "سفارش با موفقیت تکمیل شد" });
        else
            return Results.BadRequest(new { success = false, message = "خطا در تکمیل سفارش" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Accepts a taker order for a maker order
/// </summary>
/// <param name="makerOrderId">Unique identifier of the maker order</param>
/// <param name="takerOrderId">Unique identifier of the taker order</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Success status of the acceptance operation</returns>
/// <response code="200">Taker order accepted successfully</response>
/// <response code="400">Invalid request or acceptance failed</response>
app.MapPost("/api/orders/{makerOrderId}/accept-taker/{takerOrderId}", async (Guid makerOrderId, Guid takerOrderId, OrderService orderService) =>
{
    try
    {
        var success = await orderService.AcceptTakerOrderAsync(makerOrderId, takerOrderId);
        if (success)
            return Results.Ok(new { success = true, message = "سفارش taker با موفقیت پذیرفته شد" });
        else
            return Results.BadRequest(new { success = false, message = "خطا در پذیرش سفارش taker" });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Creates a new market order
/// </summary>
/// <param name="request">Market order request containing asset, amount, user ID, type, trading type, and optional notes</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Created market order details with success status</returns>
/// <response code="200">Market order created successfully</response>
/// <response code="400">Invalid request data or business rule violation</response>
/// <response code="403">Unauthorized access</response>
app.MapPost("/api/orders/market", async (CreateMarketOrderRequest request, OrderService orderService) =>
{
    try
    {
        var command = new CreateMarketOrderCommand(
            request.Asset,
            request.Amount,
            request.UserId,
            request.Type,
            request.TradingType,
            request.Notes
        );

        var order = await orderService.CreateMarketOrderAsync(command);
        return Results.Ok(new { success = true, message = "سفارش بازار با موفقیت ثبت شد", order = order });
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Forbid();
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

/// <summary>
/// Gets best bid and ask prices for a specific asset
/// </summary>
/// <param name="asset">Trading asset symbol</param>
/// <param name="tradingType">Type of trading (Spot or Futures)</param>
/// <param name="orderService">Order service for business logic</param>
/// <returns>Best bid and ask prices</returns>
/// <response code="200">Best bid/ask retrieved successfully</response>
/// <response code="400">Invalid request or error occurred</response>
app.MapGet("/api/orders/market/{asset}/prices", async (string asset, TradingType tradingType, OrderService orderService) =>
{
    try
    {
        var prices = await orderService.GetBestBidAskAsync(asset, tradingType);
        return Results.Ok(new { success = true, prices = prices });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
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
    OrderType Type,
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
/// Request model for creating a new market order
/// </summary>
public record CreateMarketOrderRequest(
    /// <summary>
    /// Trading asset symbol (e.g., BTC, ETH, USDT)
    /// </summary>
    string Asset, 
    /// <summary>
    /// Order quantity/amount
    /// </summary>
    decimal Amount, 
    /// <summary>
    /// Unique identifier of the user placing the order
    /// </summary>
    Guid UserId, 
    /// <summary>
    /// Type of order (Buy or Sell)
    /// </summary>
    OrderType Type,
    /// <summary>
    /// Trading type (Spot or Futures)
    /// </summary>
    TradingType TradingType,
    /// <summary>
    /// Optional notes for the order
    /// </summary>
    string? Notes = null);

