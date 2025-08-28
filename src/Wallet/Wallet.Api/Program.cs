using Microsoft.EntityFrameworkCore;
using Wallet.Core;
using Wallet.Infrastructure;
using Wallet.Application;
using TallaEgg.Core.Requests.Wallet;
using TallaEgg.Core.DTOs;
using TallaEgg.Core.DTOs.Wallet;
using Wallet.Application.Mappers;

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
    c.SwaggerDoc("v1", new() { Title = "TallaEgg Users API", Version = "v1" });

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
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TallaEgg Users API v1");
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

app.MapPost("/api/wallet/deposit", async (DepositRequest request, IWalletService walletService) =>
{
    try
    {
       var result = await walletService.DepositAsync(request.UserId, request.Asset, request.Amount, request.ReferenceId);
       return Results.Ok(ApiResponse<WalletDepositDTO>.Ok(result, "عملیات با موفقیت انجام شد"));

    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<WalletDepositDTO>.Fail(ex.Message));
    }
  
});

app.MapPost("/api/wallet/lockBalance", async (BaseWalletRequest request, IWalletService walletService) =>
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

app.MapPost("/api/wallet/unlockBalance", async (BaseWalletRequest request, IWalletService walletService) =>
{
    try
    {
        var result = await walletService.UnlockBalanceAsync(request.UserId, request.Asset, request.Amount);
        return Results.Ok(ApiResponse<bool>.Ok(result, "عملیات با موفقیت انجام شد"));
    }
    catch (Exception ex)
    {
        return Results.BadRequest(ApiResponse<bool>.Fail(ex.Message));
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

// Market order balance validation and update endpoints
app.MapPost("/api/wallet/market/validate-balance", async (ValidateBalanceRequest request, IWalletService walletService) =>
{
    var result = await walletService.ValidateBalanceForMarketOrderAsync(request.UserId, request.Asset, request.Amount, request.OrderType);
    return result.success ? 
        Results.Ok(new { success = true, message = result.message, hasSufficientBalance = result.hasSufficientBalance }) :
        Results.BadRequest(new { success = false, message = result.message });
});

app.MapPost("/api/wallet/market/update-balance", async (UpdateBalanceRequest request, IWalletService walletService) =>
{
    var result = await walletService.UpdateBalanceForMarketOrderAsync(request.UserId, request.Asset, request.Amount, request.OrderType, request.OrderId);
    return result.success ? 
        Results.Ok(new { success = true, message = result.message }) :
        Results.BadRequest(new { success = false, message = result.message });
});

app.Run();

// Request models
public record WithdrawRequest(Guid UserId, string Asset, decimal Amount, string? ReferenceId = null);
public record ChargeRequest(Guid UserId, string Asset, decimal Amount, string? PaymentMethod = null);
public record TransferRequest(Guid FromUserId, Guid ToUserId, string Asset, decimal Amount);
public record CreditRequest(Guid UserId, string Asset, decimal Amount);
public record DebitRequest(Guid UserId, string Asset, decimal Amount);

// Market order balance request models
public record ValidateBalanceRequest(Guid UserId, string Asset, decimal Amount, int OrderType); // 0 = Buy, 1 = Sell
public record UpdateBalanceRequest(Guid UserId, string Asset, decimal Amount, int OrderType, Guid OrderId); // 0 = Buy, 1 = Sell 