using Microsoft.AspNetCore.Mvc;
using TallaEgg.Api.Modules.Wallet.Application;
using TallaEgg.Api.Modules.Wallet.Core;

namespace TallaEgg.Api.Modules.Wallet.Endpoints;

public static class WalletEndpoints
{
    public static void MapWalletEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/wallet")
            .WithTags("Wallet")
            .WithOpenApi();

        // Get balance
        group.MapGet("/balance/{userId:guid}/{asset}", async (
            Guid userId,
            string asset,
            [FromServices] IWalletService walletService) =>
        {
            var balance = await walletService.GetBalanceAsync(userId, asset);
            return Results.Ok(new { userId, asset, balance });
        })
        .WithName("GetBalance")
        .WithSummary("Get wallet balance")
        .WithDescription("Get the balance for a specific asset in user's wallet");

        // Get user wallets
        group.MapGet("/user-wallets/{userId:guid}", async (
            Guid userId,
            [FromServices] IWalletService walletService) =>
        {
            var wallets = await walletService.GetUserWalletsAsync(userId);
            return Results.Ok(wallets);
        })
        .WithName("GetUserWallets")
        .WithSummary("Get user wallets")
        .WithDescription("Get all wallets for a specific user");

        // Deposit
        group.MapPost("/deposit", async (
            [FromBody] DepositRequest request,
            [FromServices] IWalletService walletService) =>
        {
            var result = await walletService.DepositAsync(
                request.UserId, 
                request.Asset, 
                request.Amount, 
                request.ReferenceId);
            
            if (result.success)
            {
                return Results.Ok(new { 
                    success = true, 
                    message = result.message 
                });
            }
            
            return Results.BadRequest(new { 
                success = false, 
                message = result.message 
            });
        })
        .WithName("Deposit")
        .WithSummary("Deposit funds")
        .WithDescription("Deposit funds to user's wallet");

        // Withdraw
        group.MapPost("/withdraw", async (
            [FromBody] WithdrawRequest request,
            [FromServices] IWalletService walletService) =>
        {
            var result = await walletService.WithdrawAsync(
                request.UserId, 
                request.Asset, 
                request.Amount, 
                request.ReferenceId);
            
            if (result.success)
            {
                return Results.Ok(new { 
                    success = true, 
                    message = result.message 
                });
            }
            
            return Results.BadRequest(new { 
                success = false, 
                message = result.message 
            });
        })
        .WithName("Withdraw")
        .WithSummary("Withdraw funds")
        .WithDescription("Withdraw funds from user's wallet");

        // Transfer
        group.MapPost("/transfer", async (
            [FromBody] TransferRequest request,
            [FromServices] IWalletService walletService) =>
        {
            var result = await walletService.TransferAsync(
                request.FromUserId, 
                request.ToUserId, 
                request.Asset, 
                request.Amount);
            
            if (result.success)
            {
                return Results.Ok(new { 
                    success = true, 
                    message = result.message 
                });
            }
            
            return Results.BadRequest(new { 
                success = false, 
                message = result.message 
            });
        })
        .WithName("Transfer")
        .WithSummary("Transfer funds")
        .WithDescription("Transfer funds between users");

        // Get user transactions
        group.MapGet("/transactions/{userId:guid}", async (
            Guid userId,
            [FromQuery] string? asset,
            [FromServices] IWalletService walletService) =>
        {
            var transactions = await walletService.GetUserTransactionsAsync(userId, asset);
            return Results.Ok(transactions);
        })
        .WithName("GetUserTransactions")
        .WithSummary("Get user transactions")
        .WithDescription("Get all transactions for a specific user");

        // Charge wallet
        group.MapPost("/charge", async (
            [FromBody] ChargeRequest request,
            [FromServices] IWalletService walletService) =>
        {
            var result = await walletService.ChargeWalletAsync(
                request.UserId, 
                request.Asset, 
                request.Amount, 
                request.PaymentMethod);
            
            if (result.success)
            {
                return Results.Ok(new { 
                    success = true, 
                    message = result.message 
                });
            }
            
            return Results.BadRequest(new { 
                success = false, 
                message = result.message 
            });
        })
        .WithName("ChargeWallet")
        .WithSummary("Charge wallet")
        .WithDescription("Charge user's wallet with funds");
    }
}

public record DepositRequest(Guid UserId, string Asset, decimal Amount, string? ReferenceId = null);
public record WithdrawRequest(Guid UserId, string Asset, decimal Amount, string? ReferenceId = null);
public record TransferRequest(Guid FromUserId, Guid ToUserId, string Asset, decimal Amount);
public record ChargeRequest(Guid UserId, string Asset, decimal Amount, string? PaymentMethod = null);
