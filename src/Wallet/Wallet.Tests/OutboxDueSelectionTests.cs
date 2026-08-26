using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Orders.Application.Services;
using Orders.Core;
using Orders.Infrastructure;
using TallaEgg.Core.DTOs.Order;
using TallaEgg.Core.DTOs.Wallet;
using TallaEgg.Infrastructure.Clients;
using Wallet.Tests.Fakes;

namespace Wallet.Tests;

/// <summary>
/// پردازشگر outbox باید دقیقاً پیام‌هایی را بردارد که «سررسید» شده‌اند، و هیچ‌کدام از
/// بقیه را (issue #46).
///
/// این انتخاب تنها چیزی است که میان «تلاش مجدد» و «تلاش بی‌وقفه» فرق می‌گذارد. عقب‌نشینی
/// نمایی در <see cref="OutboxMessageTests"/> ثابت شده — ولی آن فقط <c>NextAttemptAt</c> را
/// جلو می‌برد. اگر کوئری آن را نادیده بگیرد، عقب‌نشینی هیچ اثری ندارد و پیامی که سرویس
/// کیف پول را از پا انداخته، هر پنج ثانیه دوباره به آن می‌کوبد تا سهمیهٔ تلاشش تمام شود.
///
/// همین‌طور یک پیام <c>Failed</c> نباید دوباره برداشته شود: برداشتنش یعنی مسیر re-drive
/// دستی (#39) بی‌معنا می‌شود، چون سیستم خودش کاری را که اپراتور باید آگاهانه انجام بدهد
/// تکرار می‌کند.
/// </summary>
public class OutboxDueSelectionTests : IDisposable
{
    private const int MaxRetries = 5;
    private static readonly TimeSpan BaseDelay = TimeSpan.FromSeconds(10);

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _provider;
    private readonly RecordingWalletClient _wallet = new();

