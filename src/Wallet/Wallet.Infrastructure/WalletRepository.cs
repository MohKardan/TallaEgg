using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TallaEgg.Core;
using TallaEgg.Core.Enums.Wallet;
using Wallet.Core;

namespace Wallet.Infrastructure;

public class WalletRepository : IWalletRepository
{
    private readonly WalletDbContext _context;
    private readonly ILogger<WalletRepository> _logger;

    public WalletRepository(ILogger<WalletRepository> logger, WalletDbContext context)
    {
        _context = context;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<WalletEntity?> GetWalletAsync(Guid userId, string asset)
    {
        return await _context.Wallets
            .FirstOrDefaultAsync(w => w.UserId == userId && w.Asset.ToUpper() == asset.ToUpper());
    }

    public async Task<IEnumerable<WalletEntity>> GetUserWalletsAsync(Guid userId)
    {
        return await _context.Wallets
            .Where(w => w.UserId == userId)
            .OrderBy(w => w.Asset)
            .ToListAsync();
    }

    /// <summary>
    /// ایجاد کیف پول جدید در دیتابیس
    /// </summary>
    /// <param name="wallet">کیف پول برای ایجاد</param>
    /// <returns>کیف پول ایجاد شده</returns>
    public async Task<WalletEntity> CreateWalletAsync(WalletEntity wallet)
    {
        try
        {
            // بررسی مجدد وجود کیف پول (Race Condition Prevention)
            var existingWallet = await GetWalletAsync(wallet.UserId, wallet.Asset);
            if (existingWallet != null)
            {
                _logger.LogWarning("Wallet already exists during creation for user {UserId}, asset {Asset}",
                    wallet.UserId, wallet.Asset);
                return existingWallet;
            }

            _context.Wallets.Add(wallet);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Successfully created wallet {WalletId} for user {UserId}, asset {Asset}",
                wallet.Id, wallet.UserId, wallet.Asset);

            return wallet;
        }
        catch (DbUpdateException ex) when (ex.InnerException?.Message?.Contains("duplicate") == true ||
                                          ex.InnerException?.Message?.Contains("UNIQUE") == true)
        {
            _logger.LogWarning("Duplicate wallet creation attempted for user {UserId}, asset {Asset}. Returning existing wallet.",
                wallet.UserId, wallet.Asset);

            // در صورت تکراری بودن، کیف پول موجود را برگردان
            var existingWallet = await GetWalletAsync(wallet.UserId, wallet.Asset);
            if (existingWallet != null)
                return existingWallet;

            throw; // اگر هنوز هم پیدا نشد، خطا را دوباره پرتاب کن
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating wallet for user {UserId}, asset {Asset}",
                wallet.UserId, wallet.Asset);
            throw;
        }
    }
    public async Task<WalletEntity> UpdateWalletAsync(WalletEntity wallet, Transaction transaction = null)
    {
        if (wallet == null)
        {
            _logger.LogError("Attempted to update a null wallet entity");
            throw new ArgumentNullException(nameof(wallet));
        }

        try
        {
            if (transaction != null)
            {
                _context.Transactions.Add(transaction);
            }
            else
            {
                _logger.LogDebug("No transaction provided while updating wallet {WalletId}", wallet.Id);
            }

            wallet.UpdatedAt = DateTime.UtcNow;
            _context.Wallets.Update(wallet);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated wallet {WalletId} for user {UserId}", wallet.Id, wallet.UserId);

            return wallet;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogWarning(ex, "Concurrency conflict while updating wallet {WalletId} for user {UserId}", wallet.Id, wallet.UserId);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating wallet {WalletId} for user {UserId}", wallet.Id, wallet.UserId);
            throw;
        }
    }

    public async Task<WalletEntity> LockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        var wallet = await GetWalletAsync(userId, asset);

        if (wallet == null)
        {
            // Hit live: selling BTC/SEKE_BAHAR failed for every customer and the market maker
            // alike with "کیف پول پیدا نشد", because CreateDefaultWalletsAsync only ever seeds
            // Toman/MAUA/CREDIT_MAUA at registration — a newer trading symbol's wallet never
            // existed for anyone until now. Same fix as WalletService.IncreaseBalanceAsync
            // (issue: charge-command bugs earlier in this conversation), applied here since
            // trading locks collateral through this repository directly, not through that
            // service. A genuinely unknown asset still fails instead of creating a phantom
            // wallet.
            if (!CurrenciesConstant.IsValidCurrency(asset))
                throw new ArgumentNullException("کیف پول پیدا نشد", nameof(wallet));

            wallet = await CreateWalletAsync(WalletEntity.Create(userId, asset));
        }

        var transaction = Transaction.Create(
          wallet.Id,
          amount,
          asset,
          TransactionType.Freeze,
          wallet.Balance,
          wallet.Balance - amount,
          null,
          TransactionStatus.Completed,
          "LockBalance transaction",
          null,
          null
      );
        wallet.LockBalance(amount);

        await UpdateWalletAsync(wallet, transaction);
        return wallet;
    }

    public async Task<WalletEntity> UnlockBalanceAsync(Guid userId, string asset, decimal amount)
    {
        var wallet = await GetWalletAsync(userId, asset);

        if (wallet == null)
        {
            // Same reasoning as LockBalanceAsync — kept symmetric even though, in the normal
            // order-cancellation flow, a Lock always precedes the matching Unlock and so the
            // wallet already exists by then.
            if (!CurrenciesConstant.IsValidCurrency(asset))
                throw new ArgumentNullException("کیف پول پیدا نشد", nameof(wallet));

            wallet = await CreateWalletAsync(WalletEntity.Create(userId, asset));
        }

        if (amount < 0)
            throw new ArgumentOutOfRangeException(nameof(amount), "مقدار آزادسازی نمی‌تواند منفی باشد.");

        // آزادسازیِ بیش از آنچه قفل است، LockedBalance را منفی می‌کند و همزمان Balance
        // را بالا می‌برد — یعنی از هیچ، پول می‌سازد. پیش‌تر هیچ گاردی وجود نداشت و
        // مسیر لغو سفارش مقدار را با فرمولی متفاوت از فرمول قفل حساب می‌کرد، پس این
        // حالت واقعاً قابل رسیدن بود (issue #52).
        if (amount > wallet.LockedBalance)
        {
            _logger.LogError(
                "Refusing to unlock {Amount} {Asset} for user {UserId}: only {Locked} is locked.",
                amount, asset, userId, wallet.LockedBalance);

            throw new InvalidOperationException(
                $"مقدار آزادسازی ({amount}) از موجودی قفل‌شده ({wallet.LockedBalance}) بیشتر است.");
        }

        var transaction = Transaction.Create(
          wallet.Id,
          amount,
          asset,
          TransactionType.Unfreeze,
          wallet.Balance,
          wallet.Balance + amount,
          null,
          TransactionStatus.Completed,
          "UnLockBalance transaction",
          null,
          null
      );
        wallet.UnLockBalance(amount);

        await UpdateWalletAsync(wallet, transaction);
        return wallet;
    }

    public async Task<WalletEntity> IncreaseBalanceForTradeAsync(Guid userId, string asset, decimal amount)
    {
        var wallet = await GetWalletAsync(userId, asset);

        if (wallet == null)
        {
            // The most serious of the three (Lock/Unlock/this): this is trade settlement
            // crediting the buyer's side. Without this fix, a buyer's first-ever purchase of a
            // newer symbol would have the seller's collateral already consumed while the buyer
            // receives nothing — the outbox settlement failing here, not merely a customer-facing
            // "wallet not found" message (issue #39 territory: a stuck settlement, not just a
            // refused request).
            if (!CurrenciesConstant.IsValidCurrency(asset))
                throw new ArgumentNullException("کیف پول پیدا نشد", nameof(wallet));

            wallet = await CreateWalletAsync(WalletEntity.Create(userId, asset));
        }

        var transaction = Transaction.Create(
          wallet.Id,
          amount,
          asset,
          TransactionType.Trade,
          wallet.Balance,
          wallet.Balance + amount,
          null,
          TransactionStatus.Completed,
          "IncreaseBalanceAsync transaction",
          null,
          null
                                            );
        wallet.IncreaseBalance(amount);

        await UpdateWalletAsync(wallet, transaction);
        return wallet;
    }


    public async Task<Transaction> CreateTransactionAsync(Transaction transaction)
    {
        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }

    public async Task<(bool Success, string Message)> SettleTradeAsync(
        Guid tradeId, Guid buyerUserId, Guid sellerUserId,
        string symbol, decimal quantity, decimal quoteQuantity,
        decimal feeBuyer, decimal feeSeller)
    {
        var referenceId = tradeId.ToString();

        // مسیر سریع: اگر معامله از قبل تسویه شده، بدون باز کردن تراکنش برمی‌گردیم.
        //
        // این بررسی «تضمین» نیست — فقط بهینه‌سازی است. تحویل مجدد outbox حالت عادی است
        // (طراحی صریحاً اجازه می‌دهد پیامی که موفق شده دوباره فرستاده شود)، پس ارزش دارد
        // که مسیر پرتکرار بدون هزینهٔ تراکنش و بدون تولید استثنا رد شود.
        //
        // تضمین واقعی، کلید اصلی جدول TradeSettlements است که پایین‌تر اعمال می‌شود.
        // پیش‌تر همین SELECT تنها محافظ بود و چون بیرون از تراکنش اجرا می‌شد، دو تسویهٔ
        // همزمان می‌توانستند هر دو از آن رد شوند و پول دو برابر جابه‌جا شود (issue #42).
        if (await _context.TradeSettlements.AnyAsync(s => s.TradeId == tradeId))
        {
            _logger.LogInformation("Trade {TradeId} already settled; skipping (idempotent).", tradeId);
            return (true, "Trade already settled.");
        }

        // Symbol is BASE/QUOTE, e.g. MAUA/IRT. Parse defensively (never symbol.Split('/')[1] blindly).
        var parts = symbol?.Split('/');
        if (parts is not { Length: 2 } || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
            return (false, $"Invalid symbol '{symbol}'. Expected BASE/QUOTE.");

        var baseAsset = parts[0].Trim().ToUpperInvariant();   // e.g. MAUA (gold)
        var quoteAsset = parts[1].Trim().ToUpperInvariant();  // e.g. IRT (toman)

        if (quantity <= 0 || quoteQuantity <= 0)
            return (false, "Quantity and quoteQuantity must be positive.");
        if (feeBuyer < 0 || feeSeller < 0)
            return (false, "Fees cannot be negative.");

        // دفاع در عمق: تطبیق نباید اجازهٔ خودمعاملگی بدهد، اما اگر مسیری آن را دور بزند
        // تسویه روی یک کیف پول مشترک انجام می‌شود و رد حسابرسی نادرست تولید می‌کند.
        if (buyerUserId == sellerUserId)
        {
            _logger.LogError("Refusing to settle trade {TradeId}: buyer and seller are the same user.", tradeId);
            return (false, "Buyer and seller must be different users.");
        }

        // Fail closed on fees. Settlement debits each payer the full amount but would
        // credit the receiver the amount minus the fee — and the difference is credited
        // to NO account, so a non-zero fee silently destroys money on every trade.
        // Fee rates are 0 for the MVP, so this guard is inert today; it exists so that
        // restoring a non-zero rate produces a loud, visible failure instead of a slow
        // leak. Remove it only together with fee crediting to the fee account (issue #35).
        if (feeBuyer != 0m || feeSeller != 0m)
        {
            _logger.LogError(
                "Refusing to settle trade {TradeId}: non-zero fees are not supported because collected " +
                "fees are not credited to any account (feeBuyer={FeeBuyer}, feeSeller={FeeSeller}). See issue #35.",
                tradeId, feeBuyer, feeSeller);
            return (false, "Fee crediting is not implemented; settlement refused to avoid losing the fee amount.");
        }

        var buyerReceivesBase = quantity;      // no fee is deducted while fees are disabled
        var sellerReceivesQuote = quoteQuantity;

        await using var tx = await _context.Database.BeginTransactionAsync();
        try
        {
            // Fetch all four wallets as tracked entities; every change below is persisted by a single SaveChanges.
            var buyerQuote = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == buyerUserId && w.Asset == quoteAsset);
            var buyerBase = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == buyerUserId && w.Asset == baseAsset);
            var sellerBase = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == sellerUserId && w.Asset == baseAsset);
            var sellerQuote = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == sellerUserId && w.Asset == quoteAsset);

            // buyerBase and sellerQuote are the *receiving* sides — for a newer symbol nobody
            // has ever bought before, the buyer's base-asset wallet (and, symmetrically, a
            // first-time seller's quote-asset wallet) may never have been created. Without this,
            // the entire settlement rolled back — collateral already locked at order time, buyer
            // credited nothing — the most serious of the wallet-creation gaps found in this
            // conversation, since it fails settlement itself rather than a customer-facing
            // request. buyerQuote/sellerBase are the *locked* sides and should already exist from
            // LockBalanceAsync's own fix, but are created here too for symmetry and safety.
            if (buyerQuote is null || buyerBase is null || sellerBase is null || sellerQuote is null)
            {
                if (!CurrenciesConstant.IsValidCurrency(baseAsset) || !CurrenciesConstant.IsValidCurrency(quoteAsset))
                {
                    await tx.RollbackAsync();
                    return (false, "One or more participant wallets were not found.");
                }

                buyerQuote ??= await CreateWalletAsync(WalletEntity.Create(buyerUserId, quoteAsset));
                buyerBase ??= await CreateWalletAsync(WalletEntity.Create(buyerUserId, baseAsset));
                sellerBase ??= await CreateWalletAsync(WalletEntity.Create(sellerUserId, baseAsset));
                sellerQuote ??= await CreateWalletAsync(WalletEntity.Create(sellerUserId, quoteAsset));
            }

            // The collateral must actually be locked. Guards against the lock-after-match ordering bug (audit C-5):
            // settling from funds that were never locked would drive a balance negative.
            if (buyerQuote.LockedBalance < quoteQuantity)
            {
                await tx.RollbackAsync();
                return (false, $"Buyer locked {quoteAsset} ({buyerQuote.LockedBalance}) is less than required ({quoteQuantity}).");
            }
            if (sellerBase.LockedBalance < quantity)
            {
                await tx.RollbackAsync();
                return (false, $"Seller locked {baseAsset} ({sellerBase.LockedBalance}) is less than required ({quantity}).");
            }

            // Buyer pays quote: consume the locked collateral. The funds were reserved at
            // order time (possibly credit-backed, so available Balance may be negative);
            // consuming them removes the lock and leaves the debt on Balance.
            var buyerQuoteBefore = buyerQuote.Balance;
            buyerQuote.ConsumeLockedBalance(quoteQuantity);
            AddTradeTransaction(buyerQuote, quoteQuantity, quoteAsset, buyerQuoteBefore, buyerQuote.Balance, referenceId, "Buyer paid quote asset (from locked funds)");

            // Buyer receives base.
            var buyerBaseBefore = buyerBase.Balance;
            buyerBase.IncreaseBalance(buyerReceivesBase);
            AddTradeTransaction(buyerBase, buyerReceivesBase, baseAsset, buyerBaseBefore, buyerBase.Balance, referenceId, "Buyer received base asset");

            // Seller pays base: consume the locked collateral (same credit-aware handling).
            var sellerBaseBefore = sellerBase.Balance;
            sellerBase.ConsumeLockedBalance(quantity);
            AddTradeTransaction(sellerBase, quantity, baseAsset, sellerBaseBefore, sellerBase.Balance, referenceId, "Seller paid base asset (from locked funds)");

            // Seller receives quote.
            var sellerQuoteBefore = sellerQuote.Balance;
            sellerQuote.IncreaseBalance(sellerReceivesQuote);
            AddTradeTransaction(sellerQuote, sellerReceivesQuote, quoteAsset, sellerQuoteBefore, sellerQuote.Balance, referenceId, "Seller received quote asset");

            var now = DateTime.UtcNow;
            buyerQuote.UpdatedAt = now; buyerBase.UpdatedAt = now; sellerBase.UpdatedAt = now; sellerQuote.UpdatedAt = now;
            _context.Wallets.UpdateRange(buyerQuote, buyerBase, sellerBase, sellerQuote);

            // سد یکتایی: این سطر داخل همان تراکنشِ جابه‌جایی پول درج می‌شود.
            //
            // چون TradeId کلید اصلی است، اگر تسویهٔ همزمانِ دیگری زودتر commit کرده باشد،
            // این درج با نقض کلید تکراری شکست می‌خورد و کل تراکنش — شامل هر چهار تغییر
            // موجودی — برگردانده می‌شود. یعنی «دقیقاً یک بار» را دیتابیس تضمین می‌کند،
            // نه ترتیب اجرای کد.
            _context.TradeSettlements.Add(
                TradeSettlement.Create(tradeId, buyerUserId, sellerUserId, symbol!, quantity, quoteQuantity));

            await _context.SaveChangesAsync(); // یک save: ۴ تغییر موجودی + ۴ سطر تراکنش + ۱ سطر تسویه
            await tx.CommitAsync();

            _logger.LogInformation(
                "Settled trade {TradeId}: buyer={Buyer} seller={Seller} {Qty} {Base} for {Quote} {QuoteAsset}",
                tradeId, buyerUserId, sellerUserId, quantity, baseAsset, quoteQuantity, quoteAsset);
            return (true, "Trade settled.");
        }
        catch (DbUpdateException ex) when (IsDuplicateSettlement(ex))
        {
            // بازندهٔ رقابت. تسویهٔ همزمانِ دیگری زودتر commit کرده و کلید اصلی، درج دوم
            // را رد کرده است. rollback همهٔ تغییرات موجودی این تلاش را برمی‌گرداند، پس
            // نتیجهٔ نهایی دقیقاً یک تسویه است.
            //
            // این را به «موفقیت» ترجمه می‌کنیم و نه خطا، چون از دید فراخوان واقعاً موفق
            // است: معامله تسویه شده. اگر خطا برمی‌گرداندیم، پردازشگر outbox پیام را
            // شکست‌خورده تلقی می‌کرد، پنج بار retry می‌کرد و در نهایت به Failed می‌رسید —
            // یعنی یک معاملهٔ کاملاً سالم به‌عنوان «گیرکرده» به اپراتور هشدار می‌داد.
            await tx.RollbackAsync();

            _logger.LogInformation(
                "Trade {TradeId} was settled concurrently by another caller; this attempt was rolled back (idempotent).",
                tradeId);

            return (true, "Trade already settled.");
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Error settling trade {TradeId}", tradeId);
            return (false, $"Settlement error: {ex.Message}");
        }
    }

    /// <summary>
    /// تشخیص اینکه آیا شکست ذخیره‌سازی به‌خاطر درج تکراری در TradeSettlements بوده است.
    ///
    /// عمداً به کد خطای خاص SQL Server (۲۶۲۷ برای نقض قید، ۲۶۰۱ برای ایندکس یکتا) تکیه
    /// نمی‌کنیم، چون تست‌ها روی SQLite اجرا می‌شوند و کد خطای دیگری تولید می‌کند. اگر
    /// فقط کد SQL Server را می‌پذیرفتیم، این مسیر در تست‌ها هرگز اجرا نمی‌شد — یعنی
    /// دقیقاً همان چیزی که باید تضمین شود، بی‌آزمون می‌ماند.
    ///
    /// در عوض از خودِ EF می‌پرسیم چه چیزی در حال درج بوده: اگر تنها موجودیتِ Added از
    /// نوع TradeSettlement باشد، تنها دلیل ممکنِ نقض یکتایی همان کلید تکراری است.
    /// </summary>
    private static bool IsDuplicateSettlement(DbUpdateException ex) =>
        ex.Entries.Count > 0 && ex.Entries.All(e => e.Entity is TradeSettlement);

    /// <summary>Adds (but does not save) a completed Trade transaction record to the current unit of work.</summary>
    private void AddTradeTransaction(WalletEntity wallet, decimal amount, string asset,
        decimal balanceBefore, decimal balanceAfter, string referenceId, string description)
    {
        var transaction = Transaction.Create(
            wallet.Id, amount, asset, TransactionType.Trade,
            balanceBefore, balanceAfter, null, TransactionStatus.Completed,
            description, referenceId, null);
        _context.Transactions.Add(transaction);
    }

    public async Task<WalletTransaction?> GetTransactionAsync(Guid transactionId)
    {
        return await _context.WalletTransactions.FindAsync(transactionId);
    }

    public async Task<IEnumerable<WalletTransaction>> GetUserTransactionsAsync(Guid userId, string? asset = null)
    {
        var query = _context.WalletTransactions.Where(wt => wt.UserId == userId);
        if (!string.IsNullOrEmpty(asset))
            query = query.Where(wt => wt.Asset == asset);

        return await query.OrderByDescending(wt => wt.CreatedAt).ToListAsync();
    }

    public async Task<IEnumerable<WalletTransaction>> GetTransactionsByReferenceAsync(string referenceId)
    {
        return await _context.WalletTransactions
            .Where(wt => wt.ReferenceId == referenceId)
            .OrderByDescending(wt => wt.CreatedAt)
            .ToListAsync();
    }

    public async Task<WalletTransaction> UpdateTransactionAsync(WalletTransaction transaction)
    {
        _context.WalletTransactions.Update(transaction);
        await _context.SaveChangesAsync();
        return transaction;
    }


}




