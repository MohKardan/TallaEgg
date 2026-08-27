
using TallaEgg.Core;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Core.Enums.Order;
using TallaEgg.Core.Enums.Wallet;
using TallaEgg.Core.Utilties;
using Wallet.Application.Mappers;
using Wallet.Core;
using TallaEgg.Core.ErrorHandling;

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
        if (wallet == null) throw new BusinessRuleException("کیف پول پیدا نشد");
        return _walletMapper.Map(wallet);
    }

    /// <summary>
    /// Increases a user's wallet balance. Formerly named CreditAsync.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <param name="asset">Asset code.</param>
    /// <param name="amount">Amount to add.</param>
    /// <param name="refId">Optional reference id.</param>
    /// <returns>The updated wallet and the transaction recorded for it.</returns>
    public async Task<(WalletEntity walletEntity, Transaction transactionEntity)> IncreaseBalanceAsync(Guid userId, string asset, decimal amount, string? refId = null)
    {
   
        var wallet = await _walletRepository.GetWalletAsync(userId, asset);

        if (wallet == null)
        {
            // Only CreateDefaultWalletsAsync's three wallets (Toman, MAUA, CREDIT_MAUA) exist at
            // registration. Every other asset — a newer trading symbol, or its own CREDIT_
            // ledger — has no wallet for anyone, existing user or new, until they first receive
            // one; creating it here on first deposit is that "first receive". A genuinely
            // unknown asset code (a typo, not a real symbol) still fails loudly instead of
            // silently creating a phantom wallet.
            if (!CurrenciesConstant.IsValidCurrency(asset))
                throw new BusinessRuleException("کیف پول وجود ندارد");

            wallet = await _walletRepository.CreateWalletAsync(WalletEntity.Create(userId, asset));
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
                "Add Funds or Deposit transaction",
                refId,
                null
            );
            wallet.IncreaseBalance(amount);
            await _walletRepository.UpdateWalletAsync(wallet,transaction);
        return (wallet, transaction);
             
    }
    /// <summary>
    /// Decreases a user's wallet balance. Formerly named DeCreditAsync.
    /// </summary>
    public async Task<(WalletEntity walletEntity, Transaction transactionEntity)> DecreaseBalanceAsync(Guid userId, string asset, decimal amount, string? refId = null)
    {


        var wallet = await _walletRepository.GetWalletAsync(userId, asset);

        if (wallet == null)
        {
            // Same reasoning as IncreaseBalanceAsync: a valid asset with no wallet yet gets one
            // lazily rather than failing "wallet does not exist" for something that was never
            // credited. Decreasing an empty, just-created wallet then behaves exactly like
            // decreasing any other zero-balance wallet — unrelated to this fix.
            if (!CurrenciesConstant.IsValidCurrency(asset))
                throw new BusinessRuleException("کیف پول وجود ندارد");

            wallet = await _walletRepository.CreateWalletAsync(WalletEntity.Create(userId, asset));
        }

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
        

        var result = await IncreaseBalanceAsync(userId, asset, amount,referenceId);
      

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
       
        var result = await DecreaseBalanceAsync(userId, asset, amount,referenceId);
      
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
            throw new BusinessRuleException("کیف پول یکی از طرفین وجود ندارد");

        if (fromUserId == toUserId)
            throw new BusinessRuleException("انتقال به خود امکان‌پذیر نیست.");

        // Never implemented: this returns an empty DTO, so a caller is told the trade succeeded
        // while no balance moves. That is audit finding C-8. The endpoint in front of it,
        // POST /api/wallet/transaction/trade, returns 501 whenever
        // FeatureFlags:QuarantineStubEndpoints is on, and it defaults to on. Do not turn that flag
        // off expecting a working implementation behind it — there is none. Either implement this
        // method or delete it together with the endpoint.
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

        // Credit to destination user.
        //
        // Not atomic, and the compensating rollback that used to sit here was commented out: if
        // this throws, the source has already been debited and the money is gone. The two audit
        // records below are also never written, so a "successful" transfer leaves no trail. Safe
        // only because nothing reaches this method — every endpoint that called it is commented
        // out in Wallet.Api/Program.cs. Implement it against the transactional pattern in
        // WalletRepository.SettleTradeAsync before exposing it again, or delete it.
        var creditSuccess = await IncreaseBalanceAsync(toUserId, asset, amount);

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

        return (true, "انتقال با موفقیت انجام شد.");
    }

    public async Task<(bool success, string message)> ChargeWalletAsync(Guid userId, string asset, decimal amount, string? paymentMethod = null)
    {
        if (amount <= 0)
            return (false, "مقدار شارژ باید بزرگتر از صفر باشد.");

        if (amount > 1000000) // Top-up cap: one million.
            return (false, "مقدار شارژ از حد مجاز بیشتر است.");

        var success = await IncreaseBalanceAsync(userId, asset, amount);

        return true ? (true, "شارژ کیف پول با موفقیت انجام شد.") : (false, "خطا در شارژ کیف پول.");
    }

    /// <summary>
    /// Creates the wallets a new user starts with: Toman, gold, and the gold credit ledger.
    /// Every other asset's wallet is created lazily on first deposit — see IncreaseBalanceAsync.
    /// </summary>
    /// <param name="userId">User id.</param>
    /// <returns>The wallets that were created.</returns>
    public async Task<IEnumerable<WalletDTO>> CreateDefaultWalletsAsync(Guid userId)
    {
        var wallets = new List<WalletDTO>();

        try
        {
            var irrWallet = WalletEntity.Create
            (
                 userId,
                 CurrenciesConstant.Toman
            );
            var irrResult = await _walletRepository.CreateWalletAsync(irrWallet);
            wallets.Add(_walletMapper.Map(irrWallet));


            var mauaWallet = WalletEntity.Create
            (
                 userId,
                 CurrenciesConstant.Maua
            );
            var mauaResult = await _walletRepository.CreateWalletAsync(mauaWallet);
            wallets.Add(_walletMapper.Map(mauaWallet));


            var creditMauaWallet = WalletEntity.Create
            (
                 userId,
                 CurrenciesConstant.Credit_MAUA
            );
            var creditMauaResult = await _walletRepository.CreateWalletAsync(creditMauaWallet);
            wallets.Add(_walletMapper.Map(creditMauaWallet));

            return wallets;
        }
        catch (Exception ex)
        {
            throw new Exception($"خطا در ایجاد کیف پول‌های پیش‌فرض: {ex.Message}");
        }
    }

    public Task<(bool Success, string Message)> SettleTradeAsync(
        Guid tradeId, Guid buyerUserId, Guid sellerUserId,
        string symbol, decimal quantity, decimal quoteQuantity,
        decimal feeBuyer, decimal feeSeller)
        => _walletRepository.SettleTradeAsync(
            tradeId, buyerUserId, sellerUserId,
            symbol, quantity, quoteQuantity, feeBuyer, feeSeller);
}