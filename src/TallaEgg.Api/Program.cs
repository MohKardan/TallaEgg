using Microsoft.EntityFrameworkCore;
using Orders.Core;
using Orders.Infrastructure;
using Orders.Application;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server (در appsettings.json هم می‌توان قرار داد)
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb") ??
        "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;"));

builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<CreateOrderCommandHandler>();

var app = builder.Build();

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

app.Run();