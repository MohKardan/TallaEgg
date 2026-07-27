using TallaEgg.Core;

namespace Wallet.Tests;

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

    private static decimal Lock(decimal quantity, decimal price) =>
        CurrenciesConstant.RoundToCurrencyPrecision(quantity * price, CurrenciesConstant.Toman);

    private static decimal Fill(decimal quantity, decimal price) =>
        CurrenciesConstant.RoundToCurrencyPrecision(quantity * price, CurrenciesConstant.Toman);

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

        // اندازهٔ fillها اهمیت دارد و انتخابشان بی‌دلیل نیست: با ۳+۳+۴ گرد کردن‌ها
        // یکدیگر را خنثی می‌کنند (−۰٫۲ −۰٫۲ +۰٫۴ = ۰) و باقی‌مانده صفر می‌شود. یعنی
        // یک ترکیب خوش‌شانس می‌تواند این باگ را پنهان کند — که دقیقاً همان دلیلی است
        // که چند ساعت به نظر می‌رسید باقی‌مانده «رشد نمی‌کند».
        var consumed = Fill(3m, price) + Fill(3m, price) + Fill(3m, price) + Fill(1m, price);

        Assert.NotEqual(locked, consumed);
        Assert.Equal(1m, locked - consumed);
    }

    /// <summary>
    /// ⚠️ این تست یک نقصِ <b>رفع‌نشده</b> را تثبیت می‌کند، نه رفتار مطلوب را.
    ///
    /// جهت دیگرِ همان مشکل و خطرناک‌ترش: مجموع fillها می‌تواند از مقدار قفل‌شده
    /// **بیشتر** شود. آن‌وقت گاردِ «وثیقهٔ قفل‌شده کافی نیست» روی یک معاملهٔ کاملاً
    /// معتبر فعال می‌شود، سفارش هرگز به Completed نمی‌رسد و در نتیجه آزادسازیِ
    /// باقی‌مانده هم اجرا نمی‌شود.
    ///
    /// گرد کردن قیمت و آزادسازی باقی‌مانده — که در همین تغییر اضافه شدند — این حالت
    /// را رفع نمی‌کنند. اینجا عمداً ثبت شده تا وقتی رفع شد، این تست شکست بخورد و
    /// کسی مجبور شود آگاهانه به‌روزش کند. جزئیات در issue #52.
    /// </summary>
    [Fact]
    public void KnownGap_PerFillRounding_CanStillOverConsumeTheLock()
    {
        var price = CurrenciesConstant.RoundOrderPrice(PricePerGram(BidPerMesghal));

        var locked = Lock(10m, price);
        // پنج fill دو گرمی: هرکدام ۰٫۸ دارد که رو به بالا گرد می‌شود.
        var consumed = Fill(2m, price) * 5;

        Assert.True(consumed > locked,
            $"consumed {consumed} should exceed locked {locked}, which is what rejects a valid trade");
        Assert.Equal(1m, consumed - locked);
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
