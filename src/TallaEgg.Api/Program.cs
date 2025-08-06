using Microsoft.EntityFrameworkCore;
using Orders.Core;
using Orders.Infrastructure;
using Orders.Application;
using Users.Application;
using Users.Core;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server (در appsettings.json هم می‌توان قرار داد)
builder.Services.AddDbContext<OrdersDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("OrdersDb") ??
        "Server=localhost;Database=TallaEggOrders;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("TallaEgg.Api")));

// فقط سرویس‌های مربوط به Orders و Price ثبت شوند
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IPriceRepository, PriceRepository>();
builder.Services.AddScoped<CreateOrderCommandHandler>();
builder.Services.AddScoped<PriceService>();
builder.Services.AddScoped<IAuthorizationService, AuthorizationService>();

// اگر نیاز به اطلاعات کاربر دارید، یک کلاینت HTTP برای ارتباط با سرویس Users بسازید و ثبت کنید
// builder.Services.AddHttpClient<IUsersApiClient, UsersApiClient>(client => { client.BaseAddress = new Uri("http://users-service-url"); });

// اضافه کردن CORS
builder.Services.AddCors();

var app = builder.Build();

// تنظیم CORS
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

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
app.MapGet("/api/user/getUserIdByInvitationCode/{invitationCode}", async ([FromRoute] string invitationCode, [FromServices] Users.Application.UserService userService) =>
{
    var id = await userService.GetUserIdByInvitationCode(invitationCode);
    return Results.Ok(id);
});
app.MapPost("/api/user/validate-invitation", async ([FromBody] ValidateInvitationRequest request, [FromServices] Users.Application.UserService userService) =>
{
    var result = await userService.ValidateInvitationCodeAsync(request.InvitationCode);
    return Results.Ok(new { isValid = result.isValid, message = result.message });
});
app.MapPost("/api/user/register", async ([FromBody] Users.Core.User user, [FromServices] Users.Application.UserService userService) =>
{
    try
    {
        var res = await userService.RegisterUserAsync(user);
        return Results.Ok(new { success = true, userId = res.Id });
    }
    catch (Exception ex)
    {
        return Results.BadRequest(new { success = false, message = ex.Message });
    }
});
app.MapPost("/api/user/update-phone", async ([FromBody] UpdatePhoneRequest request, [FromServices] Users.Application.UserService userService) =>
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
app.MapGet("/api/user/{telegramId}", async ([FromRoute] long telegramId, [FromServices] Users.Application.UserService userService) =>
{
    var user = await userService.GetUserByTelegramIdAsync(telegramId);
    if (user == null)
        return Results.NotFound();
    return Results.Ok(user);
});
app.MapPost("/api/user/update-role", async ([FromBody] UpdateUserRoleRequest request, [FromServices] Users.Core.IUserRepository userRepository, [FromServices] IAuthorizationService authService) =>
{
    var canManageUsers = await authService.CanManageUsersAsync(request.RequestingUserId);
    if (!canManageUsers)
        return Results.Forbid();
    if (!Enum.TryParse<Users.Core.UserRole>(request.NewRole, true, out var newRoleEnum))
        return Results.BadRequest(new { message = "نقش نامعتبر است." });
    var user = await userRepository.UpdateUserRoleAsync(request.UserId, newRoleEnum);
    if (user == null)
        return Results.NotFound(new { message = "کاربر یافت نشد." });
    return Results.Ok(new { success = true, message = "نقش کاربر با موفقیت به‌روزرسانی شد.", user });
});
app.MapGet("/api/users/by-role/{role}", async ([FromRoute] string role, [FromServices] Users.Core.IUserRepository userRepository, [FromServices] IAuthorizationService authService) =>
{
    var canManageUsers = await authService.CanManageUsersAsync(Guid.Empty);
    if (!canManageUsers)
        return Results.Forbid();
    if (!Enum.TryParse<UserRole>(role, true, out var userRole))
        return Results.BadRequest(new { message = "نقش نامعتبر است." });
    var users = await userRepository.GetUsersByRoleAsync(userRole);
    return Results.Ok(users);
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
public record UpdateUserRoleRequest(Guid RequestingUserId, Guid UserId, string NewRole);