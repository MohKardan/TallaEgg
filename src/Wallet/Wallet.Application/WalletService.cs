
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Enums.Wallet;
using TallaEgg.Core.Utilties;
using Wallet.Application.Mappers;
using Wallet.Core;

namespace Wallet.Application;

public class WalletService : IWalletService
{
    private readonly IWalletRepository _walletRepository;
    private readonly WalletMapper _walletMapper;

    public WalletService(IWalletRepository walletRepository, WalletMapper walletMapper)
    {
        _walletRepository = walletRepository;
        _walletMapper = walletMapper;
    }

    public async Task<WalletDTO> GetBalanceAsync(Guid userId, string asset)
    {
        var wallet = await _walletRepository.GetWalletAsync(userId, asset);
        if (wallet == null) throw new Exception("کیف پول پیدا نشد");
        return _walletMapper.Map(wallet);
    }

    public async Task<(WalletEntity walletEntity, Transaction transactionEntity)> CreditAsync(Guid userId, string asset, decimal amount, string? refId = null)
    {
   

        var wallet = await _walletRepository.GetWalletAsync(userId, asset);

        if (wallet == null)
        {
            // Create new wallet
            wallet = WalletEntity.Create
            (
                 userId,
                 asset
            );
            await _walletRepository.CreateWalletAsync(wallet);

        }
            // Update existing wallet
            // Create transaction record
            var transaction = Transaction.Create(
                wallet.Id,
                amount,
                asset,
                TransactionType.Deposit,
                wallet.Balance,
                wallet.Balance + amount,
                null,
                TransactionStatus.Completed,
                "Credit transaction",
                refId,
                null
            );
            wallet.IncreaseBalance(amount);
            await _walletRepository.UpdateWalletAsync(wallet,transaction);
        return (wallet, transaction);
             
    }

    public async Task<(WalletEntity walletEntity, Transaction transactionEntity)> DeCreditAsync(Guid userId, string asset, decimal amount, string? refId = null)
    {


        var wallet = await _walletRepository.GetWalletAsync(userId, asset);

        if (wallet == null)
            throw new ArgumentException("کیف پول وجود ندارد");
       

        // Update existing wallet
        // Create transaction record
        var transaction = Transaction.Create(
            wallet.Id,
            amount,
            asset,
            TransactionType.Withdraw,
            wallet.Balance,
            wallet.Balance - amount,
            null,
            TransactionStatus.Completed,
            "DeCredit transaction",
            refId,
            null
        );
        wallet.DecreaseBalance(amount);
        await _walletRepository.UpdateWalletAsync(wallet, transaction);
        return (wallet, transaction);

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
            Type = TransactionType.Withdraw,
            Status = TransactionStatus.Completed,
            Description = "Debit transaction",
            CreatedAt = DateTime.UtcNow,
            CompletedAt = DateTime.UtcNow
        };
       // await _walletRepository.CreateTransactionAsync(transaction);

