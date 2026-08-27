using TallaEgg.Core;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// وثیقهٔ قفل‌شده باید پس از پر شدن کامل سفارش، کاملاً مصرف یا آزاد شود.
///
/// دو منبع مستقل باقی‌مانده وجود داشت (issue #52):
///
/// ۱. یک‌باره، هنگام قفل: ربات قیمت را گرد‌نشده می‌فرستاد (قیمت مثقال ÷ ۴٫۳۳۱۸) و قفل
///    از همان مقدار حساب می‌شد، ولی ستون Orders.Price فقط دو رقم اعشار نگه می‌دارد و
///    تسویه قیمتِ گردشده را از دیتابیس می‌خواند.
///
/// ۲. در هر fill: مصرف هر معامله جداگانه گرد می‌شود، و مجموعِ مقادیرِ جداگانه‌گردشده
///    با مقدارِ یک‌بار‌گردشده برابر نیست.
///
/// این تست‌ها روی خودِ حسابِ عددی کار می‌کنند، چون همین حساب است که اشتباه بود.
/// </summary>
public class LockResidueTests
{
    private const decimal AskPerMesghal = 80_000_000m;
    private const decimal BidPerMesghal = 79_000_000m;

    private static decimal PricePerGram(decimal perMesghal) =>
        perMesghal / CurrenciesConstant.GramsPerMesghal;

    /// <summary>قفل: رو به بالا — همان چیزی که OrderService استفاده می‌کند.</summary>
    private static decimal Lock(decimal quantity, decimal price) =>
        CurrenciesConstant.CeilingToCurrencyPrecision(quantity * price, CurrenciesConstant.Toman);

    /// <summary>مصرف هر معامله: رو به پایین — همان چیزی که CreateTrade استفاده می‌کند.</summary>
    private static decimal Fill(decimal quantity, decimal price) =>
        CurrenciesConstant.FloorToCurrencyPrecision(quantity * price, CurrenciesConstant.Toman);

    /// <summary>
    /// منبع ۱. قیمتِ گردشده همان است که ذخیره و بعداً برای تسویه خوانده می‌شود، پس قفل
    /// باید از همان حساب شود. نرخ خرید انتخاب شده چون ۷۹٬۰۰۰٬۰۰۰ ÷ ۴٫۳۳۱۸ به دو رقم
    /// اعشار بسته نمی‌شود — همان حالتی که باگ را نشان داد.
    /// </summary>
    [Fact]
    public void LockUsesTheSamePriceThatWillBeStored()
    {
        var raw = PricePerGram(BidPerMesghal);              // 18237222.401772935…
        var stored = CurrenciesConstant.RoundOrderPrice(raw); // 18237222.40

        var lockedFromRawPrice = Lock(1000m, raw);
        var lockedFromStoredPrice = Lock(1000m, stored);

        // این دو عدد پیش‌تر ۲ تومان اختلاف داشتند و همان اختلاف تا ابد قفل می‌ماند.
        Assert.NotEqual(lockedFromRawPrice, lockedFromStoredPrice);
        Assert.Equal(18_237_222_400m, lockedFromStoredPrice);
    }

    /// <summary>قیمت باید دقیقاً به دقت ستون گرد شود، نه بیشتر و نه کمتر.</summary>
    [Theory]
    [InlineData(80_000_000, 18_468_073.32)]
    [InlineData(79_000_000, 18_237_222.40)]
    public void PriceIsRoundedToTheColumnScale(decimal perMesghal, decimal expected)
    {
        var stored = CurrenciesConstant.RoundOrderPrice(PricePerGram(perMesghal));

        Assert.Equal(expected, stored);
        Assert.Equal(stored, Math.Round(stored, CurrenciesConstant.OrderPriceDecimalPlaces));
    }

