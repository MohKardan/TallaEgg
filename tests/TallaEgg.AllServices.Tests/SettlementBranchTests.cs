using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wallet.Core;
using Wallet.Infrastructure;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// شاخه‌های <c>SettleTradeAsync</c> که مسیر خوشبینانه به آن‌ها نمی‌رسد (issue #46).
///
/// <see cref="SettleTradeAsyncTests"/> مسیر موفق، تکرارپذیری، خودمعاملگی، کارمزد غیرصفر و
/// وثیقهٔ قفل‌نشده را پوشش می‌دهد. آنچه اینجا اضافه می‌شود سه شاخهٔ باقی‌مانده است: نبودِ
/// کیف پول یکی از طرفین، نماد بدشکل، و تسویه‌ای که پشتوانه‌اش اعتبار است (موجودی در دسترسِ
/// منفی).
///
/// شاخهٔ سوم مهم‌ترین است: تا امروز فقط با معاملهٔ دستی روی محیط توسعه دیده شده بود.
/// <c>ConsumeLockedBalance</c> دقیقاً برای همین حالت نوشته شد — اگر کسی آن را با
/// <c>UnLockBalance</c> + <c>DecreaseBalance</c> جایگزین کند، گاردِ «موجودی منفی نشود» در
/// <c>DecreaseBalance</c> فعال می‌شود و تسویهٔ هر مشتری بدهکار شکست می‌خورد — بدون اینکه
/// هیچ تست دیگری بشکند.
/// </summary>
public class SettlementBranchTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WalletDbContext _context;
    private readonly WalletRepository _repository;

    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();

    private const string Base = "MAUA";
    private const string Quote = "IRT";
    private const decimal Quantity = 2m;
    private const decimal QuoteQuantity = 1000m;

    public SettlementBranchTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options;
        _context = new WalletDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new WalletRepository(NullLogger<WalletRepository>.Instance, _context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private WalletEntity SeedWallet(Guid userId, string asset, decimal balance, decimal locked)
    {
        var wallet = WalletEntity.Create(userId, asset);
        wallet.Balance = balance;
        wallet.LockedBalance = locked;
        _context.Wallets.Add(wallet);
        return wallet;
    }

    private void SeedFullyLockedWallets()
    {
        SeedWallet(_buyerId, Quote, balance: 0m, locked: QuoteQuantity);
        SeedWallet(_buyerId, Base, balance: 0m, locked: 0m);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();
    }

    private async Task<WalletEntity> ReloadAsync(Guid userId, string asset)
    {
        _context.ChangeTracker.Clear();
        return await _context.Wallets.SingleAsync(w => w.UserId == userId && w.Asset == asset);
    }

    private Task<(bool Success, string Message)> SettleAsync(Guid tradeId, string symbol = $"{Base}/{Quote}") =>
        _repository.SettleTradeAsync(
            tradeId, _buyerId, _sellerId, symbol, Quantity, QuoteQuantity, feeBuyer: 0m, feeSeller: 0m);

    // ── کیف پول ناموجود ─────────────────────────────────────────────────────────

    /// <summary>
    /// تسویه چهار کیف پول لازم دارد. کیف پول «برای دریافت» ممکن است هرگز ساخته نشده باشد —
    /// مشتری‌ای که تا امروز فقط تومان داشته، کیف پول طلا ندارد.
    ///
    /// این دقیقاً همان چیزی بود که در محیط واقعی خرید اول یک نماد جدید (مثل BTC/IRT) را رد
    /// می‌کرد — وثیقهٔ فروشنده قبلاً قفل شده بود، ولی چون کیف پول طلای خریدار وجود نداشت
    /// کل تسویه با «wallets were not found» رول‌بک می‌شد و وثیقه بدون معاملهٔ کامل قفل
    /// می‌ماند. رفتار درست این است: کیف پولِ ناموجود (برای دارایی معتبر) در همان لحظه ساخته
    /// شود و تسویه کامل انجام شود، همان‌طور که کیف پول تومان/طلا در ثبت‌نام لِیزی ساخته
    /// می‌شود.
    /// </summary>
    [Fact]
    public async Task WhenAParticipantWalletIsMissing_ItIsCreatedAndSettlementSucceeds()
    {
        // کیف پول طلای خریدار ساخته نشده — همان کیفی که باید طلا را دریافت کند.
        SeedWallet(_buyerId, Quote, balance: 0m, locked: QuoteQuantity);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();

        var tradeId = Guid.NewGuid();
        var (success, message) = await SettleAsync(tradeId);

        Assert.True(success, message);

        var buyerBase = await ReloadAsync(_buyerId, Base);
        Assert.Equal(Quantity, buyerBase.Balance);
        var buyerQuote = await ReloadAsync(_buyerId, Quote);
        Assert.Equal(0m, buyerQuote.LockedBalance);
        var sellerBase = await ReloadAsync(_sellerId, Base);
        Assert.Equal(0m, sellerBase.LockedBalance);
        Assert.Equal(0m, sellerBase.Balance);

        Assert.Equal(4, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));
        Assert.Equal(1, await _context.TradeSettlements.CountAsync(s => s.TradeId == tradeId));
    }

    /// <summary>
    /// یک نماد بدشکل که هیچ‌کدام از دو طرفش دارایی واقعی نیست (پس نه فقط ناموجود بلکه
    /// ناشناخته) نباید کیف پول جعلی بسازد — همان گاردِ <c>IsValidCurrency</c> که در سطح
    /// شارژ ادمین هم اعمال می‌شود.
    /// </summary>
    [Fact]
    public async Task WhenAParticipantWalletIsMissingForAnUnknownAsset_NothingMoves()
    {
        SeedWallet(_buyerId, "NOT_A_REAL_ASSET", balance: 0m, locked: QuoteQuantity);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, "NOT_A_REAL_ASSET", balance: 0m, locked: 0m);
        _context.SaveChanges();

        var tradeId = Guid.NewGuid();
        var (success, message) = await SettleAsync(tradeId, $"{Base}/NOT_A_REAL_ASSET");

        Assert.False(success);
        Assert.Contains("wallets were not found", message);

        var sellerBase = await ReloadAsync(_sellerId, Base);
        Assert.Equal(Quantity, sellerBase.LockedBalance);
        Assert.Equal(0m, sellerBase.Balance);

        Assert.Equal(0, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));
        Assert.Equal(0, await _context.TradeSettlements.CountAsync(s => s.TradeId == tradeId));
    }

    // ── نماد بدشکل ──────────────────────────────────────────────────────────────

    /// <summary>
    /// نماد باید دقیقاً <c>BASE/QUOTE</c> باشد. کد قبلاً جاهایی <c>Split('/')[1]</c> کورکورانه
    /// می‌نوشت؛ با نماد بدشکل آن یک <c>IndexOutOfRangeException</c> می‌شد که در پردازشگر
    /// outbox به «تلاش ناموفق» ترجمه می‌شد و پنج بار تکرار می‌شد — در حالی که تکرار هرگز
    /// نمی‌توانست کمکی کند، چون داده خراب بود نه شرایط.
    ///
    /// یک رد صریح، همان اشتباه را در همان تلاش اول قابل‌فهم می‌کند.
    /// </summary>
    [Theory]
    [InlineData("MAUA")]        // بدون جداکننده
    [InlineData("MAUA/")]       // نیمهٔ دوم خالی
    [InlineData("/IRT")]        // نیمهٔ اول خالی
    [InlineData("MAUA/IRT/X")]  // سه بخش
    [InlineData("")]
    [InlineData("   ")]
    public async Task AMalformedSymbol_IsRejectedWithoutTouchingAnyWallet(string symbol)
    {
        SeedFullyLockedWallets();
        var tradeId = Guid.NewGuid();

        var (success, message) = await SettleAsync(tradeId, symbol);

        Assert.False(success);
        Assert.Contains("Invalid symbol", message);

        var buyerQuote = await ReloadAsync(_buyerId, Quote);
        Assert.Equal(QuoteQuantity, buyerQuote.LockedBalance);
        Assert.Equal(0, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));
    }

    /// <summary>
    /// نماد درست ولی با حروف کوچک و فاصله باید پذیرفته شود. اگر نرمال‌سازی حذف شود، این
    /// تسویه با «کیف پول پیدا نشد» رد می‌شود — خطایی که علتش هیچ ربطی به کیف پول ندارد.
    /// </summary>
    [Fact]
    public async Task AWellFormedSymbolIsNormalised_SoCaseAndSpacingDoNotBreakSettlement()
    {
        SeedFullyLockedWallets();

        var (success, message) = await SettleAsync(Guid.NewGuid(), " maua / irt ");

        Assert.True(success, message);
        Assert.Equal(Quantity, (await ReloadAsync(_buyerId, Base)).Balance);
    }

    // ── تسویهٔ پشتوانه‌شده با اعتبار ─────────────────────────────────────────────

    /// <summary>
    /// خریدار تومان ندارد و با اعتبار می‌خرد: هنگام قفلِ وثیقه موجودی در دسترسش منفی شده
    /// است. تسویه باید انجام شود و بدهی روی <c>Balance</c> بماند.
    ///
    /// این همان حالتی است که <c>ConsumeLockedBalance</c> برایش نوشته شد. مسیر ساده‌تر —
    /// <c>UnLockBalance</c> و بعد <c>DecreaseBalance</c> — همین‌جا شکست می‌خورد، چون
    /// <c>DecreaseBalance</c> کف صفر دارد و موجودیِ منفی را رد می‌کند. یعنی هر مشتری بدهکار
    /// تسویه‌اش گیر می‌کرد، و چون هیچ تست دیگری موجودی منفی ندارد، هیچ چیز دیگری این را
    /// نمی‌گرفت.
    /// </summary>
    [Fact]
    public async Task ACreditBackedBuyer_SettlesAndKeepsTheDebtOnTheBalance()
    {
        // مسیر واقعی: کیف پول با موجودی صفر، بعد قفل وثیقه — که موجودی را منفی می‌کند.
        var buyerQuote = SeedWallet(_buyerId, Quote, balance: 0m, locked: 0m);
        buyerQuote.LockBalance(QuoteQuantity);
        SeedWallet(_buyerId, Base, balance: 0m, locked: 0m);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();

        Assert.Equal(-QuoteQuantity, buyerQuote.Balance); // بدهی پیش از تسویه

        var tradeId = Guid.NewGuid();
        var (success, message) = await SettleAsync(tradeId);

        Assert.True(success, message);

        // قفل مصرف شد، ولی بدهی سر جایش ماند — پول از هیچ ساخته نشد.
        var settledBuyerQuote = await ReloadAsync(_buyerId, Quote);
        Assert.Equal(0m, settledBuyerQuote.LockedBalance);
        Assert.Equal(-QuoteQuantity, settledBuyerQuote.Balance);

        // و طلایی که خرید را واقعاً گرفت.
        Assert.Equal(Quantity, (await ReloadAsync(_buyerId, Base)).Balance);

        // فروشنده تومانش را کامل گرفت: بدهکار بودنِ خریدار نباید از دریافتی او کم کند.
        Assert.Equal(QuoteQuantity, (await ReloadAsync(_sellerId, Quote)).Balance);

        Assert.Equal(4, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));
    }

    /// <summary>
    /// جمع ارزش خالص باید صفر بماند: آنچه خریدار بدهکار است دقیقاً همان چیزی است که
    /// فروشنده گرفته، و طلایی که خریدار گرفته دقیقاً همان است که فروشنده داده.
    ///
    /// این همان بررسی‌ای است که در جلسه‌های تست دستی با SQL دستی انجام می‌شد.
    /// </summary>
    [Fact]
    public async Task ACreditBackedSettlement_ConservesMoney()
    {
        var buyerQuote = SeedWallet(_buyerId, Quote, balance: 0m, locked: 0m);
        buyerQuote.LockBalance(QuoteQuantity);
        SeedWallet(_buyerId, Base, balance: 0m, locked: 0m);
        var sellerBase = SeedWallet(_sellerId, Base, balance: Quantity, locked: 0m);
        sellerBase.LockBalance(Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();

        Assert.True((await SettleAsync(Guid.NewGuid())).Success);

        _context.ChangeTracker.Clear();
        var all = await _context.Wallets.ToListAsync();

        // موجودی در دسترس + قفل‌شده، به تفکیک دارایی.
        var gold = all.Where(w => w.Asset == Base).Sum(w => w.Balance + w.LockedBalance);
        var toman = all.Where(w => w.Asset == Quote).Sum(w => w.Balance + w.LockedBalance);

        Assert.Equal(Quantity, gold);  // همان مقداری که فروشنده از اول داشت
        Assert.Equal(0m, toman);       // تومانی وارد سیستم نشده بود
    }
}
