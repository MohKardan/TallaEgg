using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Orders.Application;
using Orders.Core;
using Orders.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server (در appsettings.json هم می‌توان قرار داد)
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb") ??
        "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("TallaEgg.Api")));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IPriceRepository, PriceRepository>();
builder.Services.AddScoped<CreateOrderCommandHandler>();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<PriceService>();

var app = builder.Build();



// ثبت سفارش جدید توسط مشتری
app.MapGet("/api/test", async () =>
{
    
    return Results.Ok("ok");
});

// ثبت سفارش جدید توسط مشتری
app.MapPost("/api/order", async (CreateOrderCommand cmd, CreateOrderCommandHandler handler) =>
{
    var result = await handler.Handle(cmd);
    return Results.Ok(result);
});

// لیست سفارشات یک دارایی
app.MapGet("/api/orders/{asset}", async (string asset, IOrderRepository repo) =>
{
    var list = await repo.GetOrdersByAssetAsync(asset);
    return Results.Ok(list);
});

// User management endpoints
app.MapPost("/api/user/validate-invitation", async (ValidateInvitationRequest request, UserService userService) =>
{
    var result = await userService.ValidateInvitationCodeAsync(request.InvitationCode);
    return Results.Ok(new { isValid = result.isValid, message = result.message });
});

app.MapPost("/api/user/register", async (RegisterUserRequest request, UserService userService) =>
{
    try
    {
        var user = await userService.RegisterUserAsync(
            request.TelegramId, 
            request.Username, 
            request.FirstName, 
            request.LastName, 
            request.InvitationCode);
        return Results.Ok(new { success = true, userId = user.Id });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapPost("/api/user/update-phone", async (UpdatePhoneRequest request, UserService userService) =>
{
    try
    {
        var user = await userService.UpdateUserPhoneAsync(request.TelegramId, request.PhoneNumber);
        return Results.Ok(new { success = true, message = "شماره تلفن با موفقیت ثبت شد." });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapGet("/api/user/{telegramId}", async (long telegramId, UserService userService) =>
{
    var user = await userService.GetUserByTelegramIdAsync(telegramId);
    if (user == null)
        return Results.NotFound();
    return Results.Ok(user);
});

// Price endpoints
app.MapGet("/api/prices/{asset}", async (string asset, PriceService priceService) =>
{
    var price = await priceService.GetLatestPriceAsync(asset);
    if (price == null)
        return Results.NotFound();
    return Results.Ok(price);
});

app.MapGet("/api/prices", async (PriceService priceService) =>
{
    var prices = await priceService.GetAllPricesAsync();
    return Results.Ok(prices);
});

app.MapPost("/api/prices", async (UpdatePriceRequest request, PriceService priceService) =>
{
    try
    {
        var price = await priceService.UpdatePriceAsync(request.Asset, request.BuyPrice, request.SellPrice, request.Source);
        return Results.Ok(price);
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});



app.Run();

// Request models
public record ValidateInvitationRequest(string InvitationCode);
public record RegisterUserRequest(long TelegramId, string? Username, string? FirstName, string? LastName, string InvitationCode);
public record UpdatePhoneRequest(long TelegramId, string PhoneNumber);
public record UpdatePriceRequest(string Asset, decimal BuyPrice, decimal SellPrice, string Source = "Manual");