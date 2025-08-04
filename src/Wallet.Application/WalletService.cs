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
} 