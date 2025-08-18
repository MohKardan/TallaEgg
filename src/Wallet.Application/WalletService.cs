
using Wallet.Core;

namespace Wallet.Application;

public class WalletService : IWalletService
{
    private readonly IWalletRepository _walletRepository;

    public WalletService(IWalletRepository walletRepository)
    {
        _walletRepository = walletRepository;
    }

    public async Task<decimal> GetBalanceAsync(Guid userId, string asset)
    {
        var wallet = await _walletRepository.GetWalletAsync(userId, asset);
        return wallet?.Balance ?? 0;
    }

    public async Task<bool> CreditAsync(Guid userId, string asset, decimal amount)
    {
        if (amount <= 0)
            return false;

        var wallet = await _walletRepository.GetWalletAsync(userId, asset);
        
        if (wallet == null)
        {
            // Create new wallet
            wallet = new WalletEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Asset = asset,
                Balance = amount,
                LockedBalance = 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            await _walletRepository.CreateWalletAsync(wallet);
        }
        else
        {
            // Update existing wallet
            wallet.Balance += amount;
            wallet.UpdatedAt = DateTime.UtcNow;
            await _walletRepository.UpdateWalletAsync(wallet);
        }

        // Create transaction record
        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Asset = asset,
            Amount = amount,
            Type = TransactionType.Deposit,
            Status = TransactionStatus.Completed,
            Description = "Credit transaction",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        await _walletRepository.CreateTransactionAsync(transaction);

        return true;
    }

    public async Task<bool> DebitAsync(Guid userId, string asset, decimal amount)
    {
        if (amount <= 0)
            return false;

        var wallet = await _walletRepository.GetWalletAsync(userId, asset);
        if (wallet == null || wallet.Balance < amount)
            return false;

        // Update wallet
        wallet.Balance -= amount;
        wallet.UpdatedAt = DateTime.UtcNow;
        await _walletRepository.UpdateWalletAsync(wallet);

        // Create transaction record
        var transaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Asset = asset,
            Amount = amount,
            Type = TransactionType.Withdrawal,
            Status = TransactionStatus.Completed,
            Description = "Debit transaction",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        await _walletRepository.CreateTransactionAsync(transaction);

        return true;
    }

