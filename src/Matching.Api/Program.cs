using Microsoft.EntityFrameworkCore;
using Matching.Core;
using Matching.Infrastructure;
using Matching.Application;
using Wallet.Application;
using Wallet.Core;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server
builder.Services.AddDbContext<MatchingDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MatchingDb") ??
        "Server=localhost;Database=TallaEggMatching;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("Matching.Api")));

builder.Services.AddScoped<IMatchingRepository, MatchingRepository>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<MatchingEngine>();

var app = builder.Build();

// Order management endpoints
app.MapPost("/api/orders/place", async (PlaceOrderRequest request, MatchingEngine matchingEngine) =>
{
    try
    {
        var result = await matchingEngine.PlaceOrderAsync(
            request.UserId, 
            request.Asset, 
            request.Amount, 
            request.Price, 
            request.Type);
        
        if (result.success)
        {
            return Results.Ok(new { success = true, message = result.message, order = result.order });
        }
        return Results.BadRequest(new { success = false, message = result.message });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});

app.MapPost("/api/orders/cancel", async (CancelOrderRequest request, MatchingEngine matchingEngine) =>
{
    var result = await matchingEngine.CancelOrderAsync(request.OrderId, request.UserId);
    return result.success ? 
        Results.Ok(new { success = true, message = result.message }) :
        Results.BadRequest(new { success = false, message = result.message });
});

app.MapGet("/api/orders/user/{userId}", async (Guid userId, string? asset, MatchingEngine matchingEngine) =>
{
    var orders = await matchingEngine.GetUserOrdersAsync(userId, asset);
    return Results.Ok(orders);
});

app.MapGet("/api/orders/book/{asset}", async (string asset, int depth, MatchingEngine matchingEngine) =>
{
    var orderBook = await matchingEngine.GetOrderBookAsync(asset, depth);
    return Results.Ok(orderBook);
});

// Trade endpoints
app.MapGet("/api/trades/user/{userId}", async (Guid userId, string? asset, MatchingEngine matchingEngine) =>
{
    var trades = await matchingEngine.GetUserTradesAsync(userId, asset);
    return Results.Ok(trades);
});

app.MapGet("/api/trades/recent/{asset}", async (string asset, int count, MatchingEngine matchingEngine) =>
{
    var trades = await matchingEngine.GetRecentTradesAsync(asset, count);
    return Results.Ok(trades);
});

app.Run();

// Request models
public record PlaceOrderRequest(Guid UserId, string Asset, decimal Amount, decimal Price, OrderType Type);
public record CancelOrderRequest(Guid OrderId, Guid UserId);