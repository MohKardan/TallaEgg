using TallaEgg.Core.Enums.User;
using TallaEgg.Core.Utilties;

namespace TallaEgg.AllServices.Tests;

/// <summary>
/// Reading a role out of what an operator typed.
///
/// <para>
/// This is the part of the role command that decides who gets authority, and it runs on text
/// a human typed on a phone keyboard. Every case below is something that arrives in practice:
/// the Arabic yeh, a stray zero-width non-joiner, doubled spaces, Persian digits. A parser
/// that quietly falls through on any of them either refuses a legitimate command or — far
/// worse — matches the wrong role.
/// </para>
/// </summary>
public class UserRoleNamesTests
{
    private static UserRole Parse(string input)
    {
        Assert.True(UserRoleNames.TryParse(input, out var role), $"'{input}' should parse");
        return role;
    }

    // ── names ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("کاربر عادی", UserRole.RegularUser)]
    [InlineData("عادی", UserRole.RegularUser)]
    [InlineData("حسابدار", UserRole.Accountant)]
    [InlineData("مدیر", UserRole.Admin)]
    [InlineData("ادمین", UserRole.Admin)]
    [InlineData("مدیر ارشد", UserRole.SuperAdmin)]
    [InlineData("مدیرارشد", UserRole.SuperAdmin)]
    public void PersianNamesAreRecognised(string input, UserRole expected) =>
        Assert.Equal(expected, Parse(input));

    [Theory]
    [InlineData("admin", UserRole.Admin)]
    [InlineData("ADMIN", UserRole.Admin)]
    [InlineData("Accountant", UserRole.Accountant)]
    [InlineData("SuperAdmin", UserRole.SuperAdmin)]
    public void EnglishNamesAreRecognisedRegardlessOfCase(string input, UserRole expected) =>
        Assert.Equal(expected, Parse(input));

    // ── the characters a phone keyboard actually produces ───────────────────────

    /// <summary>
    /// Arabic yeh (U+064A) and keheh/kaf (U+0643 vs U+06A9) are different code points from the
    /// Persian letters and several keyboards emit them. Without normalisation "مدير" fails
    /// against "مدیر" and the operator sees "نقش شناسایی نشد" for a word they spelled correctly.
    /// </summary>
    [Theory]
    [InlineData("مدير")]           // ARABIC YEH
    [InlineData("مدير ارشد")]
    public void ArabicLetterVariantsAreAccepted(string input) =>
        Assert.True(UserRoleNames.TryParse(input, out _));

    [Fact]
    public void ArabicYehResolvesToTheSameRole() =>
        Assert.Equal(UserRole.Admin, Parse("مدير"));

    [Fact]
    public void ArabicKafIsAccepted() =>
        Assert.Equal(UserRole.RegularUser, Parse("كاربر عادی"));

    /// <summary>A zero-width non-joiner is invisible; the operator cannot see why it failed.</summary>
    [Fact]
    public void AZeroWidthNonJoinerDoesNotBreakTheMatch() =>
        Assert.Equal(UserRole.SuperAdmin, Parse("مدیر‌ارشد"));

    [Fact]
    public void RepeatedSpacesAreCollapsed() =>
        Assert.Equal(UserRole.SuperAdmin, Parse("مدیر   ارشد"));

    [Fact]
    public void SurroundingWhitespaceIsIgnored() =>
        Assert.Equal(UserRole.Admin, Parse("  مدیر  "));

    // ── numbers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// The stored value is what appears in the database and in logs. An operator who reads
    /// <c>role=2</c> there must be able to type <c>2</c> back, or the diagnostics and the
    /// command speak different languages.
    /// </summary>
    [Theory]
    [InlineData("0", UserRole.RegularUser)]
    [InlineData("1", UserRole.Accountant)]
    [InlineData("2", UserRole.Admin)]
    [InlineData("3", UserRole.SuperAdmin)]
    public void NumericValuesAreAccepted(string input, UserRole expected) =>
        Assert.Equal(expected, Parse(input));

    [Fact]
    public void PersianDigitsAreAccepted() =>
        Assert.Equal(UserRole.Admin, Parse("۲"));

    [Fact]
    public void ANumberOutsideTheEnumIsRejected() =>
        Assert.False(UserRoleNames.TryParse("9", out _));

    // ── refusing rather than guessing ───────────────────────────────────────────

    /// <summary>
    /// The case that matters most. "مدیر ارشد" contains "مدیر"; a parser built on
    /// <c>StartsWith</c> or <c>Contains</c> would grant Admin when SuperAdmin was asked for,
    /// and the confirmation message would look correct either way.
    /// </summary>
    [Fact]
    public void SuperAdminIsNotReadAsAdmin() =>
        Assert.Equal(UserRole.SuperAdmin, Parse("مدیر ارشد"));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("مدیرکل")]
    [InlineData("رئیس")]
    [InlineData("root")]
    [InlineData("مدیر ارشد کل")]
    public void UnknownInputIsRefused(string? input) =>
        Assert.False(UserRoleNames.TryParse(input, out _));

    // ── display ─────────────────────────────────────────────────────────────────

    /// <summary>Whatever is displayed must parse back, or the help text lies about its own input.</summary>
    [Fact]
    public void EveryAssignableRoleRoundTripsThroughItsDisplayName()
    {
        foreach (var role in UserRoleNames.Assignable)
        {
            Assert.True(UserRoleNames.TryParse(UserRoleNames.Display(role), out var parsed));
            Assert.Equal(role, parsed);
        }
    }

    /// <summary>
    /// <see cref="UserRole.User"/> is the second ordinary-user value the enum carries. It is not
    /// offered as a target, but existing rows hold it, so it must still display as something a
    /// person understands rather than as "User".
    /// </summary>
    [Fact]
    public void TheDuplicateOrdinaryRoleStillDisplaysReadably()
    {
        Assert.Equal("کاربر عادی", UserRoleNames.Display(UserRole.User));
        Assert.DoesNotContain(UserRole.User, UserRoleNames.Assignable);
    }

    [Fact]
    public void TheAssignableListNamesEveryRoleWithItsNumber()
    {
        var list = UserRoleNames.AssignableList();

        foreach (var role in UserRoleNames.Assignable)
        {
            Assert.Contains(UserRoleNames.Display(role), list);
            Assert.Contains(((int)role).ToString(), list);
        }
    }
}