    public async Task<bool> LockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        return await _walletRepository.LockBalanceAsync(userId, asset, amount);
    }

    public async Task<bool> UnlockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        return await _walletRepository.UnlockBalanceAsync(userId, asset, amount);
    }

    public async Task<IEnumerable<WalletEntity>> GetUserWalletsAsync(Guid userId)
    {
        return await _walletRepository.GetUserWalletsAsync(userId);
    }

    public async Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null)
    {
        return await _walletRepository.GetUserTransactionsAsync(userId, asset);
    }

    public async Task<(bool success, string message)> DepositAsync(Guid userId, string asset, decimal amount, string? referenceId = null)
    {
        if (amount <= 0)
            return (false, "مقدار باید بزرگتر از صفر باشد.");

        var success = await CreditAsync(userId, asset, amount);
        if (success)
        {
            // Update transaction with reference
            var transactions = await _walletRepository.GetUserTransactionsAsync(userId, asset);
            var lastTransaction = transactions.FirstOrDefault();
            if (lastTransaction != null && !string.IsNullOrEmpty(referenceId))
            {
                lastTransaction.ReferenceId = referenceId;
                lastTransaction.Description = "Deposit transaction";
                await _walletRepository.UpdateTransactionAsync(lastTransaction);
            }
        }

        return success ? (true, "واریز با موفقیت انجام شد.") : (false, "خطا در واریز.");
    }

    public async Task<(bool success, string message)> WithdrawAsync(Guid userId, string asset, decimal amount, string? referenceId = null)
    {
        if (amount <= 0)
            return (false, "مقدار باید بزرگتر از صفر باشد.");

        var success = await DebitAsync(userId, asset, amount);
        if (success)
        {
            // Update transaction with reference
            var transactions = await _walletRepository.GetUserTransactionsAsync(userId, asset);
            var lastTransaction = transactions.FirstOrDefault();
            if (lastTransaction != null && !string.IsNullOrEmpty(referenceId))
            {
                lastTransaction.ReferenceId = referenceId;
                lastTransaction.Description = "Withdrawal transaction";
                await _walletRepository.UpdateTransactionAsync(lastTransaction);
            }
        }

        return success ? (true, "برداشت با موفقیت انجام شد.") : (false, "خطا در برداشت.");
    }

    public async Task<(bool success, string message)> TransferAsync(Guid fromUserId, Guid toUserId, string asset, decimal amount)
    {
        if (amount <= 0)
            return (false, "مقدار باید بزرگتر از صفر باشد.");

        if (fromUserId == toUserId)
            return (false, "انتقال به خود امکان‌پذیر نیست.");

        // Debit from source user
        var debitSuccess = await DebitAsync(fromUserId, asset, amount);
        if (!debitSuccess)
            return (false, "موجودی ناکافی برای انتقال.");

        // Credit to destination user
        var creditSuccess = await CreditAsync(toUserId, asset, amount);
        if (!creditSuccess)
        {
            // Rollback - credit back to source user
            await CreditAsync(fromUserId, asset, amount);
            return (false, "خطا در انتقال.");
        }

        // Create transfer transaction records
        var fromTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            UserId = fromUserId,
            Asset = asset,
            Amount = amount,
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Completed,
            Description = $"Transfer to user {toUserId}",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        await _walletRepository.CreateTransactionAsync(fromTransaction);

        var toTransaction = new WalletTransaction
        {
            Id = Guid.NewGuid(),
            UserId = toUserId,
            Asset = asset,
            Amount = amount,
            Type = TransactionType.Transfer,
            Status = TransactionStatus.Completed,
            Description = $"Transfer from user {fromUserId}",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
        await _walletRepository.CreateTransactionAsync(toTransaction);

        return (true, "انتقال با موفقیت انجام شد.");
    }

    public async Task<(bool success, string message)> ChargeWalletAsync(Guid userId, string asset, decimal amount, string? paymentMethod = null)
    {
        if (amount <= 0)
            return (false, "مقدار شارژ باید بزرگتر از صفر باشد.");

        if (amount > 1000000) // محدودیت شارژ: حداکثر 1 میلیون
            return (false, "مقدار شارژ از حد مجاز بیشتر است.");

        // شارژ کیف پول
        var success = await CreditAsync(userId, asset, amount);
        if (success)
        {
            // ایجاد تراکنش شارژ
            var transaction = new WalletTransaction
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                Asset = asset,
                Amount = amount,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                Description = $"شارژ کیف پول - روش پرداخت: {paymentMethod ?? "نامشخص"}",
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            };
            await _walletRepository.CreateTransactionAsync(transaction);
        }

        return success ? (true, "شارژ کیف پول با موفقیت انجام شد.") : (false, "خطا در شارژ کیف پول.");
    }

    public async Task<(bool success, string message, bool hasSufficientBalance)> ValidateBalanceForMarketOrderAsync(Guid userId, string asset, decimal amount, int orderType)
    {
        try
        {
            if (amount <= 0)
                return (false, "مقدار سفارش باید بزرگتر از صفر باشد.", false);

            var wallet = await _walletRepository.GetWalletAsync(userId, asset);
            var currentBalance = wallet?.Balance ?? 0;

            if (orderType == 0) // Buy order
            {
                // For buy orders, we need to check if user has enough USDT (or base currency)
                // This is a simplified check - in real implementation, you'd need to get the price
                var requiredAmount = amount; // This should be amount * price in real implementation
                
                if (currentBalance < requiredAmount)
                {
                    return (true, $"موجودی ناکافی. موجودی فعلی: {currentBalance}, مقدار مورد نیاز: {requiredAmount}", false);
                }
                
                return (true, "موجودی کافی است.", true);
            }
            else if (orderType == 1) // Sell order
            {
                // For sell orders, we need to check if user has enough of the asset to sell
                if (currentBalance < amount)
                {
                    return (true, $"موجودی ناکافی. موجودی فعلی: {currentBalance}, مقدار مورد نیاز: {amount}", false);
                }
                
                return (true, "موجودی کافی است.", true);
            }
            else
            {
                return (false, "نوع سفارش نامعتبر است.", false);
            }
        }
        catch (Exception ex)
        {
            return (false, $"خطا در بررسی موجودی: {ex.Message}", false);
        }
    }

    public async Task<(bool success, string message)> UpdateBalanceForMarketOrderAsync(Guid userId, string asset, decimal amount, int orderType, Guid orderId)
    {
        try
        {
            if (amount <= 0)
                return (false, "مقدار سفارش باید بزرگتر از صفر باشد.");

            if (orderType == 0) // Buy order
            {
                // For buy orders, debit the base currency (e.g., USDT) and credit the asset
                var baseCurrency = "USDT"; // This should be configurable
                var price = 45000; // This should come from the order or price service
                var totalCost = amount * price;

                // Debit base currency
                var debitSuccess = await DebitAsync(userId, baseCurrency, totalCost);
                if (!debitSuccess)
                    return (false, "موجودی ناکافی برای خرید.");

                // Credit the asset
                var creditSuccess = await CreditAsync(userId, asset, amount);
                if (!creditSuccess)
                {
                    // Rollback - credit back the base currency
                    await CreditAsync(userId, baseCurrency, totalCost);
                    return (false, "خطا در به‌روزرسانی موجودی دارایی.");
                }

                // Create transaction records
                var buyTransaction = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Asset = asset,
                    Amount = amount,
                    Type = TransactionType.Deposit,
                    Status = TransactionStatus.Completed,
                    Description = $"Market buy order {orderId} - Received {amount} {asset}",
                    ReferenceId = orderId.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };
                await _walletRepository.CreateTransactionAsync(buyTransaction);

                var costTransaction = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Asset = baseCurrency,
                    Amount = totalCost,
                    Type = TransactionType.Withdrawal,
                    Status = TransactionStatus.Completed,
                    Description = $"Market buy order {orderId} - Paid {totalCost} {baseCurrency}",
                    ReferenceId = orderId.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };
                await _walletRepository.CreateTransactionAsync(costTransaction);

                return (true, "موجودی با موفقیت به‌روزرسانی شد.");
            }
            else if (orderType == 1) // Sell order
            {
                // For sell orders, debit the asset and credit the base currency
                var baseCurrency = "USDT"; // This should be configurable
                var price = 45000; // This should come from the order or price service
                var totalValue = amount * price;

                // Debit the asset
                var debitSuccess = await DebitAsync(userId, asset, amount);
                if (!debitSuccess)
                    return (false, "موجودی ناکافی برای فروش.");

                // Credit base currency
                var creditSuccess = await CreditAsync(userId, baseCurrency, totalValue);
                if (!creditSuccess)
                {
                    // Rollback - credit back the asset
                    await CreditAsync(userId, asset, amount);
                    return (false, "خطا در به‌روزرسانی موجودی ارز پایه.");
                }

                // Create transaction records
                var sellTransaction = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Asset = asset,
                    Amount = amount,
                    Type = TransactionType.Withdrawal,
                    Status = TransactionStatus.Completed,
                    Description = $"Market sell order {orderId} - Sold {amount} {asset}",
                    ReferenceId = orderId.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };
                await _walletRepository.CreateTransactionAsync(sellTransaction);

                var valueTransaction = new WalletTransaction
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    Asset = baseCurrency,
                    Amount = totalValue,
                    Type = TransactionType.Deposit,
                    Status = TransactionStatus.Completed,
                    Description = $"Market sell order {orderId} - Received {totalValue} {baseCurrency}",
                    ReferenceId = orderId.ToString(),
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                };
                await _walletRepository.CreateTransactionAsync(valueTransaction);

                return (true, "موجودی با موفقیت به‌روزرسانی شد.");
            }
            else
            {
                return (false, "نوع سفارش نامعتبر است.");
            }
        }
        catch (Exception ex)
        {
            return (false, $"خطا در به‌روزرسانی موجودی: {ex.Message}");
        }
    }
} 