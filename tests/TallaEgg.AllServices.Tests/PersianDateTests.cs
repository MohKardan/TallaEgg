using TallaEgg.Core.Utilties;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// تبدیل تاریخ میلادی به شمسی.
///
/// نسخهٔ قبلی سال را ۶۲۱ واحد کم می‌کرد و ماه را جابه‌جا می‌کرد، اما روزِ میلادی را
/// دست‌نخورده برمی‌گرداند. نتیجه‌اش این بود که کاربر تاریخ معاملهٔ خودش را تا ۲۲ روز
/// اشتباه می‌دید — مثلاً ۲۷ ژوئیهٔ ۲۰۲۶ به‌جای «۵ مرداد» به‌صورت «۲۷ مرداد» نمایش
/// داده می‌شد.
///
/// همچنین زمان‌ها در دیتابیس UTC ذخیره می‌شوند و بدون تبدیل، ساعت اشتباه نمایش داده
/// می‌شد.
/// </summary>
public class PersianDateTests
{
    /// <summary>
    /// همان تاریخی که باگ روی آن دیده شد. ۲۷ ژوئیهٔ ۲۰۲۶ = ۵ مرداد ۱۴۰۵.
    /// </summary>
    [Fact]
    public void TheDateThatExposedTheBug_ConvertsCorrectly()
    {
        var utc = new DateTime(2026, 7, 27, 6, 0, 0, DateTimeKind.Utc);

        var result = Utils.ConvertToPersianDate(utc);

        Assert.StartsWith("1405/05/05", result);
    }

    /// <summary>
    /// چند تاریخ مرزی. مرز ماه‌ها و مرز سال جایی است که محاسبهٔ «تقریبی» قبلی بیشترین
    /// خطا را داشت.
    /// </summary>
    [Theory]
    [InlineData(2026, 3, 21, "1405/01/01")] // نوروز ۱۴۰۵
    [InlineData(2026, 3, 20, "1404/12/29")] // یک روز پیش از نوروز
    [InlineData(2026, 7, 22, "1405/04/31")] // آخرین روز تیر
    [InlineData(2026, 7, 23, "1405/05/01")] // اولین روز مرداد
    [InlineData(2026, 12, 31, "1405/10/10")]
    public void KnownDates_ConvertCorrectly(int year, int month, int day, string expected)
    {
        // ساعت ۱۲ ظهر UTC انتخاب شده تا افست +۳:۳۰ تهران روز را جابه‌جا نکند و تست
        // دربارهٔ خودِ تبدیل تقویم باشد، نه دربارهٔ مرز نیمه‌شب.
        var utc = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);

        var result = Utils.ConvertToPersianDate(utc);

        Assert.StartsWith(expected, result);
    }

    /// <summary>
    /// ساعت باید به وقت تهران باشد. معامله‌ای که ۰۹:۵۲ به وقت UTC ثبت شده، برای کاربر
    /// ایرانی ۱۳:۲۲ اتفاق افتاده است.
    /// </summary>
    [Fact]
    public void TimeIsShownInTehranTime_NotUtc()
    {
        var utc = new DateTime(2026, 7, 27, 9, 52, 0, DateTimeKind.Utc);

        var result = Utils.ConvertToPersianDate(utc);

        Assert.EndsWith("13:22", result);
    }

    /// <summary>
    /// تبدیل منطقهٔ زمانی می‌تواند تاریخ را هم عوض کند: ۲۲:۰۰ به وقت UTC یعنی ۰۱:۳۰
    /// روز بعد در تهران. اگر فقط ساعت تبدیل می‌شد و تاریخ از UTC می‌آمد، این حالت
    /// یک روز عقب نمایش داده می‌شد.
    /// </summary>
    [Fact]
    public void LateUtcEvening_RollsOverToTheNextPersianDay()
    {
        var utc = new DateTime(2026, 7, 27, 22, 0, 0, DateTimeKind.Utc);

        var result = Utils.ConvertToPersianDate(utc);

        Assert.StartsWith("1405/05/06", result);
        Assert.EndsWith("01:30", result);
    }
}
