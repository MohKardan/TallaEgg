using TallaEgg.Core.Utilties;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Gregorian to Jalali date conversion.
///
/// The previous version subtracted 621 from the year and shifted the month, but returned the
/// Gregorian day unchanged. A user could see their own trade dated up to 22 days out: 27 July 2026
/// displayed as the 27th of the Jalali month instead of the 5th.
///
/// Times are also stored in UTC, and without conversion the wrong hour was displayed.
/// </summary>
public class PersianDateTests
{
    /// <summary>
    /// The date the bug was spotted on: 27 July 2026 is 5 Mordad 1405.
    /// </summary>
    [Fact]
    public void TheDateThatExposedTheBug_ConvertsCorrectly()
    {
        var utc = new DateTime(2026, 7, 27, 6, 0, 0, DateTimeKind.Utc);

        var result = Utils.ConvertToPersianDate(utc);

        Assert.StartsWith("1405/05/05", result);
    }

    /// <summary>
    /// A few boundary dates. Month and year boundaries are where the previous approximation was
    /// most wrong.
    /// </summary>
    [Theory]
    [InlineData(2026, 3, 21, "1405/01/01")] // نوروز ۱۴۰۵
    [InlineData(2026, 3, 20, "1404/12/29")] // یک روز پیش از نوروز
    [InlineData(2026, 7, 22, "1405/04/31")] // آخرین روز تیر
    [InlineData(2026, 7, 23, "1405/05/01")] // اولین روز مرداد
    [InlineData(2026, 12, 31, "1405/10/10")]
    public void KnownDates_ConvertCorrectly(int year, int month, int day, string expected)
    {
        // Midday UTC is used so Tehran's +03:30 offset cannot shift the day, keeping the test about
        // the calendar conversion rather than the midnight boundary.
        var utc = new DateTime(year, month, day, 12, 0, 0, DateTimeKind.Utc);

        var result = Utils.ConvertToPersianDate(utc);

        Assert.StartsWith(expected, result);
    }

    /// <summary>
    /// The time must be Tehran's. A trade recorded at 09:52 UTC happened at 13:22 for an Iranian
    /// user.
    /// </summary>
    [Fact]
    public void TimeIsShownInTehranTime_NotUtc()
    {
        var utc = new DateTime(2026, 7, 27, 9, 52, 0, DateTimeKind.Utc);

        var result = Utils.ConvertToPersianDate(utc);

        Assert.EndsWith("13:22", result);
    }

    /// <summary>
    /// A time-zone conversion can change the date too: 22:00 UTC is 01:30 the next day in Tehran. If
    /// only the time were converted while the date came from UTC, this case would display a day
    /// behind.
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
