using Microsoft.EntityFrameworkCore;
using Orders.Application;
using Orders.Core;
using Orders.Infrastructure;
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

var app = builder.Build();

// Order management endpoints
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

app.MapGet("/api/orders/userorders/{userId}",
    async (
        Guid userId,
        int? pageNumber,
        int? pageSize,
        OrderService orderService) =>
    {
        // مقادیر پیش‌فرض
        var page = pageNumber.GetValueOrDefault(1);
        var size = pageSize.GetValueOrDefault(10);

        // اعتبارسنجی
        if (page <= 0) page = 1;
        if (size <= 0) size = 10;
        if (size > 100) size = 100; // حداکثر سایز برای جلوگیری از فشار به دیتابیس

        var orders = await orderService.GetOrdersByUserIdAsync(userId, page, size);

        if (orders == null || !orders.Items.Any())
            return ApiResponse<PagedResult<OrderHistoryDto>>.Fail("User not found or no orders");

        return ApiResponse<PagedResult<OrderHistoryDto>>.Ok(orders, "User loaded successfully");
    });

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

app.Run();

// Request models
public record CreateOrderRequest(
    string Asset, 
    decimal Amount, 
    decimal Price, 
    Guid UserId, 
    OrderType Type,
    TradingType TradingType,
    string? Notes = null);

public record CreateLimitOrderRequest(
    string Symbol,
    decimal Quantity,
    decimal Price,
    Guid UserId);

public record CreateTakerOrderRequest(
    Guid ParentOrderId,
    decimal Amount,
    Guid UserId,
    string? Notes = null);

public record UpdateOrderStatusRequest(OrderStatus Status, string? Notes = null);

public record CancelOrderRequest(string? Reason = null);

