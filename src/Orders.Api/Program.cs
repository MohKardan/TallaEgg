using Microsoft.EntityFrameworkCore;
using Orders.Application;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Order;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();


var app = builder.Build();

// تنظیم اتصال به دیتابیس SQL Server
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb") ??
        "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("Orders.Api")));



app.MapGet("/api/order/userorders/{userId}",
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




app.Run();

