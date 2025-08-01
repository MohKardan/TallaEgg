using Microsoft.EntityFrameworkCore;
using Orders.Core;
using Orders.Infrastructure;
using Orders.Application;

var builder = WebApplication.CreateBuilder(args);

// اتصال به SQL Server (یا هر دیتابیس دیگر)
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb") ??
        "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;"));

// DI برای Repository و Handler
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<CreateOrderCommandHandler>();

var app = builder.Build();

// Endpoint ثبت سفارش
app.MapPost("/api/order", async (CreateOrderCommand cmd, CreateOrderCommandHandler handler) =>
{
    var result = await handler.Handle(cmd);
    return Results.Ok(result);
});

// Endpoint نمایش سفارشات یک دارایی
app.MapGet("/api/orders/{asset}", async (string asset, IOrderRepository repo) =>
{
    var list = await repo.GetOrdersByAssetAsync(asset);
    return Results.Ok(list);
});

app.Run();