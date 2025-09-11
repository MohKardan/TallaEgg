using Microsoft.EntityFrameworkCore;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Requests.Trade;
using TallaEgg.Core.Requests.Wallet;
using Wallet.Application;
using Wallet.Application.Mappers;
using Wallet.Core;
using Wallet.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WalletDb") ??
        "Server=localhost;Database=TallaEggWallet;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("Wallet.Api")));

builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<IWalletService, WalletService>();
builder.Services.AddScoped<WalletMapper>();

// اضافه کردن CORS
builder.Services.AddCors();

// Add Swagger services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "TallaEgg Wallet API", Version = "v1" });

    // Include XML comments
    var xmlFile = $"{System.Reflection.Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath))
    {
        c.IncludeXmlComments(xmlPath);
    }
});

var app = builder.Build();

// تنظیم CORS
app.UseCors(builder => builder
    .AllowAnyOrigin()
    .AllowAnyMethod()
    .AllowAnyHeader());

// Add Swagger middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TallaEgg Wallet API v1");
    c.RoutePrefix = "api-docs";
});

// Wallet management endpoints
app.MapGet("/api/wallet/balance/{userId}/{asset}", async (Guid userId, string asset, IWalletService walletService) =>
{
    try
    {
        var balance = await walletService.GetBalanceAsync(userId, asset);
        return Results.Ok(ApiResponse<WalletDTO>.Ok(balance, ""));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<WalletDTO>.Fail(ex.Message));
    }
});

app.MapGet("/api/wallet/balances/{userId}", async (Guid userId, IWalletService walletService) =>
{
    var wallets = await walletService.GetUserWalletsAsync(userId);
    return Results.Ok(ApiResponse<IEnumerable<WalletDTO>>.Ok(wallets, "لیست کیف پول های کاربر"));
});

app.MapPost("/api/wallet/deposit", async (WalletRequest request, IWalletService walletService) =>
{
    try
    {
       var result = await walletService.DepositAsync(request.UserId, request.Asset, request.Amount, request.ReferenceId);
       return Results.Ok(ApiResponse<WalletBallanceDTO>.Ok(result, "عملیات با موفقیت انجام شد"));

    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<WalletBallanceDTO>.Fail(ex.Message));
    }
  
});

app.MapPost("/api/wallet/withdrawal", async (WalletRequest request, IWalletService walletService) =>
{
    try
    {
       var result = await walletService.WithdrawalAsync(request.UserId, request.Asset, request.Amount, request.ReferenceId);
       return Results.Ok(ApiResponse<WalletBallanceDTO>.Ok(result, "عملیات با موفقیت انجام شد"));

    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<WalletBallanceDTO>.Fail(ex.Message));
    }
  
});

app.MapPost("/api/wallet/lockBalance", async (WalletRequest request, IWalletService walletService) =>
{
    try
    {
       var result = await walletService.LockBalanceAsync(request.UserId, request.Asset, request.Amount);
       return Results.Ok(ApiResponse<WalletDTO>.Ok(result, "عملیات با موفقیت انجام شد"));

    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<WalletDTO>.Fail(ex.Message));
    }
  
});

app.MapPost("/api/wallet/unlockBalance", async (WalletRequest request, IWalletService walletService) =>
{
    try
    {
        var result = await walletService.UnlockBalanceAsync(request.UserId, request.Asset, request.Amount);
        return Results.Ok(ApiResponse<WalletDTO>.Ok(result, "عملیات با موفقیت انجام شد"));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<WalletDTO>.Fail(ex.Message));
    }
});


app.MapPost("/api/wallet/transaction/trade", async (TradeRequest request, IWalletService walletService) =>
{
    try
    {
        var result = await walletService.MakeTradeAsync(request.FromUserId,request.ToUserId, request.Asset, request.Amount,request.ReferenceId);
        return Results.Ok(ApiResponse<WalletBallanceDTO>.Ok(result, "عملیات با موفقیت انجام شد"));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<WalletBallanceDTO>.Fail(ex.Message));
    }
});

