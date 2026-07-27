namespace Wallet.Core;

/// <summary>
/// یک سطر به‌ازای هر معاملهٔ تسویه‌شده. کلید اصلی خودِ <see cref="TradeId"/> است.
///
/// چرا این جدول وجود دارد:
/// تضمین «هر معامله دقیقاً یک بار تسویه می‌شود» پیش‌تر فقط با یک SELECT در کد برقرار
/// می‌شد که بیرون از تراکنش اجرا می‌شد و هیچ پشتوانه‌ای در دیتابیس نداشت. دو تسویهٔ
/// همزمان از یک معامله می‌توانستند هر دو آن بررسی را رد کنند و هر دو اعمال شوند،
/// یعنی پول از هیچ ساخته می‌شد (issue #42).
///
/// با کلید اصلی بودن TradeId، این تضمین از یک «قاعدهٔ کد» به یک «حقیقت سطح schema»
/// تبدیل می‌شود: حتی اگر روزی مسیر جدیدی بررسی کد را دور بزند، دیتابیس درج دوم را
/// رد می‌کند. این همان درسی است که از #53 گرفتیم — محافظت ساختاری از محافظت رفتاری
/// قابل اتکاتر است.
///
/// چرا جدول جدا و نه ایندکس یکتا روی Transactions:
/// هر تسویه به‌درستی چهار سطر تراکنش با یک ReferenceId می‌نویسد (یک سطر برای هر پایه)،
/// پس ایندکس یکتا باید روی (WalletId, ReferenceId) می‌بود — که کار می‌کند اما نیت را
/// بیان نمی‌کند. اینجا «یک سطر = یک تسویه» مستقیماً خوانده می‌شود.
/// </summary>
public class TradeSettlement
{
    /// <summary>شناسهٔ معامله در سرویس سفارش‌ها. کلید اصلی — همین است که تسویهٔ تکراری را غیرممکن می‌کند.</summary>
    public Guid TradeId { get; private set; }

    /// <summary>زمان تسویه. برای مغایرت‌گیری و پاسخ به «این معامله کِی تسویه شد؟».</summary>
    public DateTime SettledAt { get; private set; }

    /// <summary>
    /// نماد و مقادیر برای حسابرسی نگهداری می‌شوند. بدون این‌ها، فهمیدن اینکه یک سطر
    /// تسویه به چه چیزی مربوط بوده نیازمند join با سرویس سفارش‌ها (دیتابیس دیگر) است.
    /// </summary>
    public string Symbol { get; private set; } = string.Empty;

    public decimal Quantity { get; private set; }

    public decimal QuoteQuantity { get; private set; }

    public Guid BuyerUserId { get; private set; }

    public Guid SellerUserId { get; private set; }

    /// <summary>EF Core به سازندهٔ بدون پارامتر نیاز دارد.</summary>
    private TradeSettlement() { }

    public static TradeSettlement Create(
        Guid tradeId, Guid buyerUserId, Guid sellerUserId,
        string symbol, decimal quantity, decimal quoteQuantity)
    {
        return new TradeSettlement
        {
            TradeId = tradeId,
            BuyerUserId = buyerUserId,
            SellerUserId = sellerUserId,
            Symbol = symbol,
            Quantity = quantity,
            QuoteQuantity = quoteQuantity,
            SettledAt = DateTime.UtcNow
        };
    }
}
