using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Orders.Application;
using Orders.Core;
using Orders.Infrastructure;
using System.Text.Json;
using TallaEgg.Api.Clients;
using TallaEgg.Core.Models;
using Users.Application;
using Users.Core;
using ClientRegisterUserRequest = TallaEgg.Api.Clients.RegisterUserRequest;
using ClientRegisterUserWithInvitationRequest = TallaEgg.Api.Clients.RegisterUserWithInvitationRequest;
// using TallaEgg.Core.Interfaces;
// using TallaEgg.Application.Interfaces;
// using TallaEgg.Application.Services;
// using TallaEgg.Infrastructure.Repositories;
// using TallaEgg.Infrastructure.Data;
// using TallaEgg.Core.Enums.Order;
using ClientUserDto = TallaEgg.Api.Clients.UserDto;
using ClientUserRole = TallaEgg.Api.Clients.UserRole;
using ClientUserStatus = TallaEgg.Api.Clients.UserStatus;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server (در appsettings.json هم می‌توان قرار داد)
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb") ??
        "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("TallaEgg.Api")));

// تنظیم اتصال به دیتابیس اصلی TallaEgg
// builder.Services.AddDbContext<TallaEggDbContext>(options =>
//     options.UseSqlServer(builder.Configuration.GetConnectionString("TallaEggDb") ??
//         "Server=localhost;Database=TallaEgg;Trusted_Connection=True;TrustServerCertificate=True;",
//         b => b.MigrationsAssembly("TallaEgg.Api")));

// فقط سرویس‌های مربوط به Orders و Price ثبت شوند
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
//builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<CreateOrderCommandHandler>();
builder.Services.AddScoped<CreateTakerOrderCommandHandler>();

// سرویس‌های مربوط به Symbols
// builder.Services.AddScoped<ISymbolRepository, SymbolRepository>();
// builder.Services.AddScoped<ISymbolService, SymbolService>();

// اضافه کردن HTTP Client برای ارتباط با Users microservice
if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHttpClient<IUsersApiClient, UsersApiClient>()
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
        });
}
else
{
    builder.Services.AddHttpClient<IUsersApiClient, UsersApiClient>();
}

// اضافه کردن CORS
builder.Services.AddCors();

// پیکربندی Serilog برای لاگ‌نویسی روی فایل و کنسول
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .WriteTo.File("logs/tallaegg-api-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

var app = builder.Build();

// تنظیم CORS
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());


app.MapGet("/api/symbols", () => {
    var symbols = JsonSerializer.Deserialize<List<Symbol>>(File.ReadAllText("symbols.json"));
    return Results.Ok(symbols);
});

//// ثبت سفارش جدید توسط مشتری
//app.MapPost("/api/order", async (OrderDto orderDto, CreateOrderCommandHandler handler) =>
//{
//    try
//    {
//        // Convert from OrderDto to CreateOrderCommand
//        var command = new CreateOrderCommand(
//            orderDto.Asset,
//            orderDto.Amount,
//            orderDto.Price,
//            orderDto.UserId,
//            Enum.Parse<OrderSide>(orderDto.Side, true),
//            Enum.Parse<TradingType>(orderDto.TradingType, true),
//            null
//        );

//        var result = await handler.Handle(command);
//        return Results.Ok(result);
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest(new { success = false, message = ex.Message });
//    }
//});

//// ثبت سفارش Taker جدید
//app.MapPost("/api/order/taker", async (CreateTakerOrderCommand cmd, CreateTakerOrderCommandHandler handler) =>
//{
//    var result = await handler.Handle(cmd);
//    return Results.Ok(result);
//});

//// لیست سفارشات یک دارایی
//app.MapGet("/api/orders/{asset}", async (string asset, IOrderRepository repo) =>
//{
//    var list = await repo.GetOrdersByAssetAsync(asset);
//    return Results.Ok(list);
//});

//// لیست سفارشات Maker موجود برای یک دارایی و نوع معامله
//app.MapGet("/api/orders/available/{asset}/{tradingType}", async (string asset, string tradingType, IOrderService orderService) =>
//{
//    if (!Enum.TryParse<TradingType>(tradingType, true, out var tradingTypeEnum))
//        return Results.BadRequest(new { message = "نوع معامله نامعتبر است" });

//    var orders = await orderService.GetAvailableMakerOrdersAsync(asset, tradingTypeEnum);
//    return Results.Ok(orders);
//});

