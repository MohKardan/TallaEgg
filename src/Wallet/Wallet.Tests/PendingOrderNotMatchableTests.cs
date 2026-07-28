using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.Enums.Order;

namespace Wallet.Tests;

/// <summary>
/// سفارشی که هنوز تأیید نشده نباید وارد تطبیق شود.
///
/// این ریشهٔ یافتهٔ ممیزی C-5 است. سفارش لحظه‌ای که ذخیره می‌شود <c>Pending</c> است —
/// پیش از آنکه وثیقه‌اش قفل شود. پیش‌تر پرس‌وجوهای دفتر سفارش <c>Pending</c> را هم
/// برمی‌گرداندند، پس حلقهٔ پس‌زمینه (هر ثانیه) می‌توانست سفارشی را بردارد که هیچ وثیقه‌ای
/// پشتش نبود و معامله‌ای ثبت کند که تسویه‌اش هرگز موفق نمی‌شد.
///
/// حالا ترتیب «قفل، سپس تأیید، سپس تطبیق» است و فقط <c>Confirmed</c> دیده می‌شود — پس
/// «هیچ معامله‌ای پیش از قفل وثیقه‌اش وجود ندارد» یک تضمین ساختاری است، نه یک قرارداد
/// رفتاری که کد باید رعایتش کند.
/// </summary>
public class PendingOrderNotMatchableTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly OrdersDbContext _context;
    private readonly OrderMatchingRepository _repository;

    private const string Symbol = "MAUA/IRT";

    public PendingOrderNotMatchableTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;
        _context = new OrdersDbContext(options);
        _context.Database.EnsureCreated();
        _repository = new OrderMatchingRepository(_context, NullLogger<OrderMatchingRepository>.Instance);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    /// <summary>سفارش را در وضعیت دلخواه می‌سازد. بدون Confirm، سفارش Pending می‌ماند.</summary>
    private Order AddOrder(OrderSide side, bool confirm)
    {
        var order = Order.CreateMakerOrder(Symbol, 10m, 20_000_000m, Guid.NewGuid(), side, TradingType.Spot);
        if (confirm) order.Confirm();
        _context.Orders.Add(order);
        _context.SaveChanges();
        return order;
    }

    // نکته دربارهٔ پوشش: GetBuyOrdersWithLockAsync و GetSellOrdersWithLockAsync اینجا
    // مستقیماً تست نمی‌شوند، چون با ‎OrderBy(o => o.Price)‎ مرتب می‌کنند و SQLite مرتب‌سازی
    // روی decimal را پشتیبانی نمی‌کند (روی SQL Server مشکلی نیست). هر دو از همان
    // Expression مشترکِ Matchable استفاده می‌کنند که تست GetActiveAssetsAsync پایین آن
    // را پوشش می‌دهد. این محدودیتِ محیط تست است، نه کد — و در #46 ثبت می‌شود.

    /// <summary>
    /// حلقهٔ پس‌زمینه از این فهرست تصمیم می‌گیرد کدام دارایی‌ها را اسکن کند. اگر سفارش
    /// Pending اینجا بیاید، موتور بیدار می‌شود تا روی سفارشی کار کند که هنوز وثیقه ندارد.
    /// </summary>
    [Fact]
    public async Task PendingOrders_DoNotMakeAnAssetLookActive()
    {
        AddOrder(OrderSide.Buy, confirm: false);

        Assert.Empty(await _repository.GetActiveAssetsAsync());
    }

    /// <summary>و سفارش تأییدشده باید همچنان دارایی را «فعال» نشان دهد — وگرنه تست بالا
    /// با «هیچ‌چیز هرگز فعال نیست» هم سبز می‌ماند.</summary>
    [Fact]
    public async Task ConfirmedOrders_DoMakeAnAssetActive()
    {
        AddOrder(OrderSide.Buy, confirm: true);

        Assert.Single(await _repository.GetActiveAssetsAsync());
    }

    /// <summary>
    /// دفاع در عمق: حتی اگر سفارشی از راه دیگری به تطبیق برسد، بازبینیِ داخل تراکنش هم
    /// باید آن را رد کند. این تضمین می‌کند فیلترِ پرس‌وجو و بازبینیِ اتمی از هم جدا نشوند.
    ///
    /// مقدار تطبیق عمداً <b>کمتر</b> از مقدار سفارش است تا پر شدن جزئی رخ دهد.
    /// با پر شدن کامل، <c>Order.Complete()</c> روی سفارش Pending استثنا می‌دهد و معامله
    /// به‌طور تصادفی برگردانده می‌شود — یعنی تست حتی بدون این رفع هم سبز می‌ماند و چیزی
    /// را ثابت نمی‌کند. مسیر پر شدن جزئی چنین گارد تصادفی‌ای ندارد، پس تنها همین حالت
    /// واقعاً فیلتر را می‌سنجد.
    /// </summary>
    [Fact]
    public async Task APartialMatchAgainstAPendingOrder_IsRejectedInsideTheTransaction()
    {
        var buy = AddOrder(OrderSide.Buy, confirm: true);
        var pendingSell = AddOrder(OrderSide.Sell, confirm: false);

        var (success, trade, error) = await _repository.ExecuteAtomicMatchAsync(buy, pendingSell, 4m);

        Assert.False(success, "an unconfirmed order must not be settled against");
        Assert.Null(trade);
        Assert.Empty(_context.Trades);

        // پیام مشخص، تا رد شدن به دلیل دیگری با این اشتباه گرفته نشود.
        Assert.Contains("وضعیت سفارشات", error);
    }

    /// <summary>
    /// و مسیر عادی هنوز کار می‌کند — وگرنه تست‌های بالا با «هیچ‌چیز تطبیق نمی‌خورد» هم
    /// سبز می‌ماندند.
    /// </summary>
    [Fact]
    public async Task TwoConfirmedOrders_StillMatch()
    {
        var buy = AddOrder(OrderSide.Buy, confirm: true);
        var sell = AddOrder(OrderSide.Sell, confirm: true);

        var (success, trade, error) = await _repository.ExecuteAtomicMatchAsync(buy, sell, 10m);

        Assert.True(success, error);
        Assert.NotNull(trade);
    }
}
