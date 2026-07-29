using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Wallet.Application;
using Wallet.Application.Mappers;
using Wallet.Core;
using Wallet.Infrastructure;

namespace Wallet.Tests;

/// <summary>
/// چیزی که پشت قرنطینهٔ <c>POST /api/wallet/transaction/trade</c> است (یافتهٔ ممیزی C-8،
/// issue #46).
///
/// این تست‌ها جایگزین <c>Wallet.Api/Tests/QuarantinedEndpointsTests.cs</c> هستند که حذف شد.
/// آن فایل دو مشکل داشت: اصلاً کامپایل نمی‌شد (پروژهٔ Wallet.Api نه NUnit دارد نه Moq، و
/// <c>Results.StatusCode</c> اورلودی با دو آرگومان ندارد)، و مهم‌تر اینکه در
/// <c>Setup</c> خودش یک <c>WebApplication</c> تازه می‌ساخت و <b>همان endpoint را دوباره
/// می‌نوشت</b> — یعنی نسخه‌ای را می‌سنجید که در خود فایل تست تعریف شده بود، نه چیزی که در
/// <c>Program.cs</c> اجرا می‌شود. حتی اگر endpoint واقعی حذف می‌شد، آن تست‌ها سبز
/// می‌ماندند.
///
/// چرا خطر واقعی است: اگر پرچم <c>FeatureFlags:QuarantineStubEndpoints</c> خاموش شود،
/// endpoint به <see cref="WalletService.MakeTradeAsync"/> می‌رسد — و آن متد
/// <c>new WalletBallanceDTO()</c> برمی‌گرداند، یعنی <b>HTTP 200 با اعداد صفر</b>، بدون
/// اینکه ریالی جابه‌جا شود. برای فراخوان، این از ۵۰۱ بدتر است: شکستِ خاموش به‌جای رد
/// صریح.
///
/// این تست‌ها همان را پین می‌کنند. اگر روزی <c>MakeTradeAsync</c> واقعاً پیاده‌سازی شود،
/// این فایل می‌شکند — و درست است که بشکند: همان تغییر باید قرنطینه را هم بردارد و این
/// تست‌ها را با تست‌های واقعیِ انتقال جایگزین کند.
/// </summary>
public class QuarantinedStubTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly WalletDbContext _context;
    private readonly WalletService _service;

    private readonly Guid _fromUserId = Guid.NewGuid();
    private readonly Guid _toUserId = Guid.NewGuid();
    private const string Asset = "MAUA";

    public QuarantinedStubTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<WalletDbContext>().UseSqlite(_connection).Options;
        _context = new WalletDbContext(options);
        _context.Database.EnsureCreated();

        var repository = new WalletRepository(NullLogger<WalletRepository>.Instance, _context);
        _service = new WalletService(repository, new WalletMapper());

        Seed(_fromUserId, balance: 100m);
        Seed(_toUserId, balance: 0m);
        _context.SaveChanges();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private void Seed(Guid userId, decimal balance)
    {
        var wallet = WalletEntity.Create(userId, Asset);
        wallet.Balance = balance;
        _context.Wallets.Add(wallet);
    }

    private async Task<decimal> BalanceOfAsync(Guid userId)
    {
        _context.ChangeTracker.Clear();
        return (await _context.Wallets.SingleAsync(w => w.UserId == userId && w.Asset == Asset)).Balance;
    }

    /// <summary>
    /// ادعای اصلی: متد «موفق» برمی‌گردد ولی هیچ پولی جابه‌جا نمی‌کند. دقیقاً همین است که
    /// قرنطینه را لازم می‌کند — یک متدی که خطا بدهد بی‌خطرتر بود.
    /// </summary>
    [Fact]
    public async Task MakeTradeAsync_ReportsSuccessButMovesNoMoney()
    {
        var result = await _service.MakeTradeAsync(_fromUserId, _toUserId, Asset, amount: 25m, referenceId: "REF-1");

        Assert.NotNull(result); // بدون استثنا، بدون پیام خطا — یعنی «انجام شد»

        Assert.Equal(100m, await BalanceOfAsync(_fromUserId)); // چیزی کسر نشد
        Assert.Equal(0m, await BalanceOfAsync(_toUserId));     // چیزی اضافه نشد
        Assert.Empty(_context.Transactions);                   // و هیچ ردی ثبت نشد
    }

    /// <summary>
    /// خروجی نه‌تنها اشتباه است، بلکه <b>خالی</b> است: هر فراخوانی که موجودی قبل/بعد را
    /// بخواند صفر می‌بیند و کد پیگیری هم ندارد. یعنی هیچ راهی نیست که فراخوان بفهمد چیزی
    /// انجام نشده.
    /// </summary>
    [Fact]
    public async Task MakeTradeAsync_ReturnsAnEmptyResultWithNoTrackingCode()
    {
        var result = await _service.MakeTradeAsync(_fromUserId, _toUserId, Asset, amount: 25m, referenceId: "REF-2");

        Assert.Equal(0m, result.BalanceBefore);
        Assert.Equal(0m, result.BalanceAfter);
        Assert.True(string.IsNullOrEmpty(result.TrackingCode));
    }

    /// <summary>
    /// دو گاردی که واقعاً کار می‌کنند باید بمانند — تنها اعتبارسنجی‌های این متد هستند و
    /// تنها چیزی که پیاده‌سازی آینده می‌تواند بی‌سروصدا از دست بدهد.
    /// </summary>
    [Fact]
    public async Task MakeTradeAsync_StillRefusesATransferToSelf()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MakeTradeAsync(_fromUserId, _fromUserId, Asset, 25m, "REF-3"));
    }

    [Fact]
    public async Task MakeTradeAsync_StillRefusesAMissingWallet()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _service.MakeTradeAsync(_fromUserId, Guid.NewGuid(), Asset, 25m, "REF-4"));
    }
}