//// User management endpoints (delegated to Users microservice)
//app.MapPost("/api/user/register", async ([FromBody] RegisterUserRequest request, [FromServices] IUsersApiClient usersClient) =>
//{
//    try
//    {
//        var clientRequest = new ClientRegisterUserRequest(request.TelegramId, request.Username, request.FirstName, request.LastName, request.InvitationCode);
//        var user = await usersClient.RegisterUserAsync(clientRequest);
//        if (user != null)
//            return Results.Ok(new { success = true, userId = user.Id });
//        return Results.BadRequest(new { success = false, message = "خطا در ثبت کاربر" });
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest(new { success = false, message = ex.Message });
//    }
//});

//app.MapPost("/api/user/register-with-invitation", async ([FromBody] RegisterUserWithInvitationRequest request, [FromServices] IUsersApiClient usersClient) =>
//{
//    try
//    {
//        var clientRequest = new ClientRegisterUserWithInvitationRequest(request.User);
//        var user = await usersClient.RegisterUserWithInvitationAsync(clientRequest);
//        if (user != null)
//            return Results.Ok(new { success = true, userId = user.Id });
//        return Results.BadRequest(new { success = false, message = "خطا در ثبت کاربر" });
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest(new { success = false, message = ex.Message });
//    }
//});

//app.MapGet("/api/user/getUserIdByInvitationCode/{invitationCode}", async ([FromRoute] string invitationCode, [FromServices] IUsersApiClient usersClient) =>
//{
//    var id = await usersClient.GetUserIdByInvitationCodeAsync(invitationCode);
//    return Results.Ok(id);
//});

//app.MapPost("/api/user/validate-invitation", async ([FromBody] ValidateInvitationRequest request, [FromServices] IUsersApiClient usersClient) =>
//{
//    var result = await usersClient.ValidateInvitationCodeAsync(request.InvitationCode);
//    return Results.Ok(new { isValid = result.isValid, message = result.message });
//});

//app.MapPost("/api/user/update-phone", async ([FromBody] UpdatePhoneRequest request, [FromServices] IUsersApiClient usersClient) =>
//{
//    try
//    {
//        var success = await usersClient.UpdateUserPhoneAsync(request.TelegramId, request.PhoneNumber);
//        if (success)
//            return Results.Ok(new { success = true, message = "شماره تلفن با موفقیت ثبت شد." });
//        return Results.BadRequest(new { success = false, message = "خطا در بروزرسانی شماره تلفن" });
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest(new { success = false, message = ex.Message });
//    }
//});

//app.MapPost("/api/user/update-status", async ([FromBody] UpdateStatusRequest request, [FromServices] IUsersApiClient usersClient) =>
//{
//    try
//    {
//        var success = await usersClient.UpdateUserStatusAsync(request.TelegramId, request.Status);
//        if (success)
//            return Results.Ok(new { success = true, message = "وضعیت کاربر با موفقیت به‌روزرسانی شد." });
//        return Results.BadRequest(new { success = false, message = "خطا در بروزرسانی وضعیت کاربر" });
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest(new { success = false, message = ex.Message });
//    }
//});

//app.MapGet("/api/user/{telegramId}", async ([FromRoute] long telegramId, [FromServices] IUsersApiClient usersClient) =>
//{
//    var user = await usersClient.GetUserByTelegramIdAsync(telegramId);
//    if (user == null)
//        return Results.NotFound();
//    return Results.Ok(user);
//});

//app.MapPost("/api/user/update-role", async ([FromBody] UpdateUserRoleRequest request, [FromServices] IUsersApiClient usersClient, [FromServices] IAuthorizationService authService) =>
//{
//    try
//    {
//        var canManageUsers = await authService.CanManageUsersAsync(request.RequestingUserId);
//        if (!canManageUsers)
//            return Results.Forbid();

//        var user = await usersClient.UpdateUserRoleAsync(request.UserId, request.NewRole);
//        if (user == null)
//            return Results.NotFound(new { success = false, message = "کاربر یافت نشد." });

//        return Results.Ok(new { success = true, message = "نقش کاربر با موفقیت به‌روزرسانی شد.", user });
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest(new { success = false, message = ex.Message });
//    }
//});

//app.MapGet("/api/users/by-role/{role}", async ([FromRoute] string role, [FromServices] IUsersApiClient usersClient, [FromServices] IAuthorizationService authService) =>
//{
//    try
//    {
//        var canManageUsers = await authService.CanManageUsersAsync(Guid.Empty);
//        if (!canManageUsers)
//            return Results.Forbid();

