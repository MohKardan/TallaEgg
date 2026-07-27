using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wallet.Core;
using Wallet.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// «هر معامله دقیقاً یک بار تسویه می‌شود» باید توسط دیتابیس تضمین شود، نه توسط ترتیب اجرای کد.
///
/// پیش‌تر تنها محافظ، یک SELECT بود که ۴۶ خط پیش از باز شدن تراکنش اجرا می‌شد و هیچ قید
/// یکتایی پشتش نبود. دو تسویهٔ همزمان از یک معامله می‌توانستند هر دو از آن بررسی رد شوند
/// و هر دو اعمال شوند — یعنی هر دو طرف دو برابر اعتبار می‌گرفتند و پول از هیچ ساخته
/// می‌شد (issue #42).
///
/// حالا TradeId کلید اصلی جدول TradeSettlements است و آن سطر داخل همان تراکنشِ جابه‌جایی
/// پول درج می‌شود.
/// </summary>
public class SettlementUniquenessTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly HookedWalletDbContext _context;
    private readonly WalletRepository _repository;

    private readonly Guid _buyerId = Guid.NewGuid();
    private readonly Guid _sellerId = Guid.NewGuid();

    private const string Base = "MAUA";
    private const string Quote = "IRT";
    private const decimal Quantity = 2m;
    private const decimal QuoteQuantity = 1000m;

    /// <summary>
    /// یک قلاب درست پیش از SaveChanges، برای بازسازی قطعی و بدون رقابتِ حالتی که رقیب
    /// دقیقاً در بازهٔ بین «بررسی سریع» و «درج» ما commit کرده است.
    ///
    /// چرا این روش و نه اجرای واقعی دو Task موازی: SQLite در حافظه روی یک اتصال، دو
    /// تراکنش نوشتنِ همزمان را پشتیبانی نمی‌کند، پس تست موازی یا قفل می‌شد یا وابسته به
    /// زمان‌بندی و ناپایدار می‌شد. آنچه باید اثبات شود این نیست که «رقابت رخ می‌دهد»،
    /// بلکه این است که «وقتی رخ داد، دیتابیس جلویش را می‌گیرد و کد آن را درست ترجمه
    /// می‌کند» — و این تست دقیقاً همان را با قید واقعی دیتابیس می‌سنجد.
    /// </summary>
    private sealed class HookedWalletDbContext : WalletDbContext
    {
        public Action? BeforeSave;

        public HookedWalletDbContext(DbContextOptions<WalletDbContext> options) : base(options) { }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var hook = BeforeSave;
            BeforeSave = null; // فقط یک بار اجرا شود، وگرنه rollback هم دوباره آن را صدا می‌زند
            hook?.Invoke();
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    public SettlementUniquenessTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options;
        _context = new HookedWalletDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new WalletRepository(NullLogger<WalletRepository>.Instance, _context);

        SeedFullyLockedWallets();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void SeedWallet(Guid userId, string asset, decimal balance, decimal locked)
    {
        var wallet = WalletEntity.Create(userId, asset);
        wallet.Balance = balance;
        wallet.LockedBalance = locked;
        _context.Wallets.Add(wallet);
    }

    private void SeedFullyLockedWallets()
    {
        SeedWallet(_buyerId, Quote, balance: 0m, locked: QuoteQuantity);
        SeedWallet(_buyerId, Base, balance: 0m, locked: 0m);
        SeedWallet(_sellerId, Base, balance: 0m, locked: Quantity);
        SeedWallet(_sellerId, Quote, balance: 0m, locked: 0m);
        _context.SaveChanges();
    }

    private Task<(bool Success, string Message)> SettleAsync(Guid tradeId) =>
        _repository.SettleTradeAsync(
            tradeId, _buyerId, _sellerId, $"{Base}/{Quote}", Quantity, QuoteQuantity, 0m, 0m);

    private async Task<WalletEntity> ReloadAsync(Guid userId, string asset)
    {
        _context.ChangeTracker.Clear();
        return await _context.Wallets.SingleAsync(w => w.UserId == userId && w.Asset == asset);
    }

    /// <summary>یک تسویهٔ عادی باید سطر تسویه را ثبت کند — پایهٔ همهٔ تضمین‌های بعدی.</summary>
    [Fact]
    public async Task Settlement_RecordsExactlyOneTradeSettlementRow()
    {
        var tradeId = Guid.NewGuid();

        var (success, _) = await SettleAsync(tradeId);

        Assert.True(success);
        Assert.Equal(1, await _context.TradeSettlements.CountAsync(s => s.TradeId == tradeId));
    }

    /// <summary>
    /// مورد اصلی #42: رقیب پس از رد شدن ما از بررسی سریع، commit می‌کند. درج ما باید با
    /// نقض کلید اصلی شکست بخورد و کل تراکنش برگردد.
    /// </summary>
    [Fact]
    public async Task ConcurrentSettlement_IsRefusedByTheDatabase_AndReportedAsAlreadySettled()
    {
        var tradeId = Guid.NewGuid();

        // رقیب: سطر تسویه را مستقیماً درج می‌کند، درست پیش از SaveChanges ما — یعنی همان
        // لحظه‌ای که بررسی سریع ما از قبل رد شده و دیگر کاری از آن برنمی‌آید.
        _context.BeforeSave = () =>
            _context.Database.ExecuteSqlRaw(
                @"INSERT INTO TradeSettlements
                      (TradeId, SettledAt, Symbol, Quantity, QuoteQuantity, BuyerUserId, SellerUserId)
                  VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
                tradeId, DateTime.UtcNow, $"{Base}/{Quote}", Quantity, QuoteQuantity, _buyerId, _sellerId);

        var (success, message) = await SettleAsync(tradeId);

        // از دید فراخوان موفق است: معامله تسویه شده — فقط نه توسط ما.
        // اگر خطا برمی‌گشت، پردازشگر outbox پنج بار retry می‌کرد و معاملهٔ سالم را
        // به‌عنوان «گیرکرده» به اپراتور هشدار می‌داد.
        Assert.True(success, message);
        Assert.Contains("already settled", message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// مهم‌ترین ادعا: بازندهٔ رقابت نباید هیچ پولی جابه‌جا کرده باشد. این همان چیزی است
    /// که #42 دربارهٔ آن بود — نه پیام خطا، بلکه دو برابر شدن موجودی.
    /// </summary>
    [Fact]
    public async Task ConcurrentSettlement_LeavesBalancesAndTransactionsUntouched()
    {
        var tradeId = Guid.NewGuid();

        _context.BeforeSave = () =>
            _context.Database.ExecuteSqlRaw(
                @"INSERT INTO TradeSettlements
                      (TradeId, SettledAt, Symbol, Quantity, QuoteQuantity, BuyerUserId, SellerUserId)
                  VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6})",
                tradeId, DateTime.UtcNow, $"{Base}/{Quote}", Quantity, QuoteQuantity, _buyerId, _sellerId);

        await SettleAsync(tradeId);

        // هیچ سطر تراکنشی نوشته نشده — نه اینکه نوشته و بعد جبران شده باشد.
        Assert.Equal(0, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));

        // و موجودی‌ها دقیقاً همان حالت پیش از تسویه‌اند: وثیقه هنوز قفل، هیچ دارایی‌ای منتقل نشده.
        var buyerQuote = await ReloadAsync(_buyerId, Quote);
        var buyerBase = await ReloadAsync(_buyerId, Base);
        var sellerBase = await ReloadAsync(_sellerId, Base);
        var sellerQuote = await ReloadAsync(_sellerId, Quote);

        Assert.Equal(QuoteQuantity, buyerQuote.LockedBalance);
        Assert.Equal(0m, buyerQuote.Balance);
        Assert.Equal(0m, buyerBase.Balance);
        Assert.Equal(Quantity, sellerBase.LockedBalance);
        Assert.Equal(0m, sellerBase.Balance);
        Assert.Equal(0m, sellerQuote.Balance);
    }

    /// <summary>
    /// تحویل مجدد عادی outbox (رقابتی در کار نیست) باید از مسیر سریع رد شود: بدون استثنا،
    /// بدون تراکنش، و بدون نوشتن سطر دوم.
    /// </summary>
    [Fact]
    public async Task RedeliveryAfterSuccess_IsIdempotent_AndDoesNotDoubleApply()
    {
        var tradeId = Guid.NewGuid();

        var first = await SettleAsync(tradeId);
        var second = await SettleAsync(tradeId);

        Assert.True(first.Success);
        Assert.True(second.Success);

        Assert.Equal(1, await _context.TradeSettlements.CountAsync(s => s.TradeId == tradeId));
        Assert.Equal(4, await _context.Transactions.CountAsync(t => t.ReferenceId == tradeId.ToString()));

        // موجودی‌ها فقط یک بار جابه‌جا شده‌اند.
        Assert.Equal(Quantity, (await ReloadAsync(_buyerId, Base)).Balance);
        Assert.Equal(QuoteQuantity, (await ReloadAsync(_sellerId, Quote)).Balance);
    }

    /// <summary>
    /// دو معاملهٔ متفاوت نباید همدیگر را مسدود کنند — قید یکتایی باید روی شناسهٔ معامله
    /// باشد و نه چیزی که به‌طور اتفاقی بین معاملات مشترک است (مثل طرفین یا نماد).
    /// </summary>
    [Fact]
    public async Task DifferentTrades_BetweenTheSameParties_BothSettle()
    {
        var firstTrade = Guid.NewGuid();
        var secondTrade = Guid.NewGuid();

        // وثیقهٔ لازم برای معاملهٔ دوم را هم قفل می‌کنیم.
        //
        // هر دو کیف پول در یک واحد کاری بارگذاری می‌شوند و بین آن‌ها ChangeTracker پاک
        // نمی‌شود؛ در غیر این صورت موجودیت اول detach می‌شد و تغییرش هرگز ذخیره نمی‌شد.
        _context.ChangeTracker.Clear();
        var buyerQuote = await _context.Wallets.SingleAsync(w => w.UserId == _buyerId && w.Asset == Quote);
        var sellerBase = await _context.Wallets.SingleAsync(w => w.UserId == _sellerId && w.Asset == Base);
        buyerQuote.LockedBalance += QuoteQuantity;
        sellerBase.LockedBalance += Quantity;
        await _context.SaveChangesAsync();

        var a = await SettleAsync(firstTrade);
        var b = await SettleAsync(secondTrade);

        Assert.True(a.Success, a.Message);
        Assert.True(b.Success, b.Message);
        Assert.Equal(2, await _context.TradeSettlements.CountAsync());
        Assert.Equal(8, await _context.Transactions.CountAsync());
    }
}
