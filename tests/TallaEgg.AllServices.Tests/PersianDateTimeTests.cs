using TallaEgg.Core.Utilties;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Dates shown to customers: Jalali calendar, Tehran time, Persian digits.
///
/// <para>
/// <b>This is display only.</b> Storage is unchanged and deliberately so — every timestamp in
/// the database stays Gregorian and UTC, which is what sorting, comparison and reconciliation
/// depend on. The two have nothing to do with each other, and conflating them is how a system
/// ends up with times that cannot be compared across rows.
/// </para>
///
/// <para>
/// Before this, user-facing dates were Gregorian and in UTC: a trade executed at 13:22 Tehran
/// time was shown to the customer as 09:52 on a Gregorian date. In
/// <c>TradeNotificationService</c> the variable holding it was even called
/// <c>shamsiDateTime</c> — a name that had never been true.
/// </para>
/// </summary>
public class PersianDateTimeTests
{
    // ── the offset ──────────────────────────────────────────────────────────────

    /// <summary>
    /// A fixed +03:30, not a lookup in the operating system's time-zone database.
    ///
    /// <para>
    /// Iran dropped daylight saving in 1401, so the offset is constant and a fixed value is
    /// simply correct. Looking it up carried three risks and no benefit: the zone id differs
    /// between Windows (<c>Iran Standard Time</c>) and Linux (<c>Asia/Tehran</c>), a minimal
    /// container image may carry no tz database at all, and a stale one still applies the
    /// abolished daylight rule — which would silently shift every displayed time by an hour
    /// for part of the year, on the server and not on the developer's machine.
    /// </para>
    /// </summary>
    [Fact]
    public void TehranIsThreeAndAHalfHoursAheadOfUtc()
    {
        var utc = new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc);