//        if (!Enum.TryParse<ClientUserRole>(role, true, out var userRole))
//            return Results.BadRequest(new { success = false, message = "نقش نامعتبر است." });

//        var users = await usersClient.GetUsersByRoleAsync(userRole);
//        return Results.Ok(users);
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest(new { success = false, message = ex.Message });
//    }
//});

//app.MapGet("/api/user/exists/{telegramId}", async ([FromRoute] long telegramId, [FromServices] IUsersApiClient usersClient) =>
//{
//    try
//    {
//        var exists = await usersClient.UserExistsAsync(telegramId);
//        return Results.Ok(new { exists = exists });
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest(new { success = false, message = ex.Message });
//    }
//});

// Health check endpoint to test Users API connection
app.MapGet("/api/health/users", async ([FromServices] IUsersApiClient usersClient) =>
{
    try
    {
        // Try to get a user that doesn't exist to test the connection
        var exists = await usersClient.UserExistsAsync(0);
        return Results.Ok(new { success = true, message = "Users API is accessible", exists = exists });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = $"Users API connection failed: {ex.Message}" });
    }
});

// Symbols endpoints (commented out until services are properly integrated)
// app.MapGet("/api/symbols", async ([FromServices] ISymbolService symbolService) =>
// {
//     try
//     {
//         var symbols = await symbolService.GetAllSymbolsAsync();
//         return Results.Ok(new { success = true, symbols = symbols });
//     }
//     catch (Exception ex)
//     {
//         return Results.BadRequest(new { success = false, message = ex.Message });
//     }
// });

// app.MapGet("/api/symbols/active", async ([FromServices] ISymbolService symbolService) =>
// {
//     try
//     {
//         var symbols = await symbolService.GetActiveSymbolsAsync();
//         return Results.Ok(new { success = true, symbols = symbols });
//     }
//     catch (Exception ex)
//     {
//         return Results.BadRequest(new { success = false, message = ex.Message });
//     }
// });

// app.MapGet("/api/symbols/trading-type/{tradingType}", async (string tradingType, [FromServices] ISymbolService symbolService) =>
// {
//     try
//     {
//         if (!Enum.TryParse<TradingType>(tradingType, true, out var tradingTypeEnum))
//             return Results.BadRequest(new { success = false, message = "نوع معامله نامعتبر است" });

//         var symbols = await symbolService.GetSymbolsByTradingTypeAsync(tradingTypeEnum);
//         return Results.Ok(new { success = true, symbols = symbols });
//     }
//     catch (Exception ex)
//     {
//         return Results.BadRequest(new { success = false, message = ex.Message });
//     }
// });

// app.MapGet("/api/symbols/{name}", async (string name, [FromServices] ISymbolService symbolService) =>
// {
//     try
//     {
//         var symbol = await symbolService.GetSymbolAsync(name);
//         if (symbol == null)
//             return Results.NotFound(new { success = false, message = "نماد یافت نشد" });

//         return Results.Ok(new { success = true, symbol = symbol });
//     }
//     catch (Exception ex)
//     {
//         return Results.BadRequest(new { success = false, message = ex.Message });
//     }
// });

// app.MapPost("/api/symbols", async ([FromBody] CreateSymbolRequest request, [FromServices] ISymbolService symbolService) =>
// {
//     try
//     {
//         var symbol = await symbolService.CreateSymbolAsync(
//             request.Name,
//             request.BaseAsset,
//             request.QuoteAsset,
//             request.DisplayName,
//             request.MinOrderAmount,
//             request.MaxOrderAmount,
//             request.PricePrecision,
//             request.QuantityPrecision,
//             request.IsSpotTradingEnabled,
//             request.IsFuturesTradingEnabled,
//             request.Description);

//         return Results.Ok(new { success = true, symbol = symbol });
//     }
//     catch (Exception ex)
//     {
//         return Results.BadRequest(new { success = false, message = ex.Message });
//     }
// });

// app.MapPut("/api/symbols/{id}", async (Guid id, [FromBody] UpdateSymbolRequest request, [FromServices] ISymbolService symbolService) =>
// {
//     try
//     {
//         var symbol = await symbolService.UpdateSymbolAsync(id,
//             request.DisplayName,
//             request.MinOrderAmount,
//             request.MaxOrderAmount,
//             request.PricePrecision,
//             request.QuantityPrecision,
//             request.IsSpotTradingEnabled,
//             request.IsFuturesTradingEnabled,
//             request.Status,
//             request.Description);

//         return Results.Ok(new { success = true, symbol = symbol });
//     }
//     catch (Exception ex)
//     {
//         return Results.BadRequest(new { success = false, message = ex.Message });
//     }
// });