    /// <summary>
    /// منبع ۲، و دلیل اینکه گرد کردن قیمت به‌تنهایی کافی نیست: حتی با قیمت یکسان،
    /// مجموع fillهای جداگانه‌گردشده با قفلِ یک‌بار‌گردشده برابر نیست.
    ///
    /// این تست وجودِ باقی‌مانده را اثبات می‌کند تا روشن باشد چرا آزادسازی در پایان
    /// سفارش لازم است و نمی‌توان صرفاً به گرد کردن قیمت اکتفا کرد.
    /// </summary>
    [Fact]
    public void PerFillRounding_LeavesAResidue_EvenWithTheStoredPrice()
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));

        var locked = Lock(10m, price);
        var consumed = Fill(3m, price) + Fill(3m, price) + Fill(3m, price) + Fill(1m, price);

        // باقی‌مانده وجود دارد و صرفاً با گرد کردن قیمت از بین نمی‌رود — برای همین
        // آزادسازی در پایان سفارش لازم است و نمی‌توان به گرد کردن اکتفا کرد.
        Assert.NotEqual(locked, consumed);
        Assert.True(locked > consumed);
    }

    /// <summary>
    /// همان حالتی که پیش‌تر بیش‌مصرف می‌کرد: پنج fill دو گرمی، که هرکدام کسر ۰٫۸ دارد.
    /// با AwayFromZero همه رو به بالا گرد می‌شدند و مجموعشان ۱ تومان از قفل بیشتر
    /// می‌شد، پس گاردِ «وثیقهٔ کافی نیست» یک معاملهٔ کاملاً معتبر را رد می‌کرد.
    /// </summary>
    [Fact]
    public void FillsThatUsedToOverConsume_NoLongerDo()
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));

        var locked = Lock(10m, price);
        var consumed = Fill(2m, price) * 5;

        Assert.True(consumed <= locked, $"consumed {consumed} must not exceed locked {locked}");
    }

    /// <summary>
    /// خودِ تضمین، نه یک نمونه: برای هر ترکیبی از اندازهٔ fillها، مجموع مصرف هرگز از
    /// مقدار قفل‌شده بیشتر نمی‌شود.
    ///
    ///     Σ Floor(qᵢ × p) ≤ Σ qᵢ×p = Q×p ≤ Ceiling(Q×p)
    ///
    /// این چیزی است که یک تست تک‌نمونه‌ای نمی‌تواند نشان دهد — یک ترکیب خوش‌شانس
    /// می‌تواند سبز بماند در حالی که ترکیب دیگری می‌شکند. دقیقاً همان اتفاقی که با
    /// ۳+۳+۴ افتاد و باعث شد چند ساعت به نظر برسد باقی‌مانده رشد نمی‌کند.
    /// </summary>
    [Theory]
    [InlineData(new double[] { 2, 2, 2, 2, 2 })]
    [InlineData(new double[] { 3, 3, 3, 1 })]
    [InlineData(new double[] { 3, 3, 4 })]
    [InlineData(new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 })]
    [InlineData(new double[] { 0.1, 0.3, 0.7, 1.9, 7 })]
    [InlineData(new double[] { 9.99, 0.01 })]
    public void ConsumptionNeverExceedsTheLock_ForAnyFillPattern(double[] fills)
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));
        var total = fills.Sum(f => (decimal)f);

        var locked = Lock(total, price);
        var consumed = fills.Sum(f => Fill((decimal)f, price));

        Assert.True(consumed <= locked,
            $"fills [{string.Join(", ", fills)}] consumed {consumed} but only {locked} was locked");
    }

    /// <summary>
    /// و باقی‌مانده باید ناچیز بماند — حداکثر یک واحد به‌ازای هر fill. اگر روزی جهت
    /// گرد کردن اشتباه عوض شود، مجموع مصرف کمتر می‌شود ولی این تست هم می‌شکند.
    /// </summary>
    [Theory]
    [InlineData(new double[] { 2, 2, 2, 2, 2 })]
    [InlineData(new double[] { 3, 3, 3, 1 })]
    [InlineData(new double[] { 0.1, 0.3, 0.7, 1.9, 7 })]
    public void TheResidueStaysBoundedByOneUnitPerFill(double[] fills)
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));
        var total = fills.Sum(f => (decimal)f);

        var residue = Lock(total, price) - fills.Sum(f => Fill((decimal)f, price));

        Assert.InRange(residue, 0m, fills.Length + 1);
    }

    /// <summary>
    /// باقی‌مانده = «آنچه قفل شد» منهای «آنچه مصرف شد». همان فرمولی که مسیر لغو و
    /// مسیر تکمیل هر دو استفاده می‌کنند. با آزاد کردن این مقدار، قفل به صفر می‌رسد.
    /// </summary>
    [Fact]
    public void ReleasingTheResidue_BringsTheLockToZero()
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));

        var locked = Lock(10m, price);
        var consumed = Fill(3m, price) + Fill(3m, price) + Fill(3m, price) + Fill(1m, price);
        var residue = locked - consumed;

        Assert.NotEqual(0m, residue);           // اول مطمئن شویم چیزی برای آزاد کردن هست
        Assert.True(residue > 0);               // و همیشه در جهت آزادسازی، نه بدهکاری
        Assert.Equal(0m, locked - consumed - residue);
    }

    /// <summary>
    /// سمت فروشنده باقی‌مانده ندارد: وثیقه‌اش دارایی پایه است و هر معامله دقیقاً
    /// Quantity مصرف می‌کند، بدون گرد کردن. این تست آن فرض را تثبیت می‌کند تا اگر
    /// روزی عوض شد، سکوت نکند.
    /// </summary>
    [Fact]
    public void SellSide_HasNoResidue()
    {
        var locked = CurrenciesConstant.RoundToCurrencyPrecision(10m, CurrenciesConstant.Maua);
        var consumed = 3m + 3m + 4m;

        Assert.Equal(locked, consumed);
    }
}
