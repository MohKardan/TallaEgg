using Microsoft.EntityFrameworkCore;
using Wallet.Core;
using Wallet.Infrastructure;
using Wallet.Application;

var builder = WebApplication.CreateBuilder(args);

// تنظیم اتصال به دیتابیس SQL Server
builder.Services.AddDbContext<WalletDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("WalletDb") ??
        "Server=localhost;Database=TallaEggWallet;Trusted_Connection=True;TrustServerCertificate=True;",
        b => b.MigrationsAssembly("Wallet.Api")));

builder.Services.AddScoped<IWalletRepository, WalletRepository>();
builder.Services.AddScoped<WalletService>();

var app = builder.Build();

// Wallet management endpoints
app.MapGet("/api/wallet/balance/{userId}/{asset}", async (Guid userId, string asset, WalletService walletService) =>
{
    var balance = await walletService.GetBalanceAsync(userId, asset);
    return Results.Ok(new { userId, asset, balance });
});

app.MapGet("/api/wallet/balances/{userId}", async (Guid userId, WalletService walletService) =>
{
    var wallets = await walletService.GetUserWalletsAsync(userId);
    return Results.Ok(wallets);
});

app.MapPost("/api/wallet/deposit", async (DepositRequest request, WalletService walletService) =>
{
    var result = await walletService.DepositAsync(request.UserId, request.Asset, request.Amount, request.ReferenceId);
    return result.success ? 
        Results.Ok(new { success = true, message = result.message }) :
        Results.BadRequest(new { success = false, message = result.message });
});

app.MapPost("/api/wallet/withdraw", async (WithdrawRequest request, WalletService walletService) =>
{
    var result = await walletService.WithdrawAsync(request.UserId, request.Asset, request.Amount, request.ReferenceId);
    return result.success ? 
        Results.Ok(new { success = true, message = result.message }) :
        Results.BadRequest(new { success = false, message = result.message });
});

app.MapPost("/api/wallet/transfer", async (TransferRequest request, WalletService walletService) =>
{
    var result = await walletService.TransferAsync(request.FromUserId, request.ToUserId, request.Asset, request.Amount);
    return result.success ? 
        Results.Ok(new { success = true, message = result.message }) :
        Results.BadRequest(new { success = false, message = result.message });
});

app.MapGet("/api/wallet/transactions/{userId}", async (Guid userId, string? asset, WalletService walletService) =>
{
    var transactions = await walletService.GetUserTransactionsAsync(userId, asset);
    return Results.Ok(transactions);
});

// Internal wallet operations (for matching engine)
app.MapPost("/api/wallet/internal/credit", async (CreditRequest request, WalletService walletService) =>
{
    var success = await walletService.CreditAsync(request.UserId, request.Asset, request.Amount);
    return success ? 
        Results.Ok(new { success = true }) :
        Results.BadRequest(new { success = false, message = "خطا در افزایش موجودی" });
});

app.MapPost("/api/wallet/internal/debit", async (DebitRequest request, WalletService walletService) =>
{
    var success = await walletService.DebitAsync(request.UserId, request.Asset, request.Amount);
    return success ? 
        Results.Ok(new { success = true }) :
        Results.BadRequest(new { success = false, message = "خطا در کاهش موجودی" });
});

app.Run();

// Request models
public record DepositRequest(Guid UserId, string Asset, decimal Amount, string? ReferenceId = null);
public record WithdrawRequest(Guid UserId, string Asset, decimal Amount, string? ReferenceId = null);
public record TransferRequest(Guid FromUserId, Guid ToUserId, string Asset, decimal Amount);
public record CreditRequest(Guid UserId, string Asset, decimal Amount);
public record DebitRequest(Guid UserId, string Asset, decimal Amount); 