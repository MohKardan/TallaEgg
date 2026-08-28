
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
        return _walletMapper.MapRequired(wallet);
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



    public async Task<WalletDTO> LockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        var wallet = await _walletRepository.LockBalanceAsync(userId, asset, amount);
        return _walletMapper.MapRequired(wallet);

    }

    public async Task<WalletDTO> UnlockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        var wallet = await _walletRepository.UnlockBalanceAsync(userId, asset, amount);
        return _walletMapper.MapRequired(wallet);
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
            wallets.Add(_walletMapper.MapRequired(irrWallet));


            var mauaWallet = WalletEntity.Create
            (
                 userId,
                 CurrenciesConstant.Maua
            );
            var mauaResult = await _walletRepository.CreateWalletAsync(mauaWallet);
            wallets.Add(_walletMapper.MapRequired(mauaWallet));


            var creditMauaWallet = WalletEntity.Create
            (
                 userId,
                 CurrenciesConstant.Credit_MAUA
            );
            var creditMauaResult = await _walletRepository.CreateWalletAsync(creditMauaWallet);
            wallets.Add(_walletMapper.MapRequired(creditMauaWallet));

            return wallets;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("خطا در ایجاد کیف پول‌های پیش‌فرض", ex);
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