        return true;
    }

    public async Task<WalletDTO> LockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        var wallet = await _walletRepository.LockBalanceAsync(userId, asset, amount);
        return _walletMapper.Map(wallet);

    }

    public async Task<WalletDTO> UnlockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        var wallet = await _walletRepository.UnlockBalanceAsync(userId, asset, amount);
        return _walletMapper.Map(wallet);
    }

    public async Task<IEnumerable<WalletDTO>> GetUserWalletsAsync(Guid userId)
    {
        var wallets = await _walletRepository.GetUserWalletsAsync(userId);
        return _walletMapper.Map(wallets);

    }

    public async Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null)
    {
        return await _walletRepository.GetUserTransactionsAsync(userId, asset);
    }

    public async Task<WalletBallanceDTO> DepositAsync(Guid userId, string asset, decimal amount, string? referenceId = null)
    {
        

        var result = await CreditAsync(userId, asset, amount,referenceId);
      

        return new WalletBallanceDTO
        {
            Asset = result.walletEntity.Asset,
            BalanceBefore = result.transactionEntity.BallanceBefore,
            BalanceAfter = result.transactionEntity.BallanceAfter,
            LockedBalance = result.walletEntity.LockedBalance,
            UpdatedAt = result.walletEntity.UpdatedAt,
            TrackingCode = result.transactionEntity.TrackingCode,
        };
    }

    public async Task<WalletBallanceDTO> WithdrawalAsync(Guid userId, string asset, decimal amount, string? referenceId = null)
    {
       
        var result = await DeCreditAsync(userId, asset, amount,referenceId);
      
        return new WalletBallanceDTO
        {
            Asset = result.walletEntity.Asset,
            BalanceBefore = result.transactionEntity.BallanceBefore,
            BalanceAfter = result.transactionEntity.BallanceAfter,
            LockedBalance = result.walletEntity.LockedBalance,
            UpdatedAt = result.walletEntity.UpdatedAt,
            TrackingCode = result.transactionEntity.TrackingCode,
        };
    }

    public async Task<WalletBallanceDTO> MakeTradeAsync(Guid fromUserId, Guid toUserId,string asset, decimal amount, string referenceId)
    {
        
        var fromWallet = await _walletRepository.GetWalletAsync(fromUserId, asset);
        var toWallet = await _walletRepository.GetWalletAsync(toUserId, asset);

        if (fromWallet == null || toWallet == null)
            throw new ArgumentException("کیف پول یکی از طرفین وجود ندارد");

        if (fromUserId == toUserId)
            throw new ArgumentException("انتقال به خود امکان‌پذیر نیست.");

        // var fromDebitTransaction = Transaction.Create(
        //    fromWallet.Id,
        //    amount,
        //    asset,
        //    TransactionType.Withdraw,
        //    fromWallet.Balance,
        //    fromWallet.Balance - amount,
        //    null,
        //    TransactionStatus.Completed,
        //    "maker",
        //    referenceId,
        //    null
        //);
        // fromWallet.DecreaseBalance(amount);



        // Update existing wallet
        // Create transaction record
        //var transaction = Transaction.Create(
        //    wallet.Id,
        //    amount,
        //    asset,
        //    TransactionType.Withdraw,
        //    wallet.Balance,
        //    wallet.Balance - amount,
        //    Utils.GenerateSecureRandomString(9),
        //    TransactionStatus.Completed,
        //    "Credit transaction",
        //    referenceId,
        //    null
        //);
        // todo: بالانس باید چکار بشه؟؟

        // wallet.DecreaseBalance(amount);
        //await _walletRepository.UpdateWalletAsync(wallet, transaction);

        //return new WalletBallanceDTO
        //{
        //    Asset = wallet.Asset,
        //    BalanceBefore = transaction.BallanceBefore,
        //    BalanceAfter = transaction.BallanceAfter,
        //    LockedBalance = wallet.LockedBalance,
        //    UpdatedAt = wallet.UpdatedAt,
        //    TrackingCode = transaction.TrackingCode,
        //};

        return new WalletBallanceDTO();
    }



    public async Task<(bool success, string message)> OldWithdrawAsync(Guid userId, string asset, decimal amount, string? referenceId = null)
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
        //if (!creditSuccess)
        //{
        //    // Rollback - credit back to source user
        //    await CreditAsync(fromUserId, asset, amount);
        //    return (false, "خطا در انتقال.");
        //}

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
      //  await _walletRepository.CreateTransactionAsync(fromTransaction);

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
       // await _walletRepository.CreateTransactionAsync(toTransaction);

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
        //if (success)
        //{
        //    // ایجاد تراکنش شارژ
        //    var transaction = new WalletTransaction
        //    {
        //        Id = Guid.NewGuid(),
        //        UserId = userId,
        //        Asset = asset,
        //        Amount = amount,
        //        Type = TransactionType.Deposit,
        //        Status = TransactionStatus.Completed,
        //        Description = $"شارژ کیف پول - روش پرداخت: {paymentMethod ?? "نامشخص"}",
        //        CreatedAt = DateTime.UtcNow,
        //        CompletedAt = DateTime.UtcNow
        //    };
        //    await _walletRepository.CreateTransactionAsync(transaction);
        //}

        return true ? (true, "شارژ کیف پول با موفقیت انجام شد.") : (false, "خطا در شارژ کیف پول.");
    }

}