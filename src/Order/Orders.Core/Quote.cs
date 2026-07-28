using TallaEgg.Core.Enums.Order;

namespace Orders.Core;

/// <summary>
/// مظنهٔ منتشرشدهٔ ادمین برای یک نماد: قیمت خرید و فروش او.
///
/// <para>
/// <b>مظنه یک سفارش نیست.</b> این تمام نکتهٔ issue #48 است. پیش‌تر ادمین برای اعلام قیمت
/// دو سفارش محدود ۱۰۰۰ گرمی ثبت می‌کرد و پنج مشکل از آن می‌آمد: مقدار ۱۰۰۰ دلبخواه بود،
/// حدود ۱۹ میلیارد تومان و ۱۰۰۰ گرم وثیقه فقط برای «اعلام قیمت» قفل می‌شد، پس از مصرف شدن
/// آن مقدار نقدینگی تمام می‌شد، و آنچه ادمین «مظنهٔ امروز» می‌دانست سیستم «سفارش» ذخیره
/// می‌کرد.
/// </para>
///
/// <para>
/// انتشار مظنه هیچ وثیقه‌ای قفل نمی‌کند و هیچ سفارشی در دفتر نمی‌گذارد. سفارش‌ها فقط در
/// لحظه‌ای ساخته می‌شوند که مشتری مظنه را می‌پذیرد — دقیقاً به اندازهٔ مقدار درخواستی، و
/// بلافاصله مصرف می‌شوند.
/// </para>
///
/// <para>
/// <b>سقف مقدار ندارد:</b> تصمیم کسب‌وکاری این بود که اگر ادمین موجودی کافی نداشته باشد
/// معامله انجام شود و موجودی او منفی برود — همان رفتار امروز، چون ادمین از اعتبارسنجی معاف
/// است. ریسکِ انباشته‌شدن این موقعیت در issue #61 پیگیری می‌شود.
/// </para>
/// </summary>
public class Quote
{
    public Guid Id { get; private set; }

    /// <summary>نماد به شکل BASE/QUOTE — مثلاً MAUA/IRT.</summary>
    public string Symbol { get; private set; } = string.Empty;

    /// <summary>
    /// قیمتی که ادمین <b>می‌خرد</b> — یعنی مشتری با این قیمت <b>می‌فروشد</b>.
    /// بر حسب واحد پایه ذخیره می‌شود (تومان بر گرم)، نه بر مثقال؛ تبدیل مثقال در لایهٔ
    /// ربات انجام می‌شود، همان‌جا که کاربر عدد را وارد و مشاهده می‌کند.
    /// </summary>
    public decimal BuyPrice { get; private set; }

    /// <summary>قیمتی که ادمین <b>می‌فروشد</b> — یعنی مشتری با این قیمت <b>می‌خرد</b>.</summary>
    public decimal SellPrice { get; private set; }

    /// <summary>ادمینی که مظنه را منتشر کرده؛ همان کسی که طرف مقابل هر معامله خواهد بود.</summary>
    public Guid PublishedByUserId { get; private set; }

    public DateTime PublishedAt { get; private set; }

    /// <summary>
    /// فقط یک مظنه برای هر نماد فعال است. انتشار مظنهٔ جدید، قبلی را غیرفعال می‌کند.
    /// مظنه‌های قدیمی حذف نمی‌شوند تا بعداً بشود فهمید هر معامله روی چه قیمتی انجام شده.
    /// </summary>
    public bool IsActive { get; private set; }

    public DateTime? DeactivatedAt { get; private set; }

    /// <summary>EF Core به سازندهٔ بدون پارامتر نیاز دارد.</summary>
    private Quote() { }

    public static Quote Publish(string symbol, decimal buyPrice, decimal sellPrice, Guid publishedByUserId)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("نماد نمی‌تواند خالی باشد.", nameof(symbol));

        if (buyPrice <= 0)
            throw new ArgumentException("قیمت خرید باید بزرگ‌تر از صفر باشد.", nameof(buyPrice));

        if (sellPrice <= 0)
            throw new ArgumentException("قیمت فروش باید بزرگ‌تر از صفر باشد.", nameof(sellPrice));

        // اسپرد منفی یعنی ادمین گران‌تر می‌خرد از آنچه می‌فروشد. مشتری می‌توانست بی‌نهایت
        // بار بخرد و بفروشد و هر بار سود کند، مستقیماً از جیب طلافروش. این را همین‌جا
        // می‌گیریم، نه بعد از اینکه چند معامله انجام شد.
        if (buyPrice > sellPrice)
            throw new ArgumentException(
                $"قیمت خرید ({buyPrice}) نمی‌تواند از قیمت فروش ({sellPrice}) بیشتر باشد.");

        if (publishedByUserId == Guid.Empty)
            throw new ArgumentException("شناسهٔ منتشرکننده الزامی است.", nameof(publishedByUserId));

        return new Quote
        {
            Id = Guid.NewGuid(),
            Symbol = symbol.Trim().ToUpperInvariant(),
            BuyPrice = buyPrice,
            SellPrice = sellPrice,
            PublishedByUserId = publishedByUserId,
            PublishedAt = DateTime.UtcNow,
            IsActive = true
        };
    }

    public void Deactivate()
    {
        if (!IsActive) return;

        IsActive = false;
        DeactivatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// قیمتی که مشتری با آن معامله می‌کند.
    ///
    /// عمداً روی خود موجودیت است و نه در سرویس: جابه‌جا کردن این دو، همان باگ وارونگی
    /// خریدار/فروشنده است با لباس دیگر. مشتری که می‌خرد، از ادمین می‌خرد، پس قیمتِ فروشِ
    /// ادمین را می‌پردازد.
    /// </summary>
    public decimal PriceFor(OrderSide customerSide) =>
        customerSide == OrderSide.Buy ? SellPrice : BuyPrice;
}