//app.MapPost("/api/wallet/withdraw", async (WithdrawRequest request, IWalletService walletService) =>
//{
//    var result = await walletService.WithdrawAsync(request.UserId, request.Asset, request.Amount, request.ReferenceId);
//    return result.success ? 
//        Results.Ok(new { success = true, message = result.message }) :
//        Results.BadRequest(new { success = false, message = result.message });
//});

//app.MapPost("/api/wallet/charge", async (ChargeRequest request, IWalletService walletService) =>
//{
//    var result = await walletService.ChargeWalletAsync(request.UserId, request.Asset, request.Amount, request.PaymentMethod);
//    return result.success ? 
//        Results.Ok(new { success = true, message = result.message }) :
//        Results.BadRequest(new { success = false, message = result.message });
//});

//app.MapPost("/api/wallet/transfer", async (TransferRequest request, IWalletService walletService) =>
//{
//    var result = await walletService.TransferAsync(request.FromUserId, request.ToUserId, request.Asset, request.Amount);
//    return result.success ? 
//        Results.Ok(new { success = true, message = result.message }) :
//        Results.BadRequest(new { success = false, message = result.message });
//});

app.MapGet("/api/wallet/transactions/{userId}", async (Guid userId, string? asset, IWalletService walletService) =>
{
    var transactions = await walletService.GetUserTransactionsAsync(userId, asset);
    return Results.Ok(transactions);
});

/// <summary>
/// ایجاد کیف پول‌های پیش‌فرض برای کاربر جدید (ریال، طلا، اعتبار طلا)
/// </summary>
/// <param name="userId">شناسه کاربر</param>
/// <param name="walletService">سرویس کیف پول</param>
/// <returns>لیست کیف پول‌های ایجاد شده</returns>
/// <response code="200">کیف پول‌های پیش‌فرض با موفقیت ایجاد شدند</response>
/// <response code="400">خطا در ایجاد کیف پول‌ها</response>
app.MapPost("/api/wallet/create-default/{userId}", async (Guid userId, IWalletService walletService) =>
{
    try
    {
        var wallets = await walletService.CreateDefaultWalletsAsync(userId);
        return Results.Ok(ApiResponse<IEnumerable<WalletDTO>>.Ok(wallets, "کیف پول‌های پیش‌فرض با موفقیت ایجاد شدند"));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<IEnumerable<WalletDTO>>.Fail(ex.Message));
    }
});

//// Internal wallet operations (for matching engine)
//app.MapPost("/api/wallet/internal/credit", async (CreditRequest request, IWalletService walletService) =>
//{
//    //var success = await walletService.CreditAsync(request.UserId, request.Asset, request.Amount);
//    //return success ? 
//    //    Results.Ok(new { success = true }) :
//    //    Results.BadRequest(new { success = false, message = "خطا در افزایش موجودی" });
//});

//app.MapPost("/api/wallet/internal/debit", async (DebitRequest request, IWalletService walletService) =>
//{
//    var success = await walletService.DebitAsync(request.UserId, request.Asset, request.Amount);
//    return success ? 
//        Results.Ok(new { success = true }) :
//        Results.BadRequest(new { success = false, message = "خطا در کاهش موجودی" });
//});

app.Run();

// Request models
public record WithdrawRequest(Guid UserId, string Asset, decimal Amount, string? ReferenceId = null);
public record ChargeRequest(Guid UserId, string Asset, decimal Amount, string? PaymentMethod = null);
public record TransferRequest(Guid FromUserId, Guid ToUserId, string Asset, decimal Amount);
public record CreditRequest(Guid UserId, string Asset, decimal Amount);
public record DebitRequest(Guid UserId, string Asset, decimal Amount);

// Market order balance request models
public record ValidateBalanceRequest(Guid UserId, string Asset, decimal Amount, OrderSide orderSide); // 0 = Buy, 1 = Sell
public record UpdateBalanceRequest(Guid UserId, string Asset, decimal Amount, OrderSide orderSide, Guid OrderId); // 0 = Buy, 1 = Sell 