        Assert.Equal(new DateTime(2026, 7, 31, 13, 30, 0), Utils.ToTehranTime(utc));
        Assert.Equal(TimeSpan.FromMinutes(210), Utils.TehranOffset);
    }

    /// <summary>
    /// Mid-July is when a stale tz database would apply the abolished daylight rule. The result
    /// must be the same +03:30 as any other date in the year.
    /// </summary>
    [Fact]
    public void TheOffsetDoesNotChangeWithTheSeason()
    {
        var winter = new DateTime(2026, 1, 15, 8, 0, 0, DateTimeKind.Utc);
        var summer = new DateTime(2026, 7, 15, 8, 0, 0, DateTimeKind.Utc);

        Assert.Equal(11, Utils.ToTehranTime(winter).Hour);
        Assert.Equal(30, Utils.ToTehranTime(winter).Minute);
        Assert.Equal(11, Utils.ToTehranTime(summer).Hour);
        Assert.Equal(30, Utils.ToTehranTime(summer).Minute);
    }

    /// <summary>
    /// A value read back from the database has <c>Unspecified</c> kind — the provider does not
    /// preserve it — but it was written as UTC, so it must be converted like one. Treating it
    /// as already-local would leave every stored timestamp three and a half hours behind.
    /// </summary>
    [Fact]
    public void AnUnspecifiedKindIsTreatedAsUtcBecauseThatIsHowItWasStored()
    {
        var fromDatabase = new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Unspecified);

        Assert.Equal(new DateTime(2026, 7, 31, 13, 30, 0), Utils.ToTehranTime(fromDatabase));
    }

    /// <summary>A value that is already local must not be shifted a second time.</summary>
    [Fact]
    public void ALocalValueIsNotConvertedTwice()
    {
        var alreadyLocal = new DateTime(2026, 7, 31, 13, 30, 0, DateTimeKind.Local);

        Assert.Equal(alreadyLocal, Utils.ToTehranTime(alreadyLocal));
    }

    // ── the calendar ────────────────────────────────────────────────────────────

    /// <summary>
    /// 31 July 2026 at 10:00 UTC is 13:30 in Tehran on 9 Mordad 1405.
    /// </summary>
    [Fact]
    public void AKnownInstantConvertsToTheRightJalaliDate()
    {
        var text = PersianFormat.DateTimeText(new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc));

        Assert.Contains("۱۴۰۵/۰۵/۰۹", text);
        Assert.Contains("۱۳:۳۰", text);
    }

    /// <summary>
    /// The conversion crosses midnight when it should. 21:00 UTC is already the next day in
    /// Tehran, and a formatter that converted the calendar but not the clock would put the
    /// trade on the wrong day.
    /// </summary>
    [Fact]
    public void AnInstantLateInTheUtcDayIsAlreadyTheNextDayInTehran()
    {
        var text = PersianFormat.DateTimeText(new DateTime(2026, 7, 31, 21, 0, 0, DateTimeKind.Utc));

        Assert.Contains("۱۴۰۵/۰۵/۱۰", text);   // 10 Mordad, not 9
        Assert.Contains("۰۰:۳۰", text);
    }

    /// <summary>
    /// Nowruz — the Jalali year turns on 21 March. A conversion that merely subtracted 621 from
    /// the Gregorian year would be right for most of the year and wrong here.
    /// </summary>
    [Theory]
    [InlineData(2026, 3, 20, "۱۴۰۴/۱۲/۲۹")]   // last day of 1404 — Esfand has 29 days, 1404 is not a leap year
    [InlineData(2026, 3, 21, "۱۴۰۵/۰۱/۰۱")]   // Nowruz
    public void TheYearTurnsAtNowruzNotAtTheGregorianNewYear(int y, int m, int d, string expected)
    {
        // 09:00 UTC is 12:30 in Tehran, comfortably inside the same day either side.
        var text = PersianFormat.DateTimeText(new DateTime(y, m, d, 9, 0, 0, DateTimeKind.Utc));

        Assert.Contains(expected, text);
    }

    // ── presentation ────────────────────────────────────────────────────────────

    /// <summary>
    /// Persian digits throughout — ordinal comparison, because a culture-sensitive one treats
    /// U+06F0-U+06F9 as equal to ASCII 0-9 and would pass whatever the output contained.
    /// </summary>
    [Fact]
    public void TheDateIsRenderedInPersianDigits()
    {
        var text = PersianFormat.DateTimeText(new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc));

        Assert.DoesNotContain("1405", text, StringComparison.Ordinal);
        Assert.DoesNotContain("2026", text, StringComparison.Ordinal);
        Assert.All("0123456789", c => Assert.DoesNotContain(c.ToString(), text, StringComparison.Ordinal));
    }

    /// <summary>
    /// Wrapped in a Unicode left-to-right <i>isolate</i>, not in right-to-left marks.
    ///
    /// <para>
    /// This is the fault the customer saw. RLM is a strong right-to-left character, so
    /// surrounding a fragment with it makes the inside right-to-left. For a single number that
    /// makes no visible difference, which is why it went unnoticed for every amount the bot
    /// prints — but a date and a time are <b>two</b> number runs with a space between them, and
    /// that space inherited the right-to-left direction. The bidirectional algorithm then laid
    /// the two runs out right-to-left relative to each other and the reader saw
    /// "۱۲:۰۷ ۱۴۰۵/۰۵/۰۹" — the time first.
    /// </para>
    ///
    /// <para>
    /// U+2066…U+2069 isolates the fragment instead: one left-to-right unit, internal order
    /// preserved, surrounding direction untouched. It is defined by the Unicode standard and
    /// depends on nothing about the machine or the reader's device.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDateIsWrappedInALeftToRightIsolate()
    {
        var text = PersianFormat.DateTimeText(new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc));

        // Ordinal throughout. A culture-sensitive comparison ignores invisible formatting
        // characters entirely, so every one of these assertions would pass no matter which
        // mark the string actually carried — including none at all.
        Assert.StartsWith(PersianFormat.Lri, text, StringComparison.Ordinal);
        Assert.EndsWith(PersianFormat.Pdi, text, StringComparison.Ordinal);
        Assert.DoesNotContain(PersianFormat.Rlm, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The invisible characters, asserted by code point. They cannot be read in the editor, so
    /// a bad copy-and-paste into the source would replace them with something plausible-looking
    /// and nothing else would notice.
    /// </summary>
    [Fact]
    public void TheDirectionMarksAreTheCharactersTheyClaimToBe()
    {
        Assert.Equal("‏", PersianFormat.Rlm);
        Assert.Equal("⁦", PersianFormat.Lri);
        Assert.Equal("⁩", PersianFormat.Pdi);
    }

    /// <summary>
    /// The date must come before the time in the string itself. Ordering is the whole point,
    /// and it is what a reader loses when the isolate is missing.
    /// </summary>
    [Fact]
    public void TheDateComesBeforeTheTime()
    {
        var text = PersianFormat.DateTimeText(new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc));

        var datePosition = text.IndexOf("۱۴۰۵/۰۵/۰۹", StringComparison.Ordinal);
        var timePosition = text.IndexOf("۱۳:۳۰", StringComparison.Ordinal);

        Assert.True(datePosition >= 0 && timePosition > datePosition,
            $"date must precede time, got: {text}");
    }

    // ── independence from the machine ───────────────────────────────────────────

    /// <summary>
    /// The output must not change with the culture the service happens to run under. Formatting
    /// without an explicit culture uses <c>CurrentCulture</c>, so on a server configured as
    /// fa-IR the fallback path would have printed a Jalali year through a Gregorian format
    /// string — a different answer on a different machine, from the same code and the same
    /// input.
    /// </summary>
    [Theory]
    [InlineData("en-US")]
    [InlineData("fa-IR")]
    [InlineData("ar-SA")]
    [InlineData("")]        // invariant
    public void TheOutputIsIdenticalUnderAnyMachineCulture(string cultureName)
    {
        var instant = new DateTime(2026, 7, 31, 10, 0, 0, DateTimeKind.Utc);
        var expected = "۱۴۰۵/۰۵/۰۹ ۱۳:۳۰";

        var original = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture =
                System.Globalization.CultureInfo.GetCultureInfo(cultureName);

            Assert.Contains(expected, PersianFormat.DateTimeText(instant), StringComparison.Ordinal);
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = original;
        }
    }

    /// <summary>
    /// A displayed date must never be the reason a message fails to send. <c>PersianCalendar</c>
    /// throws below its supported range, so a default or corrupt value falls back rather than
    /// taking the whole notification down with it.
    /// </summary>
    [Fact]
    public void AnOutOfRangeValueFallsBackInsteadOfThrowing()
    {
        var text = PersianFormat.DateTimeText(DateTime.MinValue);

        Assert.False(string.IsNullOrWhiteSpace(text));
    }
}