// app.MapDelete("/api/symbols/{id}", async (Guid id, [FromServices] ISymbolService symbolService) =>
// {
//     try
//     {
//         var result = await symbolService.DeleteSymbolAsync(id);
//         if (!result)
//             return Results.NotFound(new { success = false, message = "نماد یافت نشد" });

//         return Results.Ok(new { success = true, message = "نماد با موفقیت حذف شد" });
//     }
//     catch (Exception ex)
//     {
//         return Results.BadRequest(new { success = false, message = ex.Message });
//     }
// });

// app.MapPost("/api/symbols/{id}/activate", async (Guid id, [FromServices] ISymbolService symbolService) =>
// {
//     try
//     {
//         var result = await symbolService.ActivateSymbolAsync(id);
//         if (!result)
//             return Results.NotFound(new { success = false, message = "نماد یافت نشد" });

//         return Results.Ok(new { success = true, message = "نماد با موفقیت فعال شد" });
//     }
//     catch (Exception ex)
//     {
//         return Results.BadRequest(new { success = false, message = ex.Message });
//     }
// });

// app.MapPost("/api/symbols/{id}/deactivate", async (Guid id, [FromServices] ISymbolService symbolService) =>
// {
//     try
//     {
//         var result = await symbolService.DeactivateSymbolAsync(id);
//         if (!result)
//             return Results.NotFound(new { success = false, message = "نماد یافت نشد" });

//         return Results.Ok(new { success = true, message = "نماد با موفقیت غیرفعال شد" });
//     }
//     catch (Exception ex)
//     {
//         return Results.BadRequest(new { success = false, message = ex.Message });
//     }
// });

//// Price endpoints
//app.MapGet("/api/prices/{asset}", async (string asset, PriceService priceService) =>
//{
//    var price = await priceService.GetLatestPriceAsync(asset);
//    if (price == null)
//        return Results.NotFound();
//    return Results.Ok(price);
//});

//app.MapGet("/api/prices", async (PriceService priceService) =>
//{
//    var prices = await priceService.GetAllPricesAsync();
//    return Results.Ok(prices);
//});

//app.MapPost("/api/prices", async (UpdatePriceRequest request, PriceService priceService) =>
//{
//    try
//    {
//        var price = await priceService.UpdatePriceAsync(request.Asset, request.BuyPrice, request.SellPrice, request.Source);
//        return Results.Ok(price);
//    }
//    catch (Exception ex)
//    {
//        return Results.BadRequest(new { message = ex.Message });
//    }
//});

app.Run();

// Request models
public record ValidateInvitationRequest(string InvitationCode);
public record RegisterUserRequest(long TelegramId, string? Username, string? FirstName, string? LastName, string? InvitationCode = null);
public record RegisterUserWithInvitationRequest(ClientUserDto User);
public record UpdatePhoneRequest(long TelegramId, string PhoneNumber);
public record UpdateStatusRequest(long TelegramId, ClientUserStatus Status);
public record UpdatePriceRequest(string Asset, decimal BuyPrice, decimal SellPrice, string Source = "Manual");
public record UpdateUserRoleRequest(Guid RequestingUserId, Guid UserId, ClientUserRole NewRole);

// Symbol request models (commented out until services are properly integrated)
// public record CreateSymbolRequest(
//     string Name,
//     string BaseAsset,
//     string QuoteAsset,
//     string DisplayName,
//     decimal MinOrderAmount = 0.001m,
//     decimal MaxOrderAmount = 1000000m,
//     decimal PricePrecision = 2,
//     decimal QuantityPrecision = 6,
//     bool IsSpotTradingEnabled = true,
//     bool IsFuturesTradingEnabled = false,
//     string? Description = null);

// public record UpdateSymbolRequest(
//     string? DisplayName = null,
//     decimal? MinOrderAmount = null,
//     decimal? MaxOrderAmount = null,
//     decimal? PricePrecision = null,
//     decimal? QuantityPrecision = null,
//     bool? IsSpotTradingEnabled = null,
//     bool? IsFuturesTradingEnabled = null,
//     SymbolStatus? Status = null,
//     string? Description = null);

// Order DTO for Telegram Bot
public class OrderDto
{
    public string Asset { get; set; } = "";
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public Guid UserId { get; set; }
    public string Type { get; set; } = "Buy"; // "Buy" or "Sell"
    public string TradingType { get; set; } = "Spot"; // "Spot" or "Futures"
}