    public OutboxDueSelectionTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using (var setup = new OrdersDbContext(Options()))
            setup.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddScoped(_ => new OrdersDbContext(Options()));
        services.AddScoped<IWalletApiClient>(_ => _wallet);
        _provider = services.BuildServiceProvider();
    }

    private DbContextOptions<OrdersDbContext> Options() =>
        new DbContextOptionsBuilder<OrdersDbContext>().UseSqlite(_connection).Options;

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    /// <summary>هر تسویه را می‌پذیرد و شناسهٔ معامله‌ای که برایش صدا زده شده را نگه می‌دارد.</summary>
    private sealed class RecordingWalletClient : StubWalletApiClient
    {
        public List<Guid> SettledTradeIds { get; } = [];

        public override Task<(bool Success, string Message)> TradeTransactionAndBalanceChangeAsync(TradeDto trade)
        {
            SettledTradeIds.Add(trade.Id);
            return Task.FromResult((true, "settled"));
        }
    }

    private static string PayloadFor(Guid tradeId) =>
        System.Text.Json.JsonSerializer.Serialize(new TradeDto
        {
            Id = tradeId,
            BuyerUserId = Guid.NewGuid(),
            SellerUserId = Guid.NewGuid(),
            Symbol = "MAUA/IRT",
            Quantity = 10m,
            QuoteQuantity = 184_680_733m
        });

    /// <summary>یک پیام ذخیره می‌کند و اجازه می‌دهد پیش از ذخیره حالتش دستکاری شود.</summary>
    private (Guid MessageId, Guid TradeId) Seed(string type = "TradeSettlement", Action<OutboxMessage>? arrange = null)
    {
        using var db = new OrdersDbContext(Options());
        var tradeId = Guid.NewGuid();
        var message = OutboxMessage.Create(type, tradeId, PayloadFor(tradeId));
        arrange?.Invoke(message);
        db.OutboxMessages.Add(message);
        db.SaveChanges();
        return (message.Id, tradeId);
    }

    private async Task RunProcessorAsync()
    {
        var processor = new OutboxProcessorService(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<OutboxProcessorService>.Instance);

        await processor.ProcessDueMessagesAsync(CancellationToken.None);
    }

    private async Task<OutboxMessage> ReloadAsync(Guid messageId)
    {
        using var db = new OrdersDbContext(Options());
        return await db.OutboxMessages.SingleAsync(m => m.Id == messageId);
    }

    // ── چه چیزی برداشته می‌شود ──────────────────────────────────────────────────

    [Fact]
    public async Task AFreshPendingMessage_IsProcessed()
    {
        var (messageId, tradeId) = Seed();

        await RunProcessorAsync();

        Assert.Equal([tradeId], _wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Completed, (await ReloadAsync(messageId)).Status);
    }

    /// <summary>
    /// پیامی که یک بار شکست خورده تا لحظهٔ سررسیدش کنار گذاشته می‌شود. بدون این فیلتر،
    /// عقب‌نشینی نمایی فقط یک عدد در دیتابیس است و هیچ فشاری از سرویسِ در حال خفگی برداشته
    /// نمی‌شود.
    /// </summary>
    [Fact]
    public async Task APendingMessageWhoseRetryIsNotDueYet_IsSkipped()
    {
        var (messageId, _) = Seed(arrange: m => m.MarkAttemptFailed("boom", MaxRetries, BaseDelay));

        await RunProcessorAsync();

        Assert.Empty(_wallet.SettledTradeIds);

        var message = await ReloadAsync(messageId);
        Assert.Equal(OutboxMessageStatus.Pending, message.Status);
        Assert.Equal(1, message.RetryCount); // تلاشی مصرف نشد
    }

    /// <summary>
    /// پیامِ به‌طور دائم شکست‌خورده کنار گذاشته می‌شود. اگر برداشته می‌شد، هم لاگ «تسویه
    /// گیر کرده» بی‌پایان تکرار می‌شد و هم re-drive اپراتور بی‌معنا می‌شد.
    /// </summary>
    [Fact]
    public async Task AFailedMessage_IsSkipped()
    {
        var (messageId, _) = Seed(arrange: m =>
        {
            for (var i = 0; i < MaxRetries; i++)
                m.MarkAttemptFailed("boom", MaxRetries, BaseDelay);
        });

        Assert.Equal(OutboxMessageStatus.Failed, (await ReloadAsync(messageId)).Status);

        await RunProcessorAsync();

        Assert.Empty(_wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Failed, (await ReloadAsync(messageId)).Status);
    }

    /// <summary>
    /// پیام رهاشده (issue #39) هم باید کنار گذاشته شود — دقیقاً به همان دلیل پیام
    /// <c>Failed</c>: برداشتنش یعنی تصمیم آگاهانهٔ اپراتور («این هرگز تسویه نمی‌شود») را
    /// دور می‌زند.
    /// </summary>
    [Fact]
    public async Task AnAbandonedMessage_IsSkipped()
    {
        var (messageId, _) = Seed(arrange: m =>
        {
            for (var i = 0; i < MaxRetries; i++)
                m.MarkAttemptFailed("boom", MaxRetries, BaseDelay);
            m.MarkAbandoned("collateral consumed by later activity");
        });

        await RunProcessorAsync();

        Assert.Empty(_wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Abandoned, (await ReloadAsync(messageId)).Status);
    }

    /// <summary>
    /// پیام تکمیل‌شده دوباره فرستاده نمی‌شود. تسویه تکرارپذیر است، پس این ایمنی است نه
    /// درستی — ولی هر تحویل مجدد یک فراخوانی HTTP بیهوده به سرویس کیف پول است.
    /// </summary>
    [Fact]
    public async Task ACompletedMessage_IsSkipped()
    {
        var (messageId, _) = Seed(arrange: m => m.MarkCompleted());

        await RunProcessorAsync();

        Assert.Empty(_wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Completed, (await ReloadAsync(messageId)).Status);
    }

    /// <summary>
    /// پیامی که سررسیدش رسیده باید برداشته شود — وگرنه فیلتر بالا به «هرگز تلاش مجدد نکن»
    /// تبدیل می‌شود و همان تسویهٔ گیرکرده‌ای ساخته می‌شود که outbox قرار بود جلویش را
    /// بگیرد. سررسید با گذاشتن آن در گذشته شبیه‌سازی می‌شود تا تست منتظر نماند.
    /// </summary>
    [Fact]
    public async Task APendingMessageWhoseRetryHasComeDue_IsProcessed()
    {
        var (messageId, tradeId) = Seed(arrange: m =>
            m.MarkAttemptFailed("boom", MaxRetries, TimeSpan.FromSeconds(-30)));

        await RunProcessorAsync();

        Assert.Equal([tradeId], _wallet.SettledTradeIds);
        Assert.Equal(OutboxMessageStatus.Completed, (await ReloadAsync(messageId)).Status);
    }

    // ── نوع ناشناخته ────────────────────────────────────────────────────────────

    /// <summary>
    /// نوع پیامی که مسیری برایش تعریف نشده باید صریحاً رد شود، نه بی‌صدا تکمیل.
    ///
    /// اگر <c>default</c> پیام را <c>Completed</c> علامت می‌زد، افزودن نوع جدیدی از پیام —
    /// بدون نوشتن مسیرش — به صف پاک‌شده‌ای می‌رسید که هیچ کاری انجام نداده بود، و هیچ ردی
    /// هم باقی نمی‌ماند. پیام خطا باید نوع را نام ببرد، چون همان تنها سرنخِ اپراتور در
    /// <c>LastError</c> است.
    /// </summary>
    [Fact]
    public async Task AnUnknownMessageType_FailsLoudlyAndNamesTheType()
    {
        var (messageId, _) = Seed(type: "SomethingNobodyWrote");

        await RunProcessorAsync();

        Assert.Empty(_wallet.SettledTradeIds);

        var message = await ReloadAsync(messageId);
        Assert.NotEqual(OutboxMessageStatus.Completed, message.Status);
        Assert.Equal(1, message.RetryCount);
        Assert.Contains("SomethingNobodyWrote", message.LastError);
    }

    /// <summary>
    /// محتوای خراب هم باید همین‌طور رفتار کند: یک تلاش ناموفقِ ثبت‌شده، نه استثنایی که از
    /// حلقه بیرون بزند و بقیهٔ دسته را زمین بگذارد.
    /// </summary>
    [Fact]
    public async Task AnUndeserializablePayload_IsRecordedAsAFailedAttempt()
    {
        Guid messageId;
        using (var db = new OrdersDbContext(Options()))
        {
            var message = OutboxMessage.Create("TradeSettlement", Guid.NewGuid(), "this is not json");
            db.OutboxMessages.Add(message);
            db.SaveChanges();
            messageId = message.Id;
        }

        await RunProcessorAsync();

        var reloaded = await ReloadAsync(messageId);
        Assert.Equal(OutboxMessageStatus.Pending, reloaded.Status); // هنوز سهمیهٔ تلاش دارد
        Assert.Equal(1, reloaded.RetryCount);
        Assert.False(string.IsNullOrWhiteSpace(reloaded.LastError));
    }
}
